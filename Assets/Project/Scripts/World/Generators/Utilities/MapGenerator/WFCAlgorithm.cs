using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// O Motor Central do Wave Function Collapse. 
/// Trabalha estritamente com manipulação de entropia, probabilidades e propagação de estado.
/// </summary>
public class WFCAlgorithm
{
    private const float MIN_TILE_WEIGHT = 0.0001f;
    private const int WATER_LAYER = 0; // Fallback layer

    private readonly ChunkCellGrid _grid;
    private readonly Vector2Int _chunkSize;
    private readonly CompatibilityCache _compatibilityCache;
    private readonly TilesetData _tilesetData;
    private readonly Vector2Int[] _cardinalDirectionsArray = { Vector2Int.up, Vector2Int.down, Vector2Int.left, Vector2Int.right };

    private int[,] _targetLayerMap;
    private System.Random _random;
    private Vector2Int _currentChunkCoordinate;

    public WFCAlgorithm(ChunkCellGrid grid, Vector2Int chunkSize, CompatibilityCache cache, TilesetData tilesetData)
    {
        _grid = grid;
        _chunkSize = chunkSize;
        _compatibilityCache = cache;
        _tilesetData = tilesetData;
    }

    public void SetState(int[,] targetLayerMap, System.Random random, Vector2Int chunkCoordinate)
    {
        _targetLayerMap = targetLayerMap;
        _random = random;
        _currentChunkCoordinate = chunkCoordinate;
    }

    public int[,] GetTargetLayerMap() => _targetLayerMap;

    public bool ApplyTargetLayerConstraints()
    {
        var constrainedCellsList = new List<Cell>();

        for (int cellX = 0; cellX <= _chunkSize.x + 1; cellX++)
        {
            for (int cellY = 0; cellY <= _chunkSize.y + 1; cellY++)
            {
                Cell cell = _grid.CellsArray[cellX, cellY];
                int targetLayer = GetCachedTargetLayer(cell.Coordinates);

                if (RestrictCellToTargetLayer(cell, targetLayer))
                    constrainedCellsList.Add(cell);
            }
        }

        foreach (Cell constrainedCell in constrainedCellsList)
        {
            if (!constrainedCell.IsEmpty())
                PropagateConsequences(constrainedCell);
        }

        Cell contradiction = GetContradictionCell();
        if (contradiction != null)
        {
            Debug.LogWarning($"[WFC X-RAY] CONTRADIÇÃO PREMATURA! O vizinho enviou uma borda impossível de resolver.");
            LogContradictionContext(contradiction);
            return false;
        }

        return true;
    }

    public Cell ChooseCell()
    {
        int minimumPossibilities = int.MaxValue;
        var candidateCellsList = new List<Cell>();

        for (int cellX = 1; cellX <= _chunkSize.x; cellX++)
        {
            for (int cellY = 1; cellY <= _chunkSize.y; cellY++)
            {
                Cell cell = _grid.CellsArray[cellX, cellY];
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

        return candidateCellsList.Count > 0 ? candidateCellsList[_random.Next(0, candidateCellsList.Count)] : null;
    }

    public void CollapseAndPropagate(Cell cell)
    {
        int targetLayer = GetCachedTargetLayer(cell.Coordinates);
        int chosenTileIndex = SelectWeightedTileIndex(cell, targetLayer);

        if (chosenTileIndex < 0)
        {
            cell.PossibleBitsArray.SetAll(false);
            return;
        }

        cell.CollapseCell(chosenTileIndex);
        PropagateConsequences(cell);
    }

    public void PropagateConsequences(Cell startCell)
    {
        var cellQueue = new Queue<Cell>();
        cellQueue.Enqueue(startCell);

        while (cellQueue.Count > 0)
        {
            Cell currentCell = cellQueue.Dequeue();
            ProcessNeighbors(currentCell, cellQueue);
        }
    }

    private void ProcessNeighbors(Cell currentCell, Queue<Cell> cellQueue)
    {
        for (int directionIndex = 0; directionIndex < _cardinalDirectionsArray.Length; directionIndex++)
        {
            Vector2Int neighborPosition = currentCell.Coordinates + _cardinalDirectionsArray[directionIndex];
            if (!_grid.IsInsideBounds(neighborPosition)) continue;

            Cell neighborCell = _grid.CellsArray[neighborPosition.x, neighborPosition.y];
            if (neighborCell.IsCollapsed()) continue;

            if (HasRemovedUnsupportedTiles(currentCell, neighborCell, directionIndex))
                cellQueue.Enqueue(neighborCell);
        }
    }

    private bool HasRemovedUnsupportedTiles(Cell currentCell, Cell neighborCell, int directionIndex)
    {
        bool hasChanged = false;
        int tileCount = _tilesetData.TilesetList.Count;

        for (int neighborTileIndex = 0; neighborTileIndex < tileCount; neighborTileIndex++)
        {
            if (!neighborCell.PossibleBitsArray[neighborTileIndex]) continue;
            if (HasSupport(currentCell, neighborTileIndex, directionIndex)) continue;

            neighborCell.PossibleBitsArray[neighborTileIndex] = false;
            hasChanged = true;
        }

        return hasChanged;
    }

    private bool HasSupport(Cell currentCell, int neighborTileIndex, int directionIndex)
    {
        int tileCount = _tilesetData.TilesetList.Count;
        for (int currentTileIndex = 0; currentTileIndex < tileCount; currentTileIndex++)
        {
            if (!currentCell.PossibleBitsArray[currentTileIndex]) continue;
            if (_compatibilityCache.IsCompatible(currentTileIndex, neighborTileIndex, directionIndex)) return true;
        }
        return false;
    }

    private bool RestrictCellToTargetLayer(Cell cell, int targetLayer)
    {
        bool hasChanged = false;
        int tileCount = _tilesetData.TilesetList.Count;

        for (int tileIndex = 0; tileIndex < tileCount; tileIndex++)
        {
            if (!cell.PossibleBitsArray[tileIndex]) continue;
            if (_tilesetData.TilesetList[tileIndex].Metadata.Layer == targetLayer) continue;

            cell.PossibleBitsArray[tileIndex] = false;
            hasChanged = true;
        }
        return hasChanged;
    }

    private int SelectWeightedTileIndex(Cell cell, int targetLayer)
    {
        float totalWeight = 0f;
        int tileCount = _tilesetData.TilesetList.Count;

        for (int tileIndex = 0; tileIndex < tileCount; tileIndex++)
        {
            if (!cell.PossibleBitsArray[tileIndex]) continue;
            Tile tile = _tilesetData.TilesetList[tileIndex];
            if (tile.Metadata.Layer == targetLayer) totalWeight += Mathf.Max(tile.Weight, MIN_TILE_WEIGHT);
        }

        if (totalWeight <= 0f) return -1;

        float randomRoll = (float)(_random.NextDouble() * totalWeight);

        for (int tileIndex = 0; tileIndex < tileCount; tileIndex++)
        {
            if (!cell.PossibleBitsArray[tileIndex]) continue;
            Tile tile = _tilesetData.TilesetList[tileIndex];
            if (tile.Metadata.Layer != targetLayer) continue;

            randomRoll -= Mathf.Max(tile.Weight, MIN_TILE_WEIGHT);
            if (randomRoll <= 0f) return tileIndex;
        }
        return -1;
    }

    private int GetCachedTargetLayer(Vector2Int localCoordinates)
    {
        if (_targetLayerMap == null) return WATER_LAYER;
        int clampedX = Mathf.Clamp(localCoordinates.x, 0, _chunkSize.x + 1);
        int clampedY = Mathf.Clamp(localCoordinates.y, 0, _chunkSize.y + 1);
        return _targetLayerMap[clampedX, clampedY];
    }

    public Cell GetContradictionCell()
    {
        for (int cellX = 0; cellX <= _chunkSize.x + 1; cellX++)
        {
            for (int cellY = 0; cellY <= _chunkSize.y + 1; cellY++)
            {
                if (_grid.CellsArray[cellX, cellY].IsEmpty()) return _grid.CellsArray[cellX, cellY];
            }
        }
        return null;
    }

    public bool HasContradiction() => GetContradictionCell() != null;

    public void LogContradictionContext(Cell failedCell)
    {
        int targetLayer = GetCachedTargetLayer(failedCell.Coordinates);
        string log = $"<color=red><b>[WFC X-RAY] CONTRADICTION AT CHUNK {_currentChunkCoordinate}</b></color>\n";
        log += $"Local Cell: {failedCell.Coordinates} | Expected Target Layer: {targetLayer}\n";
        log += "--- Neighbor States ---\n";

        string[] dirNames = { "UP", "DOWN", "LEFT", "RIGHT" };
        
        for (int i = 0; i < _cardinalDirectionsArray.Length; i++)
        {
            Vector2Int nPos = failedCell.Coordinates + _cardinalDirectionsArray[i];
            
            if (!_grid.IsInsideBounds(nPos))
            {
                log += $"[{dirNames[i]}] -> BOUNDARY (Halo)\n";
                continue;
            }

            Cell nCell = _grid.CellsArray[nPos.x, nPos.y];
            int expectedNLayer = GetCachedTargetLayer(nCell.Coordinates);

            if (nCell.IsCollapsed())
            {
                Tile tile = _tilesetData.TilesetList[nCell.CollapsedIndex()];
                log += $"[{dirNames[i]}] -> COLLAPSED: {tile.name} (Layer {tile.Metadata.Layer}, Type {tile.Metadata.Type}) | Target Layer: {expectedNLayer}\n";
            }
            else if (nCell.IsEmpty()) log += $"[{dirNames[i]}] -> EMPTY (Also failed)\n";
            else log += $"[{dirNames[i]}] -> WAITING: {nCell.CountPossible()} possibilities | Target Layer: {expectedNLayer}\n";
        }
        Debug.LogError(log);
    }
}