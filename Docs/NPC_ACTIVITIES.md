# Atividades de moradores

Camada extensível de pontos de interação para rotinas opcionais, sem substituir passeio nem visita a edifícios.

## Arquivos

| Arquivo | Função |
| --- | --- |
| `Scripts/Npcs/Activities/NpcActivityType.cs` | `SitBench`, `Fish` |
| `Scripts/Npcs/Activities/NpcInteractionPoint.cs` | Posição de aproximação, pose final, yaw, origem, validade |
| `Scripts/Npcs/Activities/NpcInteractionPointState.cs` | Disponível, reservado, ocupado |
| `Scripts/Npcs/Activities/NpcInteractionRegistry.cs` | Registro central e reserva atômica (thread principal) |
| `Scripts/Npcs/Activities/BenchInteractionBuilder.cs` | Assentos para `rustic_bench` e `village_bench` |
| `Scripts/Npcs/Activities/FishingInteractionBuilder.cs` | Margens walkáveis ao lado de peças `LinkGroup = river` |
| `Scripts/Npcs/Activities/NpcActivityVisual.cs` | Sentar, levantar e poses de pesca no modelo blocky |
| `Scripts/Npcs/Activities/NpcFishingRodVisual.cs` | Vara, linha (cilindro) e boia procedurais |
| `Scripts/Npcs/NpcRoutineState.cs` | Estados `GoingToActivity`, `AligningToActivity`, `PerformingActivity`, `LeavingActivity` |
| `Scripts/Npcs/NpcRoutineConfig.cs` | Pesos, cooldown, durações de sentar e pescar |

`NpcPopulationManager` cria o registry, registra bancos e rios já colocados, escuta `BuildingPlaced` / `BuildingRemoved` e expõe `CollectReachableActivities` (A* até a célula de aproximação, sem varrer o mapa a cada frame).

## Pontos de interação

Cada ponto tem id estável na sessão (`bench-N`, `fish-N`), tipo, célula e mundo de aproximação, pose final (assento ou margem), yaw voltado para o assento/água, capacidade 1, referência ao nó visual da origem e `IsValid` (edifício ainda existe, margem ainda walkável).

Reserva: `TryReserve` só aceita se o estado for `Available`. Ao iniciar a animação, `MarkOccupied`. `Release` no fim ou cancelamento. Demolição chama `UnregisterBySource` e `HandleActivitySourceRemoved` no NPC afetado.

## Rotina

Depois do idle, o sorteio continua: passeio preferencial, visita (`VisitChance`), atividade (`ActivityChance`), senão wander. Cooldown por NPC (`ActivityCooldownSeconds`) reduz repetição. Pesos `SitWeight` e `FishWeight` diversificam escolhas.

Fluxo: `GoingToActivity` (mesmo travel A*) → `AligningToActivity` (slide curto + yaw) → `PerformingActivity` → `LeavingActivity` → idle/wander.

Caminho inválido: repath; falha libera reserva e volta ao idle. Banco demolido: cancela, solta colisão se sentado, reposiciona perto do footprint.

## Sentar em bancos

Bancos do catálogo (`rustic_bench`, `village_bench`) ganham um assento no centro do footprint (espaço local + rotação R do nó). Aproximação na célula walkável cardinal mais afastada do assento.

Animação: flexão de pernas/braços, rebaixamento do `VisualRoot`, tronco inclinado; colisão da cápsula desliga enquanto sentado para evitar empurrões. Duração 8–20 s (exports `MinSitSeconds` / `MaxSitSeconds`). Levantar restaura pose e `PlayerWalkAnimation`.

## Pescar

Somente margens reais: células walkáveis cardinais adjacentes a células visuais ocupadas por rios (`river_3x3`, `river_6x4`, `river_10x8`). Sem rio no mapa, nenhum ponto de pesca é inventado.

NPC fica na margem, olha para a água, vara procedural na mão direita, lançamento (0,55 s), espera com boia e linha visível (10–25 s), recolhimento. Sem peixes, inventário ou economia.

## Construção dinâmica

`BuildingPlaced`: registra assentos ou margens; NPCs em travel só repath se necessário. `BuildingRemoved`: remove pontos da origem e interrompe quem reservou/ocupava. Previews/ghosts não passam por `BuildingPlaced`.

## Performance

Registry em dicionário; candidatos só quando o NPC decide sair do idle. Vara/linha/boia reutilizadas por NPC. Sem física na linha. Pensado para ≥20 moradores.

## Testes

### Automatizados

- `dotnet build` — compilação C#.
- `GridLogicTestRunner` — reserva dupla no mesmo assento (`CheckInteractionRegistry`).
- Godot mono headless com cena de testes de grid (como em `Docs/NPCS.md`).

### Visuais / manuais (não executados nesta sessão)

- Dois NPCs tentando o mesmo banco (um deve esperar outro destino).
- Sentar e levantar em banco rotacionado (R = 0, 1, 2, 3).
- Colocar banco durante a partida e ver registro automático.
- Demolir banco com NPC sentado.
- Pesca na Starter Plaza (segmentos `river_6x4`).
- Lançamento, boia e recolhimento da linha.
- Demolir trecho de rio durante pesca.
- Mapa sem rio: só sentar, sem pesca.
- Entrada/saída de casas visitáveis inalterada.

## Exports (`NpcPopulationManager`)

| Export | Padrão | Uso |
| --- | --- | --- |
| `ActivityChance` | 0,38 | Chance após visita falhar no sorteio |
| `ActivityCooldownSeconds` | 14 | Espera mínima entre atividades |
| `SitWeight` / `FishWeight` | 1,0 / 0,85 | Sorteio entre pontos alcançáveis |
| `MinSitSeconds` / `MaxSitSeconds` | 8 / 20 | Tempo sentado |
| `MinFishSeconds` / `MaxFishSeconds` | 10 / 25 | Tempo com linha na água |

## Extensão futura

Novos tipos: enum `NpcActivityType`, builder que registra pontos, ramos em `NpcActivityVisual` e `NpcController.TickPerformingActivity`. Preferir validade por origem e reserva via registry.
