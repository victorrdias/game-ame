#!/usr/bin/env bash
set -euo pipefail
SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
SCRIPT="$SCRIPT_DIR/generate_cozy_house.py"
OUT="$SCRIPT_DIR/../../Assets/Models/vn_building_cozy_house.glb"

BLENDER="${BLENDER_EXE:-}"

if [[ -z "$BLENDER" ]]; then
  if command -v blender >/dev/null 2>&1; then
    BLENDER="blender"
  elif [[ -x "/c/Program Files/Blender Foundation/Blender 4.2/blender.exe" ]]; then
    BLENDER="/c/Program Files/Blender Foundation/Blender 4.2/blender.exe"
  elif [[ -x "/c/Program Files/Blender Foundation/Blender 4.3/blender.exe" ]]; then
    BLENDER="/c/Program Files/Blender Foundation/Blender 4.3/blender.exe"
  fi
fi

if [[ -z "$BLENDER" ]]; then
  echo "[ERRO] Blender não encontrado. Defina BLENDER_EXE ou instale Blender."
  exit 1
fi

echo "Usando: $BLENDER"
"$BLENDER" --background --python "$SCRIPT"

if [[ -f "$OUT" ]]; then
  echo "GLB gerado: $OUT ($(wc -c < "$OUT") bytes)"
else
  echo "[ERRO] GLB não encontrado: $OUT"
  exit 1
fi
