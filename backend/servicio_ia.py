"""Integración con la API de Gemini y construcción del prompt NEM."""
from typing import List

from google import genai
from google.genai import errors as genai_errors
from google.genai import types

from models import PerfilGrupo, SolicitudPlaneacion

MODELOS_DISPONIBLES = [
    {"id": "gemini-3.8-flash", "nombre": "Gemini 3.8 Flash (recomendado)"},
    {"id": "gemini-3.5-flash-lite", "nombre": "Gemini 3.5 Flash-Lite (más rápido y económico)"},
    {"id": "gemini-3.1-pro-preview", "nombre": "Gemini 3.1 Pro (máxima calidad, más lento)"},
]
IDS_MODELOS = {m["id"] for m in MODELOS_DISPONIBLES}

_TIMEOUT_MS = 240_000  # 4 minutos


class ErrorIA(Exception):
    """Error de la IA con un mensaje entendible para el docente."""

    def __init__(self, mensaje: str, status: int = 502):
        super().__init__(mensaje)
        self.mensaje = mensaje
        self.status = status


def _cliente(api_key: str) -> genai.Client:
    return genai.Client(api_key=api_key, http_options=types.HttpOptions(timeout=_TIMEOUT_MS))


def _traducir_error(e: Exception) -> ErrorIA:
    if isinstance(e, genai_errors.APIError):
        codigo = getattr(e, "code", None) or 0
        texto = f"{getattr(e, 'status', '')} {getattr(e, 'message', '')} {e}".lower()
        if codigo in (401, 403) or "api key" in texto or "api_key" in texto:
            return ErrorIA("La API key no es válida o no tiene permisos. Revísala en Configuración → API de Gemini.", 401)
        if codigo == 429 or "quota" in texto or "resource_exhausted" in texto:
            return ErrorIA("Se alcanzó el límite de uso de tu API key. Espera unos minutos o revisa tu cuota en Google AI Studio.", 429)
        if codigo == 404:
            return ErrorIA("El modelo seleccionado no está disponible para tu API key. Elige otro en Configuración.", 400)
        if codigo == 400:
            return ErrorIA(f"Gemini rechazó la solicitud: {getattr(e, 'message', e)}", 400)
        if codigo >= 500:
            return ErrorIA("El servicio de Gemini no está disponible en este momento. Intenta de nuevo en unos minutos.", 503)
        return ErrorIA(f"Error de Gemini ({codigo}): {getattr(e, 'message', e)}", 502)
    nombre = type(e).__name__.lower()
    if "timeout" in nombre:
        return ErrorIA("Gemini tardó demasiado en responder. Intenta de nuevo o reduce la duración de la secuencia.", 504)
    if "connect" in nombre or "network" in nombre:
        return ErrorIA("No se pudo conectar con Gemini. Revisa tu conexión a internet.", 503)
    return ErrorIA(f"Error inesperado al comunicarse con Gemini: {e}", 502)


def probar_api_key(api_key: str) -> None:
    """Lanza ErrorIA si la clave no funciona. No consume tokens."""
    try:
        cliente = _cliente(api_key)  # se conserva la referencia: si se recolecta, el SDK cierra la conexión
        pager = cliente.models.list(config={"page_size": 1})
        next(iter(pager), None)
    except Exception as e:  # noqa: BLE001
        raise _traducir_error(e) from e


# ---------------------------------------------------------------------------
# Prompt
# ---------------------------------------------------------------------------
INSTRUCCIONES_SISTEMA = """\
Eres un asesor pedagógico experto en la Nueva Escuela Mexicana (NEM) y en el \
Plan de Estudios 2022 de la SEP para educación primaria. Diseñas planeaciones \
didácticas realistas, inclusivas, contextualizadas en la comunidad y listas \
para usarse en el aula.

Reglas de formato (obligatorias):
- Responde ÚNICAMENTE con el documento de la planeación, en español de México, \
  en Markdown. Sin saludos ni comentarios fuera del documento.
- Usa '# ' solo una vez para el título. Usa '## ' para secciones y '### ' para subsecciones.
- Puedes usar listas con '- ', listas numeradas '1. ', negritas '**texto**' y \
  tablas Markdown simples (con encabezado y fila separadora '|---|').
- No uses HTML, bloques de código ni emojis.

Reglas pedagógicas:
- Respeta exactamente el campo formativo, contenido y PDA proporcionados; no \
  inventes PDA nuevos.
- Organiza la secuencia según las fases propias de la metodología elegida.
- Cada sesión debe tener Inicio, Desarrollo y Cierre con minutos que sumen la \
  duración indicada de la sesión.
- Integra los ejes articuladores de forma explícita y natural.
- Incluye ajustes razonables concretos para cada barrera de aprendizaje (BAP) y \
  diversifica actividades según los estilos de aprendizaje del grupo.
- Usa recursos accesibles y de bajo costo, acordes al entorno sociocultural.
"""

FASES_METODOLOGIA = {
    "proyectos comunitarios": "Fases: 1) Planeación (identificación, recuperación, planificación), "
    "2) Acción (acercamiento, comprensión y producción, reconocimiento, concreción), "
    "3) Intervención (integración, difusión, consideraciones, avances).",
    "aprendizaje basado en problemas": "Fases (ABP): 1) Presentemos, 2) Recolectemos, "
    "3) Formulemos el problema, 4) Organicemos la experiencia, 5) Vivamos la experiencia, "
    "6) Resultados y análisis.",
    "steam": "Fases (STEAM como enfoque): 1) Introducción al tema, 2) Diseño de investigación, "
    "3) Organizar y estructurar las respuestas, 4) Presentación de resultados, "
    "5) Metacognición.",
    "aprendizaje-servicio": "Etapas (AS): 1) Punto de partida, 2) Lo que sé y lo que quiero saber, "
    "3) Organicemos las actividades, 4) Creatividad en marcha, 5) Compartimos y evaluamos lo aprendido.",
}


def _lista(valores: List[str], vacio: str = "No especificado") -> str:
    return ", ".join(valores) if valores else vacio


def _viñetas(valores: List[str], vacio: str = "- (no especificado)") -> str:
    return "\n".join(f"- {v}" for v in valores) if valores else vacio


def construir_prompt(perfil: PerfilGrupo, s: SolicitudPlaneacion) -> str:
    c, d, e = s.curricula, s.diseno, s.evaluacion
    fases = FASES_METODOLOGIA.get(d.metodologia.strip().lower(), "")

    secciones = [
        "1. Datos generales (docente, escuela, grado(s), ciclo escolar, campo formativo, metodología, duración).",
        "2. Propósito de la secuencia (a partir de la intención didáctica).",
        "3. Vinculación curricular (campo formativo, ejes articuladores, contenido, PDA).",
        f"4. Secuencia didáctica: {d.duracion_dias} sesión(es) de {d.minutos_por_sesion} min. "
        "Para cada sesión: '### Sesión N: título', fase de la metodología, Inicio/Desarrollo/Cierre "
        "con minutos, materiales y producto o evidencia.",
        "5. Ajustes razonables e inclusión (por BAP y estilos de aprendizaje).",
        f"6. Evaluación formativa: construye el instrumento completo ({e.instrumento}) con los criterios "
        "indicados (si no hay criterios, propón 4 a 6 alineados a los PDA). "
        + ("Para rúbrica usa una tabla con niveles Destacado / Satisfactorio / En proceso / Requiere apoyo. "
           if "rúbrica" in e.instrumento.lower() or "rubrica" in e.instrumento.lower() else "")
        + ("Para lista de cotejo usa una tabla con columnas Criterio | Sí | No | Observaciones. "
           if "cotejo" in e.instrumento.lower() else "")
        + ("Para guía de observación usa una tabla con aspectos a observar y espacio para registro. "
           if "observación" in e.instrumento.lower() or "observacion" in e.instrumento.lower() else ""),
    ]
    n = 7
    if e.generar_ficha:
        secciones.append(f"{n}. Ficha de contenido: conceptos clave del tema explicados para el nivel del grupo.")
        n += 1
    secciones.append(f"{n}. Recomendaciones y notas para el docente.")

    return f"""\
Elabora una planeación didáctica con la siguiente información.

## Contexto del grupo
- Docente: {perfil.nombre_docente or 'No especificado'}
- Escuela: {perfil.escuela or 'No especificada'}
- Ciclo escolar: {perfil.ciclo_escolar or 'No especificado'}
- Grado(s): {_lista(perfil.grados)}
- Número de alumnos: {perfil.cantidad_alumnos or 'No especificado'}
- Región: {perfil.region or 'No especificada'}
- Entorno sociocultural: {perfil.entorno or 'No especificado'}
- Estilos de aprendizaje predominantes (VARK): {_lista(perfil.estilos_aprendizaje)}
- Barreras para el aprendizaje (BAP):
{_viñetas(perfil.barreras_aprendizaje, '- Ninguna identificada')}
- Características del desarrollo del grupo: {perfil.caracteristicas or 'No especificadas'}

## Estructura curricular NEM
- Campo formativo: {c.campo_formativo}
- Ejes articuladores: {_lista(c.ejes_articuladores)}
- Contenido: {c.contenido}
- Subcontenido: {c.subcontenido or 'No especificado'}
- Intención didáctica: {c.intencion_didactica or 'No especificada'}
- Procesos de Desarrollo del Aprendizaje (PDA):
{_viñetas(c.pda)}

## Diseño
- Tema de interés / proyecto: {d.tema_interes}
- Metodología: {d.metodologia}. {fases}
- Duración: {d.duracion_dias} día(s), {d.minutos_por_sesion} minutos por sesión.

## Evaluación
- Instrumento: {e.instrumento}
- Criterios del docente:
{_viñetas(e.criterios, '- (propón criterios adecuados)')}
- Notas del docente: {e.notas_docente or 'Ninguna'}

## Estructura requerida del documento
{chr(10).join(secciones)}
"""


def generar_planeacion(api_key: str, modelo: str, perfil: PerfilGrupo, solicitud: SolicitudPlaneacion) -> str:
    prompt = construir_prompt(perfil, solicitud)
    try:
        cliente = _cliente(api_key)
        respuesta = cliente.models.generate_content(
            model=modelo,
            contents=prompt,
            config=types.GenerateContentConfig(
                system_instruction=INSTRUCCIONES_SISTEMA,
                temperature=0.7,
            ),
        )
    except Exception as e:  # noqa: BLE001
        raise _traducir_error(e) from e

    texto = (respuesta.text or "").strip()
    if not texto:
        motivo = ""
        try:
            motivo = str(respuesta.candidates[0].finish_reason)
        except Exception:  # noqa: BLE001
            pass
        raise ErrorIA(f"Gemini no devolvió contenido{f' ({motivo})' if motivo else ''}. Intenta de nuevo.", 502)

    # Quita un posible cercado ```markdown ... ``` que algunos modelos agregan.
    if texto.startswith("```"):
        lineas = texto.splitlines()[1:]
        if lineas and lineas[-1].strip().startswith("```"):
            lineas = lineas[:-1]
        texto = "\n".join(lineas).strip()
    return texto
