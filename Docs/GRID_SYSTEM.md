# Grades visual e de posicionamento

A grade desenhada no terreno continua com células de **2 m**. O posicionamento usa uma grade lógica **3× mais fina**: cada célula visual contém **9 subtiles** de **2/3 m** (`2 / 3`).

Origem compartilhada: o canto do terreno é o mesmo para as duas grades. Índices são inteiros. A conversão mundo → índice usa `floor((world + halfExtent) / cellSize)`, com `cellSize` igual a `2` ou `2/3`. Não há uma segunda origem.

## Tipos

| Tipo | Papel |
|------|--------|
| `TownGridLayout` | Dono das duas grades e das conversões |
| `GridSystem` | Grade genérica (visual ou placement) |
| `BuildingOccupancyGrid` | Ocupação em **subtiles**, camadas Structure e Path |
| `TownWalkability` | Navegação ainda em células **visuais** de 2 m |
| `TerrainGrid` | Desenha só a grade de 2 m |

`BuildingManager.Grid` continua sendo a grade visual, usada pelos NPCs. `BuildingManager.Layout.Placement` é a grade de construção.

## Footprints

`BuildingDefinition.SizeInCells` passou a contar **subtiles**, não células de 2 m.

| Objeto | Subtiles | Mundo aproximado |
|--------|----------|------------------|
| Poste, flores, lanterna, placa, caminhos | 1×1 | 0,67 m |
| Arbusto | 2×2 | 1,33 m |
| Árvore | 2×2 | tronco / colisão, não a copa inteira |
| Banco | 3×1 | 2,0 × 0,67 m |
| Poço | 3×3 | 2 m (a antiga célula visual) |
| Carroça | 3×2 | |
| Casa, loja, quiosque, cottages, fonte | 6×6 | 4 m (antigo 2×2 visual) |
| Cottage de referência | 9×9 | 6 m (antigo 3×3 visual) |

Objetos já colocados na cena **não são movidos**. O registro inicial recalcula a origem em subtiles a partir da posição mundial existente, então o centro do footprint continua no pivô do nó.

## Preview e rotação

O preview e o destaque no chão usam só os subtiles do footprint. A subdivisão 3×3 **não** é desenhada no terreno. `R` gira o footprint em passos de 90° (um banco 3×1 vira 1×3). Q/E gira a câmera e não entra no cálculo da célula.

## Colisão

Continua **um colisor por objeto**, não um por subtile. O tamanho da caixa acompanha o footprint em metros (`subtiles × 2/3`). Um poste 1×1 não bloqueia 2×2 m. Flores e caminhos seguem com `BlocksMovement = false`.

## NPCs

O A* permanece na resolução de **2 m** (centros das células visuais). Uma célula visual fica bloqueada quando:

- o subtile central bloqueia movimento, ou
- 6 ou mais dos 9 subtiles bloqueiam movimento.

Um poste no canto **não** fecha a célula inteira. O `CharacterBody3D` do NPC ainda colide com o colisor físico do objeto. Caminhos entre centros de 2 m não desenham corredores de um único subtile; isso fica documentado de propósito para não atravessar casas. Colocar ou remover um objeto continua disparando o recálculo das rotas, porque a walkability lê a ocupação ao vivo.
