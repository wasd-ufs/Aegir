using System;
using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(WFCSolver), typeof(ChunkRenderer), typeof(EntitySpawner))]
public class MapGenerator : MonoBehaviour
{
    [SerializeField] private Vector2Int _chunkSize;
    [SerializeField] private TilesetData _tilesetData;
    [SerializeField] private RuleManager _ruleManager;

    public Vector2Int ChunkSize => _chunkSize;
    public bool IsGenerating => _wfcSolver.IsGenerating;
    public Action<MapGenerator, bool> OnGenerationComplete;
    public ChunkRenderer Renderer => _chunkRenderer;

    private WFCSolver _wfcSolver;
    private ChunkRenderer _chunkRenderer;
    private EntitySpawner _entitySpawner;
    private ChunkCellGrid _grid;
    private CompatibilityCache _cache;
    private WorldGenerator _worldGenerator;
    private GameObject _player;

    private void Awake()
    {
        _wfcSolver = GetComponent<WFCSolver>();
        _chunkRenderer = GetComponent<ChunkRenderer>();
        _entitySpawner = GetComponent<EntitySpawner>();

        _grid = new ChunkCellGrid(_chunkSize, _tilesetData);
        _cache = new CompatibilityCache(_ruleManager, _tilesetData);

        _cache.BuildCache();
        _wfcSolver.Setup(_grid, _chunkSize, _cache);

        _wfcSolver.OnMapRenderRequested += HandleMapRenderRequest;
        _wfcSolver.OnGenerationComplete += HandleGenerationComplete;
    }

    public void Setup(GameObject player, WorldGenerator worldGenerator)
    {
        _player = player;
        _worldGenerator = worldGenerator;

        foreach (var npcComponent in GetComponentsInChildren<NPCsMovement>())
        {
            npcComponent.Setup(player, worldGenerator);
        }
    }

    public bool GenerateChunkSync(Dictionary<Vector2Int, Tile> borderTilesDictionary, Vector2Int chunkCoord, float noiseScale)
    {
        int maxRestarts = 10;
        _wfcSolver.SetupSyncGenerationParameters(chunkCoord, noiseScale);

        for (int attempt = 0; attempt < maxRestarts; attempt++)
        {
            _grid.InitializeCells(borderTilesDictionary, _wfcSolver.PropagateConsequences);

            if (_wfcSolver.RunCollapseSync())
            {
                _chunkRenderer.RenderMap(_grid, _chunkSize);
                SpawnEntities();
                return true;
            }
        }

        return false;
    }

    public void GenerateChunkAsync(Dictionary<Vector2Int, Tile> borderTilesDictionary, Vector2Int chunkCoord, float noiseScale)
    {
        int maxRestarts = 10;
        StartCoroutine(AsyncGenerationRoutine(borderTilesDictionary, chunkCoord, noiseScale, maxRestarts));
    }

    private System.Collections.IEnumerator AsyncGenerationRoutine(Dictionary<Vector2Int, Tile> borderTilesDictionary, Vector2Int chunkCoord, float noiseScale, int maxRestarts)
    {
        for (int attempt = 0; attempt < maxRestarts; attempt++)
        {
            _grid.InitializeCells(borderTilesDictionary, _wfcSolver.PropagateConsequences);
            _wfcSolver.StartAsyncGeneration(chunkCoord, noiseScale);
            
            while (_wfcSolver.IsGenerating)
            {
                yield return null;
            }

            if (_wfcSolver.HasGenerationSucceeded)
            {
                _chunkRenderer.RenderMap(_grid, _chunkSize);
                yield break;
            }
        }
    }

    public void UpdateHaloAndRepropagate(Dictionary<Vector2Int, Tile> newHaloTilesDictionary)
    {
        if (_grid.CellsArray == null) return;

        foreach (var keyValuePair in newHaloTilesDictionary)
        {
            if (!_grid.IsInsideBounds(keyValuePair.Key)) continue;

            Cell haloCell = _grid.CellsArray[keyValuePair.Key.x, keyValuePair.Key.y];
            if (haloCell.IsCollapsed()) continue;

            int tileIndex = _tilesetData.TilesetList.IndexOf(keyValuePair.Value);
            if (tileIndex < 0) continue;

            haloCell.CollapseCell(tileIndex);
            _wfcSolver.PropagateConsequences(haloCell);
        }

        _chunkRenderer.RenderMap(_grid, _chunkSize);
    }

    public void ForceWaterChunk(Dictionary<Vector2Int, Tile> borderTilesDictionary)
    {
        _grid.InitializeCells(borderTilesDictionary, _wfcSolver.PropagateConsequences);

        int waterTileIndex = _tilesetData.TilesetList.FindIndex(tile => tile.Metadata.Layer == 0);
        if (waterTileIndex == -1) return;

        for (int x = 0; x < _grid.GridWidth; x++)
        {
            for (int y = 0; y < _grid.GridHeight; y++)
            {
                _grid.CellsArray[x, y].CollapseCell(waterTileIndex);
            }
        }

        _chunkRenderer.RenderMap(_grid, _chunkSize);
    }

    public Tile GetTileAt(int localX, int localY)
    {
        return _grid.GetTileAt(localX, localY);
    }

    public byte[] GetChunkData()
    {
        if (_grid.CellsArray == null) return null;

        byte[] serializedDataArray = new byte[_chunkSize.x * _chunkSize.y];
        for (int x = 0; x < _chunkSize.x; x++)
        {
            for (int y = 0; y < _chunkSize.y; y++)
            {
                Cell cell = _grid.CellsArray[x + 1, y + 1];
                int tileIndex = cell.CollapsedIndex();
                serializedDataArray[x * _chunkSize.y + y] = tileIndex >= 0 ? (byte)tileIndex : (byte)0;
            }
        }

        return serializedDataArray;
    }

    public void LoadFromData(byte[] serializedDataArray)
    {
        _grid.InitializeCells(null, null);

        for (int x = 0; x < _chunkSize.x; x++)
        {
            for (int y = 0; y < _chunkSize.y; y++)
            {
                _grid.CellsArray[x + 1, y + 1].CollapseCell(serializedDataArray[x * _chunkSize.y + y]);
            }
        }

        _chunkRenderer.RenderMap(_grid, _chunkSize);
    }

    public void SpawnEntities()
    {
        if (_worldGenerator == null || _player == null) return;
        _entitySpawner.SpawnEntities(_grid, _chunkSize, _chunkRenderer.Tilemap, _worldGenerator.CreaturesContainer, _player, _worldGenerator);
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