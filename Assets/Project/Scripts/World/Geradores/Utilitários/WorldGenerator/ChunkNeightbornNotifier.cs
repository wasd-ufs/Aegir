using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Notifica chunks vizinhos quando um novo chunk termina de ser gerado,
/// enviando suas bordas via <see cref="MapGenerator.UpdateHaloAndRepropagate"/>.
/// Isso permite que vizinhos restrinjam suas células internas de borda
/// com os tiles recém-definidos.
/// </summary>
public class ChunkNeighborNotifier
{
    // =========================================================================
    // Campos Privados
    // =========================================================================

    private readonly ChunkLifecycleManager _lifecycleManager;
    private readonly Vector2Int _chunkSize;

    // =========================================================================
    // Inicialização
    // =========================================================================

    public ChunkNeighborNotifier(ChunkLifecycleManager lifecycleManager, Vector2Int chunkSize)
    {
        _lifecycleManager = lifecycleManager;
        _chunkSize        = chunkSize;
    }

    // =========================================================================
    // API Pública
    // =========================================================================

    /// <summary>
    /// Envia as bordas do chunk recém-gerado para cada vizinho ativo ou pendente.
    /// </summary>
    public void NotifyNeighbors(Vector2Int chunkPosition, MapGenerator newChunk)
    {
        var sidesArray = new[]
        {
            (direction: Vector2Int.left,  sourceColumn: 0,                isVertical: true,  fixedHaloIndex: _chunkSize.x + 1),
            (direction: Vector2Int.right, sourceColumn: _chunkSize.x - 1, isVertical: true,  fixedHaloIndex: 0),
            (direction: Vector2Int.down,  sourceColumn: 0,                isVertical: false, fixedHaloIndex: _chunkSize.y + 1),
            (direction: Vector2Int.up,    sourceColumn: _chunkSize.y - 1, isVertical: false, fixedHaloIndex: 0)
        };

        foreach (var side in sidesArray)
        {
            Vector2Int neighborPosition = chunkPosition + side.direction;
            MapGenerator neighborChunk  = _lifecycleManager.GetActiveOrPendingChunk(neighborPosition);

            if (neighborChunk == null) continue;

            var haloUpdateDictionary = BuildHaloUpdate(newChunk, side.sourceColumn, side.isVertical, side.fixedHaloIndex);

            if (haloUpdateDictionary.Count > 0)
                neighborChunk.UpdateHaloAndRepropagate(haloUpdateDictionary);
        }
    }

    // =========================================================================
    // Helpers Privados
    // =========================================================================

    /// <summary>
    /// Lê os tiles da borda do chunk recém-gerado e os mapeia para as
    /// coordenadas de halo correspondentes no chunk vizinho.
    /// </summary>
    private Dictionary<Vector2Int, Tile> BuildHaloUpdate(
        MapGenerator sourceChunk,
        int sourceColumn,
        bool isVertical,
        int fixedHaloIndex)
    {
        var haloUpdateDictionary = new Dictionary<Vector2Int, Tile>();
        int limitCount = isVertical ? _chunkSize.y : _chunkSize.x;

        for (int i = 0; i < limitCount; i++)
        {
            Tile tile = isVertical
                ? sourceChunk.GetTileAt(sourceColumn, i)
                : sourceChunk.GetTileAt(i, sourceColumn);

            if (tile == null) continue;

            Vector2Int haloCoordinate = isVertical
                ? new Vector2Int(fixedHaloIndex, i + 1)
                : new Vector2Int(i + 1, fixedHaloIndex);

            haloUpdateDictionary[haloCoordinate] = tile;
        }

        return haloUpdateDictionary;
    }
}