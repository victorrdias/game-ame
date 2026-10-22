@echo off
chcp 65001 >nul
setlocal
cd /d "%~dp0"

set "SCRIPT=%~dp0generate_village_decorations.py"
set "BLENDER="
if defined BLENDER_EXE if exist "%BLENDER_EXE%" set "BLENDER=%BLENDER_EXE%"
if not defined BLENDER if exist "C:\Program Files\Blender Foundation\Blender 5.2\blender.exe" set "BLENDER=C:\Program Files\Blender Foundation\Blender 5.2\blender.exe"
where blender >nul 2>&1
if not defined BLENDER if %ERRORLEVEL%==0 set "BLENDER=blender"

if not defined BLENDER (
  echo [ERRO] Blender nao encontrado. Defina BLENDER_EXE.
  exit /b 1
)

"%BLENDER%" --background --python "%SCRIPT%"
exit /b %ERRORLEVEL%
