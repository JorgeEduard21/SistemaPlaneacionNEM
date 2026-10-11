using System.Net;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json;
using Frontend.Models;

namespace Frontend.Services
{
    /// <summary>Error de la API con un mensaje listo para mostrarse al docente.</summary>
    public sealed class ApiException : Exception
    {
        public int StatusCode { get; }
        public ApiException(string mensaje, int statusCode = 0, Exception? inner = null) : base(mensaje, inner)
            => StatusCode = statusCode;
    }

    /// <summary>Cliente HTTP del backend local (FastAPI).</summary>
    public sealed class ApiClient : IDisposable
    {
        public const int Puerto = 8765;
        public static readonly Uri UrlBase = new($"http://127.0.0.1:{Puerto}/");

        private static readonly TimeSpan TiempoNormal = TimeSpan.FromSeconds(30);
        private static readonly TimeSpan TiempoGeneracion = TimeSpan.FromMinutes(5);

        private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web)
        {
            PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
        };

        private readonly HttpClient _http = new() { BaseAddress = UrlBase, Timeout = Timeout.InfiniteTimeSpan };

        // ------------------------------------------------------------------ Salud
        public async Task<bool> EstaDisponibleAsync(CancellationToken ct = default)
        {
            try
            {
                using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
                cts.CancelAfter(TimeSpan.FromSeconds(2));
                using var r = await _http.GetAsync("api/salud", cts.Token);
                return r.IsSuccessStatusCode;
            }
            catch { return false; }
        }

        // ---------------------------------------------------------- Configuración
        public Task<EstadoConfiguracion> ObtenerConfiguracionAsync()
            => EnviarAsync<EstadoConfiguracion>(HttpMethod.Get, "api/configuracion");

        public Task<EstadoConfiguracion> GuardarApiKeyAsync(string apiKey, bool validar = true)
            => EnviarAsync<EstadoConfiguracion>(HttpMethod.Put, "api/configuracion/api-key", new { api_key = apiKey, validar });

        public Task<EstadoConfiguracion> EliminarApiKeyAsync()
            => EnviarAsync<EstadoConfiguracion>(HttpMethod.Delete, "api/configuracion/api-key");

        public Task<ResultadoPrueba> ProbarApiKeyAsync(string? apiKey)
            => EnviarAsync<ResultadoPrueba>(HttpMethod.Post, "api/configuracion/api-key/probar", new { api_key = apiKey });

        public Task<EstadoConfiguracion> CambiarModeloAsync(string modelo)
            => EnviarAsync<EstadoConfiguracion>(HttpMethod.Put, "api/configuracion/modelo", new { modelo });

        // ------------------------------------------------------------------ Perfil
        public Task<PerfilGrupo> ObtenerPerfilAsync()
            => EnviarAsync<PerfilGrupo>(HttpMethod.Get, "api/perfil");

        public Task<PerfilGrupo> GuardarPerfilAsync(PerfilGrupo perfil)
            => EnviarAsync<PerfilGrupo>(HttpMethod.Put, "api/perfil", perfil);

        // ------------------------------------------------------------ Planeaciones
        public Task<Planeacion> GenerarPlaneacionAsync(SolicitudPlaneacion solicitud, CancellationToken ct)
            => EnviarAsync<Planeacion>(HttpMethod.Post, "api/planeaciones/generar", solicitud, TiempoGeneracion, ct);

        public Task<List<ResumenPlaneacion>> ListarPlaneacionesAsync()
            => EnviarAsync<List<ResumenPlaneacion>>(HttpMethod.Get, "api/planeaciones");

        public Task<Planeacion> ObtenerPlaneacionAsync(string id)
            => EnviarAsync<Planeacion>(HttpMethod.Get, $"api/planeaciones/{id}");

        public Task<Planeacion> ActualizarPlaneacionAsync(string id, object cambios)
            => EnviarAsync<Planeacion>(HttpMethod.Patch, $"api/planeaciones/{id}", cambios);

        public Task EliminarPlaneacionAsync(string id)
            => EnviarAsync<object?>(HttpMethod.Delete, $"api/planeaciones/{id}");

        public async Task<(byte[] Contenido, string NombreArchivo)> DescargarDocxAsync(string id)
        {
            using var r = await EjecutarAsync(new HttpRequestMessage(HttpMethod.Get, $"api/planeaciones/{id}/docx"), TiempoNormal, default);
            var bytes = await r.Content.ReadAsByteArrayAsync();
            var nombre = r.Content.Headers.ContentDisposition?.FileNameStar
                         ?? r.Content.Headers.ContentDisposition?.FileName?.Trim('"')
                         ?? "planeacion.docx";
            return (bytes, nombre);
        }

        // --------------------------------------------------------------- Internos
        private async Task<T> EnviarAsync<T>(HttpMethod metodo, string ruta, object? cuerpo = null,
                                             TimeSpan? tiempo = null, CancellationToken ct = default)
        {
            var solicitud = new HttpRequestMessage(metodo, ruta);
            if (cuerpo != null)
                solicitud.Content = JsonContent.Create(cuerpo, cuerpo.GetType(), options: Json);

            using var r = await EjecutarAsync(solicitud, tiempo ?? TiempoNormal, ct);
            if (r.StatusCode == HttpStatusCode.NoContent || typeof(T) == typeof(object))
                return default!;
            return (await r.Content.ReadFromJsonAsync<T>(Json, ct))!;
        }

        private async Task<HttpResponseMessage> EjecutarAsync(HttpRequestMessage solicitud, TimeSpan tiempo, CancellationToken ct)
        {
            using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
            cts.CancelAfter(tiempo);
            HttpResponseMessage r;
            try
            {
                r = await _http.SendAsync(solicitud, cts.Token);
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested)
            {
                throw;
            }
            catch (OperationCanceledException ex)
            {
                throw new ApiException("La operación tardó demasiado. Intenta de nuevo.", 408, ex);
            }
            catch (HttpRequestException ex)
            {
                throw new ApiException("No se pudo conectar con el servidor local de la aplicación.", 0, ex);
            }

            if (r.IsSuccessStatusCode) return r;

            var mensaje = await LeerMensajeErrorAsync(r);
            var codigo = (int)r.StatusCode;
            r.Dispose();
            throw new ApiException(mensaje, codigo);
        }

        private static async Task<string> LeerMensajeErrorAsync(HttpResponseMessage r)
        {
            try
            {
                using var doc = JsonDocument.Parse(await r.Content.ReadAsStringAsync());
                if (doc.RootElement.TryGetProperty("detail", out var detalle))
                {
                    if (detalle.ValueKind == JsonValueKind.String)
                        return detalle.GetString()!;
                    if (detalle.ValueKind == JsonValueKind.Array && detalle.GetArrayLength() > 0)
                    {
                        // Errores de validación de FastAPI (422)
                        var primero = detalle[0];
                        var campo = primero.TryGetProperty("loc", out var loc) && loc.GetArrayLength() > 0
                            ? loc[loc.GetArrayLength() - 1].ToString() : "";
                        var msg = primero.TryGetProperty("msg", out var m) ? m.GetString() : "";
                        return $"Dato inválido{(string.IsNullOrEmpty(campo) ? "" : $" en '{campo}'")}: {msg}";
                    }
                }
            }
            catch { /* respuesta no JSON */ }
            return $"El servidor respondió con un error ({(int)r.StatusCode}).";
        }

        public void Dispose() => _http.Dispose();
    }
}

