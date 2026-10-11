using System.Globalization;
using System.Text.Json.Serialization;

namespace Frontend.Models
{
    // DTOs que reflejan los modelos del backend (Python). Se serializan en snake_case.

    public class ModeloIa
    {
        public string Id { get; set; } = "";
        public string Nombre { get; set; } = "";
    }

    public class EstadoConfiguracion
    {
        public bool ApiKeyConfigurada { get; set; }
        public string? ApiKeyMascara { get; set; }
        public string? ApiKeyOrigen { get; set; }
        public string Modelo { get; set; } = "";
        public List<ModeloIa> ModelosDisponibles { get; set; } = new();
        public bool PerfilConfigurado { get; set; }
        public string DirectorioDatos { get; set; } = "";
    }

    public class ResultadoPrueba
    {
        public bool Valida { get; set; }
        public string Mensaje { get; set; } = "";
    }

    public class PerfilGrupo
    {
        public string NombreDocente { get; set; } = "";
        public string Escuela { get; set; } = "";
        public string CicloEscolar { get; set; } = "";
        public List<string> Grados { get; set; } = new();
        public int? CantidadAlumnos { get; set; }
        public string Region { get; set; } = "";
        public string Entorno { get; set; } = "";
        public List<string> EstilosAprendizaje { get; set; } = new();
        public List<string> BarrerasAprendizaje { get; set; } = new();
        public string Caracteristicas { get; set; } = "";
        public string? Actualizado { get; set; }

        [JsonIgnore]
        public bool EstaCompleto => Grados.Count > 0 && CantidadAlumnos.HasValue && !string.IsNullOrWhiteSpace(Entorno);
    }

    public class DatosCurriculares
    {
        public string CampoFormativo { get; set; } = "";
        public List<string> EjesArticuladores { get; set; } = new();
        public string Contenido { get; set; } = "";
        public string Subcontenido { get; set; } = "";
        public string IntencionDidactica { get; set; } = "";
        public List<string> Pda { get; set; } = new();
    }

    public class DisenoPlaneacion
    {
        public string TemaInteres { get; set; } = "";
        public string Metodologia { get; set; } = "";
        public int DuracionDias { get; set; }
        public int MinutosPorSesion { get; set; }
    }

    public class PlanEvaluacion
    {
        public string Instrumento { get; set; } = "";
        public List<string> Criterios { get; set; } = new();
        public bool GenerarFicha { get; set; } = true;
        public bool GenerarEditable { get; set; } = true;
        public string NotasDocente { get; set; } = "";
    }

    public class SolicitudPlaneacion
    {
        public DatosCurriculares Curricula { get; set; } = new();
        public DisenoPlaneacion Diseno { get; set; } = new();
        public PlanEvaluacion Evaluacion { get; set; } = new();
        public PerfilGrupo? Perfil { get; set; }
    }

    public class Planeacion
    {
        public string Id { get; set; } = "";
        public string Titulo { get; set; } = "";
        public string Creada { get; set; } = "";
        public string Modelo { get; set; } = "";
        public PerfilGrupo Perfil { get; set; } = new();
        public SolicitudPlaneacion Solicitud { get; set; } = new();
        public string Contenido { get; set; } = "";
        public bool Favorita { get; set; }

        [JsonIgnore] public string FechaLegible => Fechas.Legible(Creada);
    }

    public class ResumenPlaneacion
    {
        public string Id { get; set; } = "";
        public string Titulo { get; set; } = "";
        public string Creada { get; set; } = "";
        public string CampoFormativo { get; set; } = "";
        public string Metodologia { get; set; } = "";
        public List<string> Grados { get; set; } = new();
        public bool Favorita { get; set; }

        [JsonIgnore] public string FechaLegible => Fechas.Legible(Creada);
        [JsonIgnore] public string GradosTexto => string.Join(", ", Grados);
    }

    public static class Fechas
    {
        private static readonly CultureInfo Es = new("es-MX");

        public static string Legible(string? iso)
        {
            if (string.IsNullOrWhiteSpace(iso) || !DateTime.TryParse(iso, CultureInfo.InvariantCulture, DateTimeStyles.None, out var f))
                return "";
            var hoy = DateTime.Today;
            if (f.Date == hoy) return $"Hoy, {f:HH:mm}";
            if (f.Date == hoy.AddDays(-1)) return $"Ayer, {f:HH:mm}";
            return f.ToString("d 'de' MMMM yyyy, HH:mm", Es);
        }
    }
}

