"""Persistencia local en JSON (configuración, perfil del grupo e historial).

Los datos viven en %APPDATA%\\SistemaPlaneacionNEM (o en la ruta indicada por la
variable de entorno PLANEACION_NEM_DATA). Las escrituras son atómicas para no
corromper archivos si la aplicación se cierra a mitad de un guardado.
"""
import json
import os
import re
import threading
import uuid
from datetime import datetime
from pathlib import Path
from typing import List, Optional

import seguridad
from models import Planeacion, PerfilGrupo, ResumenPlaneacion

MODELO_POR_DEFECTO = "gemini-3.8-flash"

_lock = threading.RLock()


def _directorio_base() -> Path:
    personalizado = os.environ.get("PLANEACION_NEM_DATA")
    if personalizado:
        base = Path(personalizado)
    else:
        base = Path(os.environ.get("APPDATA") or Path.home()) / "SistemaPlaneacionNEM"
    (base / "planeaciones").mkdir(parents=True, exist_ok=True)
    return base


DIR_DATOS = _directorio_base()
ARCHIVO_CONFIG = DIR_DATOS / "config.json"
ARCHIVO_PERFIL = DIR_DATOS / "perfil.json"
DIR_PLANEACIONES = DIR_DATOS / "planeaciones"


def ahora_iso() -> str:
    return datetime.now().replace(microsecond=0).isoformat()


# ---------------------------------------------------------------------------
# Utilidades JSON
# ---------------------------------------------------------------------------
def _leer_json(ruta: Path, defecto):
    try:
        with ruta.open("r", encoding="utf-8") as f:
            return json.load(f)
    except FileNotFoundError:
        return defecto
    except (json.JSONDecodeError, OSError):
        # Archivo dañado: se respalda y se continúa con valores por defecto.
        try:
            ruta.replace(ruta.with_suffix(ruta.suffix + ".corrupto"))
        except OSError:
            pass
        return defecto


def _escribir_json(ruta: Path, datos) -> None:
    temporal = ruta.with_suffix(ruta.suffix + ".tmp")
    with temporal.open("w", encoding="utf-8") as f:
        json.dump(datos, f, ensure_ascii=False, indent=2)
    os.replace(temporal, ruta)


# ---------------------------------------------------------------------------
# Archivo .env (solo como respaldo para desarrollo)
# ---------------------------------------------------------------------------
def _api_key_de_entorno() -> Optional[str]:
    for nombre in ("GEMINI_API_KEY", "GOOGLE_API_KEY", "API_KEY"):
        if os.environ.get(nombre):
            return os.environ[nombre].strip()
    # Busca un .env junto al backend o en la raíz del repositorio.
    aqui = Path(__file__).resolve().parent
    for ruta in (aqui / ".env", aqui.parent / ".env"):
        if ruta.is_file():
            for linea in ruta.read_text(encoding="utf-8", errors="ignore").splitlines():
                m = re.match(r"\s*(GEMINI_API_KEY|GOOGLE_API_KEY|API_KEY)\s*=\s*(.+)", linea)
                if m:
                    valor = m.group(2).strip().strip('"').strip("'")
                    if valor:
                        return valor
    return None


# ---------------------------------------------------------------------------
# Configuración (API key y modelo)
# ---------------------------------------------------------------------------
def _config() -> dict:
    return _leer_json(ARCHIVO_CONFIG, {})


def obtener_api_key() -> "tuple[Optional[str], Optional[str]]":
    """Devuelve (api_key, origen). El origen es 'usuario' o 'entorno'."""
    with _lock:
        cifrada = _config().get("api_key")
    if cifrada:
        try:
            return seguridad.descifrar(cifrada), "usuario"
        except Exception:
            pass  # p. ej. el archivo se copió desde otra cuenta de Windows
    entorno = _api_key_de_entorno()
    if entorno:
        return entorno, "entorno"
    return None, None


def guardar_api_key(api_key: str) -> None:
    with _lock:
        cfg = _config()
        cfg["api_key"] = seguridad.cifrar(api_key)
        cfg["api_key_actualizada"] = ahora_iso()
        _escribir_json(ARCHIVO_CONFIG, cfg)


def eliminar_api_key() -> None:
    with _lock:
        cfg = _config()
        cfg.pop("api_key", None)
        cfg.pop("api_key_actualizada", None)
        _escribir_json(ARCHIVO_CONFIG, cfg)


def obtener_modelo() -> str:
    with _lock:
        return _config().get("modelo") or MODELO_POR_DEFECTO


def guardar_modelo(modelo: str) -> None:
    with _lock:
        cfg = _config()
        cfg["modelo"] = modelo
        _escribir_json(ARCHIVO_CONFIG, cfg)


def enmascarar(api_key: Optional[str]) -> Optional[str]:
    if not api_key:
        return None
    if len(api_key) <= 8:
        return "•" * len(api_key)
    return f"{api_key[:4]}{'•' * 12}{api_key[-4:]}"


# ---------------------------------------------------------------------------
# Perfil del grupo
# ---------------------------------------------------------------------------
def obtener_perfil() -> Optional[PerfilGrupo]:
    with _lock:
        datos = _leer_json(ARCHIVO_PERFIL, None)
    if not datos:
        return None
    try:
        return PerfilGrupo(**datos)
    except Exception:
        return None


def guardar_perfil(perfil: PerfilGrupo) -> PerfilGrupo:
    perfil = perfil.model_copy(update={"actualizado": ahora_iso()})
    with _lock:
        _escribir_json(ARCHIVO_PERFIL, perfil.model_dump())
    return perfil


# ---------------------------------------------------------------------------
# Historial de planeaciones
# ---------------------------------------------------------------------------
_ID_VALIDO = re.compile(r"^[a-f0-9]{32}$")


def _ruta_planeacion(planeacion_id: str) -> Path:
    if not _ID_VALIDO.match(planeacion_id):
        raise KeyError(planeacion_id)
    return DIR_PLANEACIONES / f"{planeacion_id}.json"


def nuevo_id() -> str:
    return uuid.uuid4().hex


def guardar_planeacion(planeacion: Planeacion) -> Planeacion:
    with _lock:
        _escribir_json(_ruta_planeacion(planeacion.id), planeacion.model_dump())
    return planeacion


def obtener_planeacion(planeacion_id: str) -> Planeacion:
    ruta = _ruta_planeacion(planeacion_id)
    with _lock:
        datos = _leer_json(ruta, None)
    if not datos:
        raise KeyError(planeacion_id)
    return Planeacion(**datos)


def eliminar_planeacion(planeacion_id: str) -> None:
    ruta = _ruta_planeacion(planeacion_id)
    with _lock:
        if not ruta.exists():
            raise KeyError(planeacion_id)
        ruta.unlink()


def listar_planeaciones() -> List[ResumenPlaneacion]:
    resumenes = []
    with _lock:
        archivos = list(DIR_PLANEACIONES.glob("*.json"))
    for archivo in archivos:
        datos = _leer_json(archivo, None)
        if not datos:
            continue
        try:
            p = Planeacion(**datos)
        except Exception:
            continue
        resumenes.append(ResumenPlaneacion(
            id=p.id,
            titulo=p.titulo,
            creada=p.creada,
            campo_formativo=p.solicitud.curricula.campo_formativo,
            metodologia=p.solicitud.diseno.metodologia,
            grados=p.perfil.grados,
            favorita=p.favorita,
        ))
    # Favoritas primero; dentro de cada grupo, la más reciente primero.
    favoritas = sorted([r for r in resumenes if r.favorita], key=lambda r: r.creada, reverse=True)
    resto = sorted([r for r in resumenes if not r.favorita], key=lambda r: r.creada, reverse=True)
    return favoritas + resto
