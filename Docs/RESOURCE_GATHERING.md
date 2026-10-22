# Coleta de madeira e pedra

O F continua sendo uma tecla só. O roteador em `BuildingInteractionController` escolhe a ação do alvo válido mais próximo. Construção e demolição seguem iguais: com o modo de obra aberto, o F não corta nem inspeciona.

## O que pode ser destruído

Só entra na coleta o que o catálogo marca em `BuildingDefinition.GatherKind`. O jogo não olha o nome da cena nem da malha.

| Peça | Tipo | Golpes | Drops |
|------|------|--------|-------|
| Carvalho, álamo | Madeira | 4 | 3 troncos |
| Pedra pequena, média e grande | Pedra | 4 | 2 pedaços |

Arbustos, flores, casas, caminhos e o chão do vale ficam de fora. Árvores desenhadas dentro de um GLB de terreno não são nós próprios, então não são cortadas. O componente `HarvestableResource` é criado na hora do registro, tanto para o que já está na cena quanto para o que o jogador coloca.

## Golpe

Cada F válido soma 1 no contador daquela instância. Outra árvore não herda o progresso. Há uma espera de 0,42 s entre golpes, e a repetição da tecla é ignorada. O personagem vira para o alvo sem mudar o andar. Um machado ou uma picareta de caixas aparece na frente do corpo e desce num arco curto. O recurso treme, solta lascas e toca um baque gerado em código, porque não há arquivo de áudio no projeto.

Um raio na camada de estruturas, ignorando o próprio recurso e o jogador, impede o golpe através de uma parede.

## Fim do recurso

No quarto golpe o registro sai do `BuildingOccupancyGrid`, a colisão é desligada e os subtiles daquela peça ficam livres. A navegação dos moradores lê essa grade, então o espaço volta a ser atravessável. A malha encolhe e some. Os drops nascem ao lado, pousam na altura do terreno e não têm colisão. Depois da queda, a mochila os puxa sozinha. Ver `Docs/INVENTORY.md`.

## Prompt

- `F — Cortar árvore`
- `F — Quebrar pedra`
- `F — Inspecionar construção`

Se dois alvos estão a menos de 5 cm de distância, o recurso ganha da inspeção. Se os dois são do mesmo tipo, vale o id menor.

## Testes feitos

- `GatheringTests`: quatro golpes derrubam a árvore e a pedra, o quinto golpe não conta, duas árvores não compartilham progresso, e o alvo mais próximo vence. No empate de 5 cm o recurso vence a inspeção, e o id menor desempatam dois recursos.
- `dotnet build` sem avisos.
- Godot Mono headless abre `TownPrototype` e imprime a população, sem erro de script.

## Ainda manual

- F fora do alcance e F com uma casa entre o jogador e a árvore.
- Ver o arco da ferramenta, as lascas e ouvir o baque.
- Cortar a árvore da cena e a árvore colocada pelo menu, e ver os 3 troncos ou os 2 pedaços no chão.
- Conferir que o subtile ficou livre e que um morador atravessa o espaço.
