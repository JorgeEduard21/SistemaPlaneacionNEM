"""Modelos de datos (Pydantic) compartidos por la API."""
from typing import List, Optional

from pydantic import BaseModel, Field, field_validator


def _limpiar_lista(valores: List[str]) -> List[str]:
    """Quita vacíos y duplicados conservando el orden."""
    vistos = set()
    resultado = []
    for v in valores or []:
        v = (v or "").strip()
        if v and v.lower() not in vistos:
            vistos.add(v.lower())
            resultado.append(v)
    return resultado


# ---------------------------------------------------------------------------
# Configuración
# ---------------------------------------------------------------------------
class EstadoConfiguracion(BaseModel):
    api_key_configurada: bool
    api_key_mascara: Optional[str] = None
    api_key_origen: Optional[str] = None  # "usuario" | "entorno" | None
    modelo: str
    modelos_disponibles: List[dict]
    perfil_configurado: bool
    directorio_datos: str


class GuardarApiKey(BaseModel):
    api_key: str = Field(..., min_length=10, max_length=200)
    validar: bool = True

    @field_validator("api_key")
    @classmethod
    def _strip(cls, v: str) -> str:
        v = v.strip()
        if " " in v:
            raise ValueError("La API key no debe contener espacios.")
        return v


class ProbarApiKey(BaseModel):
    api_key: Optional[str] = None  # si es None se prueba la guardada


class ResultadoPrueba(BaseModel):
    valida: bool
    mensaje: str


class CambiarModelo(BaseModel):
    modelo: str


# ---------------------------------------------------------------------------
# Perfil / contexto del grupo (se guarda una sola vez)
# ---------------------------------------------------------------------------
class PerfilGrupo(BaseModel):
    nombre_docente: str = ""
    escuela: str = ""
    ciclo_escolar: str = ""
    grados: List[str] = Field(default_factory=list)
    cantidad_alumnos: Optional[int] = Field(default=None, ge=1, le=200)
    region: str = ""
    entorno: str = ""
    estilos_aprendizaje: List[str] = Field(default_factory=list)
    barreras_aprendizaje: List[str] = Field(default_factory=list)
    caracteristicas: str = ""
    actualizado: Optional[str] = None

    _norm_listas = field_validator(
        "grados", "estilos_aprendizaje", "barreras_aprendizaje"
    )(_limpiar_lista)

    def esta_completo(self) -> bool:
        return bool(self.grados) and self.cantidad_alumnos is not None and bool(self.entorno)


# ---------------------------------------------------------------------------
# Solicitud de planeación
# ---------------------------------------------------------------------------
class DatosCurriculares(BaseModel):
    campo_formativo: str = Field(..., min_length=1)
    ejes_articuladores: List[str] = Field(default_factory=list)
    contenido: str = Field(..., min_length=1)
    subcontenido: str = ""
    intencion_didactica: str = ""
    pda: List[str] = Field(default_factory=list)

    _norm = field_validator("ejes_articuladores", "pda")(_limpiar_lista)


class DisenoPlaneacion(BaseModel):
    tema_interes: str = Field(..., min_length=1)
    metodologia: str = Field(..., min_length=1)
    duracion_dias: int = Field(..., ge=1, le=60)
    minutos_por_sesion: int = Field(..., ge=10, le=480)


class PlanEvaluacion(BaseModel):
    instrumento: str = Field(..., min_length=1)
    criterios: List[str] = Field(default_factory=list)
    generar_ficha: bool = True
    generar_editable: bool = True
    notas_docente: str = ""

    _norm = field_validator("criterios")(_limpiar_lista)


class SolicitudPlaneacion(BaseModel):
    curricula: DatosCurriculares
    diseno: DisenoPlaneacion
    evaluacion: PlanEvaluacion
    # Si no se envía, se usa el perfil guardado en Configuración.
    perfil: Optional[PerfilGrupo] = None


class Planeacion(BaseModel):
    id: str
    titulo: str
    creada: str
    modelo: str
    perfil: PerfilGrupo
    solicitud: SolicitudPlaneacion
    contenido: str  # Markdown
    favorita: bool = False


class ResumenPlaneacion(BaseModel):
    id: str
    titulo: str
    creada: str
    campo_formativo: str
    metodologia: str
    grados: List[str]
    favorita: bool = False


class ActualizarPlaneacion(BaseModel):
    titulo: Optional[str] = None
    contenido: Optional[str] = None
    favorita: Optional[bool] = None

