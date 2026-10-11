using System.IO;
using System.Text.Json;

namespace Frontend.Services
{
    /// <summary>Guarda localmente el borrador de la planeación en curso para retomarlo después.</summary>
    public static class BorradorService
    {
        private static readonly string Ruta = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "SistemaPlaneacionNEM", "borrador.json");

        private static readonly JsonSerializerOptions Json = new() { WriteIndented = true };

        public static void Guardar<T>(T borrador)
        {
            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(Ruta)!);
                File.WriteAllText(Ruta, JsonSerializer.Serialize(borrador, Json));
            }
            catch { /* no es crítico */ }
        }

        public static T? Cargar<T>() where T : class
        {
            try
            {
                return File.Exists(Ruta) ? JsonSerializer.Deserialize<T>(File.ReadAllText(Ruta)) : null;
            }
            catch { return null; }
        }

        public static void Eliminar()
        {
            try { if (File.Exists(Ruta)) File.Delete(Ruta); } catch { }
        }
    }
}

