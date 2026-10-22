# Vila Nova — Pipeline de assets 3D

Fluxo **Blender → GLB → Godot 4.7 (.NET)** para substituir geometria procedural mantendo gameplay e colisão.

## Pastas no repositório

| Pasta | Conteúdo |
|-------|----------|
| `Assets/Models/` | Arquivos `.glb` / `.gltf` importados |
| `Assets/Textures/` | PNG/WebP futuros (albedo, opcional ORM simples) |
| `Scenes/Buildings/`, `Scenes/Environment/` | Cenas Godot que referenciam scripts + `VisualRoot` |
| `Assets/Catalog/` | `BuildingDefinition` apontando para `VisualScene` |

## Convenção de cena Godot (obrigatória)

Estrutura alvo — igual aos placeholders atuais:

```
CozyHouse (Node3D)          ← raiz: posição/rotação de jogo, StructureBody aqui se existir
├── VisualRoot (Node3D)     ← substituir filhos por GLB instanciado ou meshes importadas
│   └── (meshes / GLB instance)
└── StructureBody (opcional, gerado em runtime pelo jogo)
```

- **Nome fixo:** `VisualRoot` (`CozyHouse.VisualRootName` / `CozyTree.VisualRootName`).
- Scripts como `CozyHouse.cs` hoje **geram** malha em `_Ready`; ao migrar para GLB, preferir:
  - cena `.tscn` com `VisualRoot` + filho `PackedScene` do GLB, **ou**
  - script que só instancia GLB se `VisualRoot` estiver vazio (evitar duplicar lógica de gameplay).

Gameplay (`BuildingManager`, colisão, grid) **não** depende de como `VisualRoot` foi preenchido.

## Blender — configuração

| Item | Valor |
|------|--------|
| Unidade | **1 Blender unit = 1 metro** |
| Eixo | **Y up** (padrão Blender) — GLB exporta Y-up compatível com Godot |
| Escala aplicada | **Ctrl+A → Scale** antes de exportar |
| Pivot / origem | **Centro do footprint no chão** (origem do objeto raiz exportado) |
| Frente | Fachada principal alinhada a **+Y Blender** (mapear para +Z Godot após import; verificar rotação -90° Y se necessário) |

### Footprints

| Tipo | Tamanho mundo | Células |
|------|---------------|---------|
| Prop pequeno | ~2 m × 2 m | 1×1 |
| Casa / loja / quiosque / fonte | ~4 m × 4 m | 2×2 |
| Caminho | ~1,92 m tile | 1×1 |

Modelar dentro ou sobre um guia quad **2 m** ou **4 m** para validar escala.

## Exportação GLB

1. Selecionar hierarquia visual (idealmente um único root mesh ou grupo sob um empty).
2. **File → Export → glTF 2.0 (.glb)**  
   - Format: **GLB**  
   - Include: **Selected Objects** (ou cena limpa só do asset)  
   - Transform: **+Y Up**  
   - Geometry: aplicar modificadores; triangulate se necessário  
   - Materials: **Export** (cores base; texturas embutidas se houver)
3. Salvar em `Assets/Models/` com nome descritivo.

### Nomenclatura de arquivos

```
vn_<categoria>_<nome>[_variant].glb
```

Exemplos:

- `vn_building_cozy_house.glb`
- `vn_nature_tree_round_a.glb`
- `vn_path_stone_tile.glb`
- `vn_deco_lamp_post.glb`

Evitar espaços e acentos no path.

## Import no Godot

1. Copiar `.glb` para `Assets/Models/`.
2. Godot gera `.import` automaticamente; revisar aba **Import**:
   - **Root Type:** `Node3D` (ou `MeshInstance3D` se for mesh única)
   - **Scale:** 1,0 (corrigir no Blender, não no import)
   - **Meshes:** gerar tangents se usar normal maps leves no futuro
3. Criar ou editar cena existente (ex.: `Scenes/Buildings/CozyHouse.tscn`):
   - Manter script raiz se ainda precisar de exports de cor.
   - Arrastar GLB como filho de **`VisualRoot`**.
   - Desativar ou remover geração procedural quando o GLB estiver final.
4. Atualizar `BuildingDefinition.VisualScene` se a cena mudou de path.

### Materiais no Godot

- Preferir **StandardMaterial3D** alinhado a `VillagePalette.CreateMatte` (metallic 0, specular off).
- Se o GLB traz materiais PBR metálicos, sobrescrever no import ou substituir por materiais `.tres` do projeto.
- Texturas externas: colocar em `Assets/Textures/<asset>/` e referenciar no material Godot.

## Orientação e rotação comuns

| Problema | Ajuste |
|----------|--------|
| Modelo de costas | Rotar `VisualRoot` **Y ± 90°** |
| Flutua / enterra | Mover mesh dentro de `VisualRoot` em Y; origem raiz permanece no chão |
| Escala 100× | Reexportar com scale aplicada no Blender |
| GLB com root extra | Instanciar só o nó mesh dentro de `VisualRoot` |

## Checklist antes de merge

- [ ] Footprint bate com `SizeInCells` no `.tres` do catálogo  
- [ ] Origem centrada no chão; casa 2×2 ocupa ~4 m  
- [ ] Materiais matte, paleta coerente com `ART_DIRECTION.md`  
- [ ] Visual testado em `Scenes/Art/ArtShowcase.tscn`  
- [ ] Colisão: `BlocksMovement` true gera `StructureBody` no runtime — mesh visual não precisa incluir collider no GLB  
- [ ] `dotnet build` OK  

## O que não fazer neste pipeline

- Não alterar `BuildingManager`, grid ou input para importar arte.
- Não embutir lógica C# dentro do GLB.
- Não substituir `TownPrototype` como main scene.
- Shaders complexos, pixelização fullscreen e pós-processo ficam fora do escopo atual.

## Referências no código

- Paleta e material matte: `Scripts/Visual/VillagePalette.cs`
- Primitivas atuais: `Scripts/Visual/ProceduralMeshBuilder.cs`
- Casa referência: `Scripts/Buildings/CozyHouse.cs`
- Árvore referência: `Scripts/Environment/CozyTree.cs`
- Caminhos: `Scripts/Environment/PathTileVisual.cs`
