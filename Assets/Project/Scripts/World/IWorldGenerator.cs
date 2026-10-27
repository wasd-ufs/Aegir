using UnityEngine;

/// <summary>
/// Contrato unificado de consulta do mundo procedural.
/// Permite que PlayerMovement, BoatState, CaptainState e NPCsMovement naveguem
/// e interajam com o mundo de forma desacoplada, suportando WorldGenerator e WorldGeneratorV2.
/// </summary>
public interface IWorldGenerator
{
    Tile GetTileAtWorldPosition(Vector3 worldPosition);
    Vector2Int GetChunkPosFromWorld(Vector3 worldPosition);
    bool IsChunkActive(Vector2Int chunkPosition);
    void TryGoOut(Camera mainCamera);
    Transform CreaturesContainer { get; }
    Transform PlayerTransform { get; }
    float CellSize { get; }
}
