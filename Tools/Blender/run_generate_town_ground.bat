@echo off
set "BLENDER_EXE=C:\Program Files\Blender Foundation\Blender 5.2\blender.exe"
if not exist "%BLENDER_EXE%" (
  echo Blender 5.2 nao encontrado.
  exit /b 1
)
"%BLENDER_EXE%" --background --python "%~dp0generate_town_ground.py"
exit /b %ERRORLEVEL%
