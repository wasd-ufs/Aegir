using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class WFCSolver : MonoBehaviour
{
    [SerializeField] private int _collapsesPerFrame = 10;
    [SerializeField] private RuleManager _ruleManager;
    [SerializeField] private TilesetData _tilesetData;
    private CompatibilityCache _compatibilityCache;

    public bool IsGenerating { get; private set; }
    public bool HasGenerationSucceeded { get; private set; }

    public System.Action<bool> OnGenerationComplete;
    public System.Action OnMapRenderRequested;

    private readonly Vector2Int[] _directionsArray = { Vector2Int.up, Vector2Int.down, Vector2Int.left, Vector2Int.right };
    
    private ChunkCellGrid _grid;
    private Vector2Int _chunkSize;
    private Vector2Int _currentChunkCoord;
    private float _currentNoiseScale;

    public void Setup(ChunkCellGrid grid, Vector2Int chunkSize, CompatibilityCache compatibilityCache)
    {
        _grid = grid;
        _chunkSize = chunkSize;
        _compatibilityCache = compatibilityCache;
    }

    public void PropagateConsequences(Cell startCell)
    {
        Queue<Cell> cellQueue = new Queue<Cell>();
        cellQueue.Enqueue(startCell);

        while (cellQueue.Count > 0)
        {
            Cell currentCell = cellQueue.Dequeue();
            ProcessNeighbors(currentCell, cellQueue);
        }
    }

    private void ProcessNeighbors(Cell currentCell, Queue<Cell> cellQueue)
    {
        for (int directionIndex = 0; directionIndex < 4; directionIndex++)
        {
            Vector2Int neighborPosition = currentCell.Coordinates + _directionsArray[directionIndex];
            if (!_grid.IsInsideBounds(neighborPosition)) continue;

            Cell neighborCell = _grid.CellsArray[neighborPosition.x, neighborPosition.y];
            if (neighborCell.IsCollapsed()) continue;

            if (RemoveUnsupportedTiles(currentCell, neighborCell, directionIndex))
            {
                cellQueue.Enqueue(neighborCell);
            }
        }
    }

    private bool RemoveUnsupportedTiles(Cell currentCell, Cell neighborCell, int directionIndex)
    {
        bool hasChanged = false;
        int tileCount = _tilesetData.TilesetList.Count;

        for (int neighborTileIndex = 0; neighborTileIndex < tileCount; neighborTileIndex++)
        {
            if (!neighborCell.PossibleBitsArray[neighborTileIndex]) continue;

            if (!HasSupport(currentCell, neighborTileIndex, directionIndex))
            {
                neighborCell.PossibleBitsArray[neighborTileIndex] = false;
                hasChanged = true;
            }
        }
        return hasChanged;
    }

    private bool HasSupport(Cell currentCell, int neighborTileIndex, int directionIndex)
    {
        int tileCount = _tilesetData.TilesetList.Count;
        for (int currentTileIndex = 0; currentTileIndex < tileCount; currentTileIndex++)
        {
            if (!currentCell.PossibleBitsArray[currentTileIndex]) continue;
            
            if (_compatibilityCache.IsCompatible(currentTileIndex, neighborTileIndex, directionIndex))
            {
                return true;
            }
        }
        return false;
    }

    public bool RunCollapseSync()
    {
        int totalCells = _chunkSize.x * _chunkSize.y;
        int collapsedCount = 0;
        int maxAttempts = totalCells * 3;
        int attemptsCount = 0;

        while (collapsedCount < totalCells && attemptsCount < maxAttempts)
        {
            Cell chosenCell = ChooseCell();
            if (chosenCell == null) break;

            float cellNoise = CalculateCellNoise(chosenCell.Coordinates);
            CollapseAndPropagate(chosenCell, cellNoise);

            if (HasContradiction())
            {
                _grid.RestartFromHalo();
                collapsedCount = 0;
                attemptsCount++;
                continue;
            }

            collapsedCount++;
        }

        return !HasContradiction();
    }

    public IEnumerator RunCollapseAsyncCoroutine()
    {
        IsGenerating = true;
        HasGenerationSucceeded = false;

        int totalCells = _chunkSize.x * _chunkSize.y;
        int collapsedCount = 0;
        int maxAttempts = totalCells * 3;
        int attemptsCount = 0;
        int collapsesThisFrame = 0;

        while (collapsedCount < totalCells && attemptsCount < maxAttempts)
        {
            Cell chosenCell = ChooseCell();
            if (chosenCell == null) break;

            float cellNoise = CalculateCellNoise(chosenCell.Coordinates);
            CollapseAndPropagate(chosenCell, cellNoise);

            if (HasContradiction())
            {
                _grid.RestartFromHalo();
                collapsedCount = 0;
                attemptsCount++;
            }
            else
            {
                collapsedCount++;
                OnMapRenderRequested?.Invoke();
            }

            collapsesThisFrame++;
            if (collapsesThisFrame >= _collapsesPerFrame)
            {
                collapsesThisFrame = 0;
                yield return null; 
            }
        }

        HasGenerationSucceeded = !HasContradiction();
        IsGenerating = false;
        OnGenerationComplete?.Invoke(HasGenerationSucceeded);
    }

    public void StartAsyncGeneration(Vector2Int chunkCoord, float noiseScale)
    {
        _currentChunkCoord = chunkCoord;
        _currentNoiseScale = noiseScale;
        StartCoroutine(RunCollapseAsyncCoroutine());
    }

    public void SetupSyncGenerationParameters(Vector2Int chunkCoord, float noiseScale)
    {
        _currentChunkCoord = chunkCoord;
        _currentNoiseScale = noiseScale;
    }

    private Cell ChooseCell()
    {
        int minimumPossibilities = int.MaxValue;
        List<Cell> candidateCellsList = new List<Cell>();

        for (int x = 1; x <= _chunkSize.x; x++)
        {
            for (int y = 1; y <= _chunkSize.y; y++)
            {
                Cell cell = _grid.CellsArray[x, y];
                if (cell.IsCollapsed()) continue;

                int possibilitiesCount = cell.CountPossible();
                if (possibilitiesCount == 0) continue;

                if (possibilitiesCount < minimumPossibilities) 
                { 
                    minimumPossibilities = possibilitiesCount; 
                    candidateCellsList.Clear(); 
                    candidateCellsList.Add(cell); 
                }
                else if (possibilitiesCount == minimumPossibilities)
                {
                    candidateCellsList.Add(cell);
                }
            }
        }

        return candidateCellsList.Count > 0 ? candidateCellsList[Random.Range(0, candidateCellsList.Count)] : null;
    }

    private void CollapseAndPropagate(Cell cell, float noiseValue)
    {
        int tileCount = _tilesetData.TilesetList.Count;
        float totalWeight = 0;
        
        for (int i = 0; i < tileCount; i++)
        {
            if (!cell.PossibleBitsArray[i]) continue;
            Tile tile = _tilesetData.TilesetList[i];
            totalWeight += IsTerraTile(tile) ? tile.Weight * (noiseValue * 10) : tile.Weight;
        }

        float randomRoll = Random.Range(0, totalWeight);
        int chosenIndex = -1;

        for (int i = 0; i < tileCount; i++)
        {
            if (!cell.PossibleBitsArray[i]) continue;
            Tile tile = _tilesetData.TilesetList[i];
            randomRoll -= IsTerraTile(tile) ? tile.Weight * (noiseValue * 10) : tile.Weight;
            
            if (randomRoll <= 0) 
            { 
                chosenIndex = i; 
                break; 
            }
        }

        if (chosenIndex < 0)
        {
            for (int i = tileCount - 1; i >= 0; i--)
            {
                if (cell.PossibleBitsArray[i]) 
                { 
                    chosenIndex = i; 
                    break; 
                }
            }
        }

        cell.CollapseCell(chosenIndex);
        PropagateConsequences(cell);
    }

    public bool HasContradiction()
    {
        for (int x = 1; x <= _chunkSize.x; x++)
        {
            for (int y = 1; y <= _chunkSize.y; y++)
            {
                if (_grid.CellsArray[x, y].IsEmpty()) return true;
            }
        }
        return false;
    }

    private bool IsTerraTile(Tile tile)
    {
        return tile.Metadata.Layer % 2 == 0 && tile.Metadata.Layer != 0;
    }

    private float CalculateCellNoise(Vector2Int localCoordinates)
    {
        int localX = localCoordinates.x - 1;
        int localY = localCoordinates.y - 1;

        float globalX = (_currentChunkCoord.x * _chunkSize.x) + localX;
        float globalY = (_currentChunkCoord.y * _chunkSize.y) + localY;

        return Mathf.PerlinNoise(globalX * _currentNoiseScale + 100.5f, globalY * _currentNoiseScale + 100.5f);
    }
}