# Casa Hero — referência artística

Asset gerado por `Tools/Blender/generate_hero_house.py` para evoluir a estética **cozy 3D pixel art** do Vila Nova.

## Arquivos

| Arquivo | Descrição |
|---------|-----------|
| `Assets/Models/vn_building_hero_house.glb` | Modelo hero (showcase) |
| `Assets/Textures/vn_hero_house_atlas.png` | Atlas 128×128 (cópia embutida no GLB) |
| `Assets/Models/vn_building_cozy_house.glb` | Versão anterior (gameplay + comparação) |

## Diferenças vs casa cozy (GLB antigo)

| Aspecto | Cozy GLB | Hero GLB |
|---------|----------|----------|
| Materiais | Principled + cor sólida | **Voxels + cores sólidas** (padrão checker na geometria; sem atlas esticado em UV de cubo) |
| Telhado | Duas lajes inclinadas | Beiral, cumeeira, **fileiras de telhas** voxel |
| Paredes | Caixão liso + vigas | Gesso pixel + **cantoneiras de pedra** + vigas |
| Janelas | Caixas planas | **Recorte**, vidro glow, peitoril, mullions |
| Porta | Placa simples | **Batente**, recesso, degrau, maçaneta |
| Detalhes | Floreira básica | Caixa + textura **flower** no atlas |
| Silhueta | Baixa | Cumeeira + chaminé + telhas mais legíveis |

Footprint horizontal mantido dentro de **4×4 m** (2×2 células). Origem no centro do chão, fachada **+Y** Blender → rotação **-90° Y** no Godot.

## Visualizar no ArtShowcase

1. Abrir `Scenes/Art/ArtShowcase.tscn` (F6).
2. Seção **`HouseCompare`**:  
   - **Esquerda (`CozyLegacyGlb`)**: `vn_building_cozy_house.glb`  
   - **Direita (`HeroHouseGlb`)**: `vn_building_hero_house.glb`  
3. Mesma luz/câmera ortográfica (`ShowcaseCameraRig`, size 20).
4. **Gameplay** (`TownPrototype` / F5): `CozyHouse.tscn` usa **`vn_building_hero_house.glb`** sob `VisualRoot`. O cozy GLB permanece em `Assets/Models/` para comparação no ArtShowcase.

## Regenerar

```bat
Tools\Blender\run_generate_hero_house.bat
```

Depois: Godot → reimportar GLB ou `--headless --import`.

## Critério de qualidade

Compilar/importar **não** significa aprovação artística. Avaliar manualmente silhueta isométrica, telhas, janelas e nitidez das texturas nas quatro rotações de câmera.
