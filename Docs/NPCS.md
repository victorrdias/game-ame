# Moradores

Residentes autônomos do protótipo. Não há necessidades, profissões, relacionamentos, economia nem salvamento.

## Arquivos

| Arquivo | Função |
| --- | --- |
| `Scripts/Npcs/NpcResident.cs` | Identidade: id, nome, casa opcional, edifício atual, estado |
| `Scripts/Npcs/NpcController.cs` | `CharacterBody3D`, rotina e movimento |
| `Scripts/Npcs/NpcVisual.cs` | O modelo do personagem, com túnica, calça e pele próprias, e o mesmo passo |
| `Scripts/Npcs/NpcPopulationManager.cs` | Cria, registra e reage a construção/demolição |
| `Scripts/Npcs/NpcRoutineState.cs` | Idle, Wandering, GoingToBuilding, InsideBuilding, LeavingBuilding |
| `Scripts/Npcs/NpcRoutineConfig.cs` | Velocidade, esperas, chance de visita, raio de chegada |
| `Scripts/Npcs/TownWalkability.cs` | Célula livre = dentro do terreno e sem `BlocksMovement` |
| `Scripts/Npcs/NpcGridPathfinder.cs` | A* no grid de 2 m |
| `Scripts/Npcs/BuildingEntrance.cs` | Ponto de entrada no espaço local do edifício |
| `Scenes/Npcs/NpcResident.tscn` | Corpo, cápsula e visual |

A cena principal instancia `Npcs` e `NpcPopulationManager` depois do `BuildingManager`.

## Navegação

O mapa já é um grid de ocupação que muda quando o jogador constrói ou demolhe. `NavigationAgent3D` / NavMesh foi deixado de lado: a malha precisaria ser reassada a cada obra, e o desvio de obstáculos do Godot não abre caminho em volta de uma casa nova. O A* lê o mesmo grid da construção, só quando o morador escolhe um destino ou quando uma obra invalida o caminho atual. Não há destino novo a cada frame.

Células de caminho, flores e qualquer item com `BlocksMovement = false` continuam andáveis. Casas, árvores, arbustos, bancos, postes e a fonte bloqueiam a célula inteira. O morador contorna por células vizinhas, inclusive na diagonal, sem cortar quinas.

## Entradas

`IsVisitable` e `EntranceLocalOffset` ficam no `BuildingDefinition`. O offset é local, antes da rotação R; o nó do edifício aplica essa rotação. A entrada só vale se ficar fora da caixa de colisão (com folga para a cápsula), numa célula andável e colada na fachada.

| Item | Visitável | Offset local |
| --- | --- | --- |
| Casa aconchegante | sim | `(-2.5, 0, 0)` — porta do GLB na fachada −X |
| Loja pequena | sim | `(-1, 0, 2.5)` — porta procedural em +Z |
| Quiosque | sim | `(0, 0, 2.5)` — balcão em +Z |
| Árvores, arbustos, flores, bancos, postes, fonte, caminhos | não | — |

## Rotina

O morador espera, depois passeia ou visita um edifício alcançável. Na entrada some (o nó continua existindo) e a cápsula desliga. Depois do tempo interno, volta na entrada se houver espaço. Se a porta continuar ocupada por cerca de 6 s, procura uma célula livre perto. Se o edifício for demolido com ele dentro, a colisão da obra é desligada e ele reaparece na célula livre mais próxima. A identidade não é destruída na visita.

`HomeBuildingId` começa vazio. `NpcPopulationManager.TryAssignHome(residentId, buildingInstanceId)` guarda a associação para uma etapa futura. Edifícios da cena usam id `scene:` + caminho do nó; os colocados pelo jogador usam `placed:` + guid.

## Controles

Nenhuma tecla nova. WASD, câmera, R, X e F permanecem os de antes. Moradores andam sozinhos.

Colocar uma construção sobre um morador que está no mundo falha com **Um morador está neste local.** Quem está dentro de um edifício não ocupa o chão. Caminhos e flores não viram obstáculo de navegação.

A quantidade inicial é o export `ResidentCount` no `NpcPopulationManager` (padrão 20). No mesmo nó dá para ajustar velocidade, esperas, chance de visita e raio de chegada.

## Validação

`dotnet build` concluiu sem avisos. O Godot 4.7.2 mono carregou `Scenes/TownPrototype.tscn` em headless, sem erro de script nem referência nula.

Numa simulação headless de cerca de 30 s (física a 60 Hz):

- 5 moradores surgiram com nomes distintos.
- A entrada da casa ficou em `(-2.5, 0, 0)`, do mesmo lado do mesh `door_panel`.
- Havia caminho em células até essa entrada.
- Eles caminharam dezenas de metros, contornando o grid.
- Parte entrou (`InsideBuilding`, ocultos, perto da porta), um ficou em `LeavingBuilding` com a porta ocupada, e aos 30 s todos estavam visíveis de novo: parados, passeando ou a caminho de outra visita.

Não houve inspeção de um frame renderizado. Construção e demolição com o mouse, inclusive demolir com alguém dentro, não foram executadas nesta sessão; o gancho está em `BuildingPlaced` / `BuildingRemoved`.

## Limitações

- Sem salvar. Os ids de obras colocadas na partida mudam na próxima execução.
- Sem interior 3D: entrar é esconder o morador.
- A navegação respeita a célula de 2 m, não o contorno do mesh. Um banco ocupa a célula inteira.
- Vários moradores podem estar “dentro” do mesmo edifício.
- A casa ainda não é atribuída como lar.
- O jogador esbarra nos moradores presentes. O código de WASD não mudou; a máscara de colisão do personagem passou a incluir a camada `npc`.
