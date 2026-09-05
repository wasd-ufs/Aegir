using UnityEngine;

/// <summary>
/// Serviço de consulta espacial que mapeia posições globais do mundo Unity
/// para coordenadas locais de tiles e chunks, permitindo verificar tipo de terreno.
/// </summary>
public class WorldTileQuery
{
    private ChunkLifecycleManager _lifecycleManager;
    private Vector2Int _chunkSize;
    private float _cachedCellSize;

    public WorldTileQuery(ChunkLifecycleManager lifecycleManager, Vector2Int chunkSize, float cachedCellSize)
    {
        _lifecycleManager = lifecycleManager;
        _chunkSize = chunkSize;
        _cachedCellSize = cachedCellSize;
    }

    public Vector3 GetTileWorldPosition(Vector2Int chunkPosition, int localX, int localY)
    {
        Vector3 chunkOrigin = GetChunkWorldPosition(chunkPosition);
        return new Vector3(
            chunkOrigin.x + localX * _cachedCellSize,
            chunkOrigin.y + localY * _cachedCellSize,
            0
        );
    }

    public Vector3 GetChunkWorldPosition(Vector2Int chunkPosition)
    {
        return new Vector3(
            chunkPosition.x * _chunkSize.x * _cachedCellSize,
            chunkPosition.y * _chunkSize.y * _cachedCellSize,
            0
        );
    }

    public Vector2Int GetChunkPositionFromWorld(Vector3 worldPosition)
    {
        return new Vector2Int(
            Mathf.FloorToInt(worldPosition.x / (_chunkSize.x * _cachedCellSize)),
            Mathf.FloorToInt(worldPosition.y / (_chunkSize.y * _cachedCellSize))
        );
    }

    public Tile GetTileAtWorldPosition(Vector3 worldPosition)
    {
        Vector2Int chunkPosition = GetChunkPositionFromWorld(worldPosition);
        MapGenerator chunk = _lifecycleManager.GetActiveChunk(chunkPosition);
        
        if (chunk == null) return null;

        float relativeX = worldPosition.x - chunk.transform.position.x;
        float relativeY = worldPosition.y - chunk.transform.position.y;

        int localX = Mathf.Clamp(Mathf.FloorToInt(relativeX / _cachedCellSize), 0, _chunkSize.x - 1);
        int localY = Mathf.Clamp(Mathf.FloorToInt(relativeY / _cachedCellSize), 0, _chunkSize.y - 1);

        return chunk.GetTileAt(localX, localY);
    }
    
    public Vector2Int GetPlayerChunkPosition(Transform playerTransform)
    {
        return GetChunkPositionFromWorld(playerTransform.position);
    }
}