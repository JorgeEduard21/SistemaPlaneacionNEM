@echo off
chcp 65001 >nul
echo ========================================================
echo   GENERADOR DE INSTALADOR - SISTEMA DE PLANEACION NEM
echo ========================================================
echo.

set "ROOT=%~dp0"
cd /d "%ROOT%"

echo [1/4] Compilando Backend de Python a ejecutable independiente...
cd /d "%ROOT%backend"
call ".\venv\Scripts\pyinstaller.exe" --name "backend" --onedir --noconfirm --clean --add-data "catalogo;catalogo" --copy-metadata google-genai --copy-metadata python-docx --copy-metadata uvicorn --copy-metadata fastapi main.py
if errorlevel 1 (
    echo Error al compilar el backend de Python.
    pause
    exit /b 1
)

echo.
echo [2/4] Publicando Frontend WPF (Autocontenido .NET 8)...
cd /d "%ROOT%Frontend"
dotnet publish Frontend.csproj -c Release -r win-x64 --self-contained true -p:PublishSingleFile=false -o "%ROOT%dist\staging\app"
if errorlevel 1 (
    echo Error al compilar el Frontend.
    pause
    exit /b 1
)

echo.
echo [3/4] Ensamblando archivos para el instalador...
cd /d "%ROOT%"
if exist "%ROOT%dist\staging\app\backend" rmdir /s /q "%ROOT%dist\staging\app\backend"
xcopy /E /I /Y "%ROOT%backend\dist\backend" "%ROOT%dist\staging\app\backend" >nul
xcopy /E /I /Y "%ROOT%backend\catalogo" "%ROOT%dist\staging\app\backend\catalogo" >nul
copy /Y "%ROOT%Frontend\Logo PLANEACION.ico" "%ROOT%dist\staging\app\" >nul

echo.
echo [4/4] Creando instalador con Inno Setup...
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
echo.
pause
