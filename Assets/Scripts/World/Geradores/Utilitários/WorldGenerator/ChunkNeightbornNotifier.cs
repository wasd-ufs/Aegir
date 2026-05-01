using System.Collections.Generic;
using UnityEngine;

public class ChunkNeighborNotifier
{
    private ChunkLifecycleManager _lifecycleManager;
    private Vector2Int _chunkSize;

    public ChunkNeighborNotifier(ChunkLifecycleManager lifecycleManager, Vector2Int chunkSize)
    {
        _lifecycleManager = lifecycleManager;
        _chunkSize = chunkSize;
    }

    public void NotifyNeighbors(Vector2Int chunkPosition, MapGenerator newChunk)
    {
        var sidesArray = new[]
        {
            (direction: Vector2Int.left, sourceColumn: 0, isVertical: true, fixedHaloIndex: _chunkSize.x + 1),
            (direction: Vector2Int.right, sourceColumn: _chunkSize.x - 1, isVertical: true, fixedHaloIndex: 0),
            (direction: Vector2Int.down, sourceColumn: 0, isVertical: false, fixedHaloIndex: _chunkSize.y + 1),
            (direction: Vector2Int.up, sourceColumn: _chunkSize.y - 1, isVertical: false, fixedHaloIndex: 0)
        };

        foreach (var side in sidesArray)
        {
            Vector2Int neighborPosition = chunkPosition + side.direction;
            MapGenerator neighborChunk = _lifecycleManager.GetActiveOrPendingChunk(neighborPosition);
            
            if (neighborChunk == null) continue;

            var haloUpdateDictionary = new Dictionary<Vector2Int, Tile>();
            int limitCount = side.isVertical ? _chunkSize.y : _chunkSize.x;

            for (int i = 0; i < limitCount; i++)
            {
                Tile tile = side.isVertical ? newChunk.GetTileAt(side.sourceColumn, i) : newChunk.GetTileAt(i, side.sourceColumn);
                if (tile == null) continue;

                Vector2Int haloCoordinate = side.isVertical 
                    ? new Vector2Int(side.fixedHaloIndex, i + 1) 
                    : new Vector2Int(i + 1, side.fixedHaloIndex);

                haloUpdateDictionary[haloCoordinate] = tile;
            }

            if (haloUpdateDictionary.Count > 0)
            {
                neighborChunk.UpdateHaloAndRepropagate(haloUpdateDictionary);
            }
        }
    }
}