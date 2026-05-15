using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Determina quais chunks devem estar visíveis ao redor do jogador e
/// notifica o <see cref="ChunkLifecycleManager"/> sobre o que criar ou remover.
/// <para>
/// Responsabilidade única: calcular o conjunto de coordenadas dentro do
/// <c>viewDistance</c> e compará-lo com o que já está ativo — sem instanciar,
/// salvar ou destruir nada diretamente.
/// </para>
/// </summary>
public class ChunkVisibilityTracker
{
    // =========================================================================
    // Campos Privados
    // =========================================================================

    private readonly ChunkLifecycleManager _lifecycleManager;
    private readonly int _viewDistance;

    // =========================================================================
    // Inicialização
    // =========================================================================

    public ChunkVisibilityTracker(ChunkLifecycleManager lifecycleManager, int viewDistance)
    {
        _lifecycleManager = lifecycleManager;
        _viewDistance     = viewDistance;
    }

    // =========================================================================
    // API Pública
    // =========================================================================

    /// <summary>
    /// Calcula os chunks que devem estar visíveis a partir de <paramref name="centerPosition"/>
    /// e delega ao <see cref="ChunkLifecycleManager"/> as ações de enfileirar novos chunks
    /// e remover os que saíram do campo de visão.
    /// </summary>
    public void UpdateVisibleChunks(Vector2Int centerPosition, ChunkGenerationQueue queue)
    {
        HashSet<Vector2Int> visibleCoordinatesSet = BuildVisibleSet(centerPosition);

        EnqueueMissingChunks(visibleCoordinatesSet, centerPosition, queue);
        RemoveChunksOutOfRange(visibleCoordinatesSet, queue);

        // Remove da fila posições que já não estão mais no campo de visão
        queue.RemoveAll(position => !visibleCoordinatesSet.Contains(position));
    }

    // =========================================================================
    // Helpers Privados
    // =========================================================================

    /// <summary>
    /// Constrói o conjunto de coordenadas dentro do campo de visão.
    /// </summary>
    private HashSet<Vector2Int> BuildVisibleSet(Vector2Int centerPosition)
    {
        var visibleSet = new HashSet<Vector2Int>();

        for (int x = -_viewDistance; x <= _viewDistance; x++)
            for (int y = -_viewDistance; y <= _viewDistance; y++)
                visibleSet.Add(new Vector2Int(centerPosition.x + x, centerPosition.y + y));

        return visibleSet;
    }

    /// <summary>
    /// Enfileira chunks visíveis que ainda não estão ativos, pendentes ou na fila.
    /// </summary>
    private void EnqueueMissingChunks(HashSet<Vector2Int> visibleSet, Vector2Int centerPosition, ChunkGenerationQueue queue)
    {
        foreach (Vector2Int chunkPosition in visibleSet)
        {
            bool isAlreadyActive           = _lifecycleManager.GetActiveChunk(chunkPosition) != null;
            bool isAlreadyPending          = _lifecycleManager.GetActiveOrPendingChunk(chunkPosition) != null;
            bool isAlreadyInQueue          = queue.Contains(chunkPosition);
            bool isCurrentlyBeingGenerated = queue.CurrentlyGenerating == chunkPosition;

            if (!isAlreadyActive && !isAlreadyPending && !isAlreadyInQueue && !isCurrentlyBeingGenerated)
                queue.EnqueueChunk(chunkPosition, centerPosition);
        }
    }

    /// <summary>
    /// Solicita ao <see cref="ChunkLifecycleManager"/> a remoção dos chunks
    /// que saíram do campo de visão.
    /// </summary>
    private void RemoveChunksOutOfRange(HashSet<Vector2Int> visibleSet, ChunkGenerationQueue queue)
    {
        _lifecycleManager.RemoveChunksOutOfRange(visibleSet, queue);
    }
}