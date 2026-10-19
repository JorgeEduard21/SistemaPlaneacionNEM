using System.Diagnostics;
using System.IO;
using System.Text;

namespace Frontend.Services
{
    /// <summary>
    /// Arranca automáticamente el backend de Python si no está corriendo y lo
    /// detiene al cerrar la aplicación (solo si fue iniciado por nosotros).
    /// </summary>
    public sealed class BackendManager : IDisposable
    {
        private readonly ApiClient _api;
        private Process? _proceso;
        private readonly StringBuilder _errores = new();

        public BackendManager(ApiClient api) => _api = api;

        public async Task IniciarAsync(CancellationToken ct = default)
        {
            if (await _api.EstaDisponibleAsync(ct)) return; // ya está corriendo (p. ej. iniciado a mano)

            var lanzador = BuscarLanzadorBackend()
                ?? throw new InvalidOperationException(
                    "No se encontró el backend (backend.exe o main.py). " +
                    "Verifica la instalación o define la variable de entorno PLANEACION_NEM_BACKEND.");

            _errores.Clear();

            var info = new ProcessStartInfo(lanzador.ejecutable, lanzador.argumentos)
            {
                WorkingDirectory = lanzador.directorioTrabajo,
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardError = true,
                RedirectStandardOutput = true,
                StandardErrorEncoding = Encoding.UTF8,
                StandardOutputEncoding = Encoding.UTF8,
            };
            info.Environment["PYTHONIOENCODING"] = "utf-8";
            info.Environment["PYTHONUNBUFFERED"] = "1";

            try
            {
                _proceso = Process.Start(info) ?? throw new InvalidOperationException("No se pudo iniciar el servicio de backend.");
            }
            catch (System.ComponentModel.Win32Exception)
            {
                throw new InvalidOperationException(
                    "No se pudo ejecutar el backend. Si estás en modo desarrollo, asegúrate de tener Python instalado.");
            }

            _proceso.ErrorDataReceived += (_, e) => { if (e.Data != null) lock (_errores) _errores.AppendLine(e.Data); };
            _proceso.OutputDataReceived += (_, _) => { };
            _proceso.BeginErrorReadLine();
            _proceso.BeginOutputReadLine();

            // Espera hasta 25 s a que el servidor responda.
            var limite = DateTime.UtcNow.AddSeconds(25);
            while (DateTime.UtcNow < limite)
            {
                ct.ThrowIfCancellationRequested();
                if (_proceso.HasExited)
                    throw new InvalidOperationException(DiagnosticarFallo());
                if (await _api.EstaDisponibleAsync(ct)) return;
                await Task.Delay(400, ct);
            }
            throw new InvalidOperationException("El servidor tardó demasiado en iniciar.\n\n" + UltimasLineas());
        }

        private string DiagnosticarFallo()
        {
            var log = UltimasLineas();
            if (log.Contains("ModuleNotFoundError") || log.Contains("No module named"))
                return "Faltan dependencias de Python. Ejecuta 'backend\\instalar.bat' una vez y vuelve a intentar.\n\n" + log;
            if (log.Contains("address already in use") || log.Contains("10048"))
                return $"El puerto {ApiClient.Puerto} está ocupado por otro programa.\n\n" + log;
            return "El servidor del backend se cerró inesperadamente.\n\n" + log;
        }

        private string UltimasLineas()
        {
            lock (_errores)
            {
                var lineas = _errores.ToString().Split('\n', StringSplitOptions.RemoveEmptyEntries);
                return string.Join("\n", lineas.Skip(Math.Max(0, lineas.Length - 8)));
            }
        }

        private static (string ejecutable, string argumentos, string directorioTrabajo)? BuscarLanzadorBackend()
        {
            var personalizada = Environment.GetEnvironmentVariable("PLANEACION_NEM_BACKEND");
            if (!string.IsNullOrWhiteSpace(personalizada))
            {
                var exeCustom = Path.Combine(personalizada, "backend.exe");
                if (File.Exists(exeCustom))
                    return (exeCustom, "", personalizada);

                if (File.Exists(Path.Combine(personalizada, "main.py")))
                    return (BuscarPython(personalizada), "main.py", personalizada);
            }

            var dirBase = AppContext.BaseDirectory;

            // 1. Producción instalada: backend/backend.exe dentro de la carpeta instalada
            var exeEnBackend = Path.Combine(dirBase, "backend", "backend.exe");
            if (File.Exists(exeEnBackend))
                return (exeEnBackend, "", Path.Combine(dirBase, "backend"));

            // 2. Producción instalada: backend.exe al lado de Frontend.exe
            var exeAlLado = Path.Combine(dirBase, "backend.exe");
            if (File.Exists(exeAlLado))
                return (exeAlLado, "", dirBase);

            // 3. Modo desarrollo: subir directorios buscando la carpeta backend
            var dir = new DirectoryInfo(dirBase);
            while (dir != null)
            {
                var candidata = Path.Combine(dir.FullName, "backend");
                if (Directory.Exists(candidata))
                {
                    // Buscar si fue compilado con PyInstaller en dist/backend/backend.exe
                    var exeDist = Path.Combine(candidata, "dist", "backend", "backend.exe");
                    if (File.Exists(exeDist))
                        return (exeDist, "", Path.Combine(candidata, "dist", "backend"));

                    if (File.Exists(Path.Combine(candidata, "main.py")))
                        return (BuscarPython(candidata), "main.py", candidata);
                }
                dir = dir.Parent;
            }

            return null;
        }

        private static string BuscarPython(string carpetaBackend)
        {
            foreach (var venv in new[] { "venv", ".venv" })
            {
                var exe = Path.Combine(carpetaBackend, venv, "Scripts", "python.exe");
                if (File.Exists(exe)) return exe;
            }
            return "python"; // del PATH
        }

        public void Dispose()
        {
            try
            {
                if (_proceso is { HasExited: false })
                    _proceso.Kill(entireProcessTree: true);
            }
            catch { /* ya terminó */ }
            _proceso?.Dispose();
        }
    }
}

