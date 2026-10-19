"""Catálogo NEM de contenidos y Procesos de Desarrollo de Aprendizaje (PDA).

- El catálogo base vive en backend/catalogo/fase*.json (editable).
- El catálogo personal del docente (contenidos y PDA propios) se guarda en
  %APPDATA%/SistemaPlaneacionNEM/catalogo_personal.json y se fusiona con el base.
"""
import json
import re
import threading
import uuid
from functools import lru_cache
from pathlib import Path
from typing import Dict, List, Optional

import almacenamiento as db
from models import ContenidoCatalogo

DIR_CATALOGO = Path(__file__).resolve().parent / "catalogo"
ARCHIVO_PERSONAL = db.DIR_DATOS / "catalogo_personal.json"

CAMPOS = [
    "Lenguajes",
    "Saberes y Pensamiento Científico",
    "Ética, Naturaleza y Sociedades",
    "De lo Humano y lo Comunitario",
]
_ABREV = {"Lenguajes": "len", "Saberes y Pensamiento Científico": "spc",
          "Ética, Naturaleza y Sociedades": "ens", "De lo Humano y lo Comunitario": "hyc"}

_lock = threading.RLock()


# ---------------------------------------------------------------------------
# Grados y fases
# ---------------------------------------------------------------------------
def numero_grado(grado: str) -> Optional[int]:
    m = re.search(r"([1-6])", grado or "")
    return int(m.group(1)) if m else None


def grado_canonico(grado: str) -> Optional[str]:
    n = numero_grado(grado)
    return f"{n}° Primaria" if n else None


def fase_de_grado(grado: str) -> Optional[int]:
    n = numero_grado(grado)
    return None if n is None else 3 + (n - 1) // 2  # 1-2 → 3, 3-4 → 4, 5-6 → 5


# ---------------------------------------------------------------------------
# Catálogo base
# ---------------------------------------------------------------------------
@lru_cache(maxsize=1)
def _base() -> List[ContenidoCatalogo]:
    contenidos: List[ContenidoCatalogo] = []
    for archivo in sorted(DIR_CATALOGO.glob("fase*.json")):
        datos = json.loads(archivo.read_text(encoding="utf-8"))
        fase = int(datos["fase"])
        for campo, lista in datos["campos"].items():
            for i, c in enumerate(lista, start=1):
                contenidos.append(ContenidoCatalogo(
                    id=f"f{fase}-{_ABREV.get(campo, 'otro')}-{i:02d}",
                    campo=campo, fase=fase, titulo=c["titulo"],
                    pda={grado_canonico(g) or g: list(p) for g, p in c["pda"].items()},
                ))
    return contenidos


# ---------------------------------------------------------------------------
# Catálogo personal
# ---------------------------------------------------------------------------
def _leer_personal() -> dict:
    try:
        datos = json.loads(ARCHIVO_PERSONAL.read_text(encoding="utf-8"))
    except (FileNotFoundError, json.JSONDecodeError):
        datos = {}
    datos.setdefault("contenidos", [])
    datos.setdefault("pda_extra", {})  # {contenido_id: {grado: [pda, ...]}}
    return datos


def _guardar_personal(datos: dict) -> None:
    tmp = ARCHIVO_PERSONAL.with_suffix(".tmp")
    tmp.write_text(json.dumps(datos, ensure_ascii=False, indent=2), encoding="utf-8")
    tmp.replace(ARCHIVO_PERSONAL)


def _agregar_unicos(destino: List[str], nuevos: List[str]) -> None:
    existentes = {x.strip().lower() for x in destino}
    for n in nuevos:
        n = (n or "").strip()
        if n and n.lower() not in existentes:
            destino.append(n)
            existentes.add(n.lower())


# ---------------------------------------------------------------------------
# Consultas
# ---------------------------------------------------------------------------
def listar(campo: Optional[str], grados: List[str]) -> List[ContenidoCatalogo]:
    """Contenidos del campo para los grados indicados (con PDA filtrados por grado)."""
    grados_c = [g for g in (grado_canonico(x) for x in grados) if g]
    fases = {fase_de_grado(g) for g in grados_c}

    with _lock:
        personal = _leer_personal()

    todos = [c.model_copy(deep=True) for c in _base()]
    todos += [ContenidoCatalogo(**c, personalizado=True) for c in personal["contenidos"]]

    resultado = []
    for c in todos:
        if campo and c.campo != campo:
            continue
        if fases and c.fase not in fases:
            continue
        extra = personal["pda_extra"].get(c.id, {})
        pda: Dict[str, List[str]] = {}
        for g in (grados_c or sorted(c.pda.keys() | extra.keys())):
            lista = list(c.pda.get(g, []))
            _agregar_unicos(lista, extra.get(g, []))
            if lista:
                pda[g] = lista
        c.pda = pda
        c.pda_personalizados = {g: list(v) for g, v in extra.items() if g in pda}
        resultado.append(c)

    # Personalizados primero; luego por fase y título.
    resultado.sort(key=lambda c: (not c.personalizado, c.fase, c.id if not c.personalizado else c.titulo))
    return resultado


def agregar_contenido(campo: str, titulo: str, grados: List[str], pda: List[str]) -> ContenidoCatalogo:
    grados_c = [g for g in (grado_canonico(x) for x in grados) if g]
    if not grados_c:
        raise ValueError("Indica al menos un grado válido.")
    fase = fase_de_grado(grados_c[0])
    titulo = titulo.strip()
    with _lock:
        datos = _leer_personal()
        # Si ya existe uno igual, solo se agregan los PDA nuevos.
        for c in datos["contenidos"]:
            if c["campo"] == campo and c["fase"] == fase and c["titulo"].strip().lower() == titulo.lower():
                for g in grados_c:
                    _agregar_unicos(c["pda"].setdefault(g, []), pda)
                _guardar_personal(datos)
                return ContenidoCatalogo(**c, personalizado=True)
        nuevo = {
            "id": f"p-{uuid.uuid4().hex[:12]}",
            "campo": campo, "fase": fase, "titulo": titulo,
            "pda": {g: [p.strip() for p in pda if p.strip()] for g in grados_c},
        }
        datos["contenidos"].append(nuevo)
        _guardar_personal(datos)
    return ContenidoCatalogo(**nuevo, personalizado=True)


def agregar_pda(contenido_id: str, grado: str, texto: str) -> None:
    g = grado_canonico(grado)
    if not g:
        raise ValueError("Grado no válido.")
    with _lock:
        datos = _leer_personal()
        propio = next((c for c in datos["contenidos"] if c["id"] == contenido_id), None)
        if propio is not None:
            _agregar_unicos(propio["pda"].setdefault(g, []), [texto])
        elif any(c.id == contenido_id for c in _base()):
            _agregar_unicos(datos["pda_extra"].setdefault(contenido_id, {}).setdefault(g, []), [texto])
        else:
            raise KeyError(contenido_id)
        _guardar_personal(datos)


def eliminar_contenido(contenido_id: str) -> None:
    with _lock:
        datos = _leer_personal()
        antes = len(datos["contenidos"])
        datos["contenidos"] = [c for c in datos["contenidos"] if c["id"] != contenido_id]
        if len(datos["contenidos"]) == antes:
            raise KeyError(contenido_id)
        _guardar_personal(datos)


def eliminar_pda(contenido_id: str, grado: str, texto: str) -> None:
    """Quita un PDA agregado por el docente (los del catálogo base no se eliminan)."""
    g = grado_canonico(grado) or grado
    with _lock:
        datos = _leer_personal()
        propio = next((c for c in datos["contenidos"] if c["id"] == contenido_id), None)
        lista = propio["pda"].get(g, []) if propio else datos["pda_extra"].get(contenido_id, {}).get(g, [])
        nuevos = [p for p in lista if p.strip().lower() != texto.strip().lower()]
        if len(nuevos) == len(lista):
            raise KeyError(texto)
        if propio:
            propio["pda"][g] = nuevos
        else:
            datos["pda_extra"][contenido_id][g] = nuevos
        _guardar_personal(datos)
