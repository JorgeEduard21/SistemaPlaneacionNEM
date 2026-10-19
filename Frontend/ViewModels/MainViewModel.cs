using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using Frontend.Models;
using Frontend.Services;

namespace Frontend.ViewModels
{
    public class MainViewModel : ViewModelBase
    {
        private readonly ApiClient _api;
        private readonly BackendManager _backendManager;

        public ConfiguracionViewModel ConfiguracionVM { get; }

        // --- Navegación Principal: Vista Activa ---
        // 0: Asistente de Planeación (Wizard)
        // 1: Configuración (Perfil y API Key)
        // 2: Historial de Planeaciones
        private int _vistaActual = 0;
        public int VistaActual
        {
            get => _vistaActual;
            set
            {
                if (SetProperty(ref _vistaActual, value))
                {
                    OnPropertyChanged(nameof(EsVistaWizard));
                    OnPropertyChanged(nameof(EsVistaConfiguracion));
                    OnPropertyChanged(nameof(EsVistaHistorial));
                    if (value == 2)
                    {
                        _ = CargarHistorialAsync();
                    }
                }
            }
        }

        public bool EsVistaWizard => VistaActual == 0;
        public bool EsVistaConfiguracion => VistaActual == 1;
        public bool EsVistaHistorial => VistaActual == 2;

        // --- Pasos del Asistente (Wizard) ---
        // Paso 0: Currícula NEM
        // Paso 1: Diseño de Planeación
        // Paso 2: Evaluación
        // Paso 3: Resumen y Generación
        private int _pasoActual = 0;
        public int PasoActual
        {
            get => _pasoActual;
            set { SetProperty(ref _pasoActual, value); }
        }

        // --- Estado de Conexión y Notificaciones UX ---
        private bool _backendConectado = false;
        public bool BackendConectado
        {
            get => _backendConectado;
            set { SetProperty(ref _backendConectado, value); }
        }

        private string? _mensajeNotificacion;
        public string? MensajeNotificacion
        {
            get => _mensajeNotificacion;
            set { SetProperty(ref _mensajeNotificacion, value); }
        }

        private bool _esNotificacionError;
        public bool EsNotificacionError
        {
            get => _esNotificacionError;
            set { SetProperty(ref _esNotificacionError, value); }
        }

        private bool _generandoPlaneacion = false;
        public bool GenerandoPlaneacion
        {
            get => _generandoPlaneacion;
            set { SetProperty(ref _generandoPlaneacion, value); }
        }

        private string _estadoApiKeyBanner = "";
        public string EstadoApiKeyBanner
        {
            get => _estadoApiKeyBanner;
            set { SetProperty(ref _estadoApiKeyBanner, value); }
        }

        private bool _mostrarAlertaApiKey = false;
        public bool MostrarAlertaApiKey
        {
            get => _mostrarAlertaApiKey;
            set { SetProperty(ref _mostrarAlertaApiKey, value); }
        }

        private bool _mostrarAlertaPerfil = false;
        public bool MostrarAlertaPerfil
        {
            get => _mostrarAlertaPerfil;
            set { SetProperty(ref _mostrarAlertaPerfil, value); }
        }

        // --- 1. Estructura Curricular NEM ---
        public ObservableCollection<string> CamposFormativos { get; } = new()
        {
            "Lenguajes",
            "Saberes y Pensamiento Científico",
            "Ética, Naturaleza y Sociedades",
            "De lo Humano y lo Comunitario"
        };

        private string _campoFormativo = "Lenguajes";
        public string CampoFormativo
        {
            get => _campoFormativo;
            set
            {
                if (SetProperty(ref _campoFormativo, value))
                {
                    _ = CargarCatalogoAsync();
                }
            }
        }

        public GrupoOpciones EjesArticuladoresOpciones { get; }

        private bool _sincronizandoCatalogo;

        private string _contenido = "";
        public string Contenido
        {
            get => _contenido;
            set
            {
                if (SetProperty(ref _contenido, value) && !_sincronizandoCatalogo)
                {
                    if (_contenidoCatalogoSeleccionado != null &&
                        !string.Equals(_contenidoCatalogoSeleccionado.Titulo, value?.Trim(), StringComparison.OrdinalIgnoreCase))
                    {
                        _contenidoCatalogoSeleccionado = null;
                        OnPropertyChanged(nameof(ContenidoCatalogoSeleccionado));
                        PdaSugeridosCatalogo.Clear();
                        OnPropertyChanged(nameof(TienePdaSugeridosCatalogo));
                    }
                }
            }
        }

        private string _subcontenido = "";
        public string Subcontenido
        {
            get => _subcontenido;
            set { SetProperty(ref _subcontenido, value); }
        }

        private string _intencion = "";
        public string Intencion
        {
            get => _intencion;
            set { SetProperty(ref _intencion, value); }
        }

        public ListaEditable PdaLista { get; }

        // --- Catálogo Precargado NEM (Contenidos y PDA) ---
        private readonly List<ContenidoCatalogo> _catalogoCompleto = new();
        public ObservableCollection<ContenidoCatalogo> ContenidosFiltrados { get; } = new();
        public ObservableCollection<PdaSugeridoCatalogo> PdaSugeridosCatalogo { get; } = new();

        private string _filtroCatalogo = "";
        public string FiltroCatalogo
        {
            get => _filtroCatalogo;
            set
            {
                if (SetProperty(ref _filtroCatalogo, value))
                {
                    AplicarFiltroCatalogo();
                }
            }
        }

        private ContenidoCatalogo? _contenidoCatalogoSeleccionado;
        public ContenidoCatalogo? ContenidoCatalogoSeleccionado
        {
            get => _contenidoCatalogoSeleccionado;
            set
            {
                if (SetProperty(ref _contenidoCatalogoSeleccionado, value))
                {
                    AlSeleccionarContenidoCatalogo(value);
                }
            }
        }

        public bool TienePdaSugeridosCatalogo => PdaSugeridosCatalogo.Count > 0;
        public bool TieneContenidosFiltrados => ContenidosFiltrados.Count > 0;

        public string GradosActivosCatalogoTexto
        {
            get
            {
                var grados = ConfiguracionVM.PerfilGuardado.Grados;
                return grados != null && grados.Count > 0
                    ? $"Mostrando contenidos oficiales para: {string.Join(", ", grados)}"
                    : "Mostrando todos los grados de Primaria (configura tu grado en Configuración para filtrar automáticamente)";
            }
        }

        public string GradosPerfilResumenTexto
        {
            get
            {
                var g = ConfiguracionVM.PerfilGuardado.Grados;
                return g != null && g.Count > 0 ? string.Join(", ", g) : "Sin definir";
            }
        }

        // --- 2. Diseño de la Planeación ---
        public ObservableCollection<string> Metodologias { get; } = new()
        {
            "STEAM",
            "Aprendizaje Basado en Problemas",
            "Proyectos Comunitarios",
            "Aprendizaje-Servicio"
        };

        private string _metodologia = "Proyectos Comunitarios";
        public string Metodologia
        {
            get => _metodologia;
            set { SetProperty(ref _metodologia, value); }
        }

        private string _temaInteres = "";
        public string TemaInteres
        {
            get => _temaInteres;
            set { SetProperty(ref _temaInteres, value); }
        }

        private string _duracionDias = "5";
        public string DuracionDias
        {
            get => _duracionDias;
            set { SetProperty(ref _duracionDias, value); }
        }

        private string _duracionMinutos = "50";
        public string DuracionMinutos
        {
            get => _duracionMinutos;
            set { SetProperty(ref _duracionMinutos, value); }
        }

        // --- 3. Evaluación ---
        public ObservableCollection<string> Instrumentos { get; } = new()
        {
            "Lista de Cotejo",
            "Rúbrica",
            "Guía de Observación"
        };

        private string _instrumento = "Rúbrica";
        public string Instrumento
        {
            get => _instrumento;
            set { SetProperty(ref _instrumento, value); }
        }

        public ListaEditable CriteriosLista { get; }

        private bool _generarFicha = true;
        public bool GenerarFicha
        {
            get => _generarFicha;
            set { SetProperty(ref _generarFicha, value); }
        }

        private bool _generarEditable = true;
        public bool GenerarEditable
        {
            get => _generarEditable;
            set { SetProperty(ref _generarEditable, value); }
        }

        private string _retroalimentacion = "";
        public string Retroalimentacion
        {
            get => _retroalimentacion;
            set { SetProperty(ref _retroalimentacion, value); }
        }

        // --- 4. Resultado / Resumen Final ---
        private Planeacion? _planeacionActual;
        public Planeacion? PlaneacionActual
        {
            get => _planeacionActual;
            set
            {
                if (SetProperty(ref _planeacionActual, value))
                {
                    TextoResultadoMarkdown = value?.Contenido ?? string.Empty;
                    OnPropertyChanged(nameof(TienePlaneacionGenerada));
                }
            }
        }

        public bool TienePlaneacionGenerada => PlaneacionActual != null;

        private string _textoResultadoMarkdown = string.Empty;
        public string TextoResultadoMarkdown
        {
            get => _textoResultadoMarkdown;
            set => SetProperty(ref _textoResultadoMarkdown, value);
        }

        // --- Historial ---
        public ObservableCollection<ResumenPlaneacion> Historial { get; } = new();

        public bool TienePlaneacionesEnHistorial => Historial.Count > 0;

        private ResumenPlaneacion? _planeacionSeleccionadaHistorial;
        public ResumenPlaneacion? PlaneacionSeleccionadaHistorial
        {
            get => _planeacionSeleccionadaHistorial;
            set { SetProperty(ref _planeacionSeleccionadaHistorial, value); }
        }

        // --- Comandos ---
        public ICommand ContinuarCommand { get; }
        public ICommand AtrasCommand { get; }
        public ICommand IrAPasoCommand { get; }
        public ICommand IrAVistaWizardCommand { get; }
        public ICommand IrAVistaConfiguracionCommand { get; }
        public ICommand IrAVistaHistorialCommand { get; }
        public ICommand GenerarPlaneacionCommand { get; }
        public ICommand DescargarWordCommand { get; }
        public ICommand AbrirEnGoogleDocsCommand { get; }
        public ICommand CopiarPlaneacionCommand { get; }
        public ICommand SeleccionarTodosPdaCommand { get; }
        public ICommand LimpiarPdaCommand { get; }
        public ICommand GuardarContenidoPersonalizadoCommand { get; }
        public ICommand CargarDetalleHistorialCommand { get; }
        public ICommand AbrirGoogleDocsHistorialCommand { get; }
        public ICommand DescargarWordHistorialCommand { get; }
        public ICommand EliminarDeHistorialCommand { get; }
        public ICommand CerrarNotificacionCommand { get; }

        public MainViewModel()
        {
            _api = new ApiClient();
            _backendManager = new BackendManager(_api);
            ConfiguracionVM = new ConfiguracionViewModel(this, _api);

            ConfiguracionVM.PerfilActualizado += (_, _) =>
            {
                OnPropertyChanged(nameof(GradosActivosCatalogoTexto));
                OnPropertyChanged(nameof(GradosPerfilResumenTexto));
                _ = CargarCatalogoAsync();
            };

            // Ejes articuladores con multiselección
            EjesArticuladoresOpciones = new GrupoOpciones(true);
            EjesArticuladoresOpciones.Agregar("Inclusión")
                                     .Agregar("Pensamiento Crítico")
                                     .Agregar("Interculturalidad Crítica")
                                     .Agregar("Igualdad de Género")
                                     .Agregar("Vida Saludable")
                                     .Agregar("Apropiación de las Culturas a través de la Lectura y Escritura")
                                     .Agregar("Artes y Experiencias Estéticas");

            PdaLista = new ListaEditable();
            PdaLista.Cambio += (_, _) => SincronizarChecksPdaSugeridos();
            CriteriosLista = new ListaEditable();

            Historial.CollectionChanged += (_, _) => OnPropertyChanged(nameof(TienePlaneacionesEnHistorial));

            AsyncRelayCommand.ManejadorErroresGlobal = ex =>
            {
                Notificar(ex.Message, esError: true);
            };

            // Comandos de Wizard
            ContinuarCommand = new RelayCommand(_ => AvanzarPaso(), _ => PasoActual < 3);
            AtrasCommand = new RelayCommand(_ => RetrocederPaso(), _ => PasoActual > 0);
            IrAPasoCommand = new RelayCommand(p =>
            {
                if (int.TryParse(p?.ToString(), out var paso) && paso >= 0 && paso <= 3)
                    PasoActual = paso;
            });

            // Comandos de Navegación de Vistas
            IrAVistaWizardCommand = new RelayCommand(_ => VistaActual = 0);
            IrAVistaConfiguracionCommand = new RelayCommand(_ => { VistaActual = 1; _ = ConfiguracionVM.CargarAsync(); });
            IrAVistaHistorialCommand = new RelayCommand(_ => { VistaActual = 2; _ = CargarHistorialAsync(); });

            // Comandos del Catálogo NEM
            SeleccionarTodosPdaCommand = new RelayCommand(_ => SeleccionarTodosPdaSugeridos(), _ => TienePdaSugeridosCatalogo);
            LimpiarPdaCommand = new RelayCommand(_ => LimpiarTodosPda(), _ => !PdaLista.EstaVacia);
            GuardarContenidoPersonalizadoCommand = new AsyncRelayCommand(_ => GuardarContenidoPersonalizadoAsync(), _ => !string.IsNullOrWhiteSpace(Contenido));

            // Generación y exportación de planeación
            GenerarPlaneacionCommand = new AsyncRelayCommand(_ => GenerarPlaneacionAsync(), _ => !GenerandoPlaneacion);
            DescargarWordCommand = new AsyncRelayCommand(_ => DescargarWordAsync(), _ => TienePlaneacionGenerada);
            AbrirEnGoogleDocsCommand = new RelayCommand(_ => AbrirEnGoogleDocs(PlaneacionActual), _ => TienePlaneacionGenerada);
            CopiarPlaneacionCommand = new RelayCommand(_ => CopiarPlaneacionAlPortapapeles(PlaneacionActual), _ => TienePlaneacionGenerada);

            // Historial
            CargarDetalleHistorialCommand = new AsyncRelayCommand(p => CargarDetalleHistorialAsync(p as ResumenPlaneacion));
            AbrirGoogleDocsHistorialCommand = new AsyncRelayCommand(p => AbrirGoogleDocsDesdeHistorialAsync(p as ResumenPlaneacion));
            DescargarWordHistorialCommand = new AsyncRelayCommand(p => DescargarWordDesdeHistorialAsync(p as ResumenPlaneacion));
            EliminarDeHistorialCommand = new AsyncRelayCommand(p => EliminarDeHistorialAsync(p as ResumenPlaneacion));

            CerrarNotificacionCommand = new RelayCommand(_ => MensajeNotificacion = null);

            // Inicializar Backend y Cargar Configuración en segundo plano
            _ = InicializarAplicacionAsync();
        }

        private async Task InicializarAplicacionAsync()
        {
            try
            {
                await _backendManager.IniciarAsync();
                BackendConectado = true;
                await ConfiguracionVM.CargarAsync();
                await CargarCatalogoAsync();
                await CargarHistorialAsync();
                Notificar("Sistema listo y catálogo NEM cargado.");
            }
            catch (Exception ex)
            {
                BackendConectado = false;
                Notificar($"Error al iniciar el servidor local de IA: {ex.Message}", esError: true);
            }
        }

        // ============================================================ Catálogo NEM
        public async Task CargarCatalogoAsync()
        {
            if (!BackendConectado) return;
            try
            {
                var grados = ConfiguracionVM.PerfilGuardado.Grados;
                var items = await _api.ObtenerCatalogoAsync(CampoFormativo, grados);
                _catalogoCompleto.Clear();
                _catalogoCompleto.AddRange(items);
                AplicarFiltroCatalogo();
            }
            catch (Exception ex)
            {
                Notificar($"No se pudo cargar el catálogo NEM: {ex.Message}", esError: true);
            }
        }

        private void AplicarFiltroCatalogo()
        {
            var previoId = _contenidoCatalogoSeleccionado?.Id;
            ContenidosFiltrados.Clear();

            var filtro = (FiltroCatalogo ?? "").Trim();
            IEnumerable<ContenidoCatalogo> fuente = _catalogoCompleto;

            if (!string.IsNullOrEmpty(filtro))
            {
                fuente = fuente.Where(c =>
                    c.Titulo.Contains(filtro, StringComparison.OrdinalIgnoreCase) ||
                    c.Pda.Values.SelectMany(x => x).Any(p => p.Contains(filtro, StringComparison.OrdinalIgnoreCase)));
            }

            foreach (var item in fuente)
            {
                ContenidosFiltrados.Add(item);
            }

            OnPropertyChanged(nameof(TieneContenidosFiltrados));

            if (previoId != null)
            {
                _contenidoCatalogoSeleccionado = ContenidosFiltrados.FirstOrDefault(c => c.Id == previoId);
                OnPropertyChanged(nameof(ContenidoCatalogoSeleccionado));
            }
        }

        private void AlSeleccionarContenidoCatalogo(ContenidoCatalogo? item)
        {
            PdaSugeridosCatalogo.Clear();
            if (item == null)
            {
                OnPropertyChanged(nameof(TienePdaSugeridosCatalogo));
                return;
            }

            _sincronizandoCatalogo = true;
            try
            {
                Contenido = item.Titulo;

                // Poblar los PDA sugeridos de este contenido y seleccionarlos automáticamente para ahorrar clics
                var nuevosPda = new List<string>();
                foreach (var kv in item.Pda)
                {
                    var grado = kv.Key;
                    foreach (var textoPda in kv.Value)
                    {
                        nuevosPda.Add(textoPda);
                        var sugerido = new PdaSugeridoCatalogo(grado, textoPda, seleccionado: true, AlCambiarPdaSugerido);
                        PdaSugeridosCatalogo.Add(sugerido);
                    }
                }

                PdaLista.Establecer(nuevosPda);
            }
            finally
            {
                _sincronizandoCatalogo = false;
            }

            OnPropertyChanged(nameof(TienePdaSugeridosCatalogo));
        }

        private void AlCambiarPdaSugerido(PdaSugeridoCatalogo pda)
        {
            if (_sincronizandoCatalogo) return;
            _sincronizandoCatalogo = true;
            try
            {
                var existentes = PdaLista.Elementos.ToList();
                var match = existentes.FirstOrDefault(x => string.Equals(x, pda.Texto, StringComparison.OrdinalIgnoreCase));
                if (pda.Seleccionado && match == null)
                {
                    PdaLista.Elementos.Add(pda.Texto);
                }
                else if (!pda.Seleccionado && match != null)
                {
                    PdaLista.Elementos.Remove(match);
                }
            }
            finally
            {
                _sincronizandoCatalogo = false;
            }
        }

        private void SincronizarChecksPdaSugeridos()
        {
            if (_sincronizandoCatalogo) return;
            _sincronizandoCatalogo = true;
            try
            {
                var activos = new HashSet<string>(PdaLista.Elementos, StringComparer.OrdinalIgnoreCase);
                foreach (var item in PdaSugeridosCatalogo)
                {
                    item.EstablecerSinNotificar(activos.Contains(item.Texto));
                }
            }
            finally
            {
                _sincronizandoCatalogo = false;
            }
        }

        private void SeleccionarTodosPdaSugeridos()
        {
            _sincronizandoCatalogo = true;
            try
            {
                foreach (var item in PdaSugeridosCatalogo)
                {
                    item.EstablecerSinNotificar(true);
                    if (!PdaLista.Elementos.Any(x => string.Equals(x, item.Texto, StringComparison.OrdinalIgnoreCase)))
                        PdaLista.Elementos.Add(item.Texto);
                }
            }
            finally
            {
                _sincronizandoCatalogo = false;
            }
        }

        private void LimpiarTodosPda()
        {
            _sincronizandoCatalogo = true;
            try
            {
                PdaLista.Establecer( Array.Empty<string>() );
                foreach (var item in PdaSugeridosCatalogo)
                    item.EstablecerSinNotificar(false);
            }
            finally
            {
                _sincronizandoCatalogo = false;
            }
        }

        private async Task GuardarContenidoPersonalizadoAsync()
        {
            if (string.IsNullOrWhiteSpace(Contenido)) return;

            var grados = ConfiguracionVM.PerfilGuardado.Grados;
            if (grados == null || grados.Count == 0)
                grados = new List<string> { "1° Primaria" };

            var nuevo = await _api.AgregarContenidoCatalogoAsync(
                CampoFormativo,
                Contenido.Trim(),
                grados,
                PdaLista.ComoLista());

            await CargarCatalogoAsync();
            ContenidoCatalogoSeleccionado = ContenidosFiltrados.FirstOrDefault(c => c.Id == nuevo.Id);
            Notificar("Contenido y PDA guardados en tu catálogo personal para futuras planeaciones.");
        }

        private void AvanzarPaso()
        {
            if (PasoActual < 3)
                PasoActual++;
        }

        private void RetrocederPaso()
        {
            if (PasoActual > 0)
                PasoActual--;
        }

        public async Task GenerarPlaneacionAsync()
        {
            if (ConfiguracionVM.Estado?.ApiKeyConfigurada != true)
            {
                Notificar("No has configurado tu API key de Gemini. Ve al menú de Configuración para agregarla.", esError: true);
                VistaActual = 1;
                ConfiguracionVM.SeccionActual = 1;
                return;
            }

            if (!ConfiguracionVM.PerfilGuardado.EstaCompleto)
            {
                Notificar("Debes completar el contexto de tu grupo una vez en Configuración.", esError: true);
                VistaActual = 1;
                ConfiguracionVM.SeccionActual = 0;
                return;
            }

            if (string.IsNullOrWhiteSpace(Contenido))
            {
                Notificar("Selecciona o escribe el contenido curricular en el Paso 1.", esError: true);
                PasoActual = 0;
                return;
            }

            if (string.IsNullOrWhiteSpace(TemaInteres))
            {
                Notificar("Indica el tema de interés o proyecto central en el Paso 2.", esError: true);
                PasoActual = 1;
                return;
            }

            GenerandoPlaneacion = true;
            Notificar("Generando planeación didáctica con Gemini... Esto puede tomar unos segundos.");

            try
            {
                int.TryParse(DuracionDias, out int dias);
                if (dias <= 0) dias = 5;

                int.TryParse(DuracionMinutos, out int minutos);
                if (minutos <= 0) minutos = 50;

                var solicitud = new SolicitudPlaneacion
                {
                    Curricula = new DatosCurriculares
                    {
                        CampoFormativo = CampoFormativo,
                        EjesArticuladores = EjesArticuladoresOpciones.Seleccionadas,
                        Contenido = Contenido,
                        Subcontenido = Subcontenido,
                        IntencionDidactica = Intencion,
                        Pda = PdaLista.ComoLista()
                    },
                    Diseno = new DisenoPlaneacion
                    {
                        TemaInteres = TemaInteres,
                        Metodologia = Metodologia,
                        DuracionDias = dias,
                        MinutosPorSesion = minutos
                    },
                    Evaluacion = new PlanEvaluacion
                    {
                        Instrumento = Instrumento,
                        Criterios = CriteriosLista.ComoLista(),
                        GenerarFicha = GenerarFicha,
                        GenerarEditable = GenerarEditable,
                        NotasDocente = Retroalimentacion
                    }
                };

                using var cts = new CancellationTokenSource(TimeSpan.FromMinutes(4));
                var resultado = await _api.GenerarPlaneacionAsync(solicitud, cts.Token);
                PlaneacionActual = resultado;
                _ = CargarHistorialAsync();
                PasoActual = 3;
                Notificar("¡Planeación didáctica generada exitosamente!");
            }
            catch (Exception ex)
            {
                Notificar($"Error al generar planeación: {ex.Message}", esError: true);
            }
            finally
            {
                GenerandoPlaneacion = false;
            }
        }

        public async Task DescargarWordAsync()
        {
            if (PlaneacionActual == null) return;
            await DescargarWordDePlaneacionAsync(PlaneacionActual.Id);
        }

        private async Task DescargarWordDePlaneacionAsync(string planeacionId)
        {
            try
            {
                var (contenido, nombre) = await _api.DescargarDocxAsync(planeacionId);
                var rutaDescargas = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
                    "Downloads",
                    nombre
                );

                await File.WriteAllBytesAsync(rutaDescargas, contenido);
                Notificar($"Documento Word guardado en Descargas: {nombre}");

                try
                {
                    Process.Start(new ProcessStartInfo(rutaDescargas) { UseShellExecute = true });
                }
                catch { }
            }
            catch (Exception ex)
            {
                Notificar($"Error al descargar archivo Word: {ex.Message}", esError: true);
            }
        }

        public void AbrirEnGoogleDocs(Planeacion? planeacion)
        {
            if (planeacion == null) return;

            try
            {
                ExportadorGoogleDocs.CopiarConFormatoParaGoogleDocs(planeacion);
                Process.Start(new ProcessStartInfo("https://docs.new") { UseShellExecute = true });
                Notificar("¡Planeación copiada con formato (tablas, títulos y listas)! En la pestaña de Google Docs que se abrió, solo presiona Ctrl + V para pegarla.");
            }
            catch (Exception ex)
            {
                Notificar($"No se pudo abrir Google Docs: {ex.Message}", esError: true);
            }
        }

        public void CopiarPlaneacionAlPortapapeles(Planeacion? planeacion)
        {
            if (planeacion == null) return;
            try
            {
                ExportadorGoogleDocs.CopiarConFormatoParaGoogleDocs(planeacion);
                Notificar("Planeación copiada al portapapeles con formato enriquecido (lista para pegar en Google Docs, Word o correo).");
            }
            catch (Exception ex)
            {
                Notificar($"No se pudo copiar al portapapeles: {ex.Message}", esError: true);
            }
        }

        public async Task CargarHistorialAsync()
        {
            try
            {
                var lista = await _api.ListarPlaneacionesAsync();
                Historial.Clear();
                foreach (var item in lista)
                {
                    Historial.Add(item);
                }
                OnPropertyChanged(nameof(TienePlaneacionesEnHistorial));
            }
            catch (Exception ex)
            {
                Notificar($"Error al cargar historial: {ex.Message}", esError: true);
            }
        }

        public async Task CargarDetalleHistorialAsync(ResumenPlaneacion? resumen)
        {
            if (resumen == null) return;

            try
            {
                var plan = await _api.ObtenerPlaneacionAsync(resumen.Id);
                PlaneacionActual = plan;
                VistaActual = 0;
                PasoActual = 3;
                Notificar($"Planeación '{plan.Titulo}' cargada.");
            }
            catch (Exception ex)
            {
                Notificar($"Error al abrir planeación: {ex.Message}", esError: true);
            }
        }

        private async Task AbrirGoogleDocsDesdeHistorialAsync(ResumenPlaneacion? resumen)
        {
            if (resumen == null) return;
            var plan = await _api.ObtenerPlaneacionAsync(resumen.Id);
            PlaneacionActual = plan;
            AbrirEnGoogleDocs(plan);
        }

        private async Task DescargarWordDesdeHistorialAsync(ResumenPlaneacion? resumen)
        {
            if (resumen == null) return;
            await DescargarWordDePlaneacionAsync(resumen.Id);
        }

        public async Task EliminarDeHistorialAsync(ResumenPlaneacion? resumen)
        {
            if (resumen == null) return;

            if (!Confirmar($"¿Deseas eliminar la planeación '{resumen.Titulo}'?", "Confirmar eliminación"))
                return;

            try
            {
                await _api.EliminarPlaneacionAsync(resumen.Id);
                Historial.Remove(resumen);
                OnPropertyChanged(nameof(TienePlaneacionesEnHistorial));
                if (PlaneacionActual?.Id == resumen.Id)
                {
                    PlaneacionActual = null;
                }
                Notificar("Planeación eliminada.");
            }
            catch (Exception ex)
            {
                Notificar($"Error al eliminar planeación: {ex.Message}", esError: true);
            }
        }

        // --- Métodos de interacción con el usuario y UI ---
        public void Notificar(string mensaje, bool esError = false)
        {
            MensajeNotificacion = mensaje;
            EsNotificacionError = esError;
        }

        public bool Confirmar(string mensaje, string titulo)
        {
            var res = MessageBox.Show(mensaje, titulo, MessageBoxButton.YesNo, MessageBoxImage.Question);
            return res == MessageBoxResult.Yes;
        }

        public void ActualizarEstado(EstadoConfiguracion? estado)
        {
            if (estado == null) return;

            MostrarAlertaApiKey = !estado.ApiKeyConfigurada;
            MostrarAlertaPerfil = !estado.PerfilConfigurado;
            OnPropertyChanged(nameof(GradosActivosCatalogoTexto));
            OnPropertyChanged(nameof(GradosPerfilResumenTexto));

            if (estado.ApiKeyConfigurada)
            {
                EstadoApiKeyBanner = $"API Key: Conectada ({estado.Modelo})";
            }
            else
            {
                EstadoApiKeyBanner = "API Key: No configurada";
            }
        }

        public void CerrarBackend()
        {
            _backendManager.Dispose();
            _api.Dispose();
        }
    }
}