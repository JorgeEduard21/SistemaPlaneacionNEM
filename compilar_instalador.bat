@echo off
chcp 65001 >nul
echo ========================================================
echo   GENERADOR DE INSTALADOR - SISTEMA DE PLANEACION NEM
echo ========================================================
echo.

set "ROOT=%~dp0"
cd /d "%ROOT%"

echo [1/3] Publicando Frontend WPF (Autocontenido .NET 8)...
cd /d "%ROOT%Frontend"
dotnet publish Frontend.csproj -c Release -r win-x64 --self-contained true -p:PublishSingleFile=false -o "%ROOT%dist\staging\app"
if errorlevel 1 (
    echo Error al compilar el Frontend.
    pause
    exit /b 1
)

echo.
echo [2/3] Ensamblando Backend con Runtime de Python Firmado Digitalmente...
cd /d "%ROOT%"
powershell -NoProfile -ExecutionPolicy Bypass -Command ^
  "$ErrorActionPreference = 'Stop'; " ^
  "$stageBackend = Join-Path '%ROOT%' 'dist\staging\app\backend'; " ^
  "if (Test-Path $stageBackend) { Remove-Item $stageBackend -Recurse -Force }; " ^
  "New-Item -ItemType Directory -Path $stageBackend -Force | Out-Null; " ^
  "Copy-Item (Join-Path '%ROOT%backend' '*.py') $stageBackend -Force; " ^
  "Copy-Item (Join-Path '%ROOT%backend' 'catalogo') (Join-Path $stageBackend 'catalogo') -Recurse -Force; " ^
  "$pySrc = 'C:\Users\DELL\AppData\Local\Programs\Python\Python39'; " ^
  "$pyDst = Join-Path $stageBackend 'python'; " ^
  "New-Item -ItemType Directory -Path $pyDst -Force | Out-Null; " ^
  "Copy-Item (Join-Path $pySrc 'python.exe'), (Join-Path $pySrc 'pythonw.exe'), (Join-Path $pySrc 'python3.dll'), (Join-Path $pySrc 'python39.dll'), (Join-Path $pySrc 'vcruntime140.dll'), (Join-Path $pySrc 'vcruntime140_1.dll'), (Join-Path $pySrc 'LICENSE.txt') $pyDst -Force; " ^
  "Copy-Item (Join-Path $pySrc 'DLLs') (Join-Path $pyDst 'DLLs') -Recurse -Force; " ^
  "robocopy (Join-Path $pySrc 'Lib') (Join-Path $pyDst 'Lib') /E /XD site-packages test tests idlelib tkinter turtledemo ensurepip distutils lib2to3 __pycache__ /NFL /NDL /NJH /NJS /NC /NS /NP | Out-Null; " ^
  "robocopy (Join-Path '%ROOT%backend\venv\Lib\site-packages') (Join-Path $pyDst 'Lib\site-packages') /E /XD PyInstaller* pip* setuptools* _distutils_hack __pycache__ /NFL /NDL /NJH /NJS /NC /NS /NP | Out-Null; " ^
  "Copy-Item (Join-Path '%ROOT%Frontend' 'Logo PLANEACION.ico') (Join-Path '%ROOT%' 'dist\staging\app') -Force;"

echo.
echo [3/3] Creando instalador con Inno Setup...
set "ISCC=C:\Users\DELL\AppData\Local\Programs\Inno Setup 6\ISCC.exe"
if not exist "%ISCC%" set "ISCC=C:\Program Files (x86)\Inno Setup 6\ISCC.exe"
if not exist "%ISCC%" set "ISCC=C:\Program Files\Inno Setup 6\ISCC.exe"

"%ISCC%" "%ROOT%installer.iss"
if errorlevel 1 (
    echo Error al crear el instalador con Inno Setup.
    pause
    exit /b 1
)

echo.
echo ========================================================
echo   ¡INSTALADOR CREADO CON EXITO!
echo   Ubicacion: dist\installer\Instalador_Planeacion_NEM_v1.0.0.exe
echo ========================================================
