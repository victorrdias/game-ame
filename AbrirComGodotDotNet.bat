@echo off
chcp 65001 >nul
cd /d "%~dp0"
set "PROJ=%CD%"

set "GODOT_MONO=C:\Users\vitor\Downloads\Godot_mono_4.7.2\Godot_v4.7.2-stable_mono_win64\Godot_v4.7.2-stable_mono_win64.exe"

if not exist "%GODOT_MONO%" (
  echo [ERRO] Godot .NET nao encontrado:
  echo   %GODOT_MONO%
  echo.
  echo Baixe "Godot Engine - .NET" 4.7 em https://godotengine.org/download
  echo Extraia e ajuste o caminho neste arquivo .bat se necessario.
  echo.
  echo NAO use Godot_v4.7.2-stable_win64.exe ^(sem mono^).
  pause
  exit /b 1
)

echo Abrindo Vila Nova com Godot .NET...
echo Projeto: %PROJ%
"%GODOT_MONO%" --path "%PROJ%"
set ERR=%ERRORLEVEL%
if not "%ERR%"=="0" (
  echo Godot encerrou com codigo %ERR%.
  pause
)
exit /b %ERR%
