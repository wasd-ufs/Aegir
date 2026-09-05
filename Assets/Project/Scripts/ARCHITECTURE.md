# Arquitetura do Projeto — Aegir

## Visão Geral

**Aegir** é um RPG tático 2D top-down com temática de horror cósmico marítimo e exploração procedural, desenvolvido em Unity (Unity 6 / URP 2D) com C#. O projeto combina mecânicas de navegação marítima e a pé, geração procedural de mundos infinitos via **Wave Function Collapse (WFC)**, combate tático por turnos e gerenciamento de tripulação e inventário.

---

## 1. Estrutura Modular & Assembly Definitions

O projeto é particionado em assemblies independentes para desacoplamento estrito, isolamento de testes e compilações incrementais de alta performance:

```mermaid
graph TD
    subgraph Engine & Packages
        UnityCore["UnityEngine / URP"]
        NewInput["com.unity.inputsystem"]
        UTF["Unity Test Framework / NUnit"]
    end

    subgraph Aegir Assemblies
        EditorAssembly["Aegir.Editor.asmdef<br/>(Editor Tools, WFC Window)"]
        RuntimeAssembly["Aegir.Runtime.asmdef<br/>(Core, World, Entities, Combat, Items, UI)"]
        TestsAssembly["Aegir.Tests.asmdef<br/>(EditMode Unit Tests)"]
    end

    RuntimeAssembly --> UnityCore
    RuntimeAssembly --> NewInput

    EditorAssembly --> RuntimeAssembly
    EditorAssembly --> UnityCore

    TestsAssembly --> RuntimeAssembly
    TestsAssembly --> UTF
    TestsAssembly --> UnityCore

    style RuntimeAssembly fill:#1e3a8a,stroke:#3b82f6,stroke-width:2px,color:#fff
    style EditorAssembly fill:#78350f,stroke:#f59e0b,stroke-width:2px,color:#fff
    style TestsAssembly fill:#064e3b,stroke:#10b981,stroke-width:2px,color:#fff
```

---

## 2. Grafos de Conexão dos Subsistemas (Scripts Atuais)

### 2.1 Subsistema de Geração de Mundo & Algoritmo WFC

O ecossistema procedural é orquestrado pelo `WorldGenerator`, que decompõe o ciclo de vida dos chunks, persistência em disco, ordenação por prioridade de distância e o algoritmo matemático de colapso de função de onda:

```mermaid
graph TD
    subgraph Orquestração e Ciclo de Vida
        WG["WorldGenerator<br/>(Monobehaviour Central)"]
        CLM["ChunkLifecycleManager<br/>(Active/Pending Dictionaries)"]
        CGQ["ChunkGenerationQueue<br/>(Ordenação por Distância)"]
        CNN["ChunkNeighborNotifier<br/>(Notificação de Bordas)"]
        CP["ChunkPersistence<br/>(Serialização .dat em Disco)"]
        PTC["PlayerTransitionController<br/>(Troca Barco / Capitão)"]
        SP["StructurePlacer<br/>(Spawn de Estruturas Pré-definidas)"]
        IMS["IslandMapSampler<br/>(Ruído Matemático / Bioma)"]
        IL["IslandLocator<br/>(Busca e Agrupamento de Ilhas)"]
    end

    subgraph Pipeline WFC por Chunk
        MG["MapGenerator<br/>(Geração de 1 Chunk)"]
        WFC["WFCAlgorithm<br/>(Motor Entropia MRE / Propagação)"]
        CCG["ChunkCellGrid<br/>(Grade de Células + Snapshot Halo)"]
        CC["CompatibilityCache<br/>(Matriz 3D bool[a,b,dir])"]
        RM["RuleManager<br/>(Regras de Adjacência / Bloqueios)"]
    end

    subgraph Dados e Modelos
        TD["TilesetData (ScriptableObject)"]
        T["Tile (ScriptableObject / Sockets)"]
        C["Cell (BitArray de Estados)"]
        SD["StructureData (ScriptableObject)"]
    end

    WG --> CLM
    WG --> CGQ
    WG --> CNN
    WG --> CP
    WG --> PTC
    WG --> SP
    WG --> IMS
    WG --> IL
    WG --> MG

    MG --> WFC
    MG --> CCG
    MG --> CC
    MG --> RM

    CCG --> C
    CC --> RM
    CC --> TD
    RM --> TD
    TD --> T
    WFC --> CCG
    WFC --> CC
    WFC --> TD
    SP --> SD

    style WG fill:#1e40af,stroke:#60a5fa,stroke-width:2px,color:#fff
    style WFC fill:#0284c7,stroke:#38bdf8,stroke-width:2px,color:#fff
    style MG fill:#0369a1,stroke:#7dd3fc,stroke-width:2px,color:#fff
```

---

### 2.2 Subsistema do Jogador & Máquina de Estados

O controle do jogador implementa o padrão State Pattern (`IPlayerState`), alternando dinamicamente a física, rotação e controles entre navegação marítima e locomoção a pé em terra firme:

```mermaid
graph TD
    subgraph Inputs & Câmera
        PIA["PlayerInputActions<br/>(New Input System)"]
        CF["CameraFollow<br/>(Seguimento Suave e Zoom)"]
    end

    subgraph Núcleo do Jogador
        PM["PlayerMovement<br/>(Controlador Central)"]
        IPS["«interface»<br/>IPlayerState"]
        BS["BoatState<br/>(Física Marítima, Inércia e Ondas)"]
        CS["CaptainState<br/>(Locomoção Top-Down em Terra)"]
    end

    subgraph Integração de Mundo
        PTC["PlayerTransitionController"]
        WG["WorldGenerator"]
        GS["GameState"]
    end

    PIA --> PM
    PM --> IPS
    IPS <|.. BS
    IPS <|.. CS
    PM --> BS
    PM --> CS
    PM --> CF
    PM <--> PTC
    PTC --> WG
    PM --> GS

    style PM fill:#047857,stroke:#34d399,stroke-width:2px,color:#fff
    style IPS fill:#065f46,stroke:#6ee7b7,stroke-width:2px,color:#fff
    style BS fill:#0f766e,stroke:#2dd4bf,stroke-width:2px,color:#fff
    style CS fill:#0f766e,stroke:#2dd4bf,stroke-width:2px,color:#fff
```

---

### 2.3 Subsistema de Combate & Batalha por Turnos

O combate opera como um fluxo tático isolado via Singleton (`BattleManager`), executando turnos do jogador e de IA inimiga com cálculo de forças, tabelas de fraqueza elemental e reuso eficiente de botões na UI:

```mermaid
graph TD
    subgraph Gatilho e Transição
        SF["StartFight<br/>(Gatilho por Colisão)"]
        GBT["GameBoyTransition<br/>(Animação Estilo Game Boy)"]
        BD["BattleData<br/>(Setup Visual de Cenário)"]
    end

    subgraph Orquestração do Combate
        BM["BattleManager<br/>(Singleton / Turnos & UI)"]
        CB["CombatBase<br/>(Lógica Abstrata de Efeitos)"]
        CA["CrewAttacks<br/>(Implementação Concreta de Ações)"]
    end

    subgraph Entidades e Dados
        CD_Ally["CrewData (Aliados)"]
        CD_Enemy["CrewData (Inimigos)"]
        NPC["NPCsData<br/>(Vida, Matriz Elemental, Buffs, Ações)"]
    end

    subgraph Interface de Combate
        CUI["CrewUI<br/>(Escuta Reativa OnHealthChanged)"]
    end

    SF --> GBT
    GBT --> BD
    BD --> BM

    BM --> CA
    BM --> CD_Ally
    BM --> CD_Enemy
    BM --> CUI

    CA -- herda --> CB
    CB --> CD_Ally
    CB --> CD_Enemy
    CB --> NPC

    CD_Ally --> NPC
    CD_Enemy --> NPC
    NPC -- eventos --> CUI

    style BM fill:#991b1b,stroke:#f87171,stroke-width:2px,color:#fff
    style CA fill:#b91c1c,stroke:#fca5a5,stroke-width:2px,color:#fff
    style NPC fill:#831843,stroke:#f472b6,stroke-width:2px,color:#fff
    style CUI fill:#4c1d95,stroke:#a78bfa,stroke-width:2px,color:#fff
```

---

### 2.4 Subsistema de Entidades, IA e Tripulação

Gerenciamento individual e coletivo dos tripulantes e criaturas do mapa:

```mermaid
graph TD
    subgraph IA e Mundo
        NM["NPCsMovement<br/>(Patrulha & Perseguição)"]
        RNPC["RecruitableNPC<br/>(Interação de Recrutamento)"]
        NR["NPC_Randomizer<br/>(Variação de Atributos no Spawn)"]
    end

    subgraph Tripulação e Atributos
        CD["CrewData<br/>(Coletivo da Tripulação & Permadeath)"]
        NPC["NPCsData<br/>(Vida, XP, Nível, Tabela de Resistências)"]
        INV["Inventory<br/>(Inventário Compartilhado do Grupo)"]
    end

    subgraph Eventos Reativos
        E1["OnHealthChanged(npc, cur, max)"]
        E2["OnDeath(npc)"]
        E3["OnCrewChanged()"]
    end

    NM --> NPC
    NM --> GS["GameState.ChasersCount"]
    RNPC --> CD
    NR --> NPC

    CD --> NPC
    CD --> INV

    NPC --> E1
    NPC --> E2
    CD --> E3

    style CD fill:#1e3a8a,stroke:#60a5fa,stroke-width:2px,color:#fff
    style NPC fill:#831843,stroke:#f472b6,stroke-width:2px,color:#fff
    style INV fill:#713f12,stroke:#facc15,stroke-width:2px,color:#fff
```

---

### 2.5 Subsistema de Itens & Inventário

Arquitetura ScriptableObject extensível com empilhamento dinâmico e controle de slots:

```mermaid
graph TD
    subgraph Armazenamento & UI
        INV["Inventory<br/>(Gestão de Slots & Pilhas)"]
        IUI["InventoryUI<br/>(Tela do Inventário)"]
    end

    subgraph Definição de Dados
        BID["BaseItemData<br/>(ScriptableObject Base)"]
        WD["WeaponData<br/>(Ataque Base, Raridade)"]
        AD["ArmorData<br/>(Defesa Flat, Resistência)"]
        CD["ConsumableData<br/>(Cura, Buff de Força, Duração)"]
        TD["ThrowableData<br/>(Dano Arremessável)"]
        MD["MaterialData<br/>(Materiais de Craft/Reparo)"]
    end

    subgraph Utilização
        NPC["NPCsData<br/>(EquipWeapon, EquipArmor, ApplyConsumable)"]
    end

    INV --> BID
    IUI --> INV
    BID <|-- WD
    BID <|-- AD
    BID <|-- CD
    BID <|-- TD
    BID <|-- MD

    NPC --> WD
    NPC --> AD
    NPC --> CD

    style INV fill:#713f12,stroke:#facc15,stroke-width:2px,color:#fff
    style BID fill:#854d0e,stroke:#fde047,stroke-width:2px,color:#fff
```

---

### 2.6 Subsistema de Áudio & Estado Global

```mermaid
graph TD
    GS["GameState<br/>(Barramento Estático de Flags)"]
    MM["MusicManager<br/>(Transições de Trilha por Estado)"]
    SFX["SFXManager<br/>(Efeitos Pontuais de Vitória/Derrota)"]

    GS --> MM
    BM["BattleManager"] --> GS
    BM --> SFX
    NM["NPCsMovement"] --> GS
    PM["PlayerMovement"] --> GS

    style GS fill:#374151,stroke:#9ca3af,stroke-width:2px,color:#fff
    style MM fill:#14532d,stroke:#4ade80,stroke-width:2px,color:#fff
    style SFX fill:#14532d,stroke:#4ade80,stroke-width:2px,color:#fff
```

---

### 2.7 Estrutura da Suíte de Testes Automatizados

```mermaid
graph TD
    subgraph Aegir.Tests.asmdef
        T_World["World Tests<br/>• CellTests<br/>• WFCAlgorithmTests<br/>• TileCompatibilityTests<br/>• IslandAndWorldUtilitiesTests"]
        T_Items["Items Tests<br/>• InventoryTests<br/>• ItemDataTests"]
        T_Entities["Entities Tests<br/>• NPCsDataTests<br/>• CrewDataTests<br/>• PlayerMovementStateTests"]
        T_Combat["Combat Tests<br/>• CombatBaseTests<br/>• BattleLogicTests"]
        T_Core["Core Tests<br/>• GameStateTests"]
    end

    subgraph Aegir.Runtime.asmdef
        R_World["World & WFC Engine"]
        R_Items["Inventory & ScriptableObjects"]
        R_Entities["NPCs, Crew, Player State"]
        R_Combat["BattleManager & CrewAttacks"]
        R_Core["GameState Flags"]
    end

    T_World --> R_World
    T_Items --> R_Items
    T_Entities --> R_Entities
    T_Combat --> R_Combat
    T_Core --> R_Core

    style T_World fill:#064e3b,stroke:#34d399,stroke-width:1px,color:#fff
    style T_Items fill:#064e3b,stroke:#34d399,stroke-width:1px,color:#fff
    style T_Entities fill:#064e3b,stroke:#34d399,stroke-width:1px,color:#fff
    style T_Combat fill:#064e3b,stroke:#34d399,stroke-width:1px,color:#fff
    style T_Core fill:#064e3b,stroke:#34d399,stroke-width:1px,color:#fff
```

---

## 3. Matriz de Aderência: Código Implementado vs. GDD (Game Design Document)

Comparação detalhada entre as diretrizes especificadas no documento [GDD_AEGIR_WASD.docx.pdf](file:///c:/Users/bretu/OneDrive/Documentos/GitHub/Aegir/GDD_AEGIR_WASD.docx.pdf) e a base de código C# existente:

| Seção do GDD | Funcionalidade / Mecânica | Status Atual no Código | Detalhamento Técnico |
| :--- | :--- | :---: | :--- |
| **2.3.1** | **Andar (Capitão a pé)** | **Implementado** | [PlayerMovement.cs](file:///c:/Users/bretu/OneDrive/Documentos/GitHub/Aegir/Assets/Project/Scripts/Entities/Controllers/PlayerMovement.cs) e [CaptainState.cs](file:///c:/Users/bretu/OneDrive/Documentos/GitHub/Aegir/Assets/Project/Scripts/Entities/Controllers/CaptainState.cs). Movimentação top-down via New Input System (`W/A/S/D` e Gamepad). |
| **2.3.2** | **Embarcar / Desembarcar** | **Implementado** | [PlayerTransitionController.cs](file:///c:/Users/bretu/OneDrive/Documentos/GitHub/Aegir/Assets/Project/Scripts/World/Generators/Utilities/WorldGenerator/PlayerTransitionController.cs). Validação de proximidade ao barco, checagem de tile de costa (layer 1), ocultação de sprite e transição de zoom na câmera. |
| **2.4.1** | **Navegação do Navio** | **Implementado** | [BoatState.cs](file:///c:/Users/bretu/OneDrive/Documentos/GitHub/Aegir/Assets/Project/Scripts/Entities/Controllers/BoatState.cs). Rotação direcional, aceleração, inércia na água e perturbação senoidal matemática de ondas marítimas. |
| **2.5.3** | **Geração de Mares & Chunks WFC** | **Implementado** | [WorldGenerator.cs](file:///c:/Users/bretu/OneDrive/Documentos/GitHub/Aegir/Assets/Project/Scripts/World/Generators/WorldGenerator.cs), [MapGenerator.cs](file:///c:/Users/bretu/OneDrive/Documentos/GitHub/Aegir/Assets/Project/Scripts/World/Generators/MapGenerator.cs) e [WFCAlgorithm.cs](file:///c:/Users/bretu/OneDrive/Documentos/GitHub/Aegir/Assets/Project/Scripts/World/Generators/Utilities/MapGenerator/WFCAlgorithm.cs). Chunks infinitos em espiral, halo de compatibilidade, eliminação de contradições e persistência em disco (.dat). |
| **2.5.3** | **Amostragem e Busca de Ilhas** | **Implementado** | [IslandMapSampler.cs](file:///c:/Users/bretu/OneDrive/Documentos/GitHub/Aegir/Assets/Project/Scripts/World/Generators/Utilities/WorldGenerator/IslandMapSampler.cs) (ruído determinístico) e [IslandLocator.cs](file:///c:/Users/bretu/OneDrive/Documentos/GitHub/Aegir/Assets/Project/Scripts/World/Utilities/IslandLocator.cs) (busca em anel de raio e agrupamento flood-fill). |
| **2.5.3** | **5 Camadas de Profundidade & Sanidade** | **Parcialmente Implementado** | As camadas de tiles existem em [LayerDefinition.cs](file:///c:/Users/bretu/OneDrive/Documentos/GitHub/Aegir/Assets/Project/Scripts/World/Data/LayerDefinition.cs). Porém, as **5 zonas temáticas** (Litorânea, Mar Cinzento, Abismo Sussurrante, Fossa Esquecida, Coração Adormecido) e a mecânica ativa de **Taxa de Erosão de Sanidade** (0.5 a 18 pt/h, alucinações, motim) ainda não foram codificadas. |
| **2.5.2** | **Sistema de Combate por Turnos** | **Implementado** | [BattleManager.cs](file:///c:/Users/bretu/OneDrive/Documentos/GitHub/Aegir/Assets/Project/Scripts/Combat/BattleManager.cs), [CrewAttacks.cs](file:///c:/Users/bretu/OneDrive/Documentos/GitHub/Aegir/Assets/Project/Scripts/Combat/CrewAttacks.cs) e [BattleData.cs](file:///c:/Users/bretu/OneDrive/Documentos/GitHub/Aegir/Assets/Project/Scripts/Combat/BattleData.cs). Loop de turnos, UI com pooling de botões, seleção por alvo/peso e transição visual Game Boy. |
| **2.5.2** | **Tabela de Dano Elemental & Resistências** | **Implementado** | [NPCsData.cs](file:///c:/Users/bretu/OneDrive/Documentos/GitHub/Aegir/Assets/Project/Scripts/Entities/NPCsData.cs#L225). Fraquezas/imunidades (Fantasma imune a Físico/Veneno e fraco a Sagrado; Esqueleto imune a Gelo), mitigação de armaduras e buffs temporais (`activeEffects`). |
| **2.3.4 / 2.5.5** | **Sistema de Inventário & Itens** | **Parcialmente Implementado** | [Inventory.cs](file:///c:/Users/bretu/OneDrive/Documentos/GitHub/Aegir/Assets/Project/Scripts/Items/Inventory.cs) e [InventoryUI.cs](file:///c:/Users/bretu/OneDrive/Documentos/GitHub/Aegir/Assets/Project/UI/InventoryUI.cs). Armazena slots, empilha itens e categoriza armas/armaduras/consumíveis. Contudo, o **limite por peso em quilos** (50kg player, 150kg navio, debuffs de sobrecarga) e o **baú universal das docas** do GDD ainda utilizam o modelo simplificado de contagem de slots (`_maxItemsPerInventory`). |
| **2.3.5 / 2.4.2** | **Minigame de Pesca (Player e Navio)** | **Planejado / Pendente** | O GDD projeta pesca a pé estilo *Stardew Valley* (trilho vertical) e pesca de içamento com guincho no navio estilo *Dredge* (minigame circular de acerto). Nenhum script de pesca foi implementado até o momento. |
| **2.4.3** | **Reparo do Barco (Mar e Porto)** | **Planejado / Pendente** | Mecânica de gastar materiais de madeira no mar para cura parcial ou gastar moedas em portos para reparo completo. Não implementada. |
| **2.5.10** | **Sistema de Destruição do Barco** | **Parcialmente Implementado** | A morte do Barco resulta em derrota imediata em [BattleManager.cs](file:///c:/Users/bretu/OneDrive/Documentos/GitHub/Aegir/Assets/Project/Scripts/Combat/BattleManager.cs). No entanto, as **fases de dano estéticas** (<25 HP: sprite trincado, -25% velocidade; <10 HP: fumaça, trepidação de câmera, -50% velocidade) e o **drop percentual de itens com respawn no porto** ainda não foram criados. |
| **2.5.11** | **Sistema de Upgrades do Navio** | **Planejado / Pendente** | Tiers de evolução (Casco: 100 a 450 HP; Armazenamento: 150 a 500 kg; Velas: +5% a +15% velocidade; Canhões: 2 a 8 canhões laterais). Não implementado. |
| **2.5.12** | **Mural de Missões (Quests)** | **Planejado / Pendente** | Missões de Coleta, Caça, Entrega e Chefes com bússola direcional nas vilas. Não implementado. |
| **2.5.13** | **Sistema de Bestiário** | **Planejado / Pendente** | Enciclopédia desbloqueada ao encontrar o item Bestiário com contadores progressivos de abates. Não implementado. |
| **4.3.2** | **Menu de Operações / Menu de Pausa** | **Parcialmente Implementado** | O GDD projeta uma barra horizontal superior direita com 6 abas navegáveis (*Inventário, Equipamentos, Status, Bestiário, Navio, Sistema*). Atualmente há apenas menus isolados de tela cheia. |
| **5.1 - 5.3** | **Sistema de Áudio Dinâmico** | **Implementado** | [MusicManager.cs](file:///c:/Users/bretu/OneDrive/Documentos/GitHub/Aegir/Assets/Project/Scripts/Core/MusicManager.cs) e [SFXManager.cs](file:///c:/Users/bretu/OneDrive/Documentos/GitHub/Aegir/Assets/Project/Scripts/Core/SFXManager.cs). Máquina de estados musical orientada ao `GameState` (Menu, Batalha, Perseguição, TerraFirme, Exploração) com fades suaves. |

---

## 4. Próximos Passos Sugeridos de Roadmap (Pós-Sprint)

Com a arquitetura reestruturada e coberta por testes automatizados, as próximas sprints podem focar na implementação dos sistemas-chave descritos no GDD:

1. **Sprint de Sanidade & Camadas Oceânicas:**
   - Implementar o componente `SanityManager` com degradação por camada de profundidade.
   - Definir regras de pós-processamento e névoa/iluminação volumétrica no URP para as 5 camadas do oceano.
2. **Sprint de Estado Crítico & Reparos do Navio:**
   - Adicionar os estados de dano físico do barco (<25 HP e <10 HP) com trocas de sprites e penalidades de velocidade em [BoatState.cs](file:///c:/Users/bretu/OneDrive/Documentos/GitHub/Aegir/Assets/Project/Scripts/Entities/Controllers/BoatState.cs).
   - Implementar a lógica de reparo em alto mar (via inventário) e reparo comercial nas docas.
3. **Sprint do Menu de Pausa Unificado:**
   - Construir a UI horizontal de 6 abas conforme as especificações das páginas 36 e 37 do GDD (*Inventário, Equipamentos, Status do Capitão/Barco, Bestiário, Navio, Sistema*).
4. **Sprint do Minigame de Pesca:**
   - Criar o controlador de pesca com vara (trilho vertical) e pesca pesada embarcada (guincho circular) com gatilho de combate surpresa (45%).
