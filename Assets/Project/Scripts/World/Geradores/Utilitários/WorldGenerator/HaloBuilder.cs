using System.Collections.Generic;
using UnityEngine;

public class HaloBuilder
{
    private ChunkLifecycleManager _lifecycleManager;
    private ChunkPersistence _persistence;
    private Vector2Int _chunkSize;
    private TilesetData _tilesetData;

    public HaloBuilder(ChunkLifecycleManager lifecycleManager, ChunkPersistence persistence, Vector2Int chunkSize, TilesetData tilesetData)
    {
        _lifecycleManager = lifecycleManager;
        _persistence = persistence;
        _chunkSize = chunkSize;
        _tilesetData = tilesetData;
    }

    public Dictionary<Vector2Int, Tile> BuildHalo(Vector2Int chunkPosition)
    {
        var haloDictionary = new Dictionary<Vector2Int, Tile>();

        FillHaloEdge(chunkPosition, Vector2Int.left, _chunkSize.x - 1, true, 0, haloDictionary);
        FillHaloEdge(chunkPosition, Vector2Int.right, 0, true, _chunkSize.x + 1, haloDictionary);
        FillHaloEdge(chunkPosition, Vector2Int.down, _chunkSize.y - 1, false, 0, haloDictionary);
        FillHaloEdge(chunkPosition, Vector2Int.up, 0, false, _chunkSize.y + 1, haloDictionary);

        AddHaloCorner(chunkPosition, new Vector2Int(-1, -1), new Vector2Int(_chunkSize.x - 1, _chunkSize.y - 1), new Vector2Int(0, 0), haloDictionary);
        AddHaloCorner(chunkPosition, new Vector2Int(1, -1), new Vector2Int(0, _chunkSize.y - 1), new Vector2Int(_chunkSize.x + 1, 0), haloDictionary);
        AddHaloCorner(chunkPosition, new Vector2Int(-1, 1), new Vector2Int(_chunkSize.x - 1, 0), new Vector2Int(0, _chunkSize.y + 1), haloDictionary);
        AddHaloCorner(chunkPosition, new Vector2Int(1, 1), new Vector2Int(0, 0), new Vector2Int(_chunkSize.x + 1, _chunkSize.y + 1), haloDictionary);

        return haloDictionary;
    }

    private void FillHaloEdge(Vector2Int chunkPosition, Vector2Int direction, int neighborColumn, bool isVertical, int fixedHaloIndex, Dictionary<Vector2Int, Tile> haloDictionary)
    {
        Vector2Int neighborPosition = chunkPosition + direction;
        int limitCount = isVertical ? _chunkSize.y : _chunkSize.x;
        MapGenerator neighborChunk = _lifecycleManager.GetActiveOrPendingChunk(neighborPosition);

        if (neighborChunk != null)
        {
            for (int i = 0; i < limitCount; i++)
            {
                Tile tile = isVertical ? neighborChunk.GetTileAt(neighborColumn, i) : neighborChunk.GetTileAt(i, neighborColumn);
                if (tile != null) haloDictionary[isVertical ? new Vector2Int(fixedHaloIndex, i + 1) : new Vector2Int(i + 1, fixedHaloIndex)] = tile;
            }
        }
        else
        {
            byte[] dataArray = _persistence.LoadChunkFromDisk(neighborPosition);
            if (dataArray == null) return;

            for (int i = 0; i < limitCount; i++)
            {
                int index = isVertical ? (neighborColumn * _chunkSize.y + i) : (i * _chunkSize.y + neighborColumn);
                if (index >= 0 && index < dataArray.Length)
                {
                    haloDictionary[isVertical ? new Vector2Int(fixedHaloIndex, i + 1) : new Vector2Int(i + 1, fixedHaloIndex)] = _tilesetData.TilesetList[dataArray[index]];
                }
            }
        }
    }

    private void AddHaloCorner(Vector2Int chunkPosition, Vector2Int diagonalDirection, Vector2Int neighborCoordinate, Vector2Int haloCoordinate, Dictionary<Vector2Int, Tile> haloDictionary)
    {
        if (haloDictionary.ContainsKey(haloCoordinate)) return;

        Vector2Int neighborPosition = chunkPosition + diagonalDirection;
        MapGenerator neighborChunk = _lifecycleManager.GetActiveOrPendingChunk(neighborPosition);

        if (neighborChunk != null)
        {
            Tile tile = neighborChunk.GetTileAt(neighborCoordinate.x, neighborCoordinate.y);
            if (tile != null) haloDictionary[haloCoordinate] = tile;
        }
        else
        {
            byte[] dataArray = _persistence.LoadChunkFromDisk(neighborPosition);
            if (dataArray == null) return;

            int index = neighborCoordinate.x * _chunkSize.y + neighborCoordinate.y;
            if (index >= 0 && index < dataArray.Length)
            {
                haloDictionary[haloCoordinate] = _tilesetData.TilesetList[dataArray[index]];
            }
        }
    }
}