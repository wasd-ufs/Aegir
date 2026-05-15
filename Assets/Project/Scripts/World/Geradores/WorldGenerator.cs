using UnityEngine;

/// <summary>
/// Orquestrador central do mundo procedural baseado em chunks.
/// Coordena os subsistemas de ciclo de vida, visibilidade, geração de estruturas
/// e transição do jogador — sem implementar nenhuma dessas responsabilidades diretamente.
/// </summary>
[RequireComponent(typeof(ChunkLifecycleManager), typeof(StructureGenerator), typeof(PlayerTransitionController))]
public class WorldGenerator : MonoBehaviour
{
    // =========================================================================
    // Campos Serializados
    // =========================================================================

    [Header("World Settings")]
    [SerializeField] private GameObject _chunkPrefab;
    [SerializeField] private Transform _playerTransform;
    [SerializeField] private int _viewDistance = 2;
    [SerializeField] private TilesetData _tilesetData;

    [Header("Developer Tools")]
    [SerializeField] private bool _shouldClearSaveOnStart = false;

    [Header("Containers")]
    [SerializeField] private Transform _creaturesContainer;

    // =========================================================================
    // Propriedades Públicas
    // =========================================================================

    public Transform CreaturesContainer => _creaturesContainer;

    // =========================================================================
    // Subsistemas — MonoBehaviour (componentes no mesmo GameObject)
    // =========================================================================

    private ChunkLifecycleManager _lifecycleManager;
    private StructureGenerator _structureGenerator;
    private PlayerTransitionController _transitionController;

    // =========================================================================
    // Subsistemas — Classes C# puras
    // =========================================================================

    private ChunkPersistence _persistence;
    private ChunkGenerationQueue _generationQueue;
    private ChunkVisibilityTracker _visibilityTracker;
    private WorldTileQuery _tileQuery;
    private HaloBuilder _haloBuilder;
    private ChunkNeighborNotifier _neighborNotifier;

    // =========================================================================
    // Estado Interno
    // =========================================================================

    private Vector2Int _lastPlayerChunkPosition;
    private Vector2Int _chunkSize;
    private float _cachedCellSize;

    // =========================================================================
    // Unity Callbacks
    // =========================================================================

    private void Awake()
    {
        _lifecycleManager   = GetComponent<ChunkLifecycleManager>();
        _structureGenerator = GetComponent<StructureGenerator>();
        _transitionController = GetComponent<PlayerTransitionController>();

        var mapGeneratorTemplate = _chunkPrefab.GetComponent<MapGenerator>();
        _chunkSize      = mapGeneratorTemplate.ChunkSize;
        _cachedCellSize = _chunkPrefab.GetComponent<Grid>().cellSize.x;

        _persistence    = new ChunkPersistence();
        if (_shouldClearSaveOnStart) _persistence.ClearSaveData();

        _generationQueue   = new ChunkGenerationQueue();
        _tileQuery         = new WorldTileQuery(_lifecycleManager, _chunkSize, _cachedCellSize);
        _haloBuilder       = new HaloBuilder(_lifecycleManager, _persistence, _chunkSize, _tilesetData);
        _neighborNotifier  = new ChunkNeighborNotifier(_lifecycleManager, _chunkSize);
        _visibilityTracker = new ChunkVisibilityTracker(_lifecycleManager, _viewDistance);

        _lifecycleManager.Setup(_persistence, _haloBuilder, _neighborNotifier, this, _playerTransform);
        _structureGenerator.Setup(_tileQuery, _lifecycleManager, _chunkSize, _cachedCellSize);
        _transitionController.Setup(_tileQuery, _lifecycleManager, Camera.main, _cachedCellSize);
    }

    private void Start()
    {
        _lastPlayerChunkPosition = _tileQuery.GetPlayerChunkPosition(_playerTransform);
        GenerateInitialChunks(_lastPlayerChunkPosition);
    }

    private void Update()
    {
        Vector2Int currentPlayerChunk = _tileQuery.GetPlayerChunkPosition(_playerTransform);

        if (currentPlayerChunk != _lastPlayerChunkPosition)
        {
            _lastPlayerChunkPosition = currentPlayerChunk;

            // Visibilidade delegada ao tracker — lifecycle só executa o que lhe é pedido
            _visibilityTracker.UpdateVisibleChunks(currentPlayerChunk, _generationQueue);
            _generationQueue.SortQueueByDistance(currentPlayerChunk);
        }

        if (_generationQueue.CurrentlyGenerating == null && _generationQueue.TryGetNext(out Vector2Int nextPosition))
        {
            _generationQueue.CurrentlyGenerating = nextPosition;
            Vector3 worldPosition = _tileQuery.GetChunkWorldPosition(nextPosition);
            _lifecycleManager.CreateOrLoadChunkAsync(nextPosition, worldPosition, _generationQueue);
        }

        _structureGenerator.ProcessDecorations();
    }

    // =========================================================================
    // Geração Inicial
    // =========================================================================

    private void GenerateInitialChunks(Vector2Int centerPosition)
    {
        for (int radius = 0; radius <= _viewDistance; radius++)
        {
            for (int x = -radius; x <= radius; x++)
            {
                for (int y = -radius; y <= radius; y++)
                {
                    if (Mathf.Abs(x) != radius && Mathf.Abs(y) != radius) continue;

                    Vector2Int chunkPosition = new Vector2Int(centerPosition.x + x, centerPosition.y + y);

                    if (_lifecycleManager.GetActiveChunk(chunkPosition) != null) continue;

                    Vector3 worldPosition = _tileQuery.GetChunkWorldPosition(chunkPosition);
                    _lifecycleManager.CreateOrLoadChunkSync(chunkPosition, worldPosition);
                }
            }
        }
    }

    // =========================================================================
    // API Pública
    // =========================================================================

    public Tile GetTileAtWorldPosition(Vector3 worldPosition)
    {
        return _tileQuery.GetTileAtWorldPosition(worldPosition);
    }

    public void TryTransitionToBoatOrPlayer()
    {
        PlayerMovement boatMovement = FindFirstObjectByType<PlayerMovement>();
        _transitionController.TryTransition(boatMovement);
    }

    public void TryFindWaterTile()
    {
        _transitionController.TryFindWaterTile(_playerTransform);
    }

    [ContextMenu("Limpar Dados Salvos")]
    public void ClearSaveData()
    {
        _persistence?.ClearSaveData();
    }

    // =========================================================================
    // Legacy API Bridges (compatibilidade com sistema antigo)
    // =========================================================================

    // Devolve o transform do jogador para a CameraFollow.cs não quebrar
    public Transform player => _lifecycleManager.ActivePlayer;

    // Redireciona a chamada antiga do PlayerMovement.cs para o novo sistema
    public void TryGoOut(Camera mainCamera)
    {
        TryTransitionToBoatOrPlayer();
    }

    // Redireciona a matemática de posições para o novo _tileQuery (usado pelo NPCsMovement)
    public Vector2Int GetChunkPosFromWorld(Vector3 worldPosition)
    {
        return _tileQuery.GetChunkPositionFromWorld(worldPosition);
    }

    // Redireciona a verificação de chunks para o novo _lifecycleManager (usado pelo NPCsMovement)
    public bool IsChunkActive(Vector2Int chunkPosition)
    {
        return _lifecycleManager.GetActiveChunk(chunkPosition) != null;
    }
}