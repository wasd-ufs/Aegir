# Arquitetura do Projeto — Aegir

## Visão Geral

Jogo 2D top-down de aventura marítima com geração procedural de mundo, sistema de batalha por turnos e gerenciamento de tripulação. Desenvolvido em Unity com C\#.

---

## Mapa Mental dos Sistemas

JOGO

├── Mundo Procedural (WFC)

│   ├── WorldGenerator        ← orquestrador central

│   ├── MapGenerator          ← geração de um chunk (WFC)

│   ├── RuleManager           ← regras de compatibilidade entre tiles

│   ├── Tile / TilesetData    ← dados de cada tile

│   └── Cell                  ← célula individual da grade WFC

│

├── Jogador & Movimento

│   ├── PlayerMovement        ← barco e capitão (modo água/terra)

│   ├── PlayerInputActions    ← mapeamento de input (auto-gerado)

│   └── CameraFollow          ← câmera suave com antecipação

│

├── NPCs & Criaturas

│   ├── NPCsData              ← dados, combate, efeitos e level

│   ├── NPCsMovement          ← IA de movimento (marítima/terrestre)

│   ├── NPC\_Randomizer        ← aleatorização de atributos no spawn

│   └── RecruitableNPC        ← NPC recrutável pelo jogador

│

├── Tripulação & Inventário

│   ├── CrewData              ← lista de membros e HP

│   ├── Inventory             ← slots de itens

│   ├── ItemData (abstrata)   ← base para todos os itens

│   │   ├── WeaponData

│   │   ├── ArmorData

│   │   ├── ConsumableData

│   │   ├── ThrowableData

│   │   └── MaterialData

│   ├── CrewUI                ← barras de HP da tripulação

│   ├── InventoryUI           ← tela de inventário

│   └── RecruitmentUI         ← tela de recrutamento

│

├── Batalha

│   ├── BattleManager         ← loop de turnos, botões e mensagens

│   ├── BattleData            ← setup visual e transição de batalha

│   ├── CombatBase (abstrata) ← lógica de ações e efeitos

│   ├── CrewAttacks           ← implementação concreta de CombatBase

│   └── StartFight            ← gatilho de batalha por colisão

│

├── Estado Global

│   └── GameState             ← flags estáticas (IsInBattle, IsOnWater...)

│

├── Áudio

│   ├── MusicManager          ← músicas por estado do jogo

│   └── SFXManager            ← efeitos sonoros pontuais

│

├── UI / Transições

│   ├── GameBoyTransition     ← transição animada estilo Game Boy

│   └── StartGame             ← tela inicial

│

└── Debug

    └── ClickDebug            ← log de cliques com raycast

---

## Sistema de Geração de Mundo (WFC)

### O que é WFC?

Wave Function Collapse é um algoritmo de geração procedural que garante consistência local: cada tile só pode ter vizinhos compatíveis com ele. O resultado é um mapa sem "erros" de transição (ex.: água nunca toca terra sem uma costa entre elas).

### Conceitos Fundamentais

**Chunk:** Bloco retangular de tiles (`chunkSize` × `chunkSize`).  
O mundo é um grid infinito de chunks carregados e descarregados conforme o jogador se move.

**Halo:** Anel de 1 célula ao redor do chunk interno.  
Contém tiles já definidos pelos chunks vizinhos. É usado como restrição inicial do WFC, garantindo continuidade visual entre chunks.

**Cell:** Cada posição na grade do chunk.  
Mantém um `BitArray` de tiles "possíveis". Quando só 1 bit está ativo, a célula está colapsada (tile definido).

**Socket:** Valor nos 4 cantos de um tile.  
Dois tiles vizinhos são compatíveis se os cantos compartilhados têm o mesmo valor. Isso é calculado em `Tile.IsCompatibleWith`.

### Fluxo Completo de Geração

WorldGenerator.Start()

└── GenerateInitialChunks()           ← chunks do campo de visão inicial

    └── CreateOrLoadChunkSync(pos)    ← para cada posição em espiral

        ├── BuildHalo(pos)            ← lê bordas dos chunks vizinhos

        └── MapGenerator.GenerateChunk()

            ├── EnsureCompatibilityCache()   ← tabela bool\[a,b,dir\]

            ├── InitCells(borderTiles)

            │   ├── Cria Cell\[GridW, GridH\]  ← todos os tiles possíveis

            │   ├── Colapsa células do halo  ← passagem 1

            │   ├── Propaga restrições       ← passagem 2

            │   └── Salva haloSnapshot       ← para reinícios

            └── RunCollapseSync()

                └── Loop:

                    ├── ChooseCell()              ← MRE: menor entropia

                    ├── CollapseAndPropagate()    ← colapsa \+ BFS

                    │   └── PropagateConsequences() ← remove tiles sem suporte

                    └── HasContradiction()?

                        └── sim → RestartFromHalo() ← reinicia pelo snapshot

### Tabela de Compatibilidade (`compatible[a, b, dir]`)

Construída uma única vez por chunk em `BuildCompatibilityCache()`.  
Evita chamar `RuleManager.IsBlocked()` repetidamente durante a propagação, que seria muito lento.

compatible\[tileA, tileB, direção\] \= true

    SE tileA.IsCompatibleWith(tileB, direção)   ← sockets compatíveis

    E  NÃO RuleManager.IsBlocked(tileA, tileB)  ← nenhuma regra proíbe

### Sistema de Sockets (Tile.cs)

Cada tile tem 4 cantos (NO, NE, SO, SE) com valores inteiros.  
O valor representa a camada do bioma naquele canto:

| Valor | Significado |
| :---- | :---- |
| 0 | Água |
| 1 | Costa |
| 2 | Terra |

Os sockets são **gerados automaticamente** em `OnValidate()` com base no tipo visual do tile:

| Tipo | Descrição |
| :---- | :---- |
| Bloco | Todos os cantos iguais (tile plano) |
| Costa | Uma borda inferior, oposta superior |
| Quina | 3 cantos inferiores, 1 superior (convexa) |
| QuinaInterna | 3 cantos superiores, 1 inferior (côncava) |

### Ciclo de Vida dos Chunks

Chunk entra no viewDistance

    ├── Tem arquivo .dat? → LoadFromData()  (sem WFC)

    └── Não tem?         → GenerateChunkAsync() (WFC completo)

            ↓

Chunk sai do viewDistance

    ├── Ainda gerando? → vai para pendingChunks (oculto, aguarda conclusão)

    └── Pronto?        → SaveAndDestroy() (salva .dat, destrói GameObject)

**Estados de um chunk:**

| Dicionário | Significado |
| :---- | :---- |
| `activeChunks` | Visível e completo |
| `pendingChunks` | Saiu do view enquanto ainda gerava |
| `generationQueue` | Aguardando vez de gerar (ordenado por distância) |
| `failedChunks` | Teve contradição WFC, aguarda nova tentativa |

### Persistência em Disco

Cada chunk é salvo como `chunk_X_Y.dat` — um array de bytes onde cada byte é o índice do tile colapsado naquela posição.

índice \= x \* chunkSize.y \+ y

⚠️ A ordem dos tiles no `TilesetData.tileset` nunca deve ser alterada após chunks serem salvos, pois os índices ficariam inválidos.

---

## Sistema de Batalha (Por Turnos)

### Fluxo do Loop de Batalha

StartFight (colisão) → GameBoyTransition → BattleData.StartFight()

    └── BattleManager.IniciarBatalha(enemyCrew)

        └── LoopDeBatalha() \[Coroutine\]

            ├── Turno do Player:

            │   ├── Gera botões da tripulação

            │   ├── Player seleciona ator → seleciona ação

            │   ├── CrewAttacks.ExecutarAção()

            │   │   └── CombatBase.DoAction()

            │   │       ├── Dano:   CrewData.DoDamage()

            │   │       ├── Cura:   CrewData.HealUnits()

            │   │       └── Efeito: NPCsData.AddEffect()

            │   └── WaitUntil(passarTurno)

            │

            ├── Tick de efeitos (ambos os lados)

            ├── VerificarFimDeBatalha()

            │

            └── Turno dos Inimigos:

                ├── EscolheAção() ← ponderado por peso

                ├── SortearAtor() ← elegível que pode agir

                └── ExecutarAção()

### Hierarquia de Combate

CombatBase (MonoBehaviour abstrato)

    └── CrewAttacks (concreto)

            ├── aliados  : CrewData  ← quem ataca

            └── inimigos : CrewData  ← quem recebe

### Efeitos por Turno (NPCsData)

Efeitos são armazenados em `activeEffects : List<ActiveEffect>`.  
`TickEffects()` é chamado a cada fim de turno e:

1. Aplica o efeito do turno (dano, cura)  
2. Decrementa `turnosRestantes`  
3. Remove efeitos expirados (revertendo buff de Força se for o caso)

---

## Sistema de Tripulação

### Classes de NPC

| Classe | Papel |
| :---- | :---- |
| Capitão | Unidade do jogador em terra |
| Barco | Unidade do jogador na água |
| Navegador | Tripulante de combate |
| Canhoneiro | Tripulante de combate |
| Atirador | Tripulante de combate |
| Guerreiro | Tripulante de combate |
| Cozinheiro | Suporte (cura) |
| Médico | Suporte (cura) |

**Condição de derrota:** Capitão/Barco do jogador morrem, ou todos os outros membros morrem.

### Tabela de Resistências por Tipo

Cada tipo de criatura tem multiplicadores para cada tipo de dano, definidos em `NPCsData.damageTable`. Fantasmas são imunes a Físico e Veneno, por exemplo.

---

## Transição Barco ↔ Capitão

Controlada por `WorldGenerator.TryGoOut()` e `PlayerMovement`.

Na água (isOnWater \= true):

    Pressiona Space → TryGoOut()

        └── Busca tile de costa (camada 1\) adjacente ao barco

            └── Encontrou? → capitão.SetActive(true), muda referência do player

Em terra (isOnWater \= false):

    Pressiona Space → TryGoOut()

        └── Capitão próximo do barco (\<= 1.5 células)?

            └── Sim → capitão.SetActive(false), muda referência do player

A referência `WorldGenerator.player` é o alvo que a câmera segue e que os NPCs usam para detecção/perseguição.

---

## Estado Global (GameState)

Classe estática simples que serve como barramento de estado:

| Flag | Tipo | Significado |
| :---- | :---- | :---- |
| `isGameStarted` | bool | Jogo iniciado (passada a tela inicial) |
| `IsInBattle` | bool | Batalha em andamento |
| `IsOnWater` | bool | Jogador está no barco |
| `ChasersCount` | int | Criaturas perseguindo o jogador |
| `IsBeingChased` | bool | Derivado: ChasersCount \> 0 |

Quando `IsInBattle = true` ou `isGameStarted = false`, todo movimento e física de NPCs e jogador são travados.

---

## Áudio

### MusicManager — Máquina de Estados

Estado atual \= f(GameState)

isGameStarted \= false  →  Menu

IsInBattle \= true      →  Batalha

IsBeingChased \= true   →  Perseguição

IsOnWater \= false      →  TerraFirme

default                →  Exploração

Cada estado tem uma playlist embaralhada. Ao trocar de estado, há fade out/in. Entre músicas pode haver um intervalo aleatório (`intervaloMinimo` a `intervaloMaximo` segundos).

### SFXManager

Sons pontuais (vitória, derrota, item consumido, contrato).  
`TocarVitoria()` e `TocarDerrota()` fazem fade out da música antes de tocar.

---

## Dependências Entre Scripts (Referências Críticas)

WorldGenerator

    ├── usa → MapGenerator (instancia e gerencia)

    ├── usa → StructureData (geração de estruturas)

    └── referência → player (Transform dinâmico)

MapGenerator

    ├── usa → RuleManager (IsBlocked)

    ├── usa → TilesetData (lista de tiles)

    ├── usa → Cell (grade interna)

    └── usa → WorldGenerator (Setup de NPCs filhos)

BattleManager

    ├── usa → CrewData (player e inimigo)

    ├── usa → CrewAttacks (executa ações)

    ├── usa → BattleData (EndFight)

    └── usa → CrewUI (HP visual)

NPCsData

    ├── usa → WeaponData / ArmorData (equipamento)

    └── evento → OnMorte (escutado por CrewData)

---

## Convenções do Projeto

- **Português no domínio do jogo:** nomes de variáveis de gameplay, métodos de UI e eventos usam português (ex.: `vidaMáxima`, `EquiparArma`, `OnMorte`).  
- **Inglês na infraestrutura:** nomes de padrões técnicos (ex.: `IsBlocked`, `BuildCompatibilityCache`, `PropagateConsequences`).  
- **ScriptableObjects** para dados imutáveis de design: `Tile`, `TilesetData`, `StructureData`, todos os `ItemData`.  
- **MonoBehaviours** para dados de runtime com estado: `NPCsData`, `CrewData`, `Inventory`.  
- **GameState** como único singleton de estado global — evita acoplamento direto entre sistemas.

