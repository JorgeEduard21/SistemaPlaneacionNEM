using System.Diagnostics;
using Frontend.Models;
using Frontend.Services;

namespace Frontend.ViewModels
{
    /// <summary>
    /// Configuración persistente: perfil del grupo (se captura una sola vez) y
    /// la API key de Gemini / modelo.
    /// </summary>
    public class ConfiguracionViewModel : ViewModelBase
    {
        private readonly MainViewModel _shell;
        private readonly ApiClient _api;
        private bool _cargando;

        public event EventHandler? PerfilActualizado;

        public ConfiguracionViewModel(MainViewModel shell, ApiClient api)
        {
            _shell = shell;
            _api = api;

            Grados = new GrupoOpciones(true);
            foreach (var g in new[] { "1° Primaria", "2° Primaria", "3° Primaria", "4° Primaria", "5° Primaria", "6° Primaria" })
                Grados.Agregar(g);

            Entornos = new GrupoOpciones(false)
                .Agregar("Urbano").Agregar("Semiurbano").Agregar("Rural").Agregar("Indígena").Agregar("Migrante");

            Vark = new GrupoOpciones(true)
                .Agregar("Visual", "👁️").Agregar("Auditivo", "👂").Agregar("Lectura/Escritura", "📖").Agregar("Kinestésico", "🤸");

            Barreras = new ListaEditable();

            foreach (var g in new object[] { Grados, Entornos, Vark })
                ((GrupoOpciones)g).Cambio += (_, _) => MarcarCambio();
            Barreras.Cambio += (_, _) => MarcarCambio();

            GuardarPerfilCommand = new AsyncRelayCommand(_ => GuardarPerfilAsync(), _ => HayCambios);
            DescartarCambiosCommand = new RelayCommand(_ => CargarPerfilEnFormulario(PerfilGuardado), _ => HayCambios);

            ProbarApiKeyCommand = new AsyncRelayCommand(_ => ProbarAsync(), _ => !Probando && (Estado?.ApiKeyConfigurada == true || !string.IsNullOrWhiteSpace(ApiKeyInput)));
            GuardarApiKeyCommand = new AsyncRelayCommand(_ => GuardarApiKeyAsync(), _ => !Probando && !string.IsNullOrWhiteSpace(ApiKeyInput));
            EliminarApiKeyCommand = new AsyncRelayCommand(_ => EliminarApiKeyAsync(), _ => Estado?.ApiKeyOrigen == "usuario");
            AbrirAiStudioCommand = new RelayCommand(_ => AbrirUrl("https://aistudio.google.com/apikey"));
            AbrirCarpetaDatosCommand = new RelayCommand(_ => { if (Estado != null) AbrirUrl(Estado.DirectorioDatos); });
        }

        // ================================================================ Secciones
        private int _seccionActual;
        /// <summary>0 = Perfil del grupo, 1 = API de Gemini.</summary>
        public int SeccionActual { get => _seccionActual; set => SetProperty(ref _seccionActual, value); }

        // ================================================================== Perfil
        private PerfilGrupo _perfilGuardado = new();
        public PerfilGrupo PerfilGuardado
        {
            get => _perfilGuardado;
            private set
            {
                if (SetProperty(ref _perfilGuardado, value))
                {
                    OnPropertyChanged(nameof(UltimaActualizacion));
                }
            }
        }

        private string _nombreDocente = "";
        public string NombreDocente { get => _nombreDocente; set { if (SetProperty(ref _nombreDocente, value)) MarcarCambio(); } }
        private string _escuela = "";
        public string Escuela { get => _escuela; set { if (SetProperty(ref _escuela, value)) MarcarCambio(); } }
        private string _cicloEscolar = "";
        public string CicloEscolar { get => _cicloEscolar; set { if (SetProperty(ref _cicloEscolar, value)) MarcarCambio(); } }
        private string _cantidadAlumnos = "";
        public string CantidadAlumnos { get => _cantidadAlumnos; set { if (SetProperty(ref _cantidadAlumnos, value)) MarcarCambio(); } }
        private string _region = "";
        public string Region { get => _region; set { if (SetProperty(ref _region, value)) MarcarCambio(); } }
        private string _caracteristicas = "";
        public string Caracteristicas { get => _caracteristicas; set { if (SetProperty(ref _caracteristicas, value)) MarcarCambio(); } }

        public GrupoOpciones Grados { get; }
        public GrupoOpciones Entornos { get; }
        public GrupoOpciones Vark { get; }
        public ListaEditable Barreras { get; }

        private bool _hayCambios;
        public bool HayCambios { get => _hayCambios; private set => SetProperty(ref _hayCambios, value); }

        private string? _errorPerfil;
        public string? ErrorPerfil { get => _errorPerfil; private set => SetProperty(ref _errorPerfil, value); }

        public string UltimaActualizacion => string.IsNullOrEmpty(PerfilGuardado.Actualizado)
            ? "Aún no has guardado el perfil."
            : $"Última actualización: {Fechas.Legible(PerfilGuardado.Actualizado)}";

        public AsyncRelayCommand GuardarPerfilCommand { get; }
        public RelayCommand DescartarCambiosCommand { get; }

        private void MarcarCambio()
        {
            if (_cargando) return;
            HayCambios = true;
            ErrorPerfil = null;
        }

        private void CargarPerfilEnFormulario(PerfilGrupo p)
        {
            _cargando = true;
            try
            {
                NombreDocente = p.NombreDocente;
                Escuela = p.Escuela;
                CicloEscolar = string.IsNullOrEmpty(p.CicloEscolar) ? CicloSugerido() : p.CicloEscolar;
                CantidadAlumnos = p.CantidadAlumnos?.ToString() ?? "";
                Region = p.Region;
                Caracteristicas = p.Caracteristicas;
                Grados.Establecer(p.Grados);
                Entornos.Establecer(p.Entorno);
                Vark.Establecer(p.EstilosAprendizaje);
                Barreras.Establecer(p.BarrerasAprendizaje);
            }
            finally { _cargando = false; }
            HayCambios = false;
            ErrorPerfil = null;
            OnPropertyChanged(nameof(UltimaActualizacion));
        }

        private static string CicloSugerido()
        {
            var hoy = DateTime.Today;
            var inicio = hoy.Month >= 8 ? hoy.Year : hoy.Year - 1;
            return $"{inicio}-{inicio + 1}";
        }

        private string? ValidarPerfil()
        {
            if (!Grados.TieneSeleccion) return "Selecciona al menos un grado.";
            if (!int.TryParse(CantidadAlumnos.Trim(), out var n) || n < 1 || n > 200)
                return "El número de alumnos debe ser un número entre 1 y 200.";
            if (!Entornos.TieneSeleccion) return "Selecciona el entorno sociocultural.";
            return null;
        }

        private async Task GuardarPerfilAsync()
        {
            ErrorPerfil = ValidarPerfil();
            if (ErrorPerfil != null) return;

            var perfil = new PerfilGrupo
            {
                NombreDocente = NombreDocente.Trim(),
                Escuela = Escuela.Trim(),
                CicloEscolar = CicloEscolar.Trim(),
                Grados = Grados.Seleccionadas,
                CantidadAlumnos = int.Parse(CantidadAlumnos.Trim()),
                Region = Region.Trim(),
                Entorno = Entornos.Seleccion ?? "",
                EstilosAprendizaje = Vark.Seleccionadas,
                BarrerasAprendizaje = Barreras.ComoLista(),
                Caracteristicas = Caracteristicas.Trim(),
            };
            PerfilGuardado = await _api.GuardarPerfilAsync(perfil);
            CargarPerfilEnFormulario(PerfilGuardado);
            PerfilActualizado?.Invoke(this, EventArgs.Empty);
            await RefrescarEstadoAsync();
            _shell.Notificar("Perfil del grupo guardado. Se usará en todas tus planeaciones.");
        }

        // ===================================================================== API
        private EstadoConfiguracion? _estado;
        public EstadoConfiguracion? Estado
        {
            get => _estado;
            private set
            {
                _estado = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(EstadoApiKeyTexto));
                OnPropertyChanged(nameof(ApiKeyConfigurada));
                OnPropertyChanged(nameof(Modelos));
                _cargando = true;
                ModeloSeleccionado = value?.Modelo;
                _cargando = false;
                _shell.ActualizarEstado(value);
            }
        }

        public bool ApiKeyConfigurada => Estado?.ApiKeyConfigurada == true;

        public string EstadoApiKeyTexto => Estado switch
        {
            null => "",
            { ApiKeyConfigurada: false } => "No hay ninguna API key configurada.",
            { ApiKeyOrigen: "entorno" } => $"Usando la clave del archivo .env ({Estado.ApiKeyMascara}). Guarda tu propia clave para no depender de él.",
            _ => $"API key guardada de forma segura: {Estado.ApiKeyMascara}",
        };

        private string _apiKeyInput = "";
        public string ApiKeyInput
        {
            get => _apiKeyInput;
            set { if (SetProperty(ref _apiKeyInput, value)) ResultadoPrueba = null; }
        }

        private bool _mostrarApiKey;
        public bool MostrarApiKey { get => _mostrarApiKey; set => SetProperty(ref _mostrarApiKey, value); }

        private bool _probando;
        public bool Probando { get => _probando; private set => SetProperty(ref _probando, value); }

        private ResultadoPrueba? _resultadoPrueba;
        public ResultadoPrueba? ResultadoPrueba { get => _resultadoPrueba; private set => SetProperty(ref _resultadoPrueba, value); }

        public List<ModeloIa> Modelos => Estado?.ModelosDisponibles ?? new();

        private string? _modeloSeleccionado;
        public string? ModeloSeleccionado
        {
            get => _modeloSeleccionado;
            set
            {
                if (!SetProperty(ref _modeloSeleccionado, value) || _cargando || value == null || value == Estado?.Modelo) return;
                _ = CambiarModeloAsync(value);
            }
        }

        public AsyncRelayCommand ProbarApiKeyCommand { get; }
        public AsyncRelayCommand GuardarApiKeyCommand { get; }
        public AsyncRelayCommand EliminarApiKeyCommand { get; }
        public RelayCommand AbrirAiStudioCommand { get; }
        public RelayCommand AbrirCarpetaDatosCommand { get; }

        private async Task ProbarAsync()
        {
            Probando = true;
            try
            {
                ResultadoPrueba = await _api.ProbarApiKeyAsync(string.IsNullOrWhiteSpace(ApiKeyInput) ? null : ApiKeyInput.Trim());
            }
            finally { Probando = false; }
        }

        private async Task GuardarApiKeyAsync()
        {
            Probando = true;
            ResultadoPrueba = null;
            try
            {
                Estado = await _api.GuardarApiKeyAsync(ApiKeyInput.Trim(), validar: true);
                ApiKeyInput = "";
                ResultadoPrueba = new ResultadoPrueba { Valida = true, Mensaje = "API key verificada y guardada." };
                _shell.Notificar("API key guardada. ¡Ya puedes generar planeaciones!");
            }
            catch (ApiException ex)
            {
                ResultadoPrueba = new ResultadoPrueba { Valida = false, Mensaje = ex.Message };
            }
            finally { Probando = false; }
        }

        private async Task EliminarApiKeyAsync()
        {
            if (!_shell.Confirmar("¿Seguro que deseas eliminar la API key guardada?", "Eliminar API key")) return;
            Estado = await _api.EliminarApiKeyAsync();
            ResultadoPrueba = null;
            _shell.Notificar("API key eliminada.");
        }

        private async Task CambiarModeloAsync(string modelo)
        {
            try
            {
                Estado = await _api.CambiarModeloAsync(modelo);
                _shell.Notificar("Modelo actualizado.");
            }
            catch (ApiException ex)
            {
                _shell.Notificar(ex.Message, true);
                _cargando = true; ModeloSeleccionado = Estado?.Modelo; _cargando = false;
            }
        }

        // ================================================================== Carga
        public async Task CargarAsync()
        {
            var perfilTask = _api.ObtenerPerfilAsync();
            var estadoTask = _api.ObtenerConfiguracionAsync();
            await Task.WhenAll(perfilTask, estadoTask);
            PerfilGuardado = perfilTask.Result;
            Estado = estadoTask.Result;
            OnPropertyChanged(nameof(Modelos));
            CargarPerfilEnFormulario(PerfilGuardado);
            PerfilActualizado?.Invoke(this, EventArgs.Empty);
        }

        public async Task RefrescarEstadoAsync() => Estado = await _api.ObtenerConfiguracionAsync();

        private static void AbrirUrl(string url)
        {
            try { Process.Start(new ProcessStartInfo(url) { UseShellExecute = true }); } catch { }
        }
    }
}
