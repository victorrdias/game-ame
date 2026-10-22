# Blender — Vila Nova

## Casa cozy de referência

Gera `Assets/Models/vn_building_cozy_house.glb` a partir de `generate_cozy_house.py`.

### Windows

```bat
Tools\Blender\run_generate_cozy_house.bat
```

Opcional: `set BLENDER_EXE=C:\Program Files\Blender Foundation\Blender 5.2\blender.exe`

### Git Bash / Linux

```bash
chmod +x Tools/Blender/run_generate_cozy_house.sh
BLENDER_EXE="/c/Program Files/Blender Foundation/Blender 5.2/blender.exe" Tools/Blender/run_generate_cozy_house.sh
```

### Manual

```bat
blender --background --python Tools/Blender/generate_cozy_house.py
```

Eixos no script: **Z para cima**, origem no centro do footprint no chão, fachada **+Y**. Export GLB com **Y-up** para Godot.

Parâmetros e cores ficam no topo de `generate_cozy_house.py`. Ver também `Docs/ART_DIRECTION.md` e `Docs/ASSET_PIPELINE.md`.

## Casa hero (referência artística)

```bat
Tools\Blender\run_generate_hero_house.bat
```

Gera `Assets/Models/vn_building_hero_house.glb` + `Assets/Textures/vn_hero_house_atlas.png`. Comparar em `Scenes/Art/ArtShowcase.tscn` — ver `Docs/HERO_HOUSE.md`.

## Casa de referência (sprint visual)

```bat
Tools\Blender\run_generate_reference_cottage.bat
```

Gera `Assets/Models/vn_building_reference_cottage.glb`, o `.blend` ao lado e os renders em `Assets/ArtPreviews/vn_reference_cottage_{front,back,side,iso}.png`.

Footprint de trabalho **6×6 m** (3×3 células). Não entra no catálogo nem no `TownPrototype`. Ver a casa em `Scenes/Art/ReferenceCottageShowcase.tscn` e, isolada, no `ArtShowcase`.
