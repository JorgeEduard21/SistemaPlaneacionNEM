"""API local del Sistema de Planeación NEM.

Ejecutar:  python main.py   (escucha en http://127.0.0.1:8765)
Documentación interactiva: http://127.0.0.1:8765/docs
"""
import os
import re
import warnings
from typing import List
from urllib.parse import quote

warnings.filterwarnings("ignore", category=FutureWarning)  # aviso de EOL de Python 3.9 en google-auth

import uvicorn
from fastapi import FastAPI, HTTPException, Request
from fastapi.responses import JSONResponse, Response

import almacenamiento as db
import exportar_docx
import servicio_ia as ia
from models import (
    ActualizarPlaneacion, CambiarModelo, EstadoConfiguracion, GuardarApiKey,
    PerfilGrupo, Planeacion, ProbarApiKey, ResultadoPrueba, ResumenPlaneacion,
    SolicitudPlaneacion,
)

VERSION = "1.0.0"
HOST = "127.0.0.1"
PUERTO = int(os.environ.get("PLANEACION_NEM_PORT", "8765"))

app = FastAPI(title="Sistema Planeación NEM - API", version=VERSION)


@app.exception_handler(ia.ErrorIA)
async def _manejar_error_ia(_: Request, exc: ia.ErrorIA):
    return JSONResponse(status_code=exc.status, content={"detail": exc.mensaje})


def _no_encontrada() -> HTTPException:
    return HTTPException(status_code=404, detail="La planeación no existe o fue eliminada.")


# ---------------------------------------------------------------------------
# Salud
# ---------------------------------------------------------------------------
@app.get("/api/salud")
def salud():
    api_key, _ = db.obtener_api_key()
    return {"estado": "ok", "version": VERSION, "api_key_configurada": bool(api_key)}


# ---------------------------------------------------------------------------
# Configuración
# ---------------------------------------------------------------------------
def _estado() -> EstadoConfiguracion:
    api_key, origen = db.obtener_api_key()
    perfil = db.obtener_perfil()
    return EstadoConfiguracion(
        api_key_configurada=bool(api_key),
        api_key_mascara=db.enmascarar(api_key),
        api_key_origen=origen,
        modelo=db.obtener_modelo(),
        modelos_disponibles=ia.MODELOS_DISPONIBLES,
        perfil_configurado=bool(perfil and perfil.esta_completo()),
        directorio_datos=str(db.DIR_DATOS),
    )


@app.get("/api/configuracion", response_model=EstadoConfiguracion)
def obtener_configuracion():
    return _estado()


@app.put("/api/configuracion/api-key", response_model=EstadoConfiguracion)
def guardar_api_key(datos: GuardarApiKey):
    if datos.validar:
        ia.probar_api_key(datos.api_key)  # lanza ErrorIA (401/429/...) si falla
    db.guardar_api_key(datos.api_key)
    return _estado()


@app.delete("/api/configuracion/api-key", response_model=EstadoConfiguracion)
def eliminar_api_key():
    db.eliminar_api_key()
    return _estado()


@app.post("/api/configuracion/api-key/probar", response_model=ResultadoPrueba)
def probar_api_key(datos: ProbarApiKey):
    clave = (datos.api_key or "").strip() or db.obtener_api_key()[0]
    if not clave:
        return ResultadoPrueba(valida=False, mensaje="No hay ninguna API key para probar.")
    try:
        ia.probar_api_key(clave)
    except ia.ErrorIA as e:
        return ResultadoPrueba(valida=False, mensaje=e.mensaje)
    return ResultadoPrueba(valida=True, mensaje="¡Conexión exitosa! La API key funciona correctamente.")


@app.put("/api/configuracion/modelo", response_model=EstadoConfiguracion)
def cambiar_modelo(datos: CambiarModelo):
    if datos.modelo not in ia.IDS_MODELOS:
        raise HTTPException(status_code=400, detail="Modelo no soportado.")
    db.guardar_modelo(datos.modelo)
    return _estado()


# ---------------------------------------------------------------------------
# Perfil del grupo
# ---------------------------------------------------------------------------
@app.get("/api/perfil", response_model=PerfilGrupo)
def obtener_perfil():
    return db.obtener_perfil() or PerfilGrupo()


@app.put("/api/perfil", response_model=PerfilGrupo)
def guardar_perfil(perfil: PerfilGrupo):
    return db.guardar_perfil(perfil)


# ---------------------------------------------------------------------------
# Planeaciones
# ---------------------------------------------------------------------------
def _titulo(markdown: str, respaldo: str) -> str:
    for linea in markdown.splitlines():
        m = re.match(r"^#\s+(.+)$", linea.strip())
        if m:
            return m.group(1).strip().strip("*")[:150]
    return respaldo[:150]


@app.post("/api/planeaciones/generar", response_model=Planeacion)
def generar_planeacion(solicitud: SolicitudPlaneacion):
    api_key, _ = db.obtener_api_key()
    if not api_key:
        raise HTTPException(status_code=400, detail="Configura tu API key de Gemini antes de generar una planeación.")

    perfil = solicitud.perfil or db.obtener_perfil()
    if not perfil or not perfil.grados:
        raise HTTPException(status_code=400, detail="Completa el perfil del grupo en Configuración antes de generar.")

    modelo = db.obtener_modelo()
    contenido = ia.generar_planeacion(api_key, modelo, perfil, solicitud)

    planeacion = Planeacion(
        id=db.nuevo_id(),
        titulo=_titulo(contenido, solicitud.diseno.tema_interes),
        creada=db.ahora_iso(),
        modelo=modelo,
        perfil=perfil,
        solicitud=solicitud.model_copy(update={"perfil": None}),
        contenido=contenido,
    )
    return db.guardar_planeacion(planeacion)


@app.get("/api/planeaciones", response_model=List[ResumenPlaneacion])
def listar_planeaciones():
    return db.listar_planeaciones()


@app.get("/api/planeaciones/{planeacion_id}", response_model=Planeacion)
def obtener_planeacion(planeacion_id: str):
    try:
        return db.obtener_planeacion(planeacion_id)
    except KeyError:
        raise _no_encontrada()


@app.patch("/api/planeaciones/{planeacion_id}", response_model=Planeacion)
def actualizar_planeacion(planeacion_id: str, cambios: ActualizarPlaneacion):
    try:
        p = db.obtener_planeacion(planeacion_id)
    except KeyError:
        raise _no_encontrada()
    datos = {k: v for k, v in cambios.model_dump().items() if v is not None}
    if "titulo" in datos and not datos["titulo"].strip():
        raise HTTPException(status_code=400, detail="El título no puede estar vacío.")
    return db.guardar_planeacion(p.model_copy(update=datos))


@app.delete("/api/planeaciones/{planeacion_id}", status_code=204)
def eliminar_planeacion(planeacion_id: str):
    try:
        db.eliminar_planeacion(planeacion_id)
    except KeyError:
        raise _no_encontrada()
    return Response(status_code=204)


@app.get("/api/planeaciones/{planeacion_id}/docx")
def descargar_docx(planeacion_id: str):
    try:
        p = db.obtener_planeacion(planeacion_id)
    except KeyError:
        raise _no_encontrada()
    contenido = exportar_docx.markdown_a_docx(p)
    nombre = exportar_docx.nombre_archivo(p)
    return Response(
        content=contenido,
        media_type="application/vnd.openxmlformats-officedocument.wordprocessingml.document",
        headers={"Content-Disposition": f"attachment; filename=\"planeacion.docx\"; filename*=UTF-8''{quote(nombre)}"},
    )


if __name__ == "__main__":
    uvicorn.run(app, host=HOST, port=PUERTO, log_level="info")

