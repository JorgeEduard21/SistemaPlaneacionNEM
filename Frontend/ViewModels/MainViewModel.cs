using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using Frontend.Models;
using Frontend.Services;
using Frontend.Views;

namespace Frontend.ViewModels
{
    public class MainViewModel : ViewModelBase
    {
        private readonly ApiClient _api;
        private readonly BackendManager _backendManager;

        public event EventHandler? PlaneacionGeneradaExitosamente;

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

        private string _etapaGeneracionTexto = "Analizando el contexto de tu grupo y los PDA seleccionados...";
        public string EtapaGeneracionTexto
        {
            get => _etapaGeneracionTexto;
            set { SetProperty(ref _etapaGeneracionTexto, value); }
        }

        private string _tiempoTranscurridoGeneracionTexto = "Tiempo transcurrido: 0 s  ·  Espera estimada: 20 a 45 segundos";
        public string TiempoTranscurridoGeneracionTexto
        {
            get => _tiempoTranscurridoGeneracionTexto;
            set { SetProperty(ref _tiempoTranscurridoGeneracionTexto, value); }
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
                var g = ConfiguracionVM.PerfilGuardado?.Grados;
                return g != null && g.Count > 0 ? string.Join(", ", g) : "Sin definir (configurar en Contexto)";
            }
        }

        public string AlumnosPerfilResumenTexto
        {
            get
            {
                var n = ConfiguracionVM.PerfilGuardado?.CantidadAlumnos;
                return n.HasValue ? $"{n.Value} alumnos" : "Sin definir (configurar en Contexto)";
            }
        }

        public string EntornoPerfilResumenTexto
        {
            get
            {
                var e = ConfiguracionVM.PerfilGuardado?.Entorno;
                return !string.IsNullOrWhiteSpace(e) ? e : "Sin definir (configurar en Contexto)";
            }
        }

        public string DocenteEscuelaResumenTexto
        {
            get
            {
                var d = ConfiguracionVM.PerfilGuardado?.NombreDocente;
                var esc = ConfiguracionVM.PerfilGuardado?.Escuela;
                if (!string.IsNullOrWhiteSpace(d) && !string.IsNullOrWhiteSpace(esc))
                    return $"{d} · {esc}";
                if (!string.IsNullOrWhiteSpace(d)) return d;
                if (!string.IsNullOrWhiteSpace(esc)) return esc;
                return "";
            }
        }

        public bool TieneDocenteOEscuela => !string.IsNullOrWhiteSpace(DocenteEscuelaResumenTexto);

        public bool PerfilEstaCompleto => ConfiguracionVM.PerfilGuardado?.EstaCompleto == true;
        public bool PerfilIncompleto => !PerfilEstaCompleto;

        public string ResumenPdasSeleccionadosTexto
        {
            get
            {
                var count = PdaLista.Elementos.Count;
                if (count == 0) return "🎯 Ningún PDA seleccionado aún";
                return count == 1 ? "🎯 1 PDA seleccionado para esta planeación" : $"🎯 {count} PDA seleccionados para esta planeación";
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
        public ICommand IrAConfiguracionApiCommand { get; }
        public ICommand IrAConfiguracionPerfilCommand { get; }
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
                NotificarCambiosPerfil();
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
            PdaLista.Cambio += (_, _) =>
            {
                SincronizarChecksPdaSugeridos();
                OnPropertyChanged(nameof(ResumenPdasSeleccionadosTexto));
            };
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
            IrAConfiguracionApiCommand = new RelayCommand(_ => { VistaActual = 1; ConfiguracionVM.SeccionActual = 1; _ = ConfiguracionVM.CargarAsync(); });
            IrAConfiguracionPerfilCommand = new RelayCommand(_ => { VistaActual = 1; ConfiguracionVM.SeccionActual = 0; _ = ConfiguracionVM.CargarAsync(); });
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

            // Detección instantánea en disco antes de arrancar el backend para redireccionar sin ninguna demora
            DetectarVistaInicialRapida();

            // Inicializar Backend y Cargar Configuración en segundo plano
            _ = InicializarAplicacionAsync();
        }

        private void DetectarVistaInicialRapida()
        {
            try
            {
                var baseDir = Environment.GetEnvironmentVariable("PLANEACION_NEM_DATA");
                if (string.IsNullOrWhiteSpace(baseDir))
                {
                    baseDir = Path.Combine(
                        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                        "SistemaPlaneacionNEM");
                }

                bool tieneApiKey = false;
                var rutaConfig = Path.Combine(baseDir, "config.json");
                if (File.Exists(rutaConfig))
                {
                    var jsonConfig = File.ReadAllText(rutaConfig);
                    using var doc = JsonDocument.Parse(jsonConfig);
                    if (doc.RootElement.TryGetProperty("api_key", out var propKey) &&
                        propKey.ValueKind == JsonValueKind.String &&
                        !string.IsNullOrWhiteSpace(propKey.GetString()))
                    {
                        tieneApiKey = true;
                    }
                }

                if (!tieneApiKey)
                {
                    foreach (var envName in new[] { "GEMINI_API_KEY", "GOOGLE_API_KEY", "API_KEY" })
                    {
                        if (!string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable(envName)))
                        {
                            tieneApiKey = true;
                            break;
                        }
                    }
                }

                bool tienePerfilCompleto = false;
                var rutaPerfil = Path.Combine(baseDir, "perfil.json");
                if (File.Exists(rutaPerfil))
                {
                    var jsonPerfil = File.ReadAllText(rutaPerfil);
                    var perfil = JsonSerializer.Deserialize<PerfilGrupo>(jsonPerfil, new JsonSerializerOptions
                    {
                        PropertyNameCaseInsensitive = true
                    });
                    if (perfil != null && perfil.EstaCompleto)
                    {
                        tienePerfilCompleto = true;
                    }
                }

                if (!tieneApiKey)
                {
                    _vistaActual = 1; // Pestaña Configuración inmediata
                    ConfiguracionVM.SeccionActual = 1; // 1 = Tutorial y API Key de Gemini
                    MostrarAlertaApiKey = true;
                    EstadoApiKeyBanner = "API Key: No configurada";
                    Notificar("👋 ¡Bienvenido! Paso 1 (solo la primera vez): Sigue el tutorial en pantalla para activar tu llave gratuita de Google Gemini.");
                }
                else if (!tienePerfilCompleto)
                {
                    _vistaActual = 1; // Pestaña Configuración inmediata
                    ConfiguracionVM.SeccionActual = 0; // 0 = Contexto del Grupo
                    MostrarAlertaPerfil = true;
                    EstadoApiKeyBanner = "API Key: Conectada";
                    Notificar("👋 ¡Bienvenido! Completa el contexto de tu grupo (grados, número de alumnos y entorno) y dale en 'Guardar Perfil' para comenzar.");
                }
                else
                {
                    _vistaActual = 0;
                    EstadoApiKeyBanner = "Iniciando servicio local...";
                }
            }
            catch
            {
                // Si ocurre algún error leyendo archivos locales, InicializarAplicacionAsync tomará el control
            }
        }

        private async Task InicializarAplicacionAsync()
        {
            try
            {
                await _backendManager.IniciarAsync();
                BackendConectado = true;
                await ConfiguracionVM.CargarAsync();
                NotificarCambiosPerfil();

                // Redirección prioritaria inmediata: 1° API Key -> 2° Perfil del Grupo -> 3° Asistente
                if (!ConfiguracionVM.ApiKeyConfigurada)
                {
                    VistaActual = 1; // Pestaña de Configuración
                    ConfiguracionVM.SeccionActual = 1; // Subsección API Key con Tutorial
                    Notificar("👋 ¡Bienvenido! Paso 1 (solo se hace una vez): Sigue el tutorial de aquí abajo para obtener y guardar tu llave gratuita de Google Gemini.");
                }
                else if (!ConfiguracionVM.PerfilGuardado.EstaCompleto)
                {
                    VistaActual = 1; // Pestaña de Configuración
                    ConfiguracionVM.SeccionActual = 0; // Subsección Contexto del Grupo
                    Notificar("👋 ¡Bienvenido! Por favor completa el contexto de tu grupo (grados, número de alumnos y entorno) y haz clic en 'Guardar Perfil' para comenzar.");
                }
                else
                {
                    Notificar("Sistema listo y catálogo NEM cargado.");
                }

                await Task.WhenAll(CargarCatalogoAsync(), CargarHistorialAsync());
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
                Notificar("No has configurado tu API key de Gemini. Sigue el tutorial en pantalla para activarla.", esError: true);
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
            EtapaGeneracionTexto = "Analizando el contexto de tu grupo y los PDA seleccionados...";
            TiempoTranscurridoGeneracionTexto = "Tiempo transcurrido: 0 s  ·  Tiempo estimado: 20 a 45 segundos";
            Notificar("⏳ Generando planeación didáctica con Gemini... Tiempo estimado de espera: 20 a 45 segundos.");

            using var timerCts = new CancellationTokenSource();
            var timerTask = ActualizarRelojGeneracionAsync(timerCts.Token);

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

                timerCts.Cancel();
                GenerandoPlaneacion = false;

                PlaneacionActual = resultado;
                _ = CargarHistorialAsync();
                PasoActual = 3;
                Notificar("⬇️ ¡Planeación didáctica generada exitosamente! Baja en esta pantalla para revisarla o abrirla en Google Docs / Word.");

                PlaneacionGeneradaExitosamente?.Invoke(this, EventArgs.Empty);

                MessageBox.Show(
                    "¡Tu planeación didáctica se ha generado exitosamente! 🎉\n\n" +
                    "⬇️ Recuerda bajar en esta pantalla (desplazarte hacia abajo) para revisar tu planeación completa y usar los botones para abrirla en Google Docs o descargarla en Word.",
                    "¡Planeación Lista!",
                    MessageBoxButton.OK,
                    MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                timerCts.Cancel();
                GenerandoPlaneacion = false;
                await ManejarErrorGeneracionAsync(ex);
            }
            finally
            {
                timerCts.Cancel();
                GenerandoPlaneacion = false;
                try { await timerTask; } catch { }
            }
        }

        private async Task ManejarErrorGeneracionAsync(Exception ex)
        {
            var apiEx = ex as ApiException;
            int status = apiEx?.StatusCode ?? 0;
            string msgLower = (ex.Message ?? "").ToLowerInvariant();

            string modeloIdActual = ConfiguracionVM.Estado?.Modelo ?? "gemini-3.8-flash";
            string nombreApi = modeloIdActual switch
            {
                "gemini-3.5-flash-lite" => "Google Gemini (3.5 Flash-Lite)",
                "gemini-3.1-pro-preview" => "Google Gemini (3.1 Pro)",
                "gemini-3.8-flash" => "Google Gemini (3.8 Flash)",
                _ => "Google Gemini"
            };

            // 1. Límite de tokens / cuota agotada (429 RESOURCE_EXHAUSTED)
            if (status == 429 || msgLower.Contains("límite") || msgLower.Contains("cuota") || msgLower.Contains("quota") || msgLower.Contains("tokens"))
            {
                Notificar($"Tu llave gratuita de {nombreApi} alcanzó su límite de tokens (por minuto o por día).", esError: true);

                if (modeloIdActual != "gemini-3.5-flash-lite")
                {
                    var respuesta = MessageBox.Show(
                        $"Tu llave gratuita de {nombreApi} alcanzó su límite de tokens (por minuto o por día).\n\n" +
                        "En Google Gemini cada modelo cuenta con su propio límite gratuito. El modelo ligero «Gemini 3.5 Flash-Lite» consume menos tokens y tiene disponibilidad adicional.\n\n" +
                        "¿Deseas cambiar automáticamente al modelo ligero (Gemini 3.5 Flash-Lite) para generar tu planeación ahorita mismo sin esperar?\n\n" +
                        "• Sí: Cambiar al modelo ligero y generar mi planeación ahora.\n" +
                        "• No: Esperar un minuto o cambiar de llave en Configuración.",
                        "🔋 Límite de Tokens Gratuitos de Google Gemini",
                        MessageBoxButton.YesNo,
                        MessageBoxImage.Warning);

                    if (respuesta == MessageBoxResult.Yes)
                    {
                        bool cambiado = await ConfiguracionVM.CambiarModeloDirectoAsync("gemini-3.5-flash-lite");
                        if (cambiado)
                        {
                            await GenerarPlaneacionAsync();
                            return;
                        }
                    }
                }
                else
                {
                    var respuesta = MessageBox.Show(
                        $"Tu llave gratuita de {nombreApi} alcanzó su límite de tokens (por minuto o por día).\n\n" +
                        "Tus datos de planeación siguen guardados en pantalla. Para continuar puedes:\n" +
                        "1. Esperar 1 minuto y volver a hacer clic en «Generar Planeación» (si fue límite por minuto).\n" +
                        "2. O bien, colocar otra llave gratuita de Google AI Studio en la pestaña de Configuración.\n\n" +
                        "¿Deseas abrir la pestaña de la Llave de IA (API Key) para revisar o cambiar tu llave?",
                        "🔋 Límite de Tokens Gratuitos de Google Gemini",
                        MessageBoxButton.YesNo,
                        MessageBoxImage.Warning);

                    if (respuesta == MessageBoxResult.Yes)
                    {
                        VistaActual = 1;
                        ConfiguracionVM.SeccionActual = 1;
                    }
                }
                return;
            }

            // 2. Llave inválida, revocada o sin permisos (401 / 403)
            if (status == 401 || status == 403 || msgLower.Contains("api key no es válida") || msgLower.Contains("permisos"))
            {
                Notificar("La llave de Google Gemini no es válida o fue desactivada. Revísala en Configuración.", esError: true);
                MessageBox.Show(
                    $"Tu llave gratuita de {nombreApi} no pudo validarse (puede haberse copiado incompleta o haber sido eliminada en tu cuenta de Google).\n\n" +
                    "Todos los datos que escribiste para tu planeación siguen guardados. Te llevaremos a la pestaña de la Llave de IA para que puedas pegarla de nuevo.",
                    "🔑 Revisión de Llave de Google Gemini",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);
                VistaActual = 1;
                ConfiguracionVM.SeccionActual = 1;
                return;
            }

            // 3. Problemas de conexión a internet o saturación de servidores de Google (503 / 504 / 408)
            if (status == 503 || status == 504 || status == 408 || msgLower.Contains("internet") || msgLower.Contains("tardó demasiado"))
            {
                Notificar($"Demora o desconexión temporal al comunicar con {nombreApi}.", esError: true);
                MessageBox.Show(
                    $"La conexión a internet o el servidor de {nombreApi} tardó demasiado en responder.\n\n" +
                    "No te preocupes: todos los datos que seleccionaste para tu planeación siguen intactos en pantalla. Verifica tu conexión a internet y vuelve a hacer clic en «¡Generar Planeación con Gemini! 🚀».",
                    "🌐 Conexión Temporal con Google Gemini",
                    MessageBoxButton.OK,
                    MessageBoxImage.Information);
                return;
            }

            // 4. Otros avisos devueltos por la API
            Notificar($"Aviso de {nombreApi}: {ex.Message}", esError: true);
            MessageBox.Show(
                $"El servicio de {nombreApi} reportó lo siguiente:\n\n" +
                $"«{ex.Message}»\n\n" +
                "Tus datos de planeación siguen guardados en pantalla para que puedas intentarlo de nuevo.",
                $"ℹ️ Aviso de {nombreApi}",
                MessageBoxButton.OK,
                MessageBoxImage.Information);
        }

        private async Task ActualizarRelojGeneracionAsync(CancellationToken token)
        {
            var inicio = DateTime.UtcNow;
            while (!token.IsCancellationRequested)
            {
                try
                {
                    await Task.Delay(1000, token);
                }
                catch (OperationCanceledException)
                {
                    break;
                }

                int seg = (int)(DateTime.UtcNow - inicio).TotalSeconds;
                TiempoTranscurridoGeneracionTexto = $"Tiempo transcurrido: {seg} s  ·  Tiempo estimado total: 20 a 45 segundos";

                if (seg < 7)
                    EtapaGeneracionTexto = "Analizando el contexto de tu grupo y los PDA seleccionados...";
                else if (seg < 18)
                    EtapaGeneracionTexto = $"Diseñando las sesiones con la metodología {Metodologia}...";
                else if (seg < 28)
                    EtapaGeneracionTexto = $"Estructurando las tablas, actividades y el instrumento ({Instrumento})...";
                else
                    EtapaGeneracionTexto = "Afinando los últimos detalles y el formato de tu planeación, ya casi termina...";
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
                AsistenteGoogleDocsWindow.MostrarEInsertar(planeacion, () => DescargarWordDePlaneacionAsync(planeacion.Id));
                Notificar("Abriendo Google Docs e insertando tu planeación automáticamente... Si tu internet tarda en cargar, usa el botón naranja de la esquina superior derecha.");
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

        public void NotificarCambiosPerfil()
        {
            OnPropertyChanged(nameof(GradosActivosCatalogoTexto));
            OnPropertyChanged(nameof(GradosPerfilResumenTexto));
            OnPropertyChanged(nameof(AlumnosPerfilResumenTexto));
            OnPropertyChanged(nameof(EntornoPerfilResumenTexto));
            OnPropertyChanged(nameof(DocenteEscuelaResumenTexto));
            OnPropertyChanged(nameof(TieneDocenteOEscuela));
            OnPropertyChanged(nameof(PerfilEstaCompleto));
            OnPropertyChanged(nameof(PerfilIncompleto));
            OnPropertyChanged(nameof(ResumenPdasSeleccionadosTexto));
        }

        public void ActualizarEstado(EstadoConfiguracion? estado)
        {
            if (estado == null) return;

            MostrarAlertaApiKey = !estado.ApiKeyConfigurada;
            MostrarAlertaPerfil = !estado.PerfilConfigurado;
            NotificarCambiosPerfil();

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