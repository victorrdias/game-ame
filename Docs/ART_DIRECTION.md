# Vila Nova — Direção artística

Referência visual para placeholders procedurais e para assets 3D importados (GLB). Alinha-se a `VillagePalette`, `ProceduralMeshBuilder` e à convenção **VisualRoot** usada em `CozyHouse`, `CozyTree`, caminhos e decorações.

## Visão geral

- **Estilo:** cozy 3D com leitura de “pixel art” — formas simples, cores planas, sombras suaves, silhuetas claras à distância isométrica.
- **Evitar:** PBR realista, metal brilhante, micro-detalhe, normais agressivas, paletas frias dominantes, polígonos desnecessários.
- **Prioridade:** legibilidade com câmera ortográfica afastada (`size` ~18–24), contraste entre telhado / parede / chão / vegetação.

## Escala e grid (mundo)

| Constante | Valor | Uso |
|-----------|-------|-----|
| Célula do grid | **2,0 m** | Construção e ocupação |
| Terreno de referência | **40×40 m** | `TownPrototype` / showcase |
| Casa 2×2 | ~**3,5×3,3 m** base, ~**3,2 m** altura até cumeeira | `CozyHouse` |
| Caminho 1×1 | laje **~1,92×0,05×1,92 m** | `PathTileVisual` |
| Árvore 1×1 | copa ~**2,5 m** diâmetro, tronco ~**1,75 m** | `CozyTree` |

**Regra:** o **centro lógico** do prop fica na origem do nó raiz da cena (`Node3D`); geometria visível fica sob **`VisualRoot`**. Footprints maiores (2×2) devem estar centrados na origem para bater com `GridSystem.FootprintCenterWorld`.

## Paleta (hex)

Cores canônicas em `Scripts/Visual/VillagePalette.cs`:

### Arquitetura

| Nome | Hex | Uso |
|------|-----|-----|
| WallCream | `#f3e8d4` | Paredes principais |
| WallShadow | `#e5d4bc` | Variação de parede |
| WoodWarm | `#9c6b43` | Vigas, portas secundárias |
| WoodDark | `#6f4a2e` | Detalhes escuros |
| RoofTerracotta | `#c06840` | Telhado principal |
| RoofShadow | `#9a4f32` | Aba / telha escura |
| DoorWood | `#7a4f2f` | Portas |
| TrimPaint | `#dcc9a8` | Molduras |
| WindowGlass | `#8eb5c9` | Vidro (roughness ~0,35) |
| WindowFrame | `#f0e2cc` | Caixilhos |
| StoneFoundation | `#b8a898` | Base / degraus |
| ChimneyBrick | `#a8654f` | Chaminé |

### Natureza e decoração

| Nome | Hex | Uso |
|------|-----|-----|
| TrunkBrown / TrunkDark | `#7a5230` / `#5c3d24` | Tronco |
| FoliageGreen / Light / Shadow | `#4f9160` / `#6db57a` / `#3a7350` | Copa volumosa |
| FlowerGreen / Accent | `#6f9b5a` / `#e8927c` | Flores |

### Caminhos (referência)

| Estilo | Hex aprox. | Notas |
|--------|------------|--------|
| Terra | `#9a744c` | Base compacta |
| Pedra | `#a8a29a` + faixas `#8f887f` | Padrão discreto em relevo |
| Tijolo | `#b86a4a` + listras `RoofShadow` | Faixas horizontais baixas |

## Materiais (Godot `StandardMaterial3D`)

Padrão do projeto via `VillagePalette.CreateMatte`:

- **Metallic:** `0`
- **Specular:** desligado (`Disabled`)
- **Roughness:** `0,92` (paredes/madeira); vidro até `0,35`
- **Sem** texturas obrigatórias nos placeholders; futuras texturas devem ser **baixa frequência**, pouco ruído, filtro com cuidado (ver pipeline).

Objetivo: superfície mate estilizada, não plástico espelhado.

## Iluminação

Configuração de referência (ver `Scenes/Art/ArtShowcase.tscn` e `TownPrototype.tscn`):

- **Sol:** `DirectionalLight3D`, cor quente `rgb(1, 0.93, 0.82)`, energia ~**1,05**, sombras suaves (`directional_shadow_mode` blended splits).
- **Céu:** `ProceduralSkyMaterial` — topo azul suave, horizonte claro, chão verde-acinzentado.
- **Ambiente:** energia ~**0,42**, tom quente; exposure/contrast/saturation levemente acima de 1 (`adjustment_*` ~1,02–1,06).
- **Sem** SSAO/SDFGI/glow nos placeholders — legibilidade > realismo.

## Proporções e silhueta

- **Casas:** telhado ~35–40% da altura total; beiral largo; chaminé deslocada (não centrada).
- **Árvores:** 2–4 esferas / volumes para copa; tronco cônico 8 segmentos máx. nos placeholders.
- **Props 1×1:** altura útil **0,5–2,5 m**; postes e fontes podem passar discretamente de 2 m se a silhueta continuar clara.
- **Caminhos:** quase flush ao chão (Y ~0,025–0,05) para não competir com personagem.

## Texturas (futuro)

- Resolução alvo: **128–512 px** por material de prop; atlas opcional para variantes.
- Estilo: hand-painted ou pixel upscale suave; **sem** fotorealismo.
- UVs: uma ilha por material lógico (parede, telhado, madeira).
- Cores base devem cair dentro da paleta ou ser documentadas como variante sazonal.

## Regras de consistência

1. Toda geometria substituível vive em **`VisualRoot`**; raiz mantém transform de jogo e colisão (`StructureBody` no raiz quando aplicável).
2. Novos assets respeitam **footprint em células** do catálogo (`BuildingDefinition.SizeInCells`).
3. Origem no **centro do footprint** no chão (Y = 0), eixo Y para cima, +Z como “frente” preferencial da casa.
4. Reutilizar cores de `VillagePalette` ou estender a classe — evitar hex soltos no código.
5. Testar sempre em **`Scenes/Art/ArtShowcase.tscn`** com câmera ortográfica fixa antes de trocar no catálogo.
6. Polígono: preferir formas simples; LOD não é prioridade na fase cozy prototype.

## Cena de referência

`Scenes/Art/ArtShowcase.tscn` — iluminação, casa, lojas, árvores, caminhos e decorações instanciados das cenas existentes. **Não** é a cena principal do jogo (`run/main_scene` continua `TownPrototype.tscn`).
