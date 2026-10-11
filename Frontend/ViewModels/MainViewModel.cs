using System;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
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
            set { SetProperty(ref _campoFormativo, value); }
        }

        public GrupoOpciones EjesArticuladoresOpciones { get; }

        private string _contenido = "";
        public string Contenido
        {
            get => _contenido;
            set { SetProperty(ref _contenido, value); }
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
                    OnPropertyChanged(nameof(TienePlaneacionGenerada));
                    OnPropertyChanged(nameof(TextoResultadoMarkdown));
                }
            }
        }

        public bool TienePlaneacionGenerada => PlaneacionActual != null;

        // Campo de respaldo para el texto resultado Markdown
        private string _textoResultadoMarkdown = string.Empty;

        // Propiedad ahora escribible con notificación de cambio
        public string TextoResultadoMarkdown
        {
            get => _textoResultadoMarkdown;
            set => SetProperty(ref _textoResultadoMarkdown, value);
        }

        // --- Historial ---
        public ObservableCollection<ResumenPlaneacion> Historial { get; } = new();

        private ResumenPlaneacion? _planeacionSeleccionadaHistorial;
        public ResumenPlaneacion? PlaneacionSeleccionadaHistorial
        {
            get => _planeacionSeleccionadaHistorial;
            set { SetProperty(ref _planeacionSeleccionadaHistorial, value); }
        }

        // --- Comandos ---
        public ICommand ContinuarCommand { get; }
        public ICommand AtrasCommand { get; }
        public ICommand IrAVistaWizardCommand { get; }
        public ICommand IrAVistaConfiguracionCommand { get; }
        public ICommand IrAVistaHistorialCommand { get; }
        public ICommand GenerarPlaneacionCommand { get; }
        public ICommand DescargarWordCommand { get; }
        public ICommand CargarDetalleHistorialCommand { get; }
        public ICommand EliminarDeHistorialCommand { get; }
        public ICommand CerrarNotificacionCommand { get; }

        public MainViewModel()
        {
            _api = new ApiClient();
            _backendManager = new BackendManager(_api);
            ConfiguracionVM = new ConfiguracionViewModel(this, _api);

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
            CriteriosLista = new ListaEditable();

            // Configurar manejador global de errores en AsyncRelayCommand
            AsyncRelayCommand.ManejadorErroresGlobal = ex =>
            {
                Notificar(ex.Message, esError: true);
            };

            // Comandos de Wizard
            ContinuarCommand = new RelayCommand(_ => AvanzarPaso(), _ => PasoActual < 3);
            AtrasCommand = new RelayCommand(_ => RetrocederPaso(), _ => PasoActual > 0);

            // Comandos de Navegación de Vistas
            IrAVistaWizardCommand = new RelayCommand(_ => VistaActual = 0);
            IrAVistaConfiguracionCommand = new RelayCommand(_ => { VistaActual = 1; _ = ConfiguracionVM.CargarAsync(); });
            IrAVistaHistorialCommand = new RelayCommand(_ => VistaActual = 2);

            // Generación de planeación con IA
            GenerarPlaneacionCommand = new AsyncRelayCommand(_ => GenerarPlaneacionAsync(), _ => !GenerandoPlaneacion);
            DescargarWordCommand = new AsyncRelayCommand(_ => DescargarWordAsync(), _ => TienePlaneacionGenerada);

            // Historial
            CargarDetalleHistorialCommand = new AsyncRelayCommand(p => CargarDetalleHistorialAsync(p as ResumenPlaneacion));
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
                Notificar("Backend conectado correctamente.");
            }
            catch (Exception ex)
            {
                BackendConectado = false;
                Notificar($"Error al iniciar el servidor local de IA: {ex.Message}", esError: true);
            }
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
            // Validaciones
            if (ConfiguracionVM.Estado?.ApiKeyConfigurada != true)
            {
                Notificar("No has configurado tu API key de Gemini. Ve al menú de Configuración para agregarla.", esError: true);
                VistaActual = 1;
                ConfiguracionVM.SeccionActual = 1; // Pestaña de API Key
                return;
            }

            if (!ConfiguracionVM.PerfilGuardado.EstaCompleto)
            {
                Notificar("Debes completar el contexto de tu grupo una vez en Configuración.", esError: true);
                VistaActual = 1;
                ConfiguracionVM.SeccionActual = 0; // Pestaña de Perfil
                return;
            }

            if (string.IsNullOrWhiteSpace(Contenido))
            {
                Notificar("Debes escribir el contenido curricular en el Paso 1.", esError: true);
                PasoActual = 0;
                return;
            }

            if (string.IsNullOrWhiteSpace(TemaInteres))
            {
                Notificar("Debes indicar el tema de interés o proyecto central en el Paso 2.", esError: true);
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
                PasoActual = 3; // Mostrar pantalla de resumen/resultado
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

            try
            {
                var (contenido, nombre) = await _api.DescargarDocxAsync(PlaneacionActual.Id);
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
                VistaActual = 0; // Ir a la pantalla de visualización
                PasoActual = 3;
                Notificar($"Planeación '{plan.Titulo}' cargada.");
            }
            catch (Exception ex)
            {
                Notificar($"Error al abrir planeación: {ex.Message}", esError: true);
            }
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