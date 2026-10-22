# Mochila

Uma mochila só, com 20 espaços. Madeira e pedra entram nela por proximidade. Não há peso, crafting, durabilidade, equipamento nem salvamento.

## Itens

O id estável identifica o item. O nome exibido não entra na lógica.

| Id | Nome | Pilha |
| --- | --- | --- |
| `wood` | Madeira | 99 |
| `stone` | Pedra | 99 |

`ItemCatalog` guarda as definições. O catálogo de construção continua separado.

## Mochila

`Inventory` é a regra dos espaços. `InventoryComponent` fica no jogador, fora do `PlayerController`, e avisa `InventoryChanged`.

`TryAddItem` enche pilhas do mesmo id e só então abre um espaço vazio. O retorno é o que coube. O que não coube continua no chão. `TryRemoveItem` e `Count` ficam prontos para sistemas futuros.

## Drops

Cada pedaço no mundo é um `GatherDrop`: id, quantidade, estado (disponível, atraído ou coletado) e posição. A queda inicial continua. Antes dela terminar, e antes de 0,45 s, ninguém coleta. A coleta só apaga o pedaço depois que a mochila confirma a entrada. Se couber só uma parte, a quantidade no chão diminui e o resto espera o jogador se afastar e voltar.

Não há corpo físico. Moradores atravessam os pedaços.

## Ímã

`PlayerPickupCollector` olha o grupo `world_drop` uma vez por quadro de física.

- Atração a 2 m no plano XZ.
- Coleta a 0,4 m.
- Desnível maior que 1,75 m fica de fora.
- Um raio na camada de estruturas não puxa o item através de uma parede.
- F não coleta.
- O andar do jogador não muda.

## Interface

`I` abre e fecha a mochila. O painel é pergaminho, no centro, com 20 espaços, ícone, quantidade, seleção e tooltip. O fundo do painel deixa o clique passar para o mundo. Esc fecha a mochila quando o painel de inspeção está fechado e não há obra em andamento. Inspeção e cancelamento de construção continuam na frente.

A hotbar, embaixo no centro, mostra os oito primeiros espaços e acompanha `InventoryChanged`. Ainda não usa o item.

## Feedback

Um texto curto, como `+1 Madeira`, sobe e some. Um halo pequeno acompanha, e um estalo é gerado em código. Não há arquivo de áudio no projeto.

## Testes feitos

- Pilha até 99 e o excedente no espaço seguinte.
- Mochila cheia devolve só o que coube, e o resto não entra.
- Madeira e pedra não dividem pilha.
- Remoção esvazia o espaço.
- Um drop coletado não entra de novo. Coleta parcial deixa o resto.
- Raio, atraso, desnível e item já recusado ficam de fora do ímã.
- `dotnet build` e Godot Mono headless na cena da vila.

## Ainda manual

- Cortar uma árvore e ver a madeira entrar na hotbar.
- Quebrar uma pedra e ver a pedra entrar.
- Andar para fora dos 2 m e confirmar que o pedaço não vem.
- Vários pedaços ao mesmo tempo.
- Casa entre o jogador e o drop.
- Encosta alta, fora do limite vertical.
- Abrir a mochila com I, fechar com Esc, e usar U e F ao mesmo tempo.
