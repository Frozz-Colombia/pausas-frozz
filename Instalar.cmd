@echo off
rem Instala Pausas Frozz para el usuario actual (no requiere administrador).
rem Si PausasFrozz.exe esta en la misma carpeta lo usa; si no, descarga la ultima version.
set "D=%LOCALAPPDATA%\PausasFrozz"
set "URL=https://github.com/Frozz-Colombia/pausas-frozz/releases/latest/download/PausasFrozz.exe"
if not exist "%D%" mkdir "%D%"
taskkill /im PausasFrozz.exe /f >nul 2>&1

if exist "%~dp0PausasFrozz.exe" goto copiar
echo  Descargando la ultima version de Pausas Frozz...
curl.exe -fsSL -o "%D%\PausasFrozz.exe" "%URL%"
if errorlevel 1 goto error
goto iniciar

:copiar
copy /y "%~dp0PausasFrozz.exe" "%D%\PausasFrozz.exe" >nul

:iniciar
start "" "%D%\PausasFrozz.exe"
echo.
echo  Pausas Frozz quedo instalado y en ejecucion.
echo  Lo encuentras junto al reloj de Windows - clic derecho para opciones.
echo  Se inicia solo con Windows y se actualiza solo desde internet.
echo.
pause
exit /b 0

:error
echo.
echo  No se pudo descargar. Revisa la conexion a internet e intenta de nuevo.
echo.
pause
exit /b 1
