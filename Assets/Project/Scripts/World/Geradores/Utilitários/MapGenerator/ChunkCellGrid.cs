using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class ChunkCellGrid
{
    public Cell[,] CellsArray { get; private set; }
    public int GridWidth { get; private set; }
    public int GridHeight { get; private set; }

    private BitArray[,] _haloSnapshotArray;
    private int _tileCount;
    private TilesetData _tilesetData;

    public ChunkCellGrid(Vector2Int chunkSize, TilesetData tilesetData)
    {
        GridWidth = chunkSize.x + 2;
        GridHeight = chunkSize.y + 2;
        _tilesetData = tilesetData;
        _tileCount = _tilesetData.TilesetList.Count;
    }

    public void InitializeCells(Dictionary<Vector2Int, Tile> borderTilesDictionary, System.Action<Cell> onPropagateNeeded)
    {
        CellsArray = new Cell[GridWidth, GridHeight];
        for (int x = 0; x < GridWidth; x++)
        {
            for (int y = 0; y < GridHeight; y++)
            {
                CellsArray[x, y] = new Cell(_tileCount, new Vector2Int(x, y));
            }
        }

        if (borderTilesDictionary != null)
        {
            ApplyHaloBorders(borderTilesDictionary);
            PropagateHaloBorders(borderTilesDictionary, onPropagateNeeded);
        }

        SaveHaloSnapshot();
    }

    private void ApplyHaloBorders(Dictionary<Vector2Int, Tile> borderTilesDictionary)
    {
        foreach (var keyValuePair in borderTilesDictionary)
        {
            if (!IsInsideBounds(keyValuePair.Key)) continue;
            
            int tileIndex = _tilesetData.TilesetList.IndexOf(keyValuePair.Value);
            if (tileIndex >= 0)
            {
                CellsArray[keyValuePair.Key.x, keyValuePair.Key.y].CollapseCell(tileIndex);
            }
        }
    }

    private void PropagateHaloBorders(Dictionary<Vector2Int, Tile> borderTilesDictionary, System.Action<Cell> onPropagateNeeded)
    {
        foreach (var keyValuePair in borderTilesDictionary)
        {
            if (!IsInsideBounds(keyValuePair.Key)) continue;
            onPropagateNeeded?.Invoke(CellsArray[keyValuePair.Key.x, keyValuePair.Key.y]);
        }
    }

    private void SaveHaloSnapshot()
    {
        _haloSnapshotArray = new BitArray[GridWidth, GridHeight];
        for (int x = 0; x < GridWidth; x++)
        {
            for (int y = 0; y < GridHeight; y++)
            {
                _haloSnapshotArray[x, y] = new BitArray(CellsArray[x, y].PossibleBitsArray);
            }
        }
    }

    public void RestartFromHalo()
    {
        for (int x = 0; x < GridWidth; x++)
        {
            for (int y = 0; y < GridHeight; y++)
            {
                CellsArray[x, y].CopyFrom(_haloSnapshotArray[x, y]);
            }
        }
    }

    public bool IsInsideBounds(Vector2Int position)
    {
        return position.x >= 0 && position.x < GridWidth && position.y >= 0 && position.y < GridHeight;
    }

    public Tile GetTileAt(int localX, int localY)
    {
        if (CellsArray == null) return null;
        
        Cell cell = CellsArray[localX + 1, localY + 1]; 
        if (cell.IsEmpty()) return null;

        int tileIndex = cell.CollapsedIndex();
        return tileIndex >= 0 ? _tilesetData.TilesetList[tileIndex] : null;
    }
}