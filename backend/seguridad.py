"""Cifrado de secretos con DPAPI de Windows (sin dependencias externas).

La API key se cifra ligada a la cuenta de Windows del usuario, de modo que el
archivo de configuración no expone la clave en texto plano. En otros sistemas
operativos se usa una codificación base64 simple (sin cifrado real).
"""
import base64
import sys

_PREFIJO_DPAPI = "dpapi:"
_PREFIJO_PLANO = "b64:"

if sys.platform == "win32":
    import ctypes
    from ctypes import wintypes

    class _DATA_BLOB(ctypes.Structure):
        _fields_ = [("cbData", wintypes.DWORD), ("pbData", ctypes.POINTER(ctypes.c_char))]

    _crypt32 = ctypes.windll.crypt32
    _kernel32 = ctypes.windll.kernel32
    _kernel32.LocalFree.argtypes = [ctypes.c_void_p]
    _kernel32.LocalFree.restype = ctypes.c_void_p
    _CRYPTPROTECT_UI_FORBIDDEN = 0x01

    def _a_blob(datos: bytes):
        buffer = ctypes.create_string_buffer(datos, len(datos))
        blob = _DATA_BLOB(len(datos), ctypes.cast(buffer, ctypes.POINTER(ctypes.c_char)))
        return blob, buffer  # se devuelve el buffer para mantenerlo vivo

    def _de_blob(blob: "_DATA_BLOB") -> bytes:
        try:
            return ctypes.string_at(blob.pbData, blob.cbData)
        finally:
            _kernel32.LocalFree(ctypes.cast(blob.pbData, ctypes.c_void_p))

    def _proteger(datos: bytes) -> bytes:
        entrada, _buf = _a_blob(datos)
        salida = _DATA_BLOB()
        ok = _crypt32.CryptProtectData(
            ctypes.byref(entrada), ctypes.c_wchar_p("SistemaPlaneacionNEM"),
            None, None, None, _CRYPTPROTECT_UI_FORBIDDEN, ctypes.byref(salida),
        )
        if not ok:
            raise ctypes.WinError()
        return _de_blob(salida)

    def _desproteger(datos: bytes) -> bytes:
        entrada, _buf = _a_blob(datos)
        salida = _DATA_BLOB()
        ok = _crypt32.CryptUnprotectData(
            ctypes.byref(entrada), None, None, None, None,
            _CRYPTPROTECT_UI_FORBIDDEN, ctypes.byref(salida),
        )
        if not ok:
            raise ctypes.WinError()
        return _de_blob(salida)


def cifrar(texto: str) -> str:
    datos = texto.encode("utf-8")
    if sys.platform == "win32":
        return _PREFIJO_DPAPI + base64.b64encode(_proteger(datos)).decode("ascii")
    return _PREFIJO_PLANO + base64.b64encode(datos).decode("ascii")


def descifrar(valor: str) -> str:
    if valor.startswith(_PREFIJO_DPAPI):
        if sys.platform != "win32":
            raise ValueError("Este secreto fue cifrado en Windows.")
        return _desproteger(base64.b64decode(valor[len(_PREFIJO_DPAPI):])).decode("utf-8")
    if valor.startswith(_PREFIJO_PLANO):
        return base64.b64decode(valor[len(_PREFIJO_PLANO):]).decode("utf-8")
    raise ValueError("Formato de secreto desconocido.")

