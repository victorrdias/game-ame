@echo off
chcp 65001 >nul
setlocal
cd /d "%~dp0"

set "SCRIPT=%~dp0generate_cozy_house.py"
set "OUT=%~dp0..\..\Assets\Models\vn_building_cozy_house.glb"

set "BLENDER="

if defined BLENDER_EXE if exist "%BLENDER_EXE%" set "BLENDER=%BLENDER_EXE%"

if not defined BLENDER if exist "C:\Program Files\Blender Foundation\Blender 4.2\blender.exe" set "BLENDER=C:\Program Files\Blender Foundation\Blender 4.2\blender.exe"
if not defined BLENDER if exist "C:\Program Files\Blender Foundation\Blender 4.3\blender.exe" set "BLENDER=C:\Program Files\Blender Foundation\Blender 4.3\blender.exe"
if not defined BLENDER if exist "C:\Program Files\Blender Foundation\Blender 4.4\blender.exe" set "BLENDER=C:\Program Files\Blender Foundation\Blender 4.4\blender.exe"
if not defined BLENDER if exist "C:\Program Files\Blender Foundation\Blender 4.5\blender.exe" set "BLENDER=C:\Program Files\Blender Foundation\Blender 4.5\blender.exe"
if not defined BLENDER if exist "C:\Program Files\Blender Foundation\Blender 5.2\blender.exe" set "BLENDER=C:\Program Files\Blender Foundation\Blender 5.2\blender.exe"

where blender >nul 2>&1
if not defined BLENDER if %ERRORLEVEL%==0 set "BLENDER=blender"

if not defined BLENDER (
  echo [ERRO] Blender nao encontrado.
  echo Defina BLENDER_EXE ou instale Blender e adicione ao PATH.
  echo Exemplo:
  echo   set BLENDER_EXE=C:\Program Files\Blender Foundation\Blender 4.2\blender.exe
  echo   "%~f0"
  exit /b 1
)

echo Usando: %BLENDER%
echo Script: %SCRIPT%
echo Saida esperada: %OUT%

"%BLENDER%" --background --python "%SCRIPT%"
set ERR=%ERRORLEVEL%

if exist "%OUT%" (
  echo GLB gerado: %OUT%
) else (
  echo [ERRO] Arquivo GLB nao encontrado apos execucao.
  exit /b 1
)

exit /b %ERR%
