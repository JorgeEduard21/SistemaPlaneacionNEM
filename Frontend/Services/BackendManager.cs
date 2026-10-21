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
            catch (System.ComponentModel.Win32Exception ex)
            {
                throw new InvalidOperationException(
                    $"No se pudo ejecutar el servidor local ({lanzador.ejecutable}): {ex.Message}");
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
                if (File.Exists(Path.Combine(personalizada, "main.py")))
                    return (BuscarPython(personalizada), "main.py", personalizada);

                var exeCustom = Path.Combine(personalizada, "backend.exe");
                if (File.Exists(exeCustom))
                    return (exeCustom, "", personalizada);
            }

            var dirBase = AppContext.BaseDirectory;

            // 1. Producción instalada: carpeta backend/ junto a Frontend.exe (con su propio runtime portable python/python.exe firmado)
            var dirBackendInstalado = Path.Combine(dirBase, "backend");
            if (File.Exists(Path.Combine(dirBackendInstalado, "main.py")))
                return (BuscarPython(dirBackendInstalado), "main.py", dirBackendInstalado);

            // 2. Modo desarrollo: subir directorios buscando la carpeta backend con main.py
            var dir = new DirectoryInfo(dirBase);
            while (dir != null)
            {
                var candidata = Path.Combine(dir.FullName, "backend");
                if (Directory.Exists(candidata) && File.Exists(Path.Combine(candidata, "main.py")))
                {
                    return (BuscarPython(candidata), "main.py", candidata);
                }
                dir = dir.Parent;
            }

            return null;
        }

        private static string BuscarPython(string carpetaBackend)
        {
            // 1. Runtime de Python portable incluido en el instalador (firmado digitalmente por Python Software Foundation)
            var pythonPortable = Path.Combine(carpetaBackend, "python", "python.exe");
            if (File.Exists(pythonPortable)) return pythonPortable;

            // 2. Entorno virtual de desarrollo (venv / .venv)
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

