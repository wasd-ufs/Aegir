using System.Collections.Generic;
using UnityEngine;

public class ChunkGenerationQueue
{
    public Vector2Int? CurrentlyGenerating { get; set; }
    
    private List<Vector2Int> _generationQueueList = new List<Vector2Int>();

    public void EnqueueChunk(Vector2Int position, Vector2Int playerChunkPosition)
    {
        if (!_generationQueueList.Contains(position))
        {
            _generationQueueList.Add(position);
            SortQueueByDistance(playerChunkPosition);
        }
    }

    public void SortQueueByDistance(Vector2Int playerChunkPosition)
    {
        _generationQueueList.Sort((a, b) => 
            (a - playerChunkPosition).sqrMagnitude.CompareTo((b - playerChunkPosition).sqrMagnitude));
    }

    public bool TryGetNext(out Vector2Int nextPosition)
    {
        if (_generationQueueList.Count > 0)
        {
            nextPosition = _generationQueueList[0];
            _generationQueueList.RemoveAt(0);
            return true;
        }
        nextPosition = Vector2Int.zero;
        return false;
    }

    public void RemoveAll(System.Predicate<Vector2Int> match)
    {
        _generationQueueList.RemoveAll(match);
    }

    public bool Contains(Vector2Int position)
    {
        return _generationQueueList.Contains(position);
    }
}