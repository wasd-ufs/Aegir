using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

/// <summary>
/// Responsável por gerar, renderizar e gerenciar um chunk do mapa via WFC
/// (Wave Function Collapse).
/// <para>
/// Cada chunk é uma grade de <see cref="Cell"/>s com dimensões
/// <c>chunkSize + 2</c> em cada eixo — a linha/coluna extra ao redor forma o
/// <b>halo</b>, que contém os tiles já definidos pelos chunks vizinhos e serve
/// de restrição inicial para o WFC.
/// </para>
/// <para>
/// Fluxo de geração:
/// <list type="number">
///   <item><see cref="InitCells"/> — inicializa a grade e colapsa o halo.</item>
///   <item><see cref="RunCollapseSync"/> ou <see cref="RunCollapseAsync"/> — executa o WFC.</item>
///   <item><see cref="RenderMap"/> — pinta o tilemap com os tiles escolhidos.</item>
///   <item><see cref="SpawnEntities"/> — instancia criaturas sobre as células colapsadas.</item>
/// </list>
/// </para>
/// </summary>
public class MapGenerator : MonoBehaviour
{
    // =========================================================================
    // Campos e Propriedades
    // =========================================================================

    [Header("Referências")]
    public UnityEngine.Tilemaps.Tilemap tilemap;
    public TilesetData tilesetData;
    public RuleManager ruleManager;

    [Header("Configurações do Chunk")]
    public Vector2Int chunkSize;

    [Header("Spawn Settings")]
    [Tooltip("Número máximo de criaturas que podem nascer neste chunk.")]
    public int maxCreaturesPerChunk = 10;

    [Tooltip("Quantos colapsos são feitos por frame durante a geração assíncrona.")]
    public int collapsesPerFrame = 10;

    // Dimensões reais da grade interna (inclui 1 célula de halo em cada borda)
    private int GridW => chunkSize.x + 2;
    private int GridH => chunkSize.y + 2;
    private int TileCount => tilesetData.tileset.Count;

    /// <summary>
    /// Cache de compatibilidade: <c>compatible[a, b, dir]</c> é <c>true</c> quando
    /// o tile <c>a</c> pode ter o tile <c>b</c> na direção <c>dir</c>.
    /// Evita chamar o <see cref="RuleManager"/> repetidamente durante a propagação.
    /// Índices de direção: 0=cima, 1=baixo, 2=esquerda, 3=direita.
    /// </summary>
    private bool[,,] compatible;

    private readonly Vector2Int[] directions = { Vector2Int.up, Vector2Int.down, Vector2Int.left, Vector2Int.right };

    /// <summary>Grade de células do chunk (coordenadas incluem o halo).</summary>
    private Cell[,] cells;

    /// <summary>
    /// Snapshot do estado das células logo após o halo ser aplicado.
    /// Permite reiniciar o WFC sem recalcular o halo desde o <see cref="WorldGenerator"/>.
    /// </summary>
    private BitArray[,] haloSnapshot;

    // Estado da geração assíncrona
    public bool IsGenerating { get; private set; } = false;
    public bool GenerationSucceeded { get; private set; } = false;

    /// <summary>Callback invocado ao término da geração assíncrona (sucesso ou falha).</summary>
    public System.Action<MapGenerator, bool> OnGenerationComplete;

    public WorldGenerator worldGenerator;
    public GameObject player;

    // Parâmetros de ruído armazenados para o cálculo por célula
    private Vector2Int currentChunkCoord;
    private float currentNoiseScale;

    // =========================================================================
    // Unity Callbacks
    // =========================================================================

    void Awake()
    {
        ruleManager = FindFirstObjectByType<RuleManager>();
    }

    // =========================================================================
    // API Pública
    // =========================================================================

    /// <summary>
    /// Associa o jogador e o <see cref="WorldGenerator"/> a este chunk e a todos
    /// os NPCs filhos.
    /// </summary>
    public void Setup(GameObject player, WorldGenerator worldGenerator)
    {
        this.player = player;
        this.worldGenerator = worldGenerator;

        foreach (var npc in GetComponentsInChildren<NPCsMovement>())
            npc.Setup(player, worldGenerator);
    }

    /// <summary>
    /// Geração síncrona — bloqueia o jogo até o chunk ser gerado.
    /// Usada nos chunks do campo de visão inicial para garantir que o mapa
    /// esteja pronto antes do primeiro frame visível ao jogador.
    /// </summary>
    /// <param name="borderTiles">Tiles dos chunks vizinhos que formam o halo.</param>
    /// <param name="chunkCoord">Posição deste chunk na grade de chunks.</param>
    /// <param name="noiseScale">Escala do Perlin Noise vinda do WorldGenerator.</param>
    /// <returns><c>true</c> se o chunk foi gerado sem contradições.</returns>
    public bool GenerateChunk(Dictionary<Vector2Int, Tile> borderTiles, Vector2Int chunkCoord, float noiseScale)
    {
        this.currentChunkCoord = chunkCoord;
        this.currentNoiseScale = noiseScale;
        int maxRestarts = 10;

        for (int attempt = 0; attempt < maxRestarts; attempt++)
        {
            InitCells(borderTiles);

            if (RunCollapseSync())
            {
                GenerationSucceeded = true;
                RenderMap();
                SpawnEntities();
                return true;
            }
        }

        GenerationSucceeded = false;
        return false;
    }

    /// <summary>
    /// Geração assíncrona — distribui o trabalho em frames via coroutine.
    /// Usada para chunks gerados enquanto o jogo está rodando, sem travar o loop principal.
    /// Ao concluir, invoca <see cref="OnGenerationComplete"/>.
    /// </summary>
    /// <param name="borderTiles">Tiles dos chunks vizinhos que formam o halo.</param>
    /// <param name="chunkCoord">Posição deste chunk na grade de chunks.</param>
    /// <param name="noiseScale">Escala do Perlin Noise para pesos de tiles de terra.</param>
    public void GenerateChunkAsync(Dictionary<Vector2Int, Tile> borderTiles, Vector2Int chunkCoord, float noiseScale)
    {
        this.currentChunkCoord = chunkCoord;
        this.currentNoiseScale = noiseScale;
        StartCoroutine(GenerateChunkCoroutine(borderTiles));
    }

    /// <summary>
    /// Atualiza o halo com tiles recém-gerados de um chunk vizinho e repropaga
    /// as restrições para as células internas do chunk.
    /// Chamado pelo <see cref="WorldGenerator"/> via <c>NotifyNeighbors</c>.
    /// </summary>
    /// <param name="newHaloTiles">Dicionário de coordenadas de halo → tile definido.</param>
    public void UpdateHaloAndRepropagate(Dictionary<Vector2Int, Tile> newHaloTiles)
    {
        if (cells == null) return;
        EnsureCompatibilityCache();

        foreach (var kv in newHaloTiles)
        {
            if (!IsInsideBounds(kv.Key)) continue;

            Cell haloCell = cells[kv.Key.x, kv.Key.y];
            if (haloCell.isCollapsed()) continue;

            int tileIndex = tilesetData.tileset.IndexOf(kv.Value);
            if (tileIndex < 0) continue;

            haloCell.CollapseCell(tileIndex);
            PropagateConsequences(haloCell);
        }

        RenderMap();
    }

    /// <summary>
    /// Serializa o estado das células internas para um array de bytes.
    /// Cada byte é o índice do tile colapsado na posição correspondente.
    /// Usado pelo <see cref="WorldGenerator"/> para salvar o chunk em disco.
    /// </summary>
    public byte[] GetChunkData()
    {
        if (cells == null) return null;

        byte[] data = new byte[chunkSize.x * chunkSize.y];
        for (int x = 0; x < chunkSize.x; x++)
            for (int y = 0; y < chunkSize.y; y++)
            {
                // +1 em x e y para pular as bordas do halo
                Cell cell = cells[x + 1, y + 1];
                int tileIndex = cell.CollapsedIndex();
                data[x * chunkSize.y + y] = tileIndex >= 0 ? (byte)tileIndex : (byte)0;
            }

        return data;
    }

    /// <summary>
    /// Reconstrói o estado das células a partir de dados salvos em disco.
    /// Espelho de <see cref="GetChunkData"/>.
    /// </summary>
    public void LoadFromData(byte[] data)
    {
        cells = new Cell[GridW, GridH];
        for (int x = 0; x < GridW; x++)
            for (int y = 0; y < GridH; y++)
                cells[x, y] = new Cell(TileCount, new Vector2Int(x, y));

        for (int x = 0; x < chunkSize.x; x++)
            for (int y = 0; y < chunkSize.y; y++)
                cells[x + 1, y + 1].CollapseCell(data[x * chunkSize.y + y]);

        RenderMap();
    }

    /// <summary>
    /// Força todo o chunk a ser preenchido com tile de água (camada 0).
    /// Usado para o chunk da origem (posição zero) que representa o oceano inicial.
    /// </summary>
    public void ForceWaterChunk(Dictionary<Vector2Int, Tile> borderTiles)
    {
        InitCells(borderTiles);

        int waterIdx = tilesetData.tileset.FindIndex(t => t.metadata.camada == 0);
        if (waterIdx == -1) return;

        for (int x = 0; x < GridW; x++)
            for (int y = 0; y < GridH; y++)
                cells[x, y].CollapseCell(waterIdx);

        RenderMap();
    }

    /// <summary>
    /// Retorna o <see cref="Tile"/> colapsado na posição local (<paramref name="x"/>, <paramref name="y"/>),
    /// excluindo o halo (coordenadas de 0 a chunkSize - 1).
    /// </summary>
    public Tile GetTileAt(int x, int y)
    {
        if (cells == null) return null;

        Cell c = cells[x + 1, y + 1]; // +1 para compensar o halo
        if (c.isEmpty()) return null;

        int tileIndex = c.CollapsedIndex();
        return tileIndex >= 0 ? tilesetData.tileset[tileIndex] : null;
    }

    // =========================================================================
    // Geração — Coroutine e Loop Principal
    // =========================================================================

    /// <summary>
    /// Coroutine da geração assíncrona. Repete até atingir o máximo de tentativas
    /// ou completar sem contradições. Ao final, invoca <see cref="OnGenerationComplete"/>.
    /// </summary>
    private IEnumerator GenerateChunkCoroutine(Dictionary<Vector2Int, Tile> borderTiles)
    {
        IsGenerating = true;
        GenerationSucceeded = false;

        int maxRestarts = 10;
        for (int attempt = 0; attempt < maxRestarts; attempt++)
        {
            InitCells(borderTiles);

            bool success = false;
            yield return RunCollapseAsync(result => success = result);

            if (success)
            {
                GenerationSucceeded = true;
                RenderMap();
                break;
            }
        }

        IsGenerating = false;
        OnGenerationComplete?.Invoke(this, GenerationSucceeded);
    }

    /// <summary>
    /// Executa o loop WFC de forma síncrona: escolhe célula → colapsa → propaga,
    /// reiniciando pelo snapshot do halo em caso de contradição.
    /// </summary>
    /// <returns><c>true</c> se todos os tiles foram colapsados sem contradição.</returns>
    private bool RunCollapseSync()
    {
        int totalCells  = chunkSize.x * chunkSize.y;
        int colapsadas  = 0;
        int maxAttempts = totalCells * 3;
        int attempts    = 0;

        while (colapsadas < totalCells && attempts < maxAttempts)
        {
            Cell chosen = ChooseCell();
            if (chosen == null) break;

            float cellNoise = CalculateCellNoise(chosen.coordinates);
            CollapseAndPropagate(chosen, cellNoise);

            if (HasContradiction())
            {
                RestartFromHalo();
                colapsadas = 0;
                attempts++;
                continue;
            }

            colapsadas++;
        }

        return !HasContradiction();
    }

    /// <summary>
    /// Versão assíncrona de <see cref="RunCollapseSync"/>: cede ao loop principal
    /// a cada <see cref="collapsesPerFrame"/> colapsos para não travar o jogo.
    /// </summary>
    private IEnumerator RunCollapseAsync(System.Action<bool> onDone)
    {
        int totalCells         = chunkSize.x * chunkSize.y;
        int colapsadas         = 0;
        int maxAttempts        = totalCells * 3;
        int attempts           = 0;
        int collapsesThisFrame = 0;

        while (colapsadas < totalCells && attempts < maxAttempts)
        {
            Cell chosen = ChooseCell();
            if (chosen == null) break;

            float cellNoise = CalculateCellNoise(chosen.coordinates);
            CollapseAndPropagate(chosen, cellNoise);

            if (HasContradiction())
            {
                RestartFromHalo();
                colapsadas = 0;
                attempts++;
            }
            else
            {
                colapsadas++;
                RenderMap();
            }

            collapsesThisFrame++;
            if (collapsesThisFrame >= collapsesPerFrame)
            {
                collapsesThisFrame = 0;
                yield return null; // Cede o controle por um frame
            }
        }

        onDone(!HasContradiction());
    }

    // =========================================================================
    // Inicialização de Células
    // =========================================================================

    /// <summary>
    /// Inicializa a grade de células e aplica o halo.
    /// <para>
    /// O processo ocorre em duas passagens sobre <paramref name="borderTiles"/>:
    /// <list type="number">
    ///   <item>Primeira: colapsa todas as células de borda.</item>
    ///   <item>Segunda: propaga as consequências. A separação evita contradições
    ///         causadas por propagações prematuras durante a inicialização.</item>
    /// </list>
    /// Ao final, salva um snapshot do estado pós-halo para reinícios rápidos.
    /// </para>
    /// </summary>
    private void InitCells(Dictionary<Vector2Int, Tile> borderTiles)
    {
        EnsureCompatibilityCache();

        // Cria todas as células com todos os tiles possíveis
        cells = new Cell[GridW, GridH];
        for (int x = 0; x < GridW; x++)
            for (int y = 0; y < GridH; y++)
                cells[x, y] = new Cell(TileCount, new Vector2Int(x, y));

        if (borderTiles != null)
        {
            // Passagem 1: colapsa as células do halo
            foreach (var kv in borderTiles)
            {
                if (!IsInsideBounds(kv.Key)) continue;
                int tileIndex = tilesetData.tileset.IndexOf(kv.Value);
                if (tileIndex >= 0) cells[kv.Key.x, kv.Key.y].CollapseCell(tileIndex);
            }

            // Passagem 2: propaga as restrições para as células internas
            foreach (var kv in borderTiles)
            {
                if (!IsInsideBounds(kv.Key)) continue;
                PropagateConsequences(cells[kv.Key.x, kv.Key.y]);
            }
        }

        // Salva o estado pós-halo para poder reiniciar sem recalcular do WorldGenerator
        haloSnapshot = new BitArray[GridW, GridH];
        for (int x = 0; x < GridW; x++)
            for (int y = 0; y < GridH; y++)
                haloSnapshot[x, y] = new BitArray(cells[x, y].possible);
    }

    // =========================================================================
    // WFC — Escolha, Colapso e Propagação
    // =========================================================================

    /// <summary>
    /// Seleciona a célula com menor entropia (menor número de tiles possíveis)
    /// ainda não colapsada, aplicando desempate aleatório.
    /// Implementa o critério MRE (Minimum Remaining Values) do WFC.
    /// </summary>
    /// <returns>A célula escolhida, ou <c>null</c> se todas estiverem colapsadas.</returns>
    private Cell ChooseCell()
    {
        int min = int.MaxValue;
        List<Cell> candidates = new List<Cell>();

        // Itera somente sobre as células internas (ignora o halo)
        for (int x = 1; x <= chunkSize.x; x++)
        {
            for (int y = 1; y <= chunkSize.y; y++)
            {
                Cell c = cells[x, y];
                if (c.isCollapsed()) continue;

                int count = c.CountPossible();
                if (count == 0) continue;

                if (count < min) { min = count; candidates.Clear(); candidates.Add(c); }
                else if (count == min) candidates.Add(c);
            }
        }

        return candidates.Count > 0 ? candidates[Random.Range(0, candidates.Count)] : null;
    }

    /// <summary>
    /// Escolhe um tile aleatório ponderado pelos pesos (<see cref="Tile.peso"/>)
    /// e colapsa a célula para ele, propagando as consequências.
    /// <para>
    /// O Perlin Noise (<paramref name="noise"/>) amplifica o peso dos tiles de
    /// terra (camada par e diferente de 0), favorecendo a formação de ilhas.
    /// </para>
    /// </summary>
    private void CollapseAndPropagate(Cell cell, float noise)
    {
        // Calcula o peso total iterando sobre bits — sem alocação de lista
        float pesoTotal = 0;
        for (int i = 0; i < TileCount; i++)
        {
            if (!cell.possible[i]) continue;
            Tile tile = tilesetData.tileset[i];
            pesoTotal += IsTerraTile(tile) ? tile.peso * (noise * 10) : tile.peso;
        }

        float roll   = Random.Range(0, pesoTotal);
        int   chosen = -1;

        for (int i = 0; i < TileCount; i++)
        {
            if (!cell.possible[i]) continue;
            Tile tile = tilesetData.tileset[i];
            roll -= IsTerraTile(tile) ? tile.peso * (noise * 10) : tile.peso;
            if (roll <= 0) { chosen = i; break; }
        }

        // Fallback: garante que algum tile seja escolhido
        if (chosen < 0)
            for (int i = TileCount - 1; i >= 0; i--)
                if (cell.possible[i]) { chosen = i; break; }

        cell.CollapseCell(chosen);
        PropagateConsequences(cell);
    }

    /// <summary>
    /// Propaga as restrições a partir de uma célula modificada usando BFS.
    /// Para cada vizinho ainda não colapsado, remove tiles que não têm
    /// suporte em nenhum tile da célula atual, e os enfileira se mudaram.
    /// </summary>
    private void PropagateConsequences(Cell start)
    {
        Queue<Cell> queue = new Queue<Cell>();
        queue.Enqueue(start);

        while (queue.Count > 0)
        {
            Cell current = queue.Dequeue();

            for (int d = 0; d < 4; d++)
            {
                Vector2Int neighborPos = current.coordinates + directions[d];
                if (!IsInsideBounds(neighborPos)) continue;

                Cell neighbor = cells[neighborPos.x, neighborPos.y];
                if (neighbor.isCollapsed()) continue;

                bool changed = false;

                // Para cada tile candidato do vizinho, verifica se ainda tem suporte
                for (int ni = 0; ni < TileCount; ni++)
                {
                    if (!neighbor.possible[ni]) continue;

                    bool hasSupport = false;
                    for (int ci = 0; ci < TileCount; ci++)
                    {
                        if (!current.possible[ci]) continue;
                        if (compatible[ci, ni, d]) { hasSupport = true; break; }
                    }

                    if (!hasSupport)
                    {
                        neighbor.possible[ni] = false;
                        changed = true;
                    }
                }

                if (changed) queue.Enqueue(neighbor);
            }
        }
    }

    // =========================================================================
    // Renderização e Spawn
    // =========================================================================

    /// <summary>
    /// Pinta o tilemap com os tiles das células colapsadas.
    /// Percorre apenas as células internas (sem o halo).
    /// </summary>
    private void RenderMap()
    {
        for (int x = 1; x <= chunkSize.x; x++)
            for (int y = 1; y <= chunkSize.y; y++)
                if (cells[x, y].isCollapsed())
                {
                    int tileIndex = cells[x, y].CollapsedIndex();
                    tilemap.SetTile(new Vector3Int(x - 1, y - 1, 0), tilesetData.tileset[tileIndex].tilemapTile);
                }
    }

    /// <summary>
    /// Instancia criaturas sobre as células colapsadas do chunk,
    /// respeitando o limite de <see cref="maxCreaturesPerChunk"/>.
    /// </summary>
    public void SpawnEntities()
    {
        int totalSpawned = 0;

        for (int x = 1; x <= chunkSize.x; x++)
        {
            for (int y = 1; y <= chunkSize.y; y++)
            {
                if (totalSpawned >= maxCreaturesPerChunk) return;
                if (cells[x, y].isCollapsed())
                    InstantiateCreature(cells[x, y], x, y, ref totalSpawned);
            }
        }
    }

    /// <summary>
    /// Tenta instanciar criaturas definidas em <see cref="Tile.spawnableCreatures"/>
    /// para a célula informada. Cada criatura tem sua própria chance e quantidade.
    /// </summary>
    public void InstantiateCreature(Cell cell, int x, int y, ref int spawnCount)
    {
        int tileIndex     = cell.CollapsedIndex();
        Vector3 basePos   = tilemap.GetCellCenterWorld(new Vector3Int(x - 1, y - 1, 0));
        Tile tile         = tilesetData.tileset[tileIndex];

        foreach (var entry in tile.spawnableCreatures)
        {
            if (Random.value > entry.spawnChance) continue;

            for (int j = 0; j < entry.quantity; j++)
            {
                Vector3 finalPos = basePos + new Vector3(
                    Random.Range(-0.2f, 0.2f),
                    Random.Range(-0.2f, 0.2f),
                    0
                );

                GameObject go = Instantiate(entry.creature, finalPos, Quaternion.identity, worldGenerator.creaturesContainer);
                NPCsMovement mov = go.GetComponent<NPCsMovement>();
                if (mov != null) mov.Setup(this.player, this.worldGenerator);

                spawnCount++;
            }
        }
    }

    // =========================================================================
    // Cache de Compatibilidade
    // =========================================================================

    /// <summary>Garante que o cache de compatibilidade seja construído apenas uma vez.</summary>
    private void EnsureCompatibilityCache()
    {
        if (compatible != null) return;
        BuildCompatibilityCache();
    }

    /// <summary>
    /// Constrói a tabela <see cref="compatible"/> consultando o <see cref="RuleManager"/>
    /// para cada par de tiles e cada direção. Executado uma única vez por chunk.
    /// </summary>
    private void BuildCompatibilityCache()
    {
        int n = TileCount;
        compatible = new bool[n, n, 4];

        for (int a = 0; a < n; a++)
            for (int b = 0; b < n; b++)
            {
                Tile tA = tilesetData.tileset[a];
                Tile tB = tilesetData.tileset[b];
                compatible[a, b, 0] = !ruleManager.IsBlocked(tA, tB, Vector2Int.up);
                compatible[a, b, 1] = !ruleManager.IsBlocked(tA, tB, Vector2Int.down);
                compatible[a, b, 2] = !ruleManager.IsBlocked(tA, tB, Vector2Int.left);
                compatible[a, b, 3] = !ruleManager.IsBlocked(tA, tB, Vector2Int.right);
            }
    }

    // =========================================================================
    // Helpers Privados
    // =========================================================================

    /// <summary>
    /// Restaura o estado de todas as células ao snapshot salvo após a aplicação do halo.
    /// Usado para reiniciar o WFC sem precisar reconstruir o halo do zero.
    /// </summary>
    private void RestartFromHalo()
    {
        for (int x = 0; x < GridW; x++)
            for (int y = 0; y < GridH; y++)
                cells[x, y].possible = new BitArray(haloSnapshot[x, y]);
    }

    /// <returns><c>true</c> se a posição está dentro dos limites da grade (incluindo halo).</returns>
    private bool IsInsideBounds(Vector2Int p)
        => p.x >= 0 && p.x < GridW && p.y >= 0 && p.y < GridH;

    /// <returns><c>true</c> se alguma célula interna estiver sem tiles possíveis.</returns>
    private bool HasContradiction()
    {
        for (int x = 1; x <= chunkSize.x; x++)
            for (int y = 1; y <= chunkSize.y; y++)
                if (cells[x, y].isEmpty()) return true;
        return false;
    }

    /// <summary>
    /// Retorna <c>true</c> se o tile é considerado de terra para fins de peso de noise.
    /// Terra = camada par e diferente de 0 (ex.: 2, 4...).
    /// </summary>
    private bool IsTerraTile(Tile tile)
        => tile.metadata.camada % 2 == 0 && tile.metadata.camada != 0;

    /// <summary>
    /// Calcula o Perlin Noise para uma célula baseando-se em sua posição global no mundo.
    /// Garante continuidade (seamless) entre chunks vizinhos.
    /// </summary>
    private float CalculateCellNoise(Vector2Int localCoords)
    {
        // localCoords (1 a chunkSize) -> ajusta para (0 a chunkSize-1)
        int localX = localCoords.x - 1;
        int localY = localCoords.y - 1;

        float globalX = (currentChunkCoord.x * chunkSize.x) + localX;
        float globalY = (currentChunkCoord.y * chunkSize.y) + localY;

        return Mathf.PerlinNoise(globalX * currentNoiseScale + 100.5f, globalY * currentNoiseScale + 100.5f);
    }
}