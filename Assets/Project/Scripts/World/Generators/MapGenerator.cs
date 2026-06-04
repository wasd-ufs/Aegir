using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Orquestrador do ciclo de vida de um chunk: inicializa os subsistemas,
/// coordena geração (síncrona e assíncrona), serialização e spawn de entidades.
/// Toda a lógica pesada é delegada a <see cref="WFCSolver"/>, <see cref="ChunkRenderer"/>
/// e <see cref="EntitySpawner"/>.
/// </summary>
[RequireComponent(typeof(WFCSolver), typeof(ChunkRenderer), typeof(EntitySpawner))]
public class MapGenerator : MonoBehaviour
{
    // =========================================================================
    // Campos Serializados
    // =========================================================================

    [SerializeField] private Vector2Int _chunkSize;
    [SerializeField] private TilesetData _tilesetData;
    [SerializeField] private RuleManager _ruleManager;

    // =========================================================================
    // Propriedades e Eventos
    // =========================================================================

    public Vector2Int ChunkSize   => _chunkSize;
    public bool IsGenerating      => _wfcSolver.IsGenerating;
    public ChunkRenderer Renderer => _chunkRenderer;

    public Action<MapGenerator, bool> OnGenerationComplete;

    // =========================================================================
    // Campos Privados
    // =========================================================================

    private WFCSolver _wfcSolver;
    private ChunkRenderer _chunkRenderer;
    private EntitySpawner _entitySpawner;
    private ChunkCellGrid _grid;
    private CompatibilityCache _cache;

    private WorldGenerator _worldGenerator;
    private GameObject _player;

    // =========================================================================
    // Unity Callbacks
    // =========================================================================

    private void Awake()
    {
        _wfcSolver     = GetComponent<WFCSolver>();
        _chunkRenderer = GetComponent<ChunkRenderer>();
        _entitySpawner = GetComponent<EntitySpawner>();

        _grid  = new ChunkCellGrid(_chunkSize, _tilesetData);
        _cache = new CompatibilityCache(_ruleManager, _tilesetData);

        // BuildCache e Setup do solver NÃO são chamados aqui.
        // O RuleManager pode ainda não ter rodado seu Awake quando este chunk
        // é instanciado em runtime via Instantiate. O cache é construído de
        // forma lazy em EnsureCache(), chamada no início de cada geração.

        _wfcSolver.OnMapRenderRequested += HandleMapRenderRequest;
        _wfcSolver.OnGenerationComplete += HandleGenerationComplete;
    }

    // =========================================================================
    // API Pública — Setup
    // =========================================================================

    /// <summary>
    /// Associa o jogador e o <see cref="WorldGenerator"/> a este chunk e a todos os NPCs filhos.
    /// </summary>
    public void Setup(GameObject player, WorldGenerator worldGenerator)
    {
        _player         = player;
        _worldGenerator = worldGenerator;

        foreach (var npcComponent in GetComponentsInChildren<NPCsMovement>())
            npcComponent.Setup(player, worldGenerator);
    }

    // =========================================================================
    // API Pública — Geração
    // =========================================================================

    /// <summary>
    /// Geração síncrona. Bloqueia o jogo até o chunk estar pronto.
    /// Usada nos chunks do campo de visão inicial.
    /// </summary>
    /// <returns><c>true</c> se o chunk foi gerado sem contradições.</returns>
    public bool GenerateChunkSync(Dictionary<Vector2Int, Tile> borderTilesDictionary, Vector2Int chunkCoord, float noiseScale, int worldSeed)
    {
        EnsureCache();

        int maxRestarts = 10;
        _wfcSolver.SetParameters(chunkCoord, noiseScale, worldSeed);

        for (int attempt = 0; attempt < maxRestarts; attempt++)
        {
            _grid.InitializeCells(borderTilesDictionary, _wfcSolver.PropagateConsequences);

            if (_wfcSolver.HasCompletedCollapseSync())
            {
                _chunkRenderer.RenderMap(_grid, _chunkSize);
                SpawnEntities();
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// Geração assíncrona. Distribui o trabalho em frames via coroutine.
    /// Ao concluir, invoca <see cref="OnGenerationComplete"/>.
    /// </summary>
    public void GenerateChunkAsync(Dictionary<Vector2Int, Tile> borderTilesDictionary, Vector2Int chunkCoord, float noiseScale, int worldSeed)
    {
        EnsureCache();

        int maxRestarts = 10;
        StartCoroutine(AsyncGenerationRoutine(borderTilesDictionary, chunkCoord, noiseScale, maxRestarts, worldSeed));
    }

    private IEnumerator AsyncGenerationRoutine(
    Dictionary<Vector2Int, Tile> borderTilesDictionary,
    Vector2Int chunkCoord,
    float noiseScale,
    int maxRestarts,
    int worldSeed)
    {
        _wfcSolver.SetParameters(chunkCoord, noiseScale, worldSeed);
        _grid.InitializeCells(borderTilesDictionary, _wfcSolver.PropagateConsequences);
        _wfcSolver.StartAsyncGeneration();

        while (_wfcSolver.IsGenerating)
            yield return null;

        if (_wfcSolver.HasGenerationSucceeded)
            _chunkRenderer.RenderMap(_grid, _chunkSize);

        // Delegar todo o retry ao WFC — o loop externo não faz sentido no modo async
        // O WFC já gerencia seus próprios restarts via RestartGenerationAttempt
        // OnGenerationComplete será invocado pelo WFC ao final
    }

    public void SetIslandSampler(IslandMapSampler sampler)
    {
        _wfcSolver.SetIslandSampler(sampler);
    }

    // =========================================================================
    // API Pública — Halo e Dados
    // =========================================================================

    /// <summary>
    /// Atualiza o halo com tiles recém-gerados de um chunk vizinho e repropaga
    /// as restrições para as células internas.
    /// </summary>
    public void UpdateHaloAndRepropagate(Dictionary<Vector2Int, Tile> newHaloTilesDictionary)
    {
        EnsureCache();
        if (_grid.CellsArray == null) return;

        foreach (var keyValuePair in newHaloTilesDictionary)
        {
            if (!_grid.IsInsideBounds(keyValuePair.Key)) continue;

            Cell haloCell = _grid.CellsArray[keyValuePair.Key.x, keyValuePair.Key.y];
            if (haloCell.IsCollapsed()) continue;

            int tileIndex = _tilesetData.TilesetList.IndexOf(keyValuePair.Value);
            if (tileIndex < 0) continue;

            haloCell.CollapseCell(tileIndex);
            _grid.UpdateHaloCellSnapshot(keyValuePair.Key);
            _wfcSolver.PropagateConsequences(haloCell);
        }

        _chunkRenderer.RenderMap(_grid, _chunkSize);
    }

    /// <summary>
    /// Força todo o chunk a ser preenchido com tile de água (camada 0).
    /// Usado para o chunk da origem (posição zero).
    /// </summary>
    public void ForceWaterChunk(Dictionary<Vector2Int, Tile> borderTilesDictionary)
    {
        _grid.InitializeCells(borderTilesDictionary, _wfcSolver.PropagateConsequences);

        int waterTileIndex = _tilesetData.TilesetList.FindIndex(tile => tile.Metadata.Layer == 0);
        if (waterTileIndex == -1) return;

        for (int x = 0; x < _grid.GridWidth; x++)
            for (int y = 0; y < _grid.GridHeight; y++)
                _grid.CellsArray[x, y].CollapseCell(waterTileIndex);

        _chunkRenderer.RenderMap(_grid, _chunkSize);
    }

    /// <summary>
    /// Serializa o estado das células internas para um array de bytes.
    /// </summary>
    public byte[] GetChunkData()
    {
        if (_grid.CellsArray == null) return null;

        byte[] serializedDataArray = new byte[_chunkSize.x * _chunkSize.y];
        for (int x = 0; x < _chunkSize.x; x++)
        {
            for (int y = 0; y < _chunkSize.y; y++)
            {
                Cell cell      = _grid.CellsArray[x + 1, y + 1];
                int  tileIndex = cell.CollapsedIndex();
                serializedDataArray[x * _chunkSize.y + y] = tileIndex >= 0 ? (byte)tileIndex : (byte)0;
            }
        }

        return serializedDataArray;
    }

    /// <summary>
    /// Reconstrói o estado das células a partir de dados carregados do disco.
    /// </summary>
    public void LoadFromData(byte[] serializedDataArray)
    {
        EnsureCache();
        _grid.InitializeCells(null, null);

        for (int x = 0; x < _chunkSize.x; x++)
            for (int y = 0; y < _chunkSize.y; y++)
                _grid.CellsArray[x + 1, y + 1].CollapseCell(serializedDataArray[x * _chunkSize.y + y]);

        _chunkRenderer.RenderMap(_grid, _chunkSize);
    }

    /// <summary>Retorna o tile colapsado na posição local, excluindo o halo.</summary>
    public Tile GetTileAt(int localX, int localY)
    {
        return _grid.GetTileAt(localX, localY);
    }

    /// <summary>Instancia criaturas sobre as células colapsadas do chunk.</summary>
    public void SpawnEntities()
    {
        if (_worldGenerator == null || _player == null) return;
        _entitySpawner.SpawnEntities(
            _grid,
            _chunkSize,
            _chunkRenderer.Tilemap,
            _worldGenerator.CreaturesContainer,
            _player,
            _worldGenerator);
    }

    // =========================================================================
    // Helpers Privados
    // =========================================================================

    /// <summary>
    /// Garante que o cache de compatibilidade e o solver estejam prontos antes
    /// da primeira geração.
    /// <para>
    /// Separado do <c>Awake</c> para evitar dependência de ordem de inicialização
    /// com o <see cref="RuleManager"/>: quando este chunk é instanciado via
    /// <c>Instantiate</c> em runtime, o <c>RuleManager</c> da cena já rodou seu
    /// próprio <c>Awake</c> e está seguro de consultar.
    /// </para>
    /// <para>
    /// Tanto <see cref="CompatibilityCache.BuildCache"/> quanto
    /// <see cref="WFCSolver.Setup"/> são idempotentes — chamadas repetidas
    /// não têm efeito.
    /// </para>
    /// </summary>
    private void EnsureCache()
    {
        _cache.BuildCache();
        _wfcSolver.Setup(_grid, _chunkSize, _cache, _tilesetData);
    }

    private void HandleMapRenderRequest()
    {
        _chunkRenderer.RenderMap(_grid, _chunkSize);
    }

    private void HandleGenerationComplete(bool isSuccess)
    {
        OnGenerationComplete?.Invoke(this, isSuccess);
    }
}