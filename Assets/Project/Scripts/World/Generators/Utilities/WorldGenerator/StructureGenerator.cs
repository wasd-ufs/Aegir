using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Avalia a matriz colapsada e tenta instanciar "plantas" (blueprints) de estruturas
/// respeitando raios de isolamento e sobreposições de camadas.
/// </summary>
public class StructureGenerator : MonoBehaviour
{
    [SerializeField] private List<StructureData> _structuresList;
    [SerializeField] private Transform _structuresContainer;

    [Serializable]
    public struct StructureSaveData
    {
        public string structureName;
        public Vector3 structureWorldPosition;
        public float isolationRadius;
        public Vector2Int chunkPosition; 
    }

    public List<StructureSaveData> SavedStructuresList { get; private set; } = new List<StructureSaveData>();

    private WorldTileQuery _tileQuery;
    private Vector2Int _chunkSize;
    private float _cachedCellSize;
    private ChunkLifecycleManager _lifecycleManager;
    private int _worldSeed; 

    public void Setup(WorldTileQuery tileQuery, ChunkLifecycleManager lifecycleManager, Vector2Int chunkSize, float cachedCellSize, int worldSeed)
    {
        _tileQuery = tileQuery;
        _lifecycleManager = lifecycleManager;
        _chunkSize = chunkSize;
        _cachedCellSize = cachedCellSize;
        _worldSeed = worldSeed; // NOVO
    }

    // API Pública — Limpeza por Chunk

    /// <summary>
    /// Remove da lista todas as estruturas que pertencem ao chunk destruído.
    /// Chamado pelo ChunkLifecycleManager em SaveAndDestroy.
    /// </summary>
    public void ClearStructuresForChunk(Vector2Int chunkPosition)
    {
        SavedStructuresList.RemoveAll(s => s.chunkPosition == chunkPosition);
    }

    // Processamento

    public void ProcessDecorations()
    {
        var waitingChunksList = _lifecycleManager.GetChunksWaitingForDecoration();

        for (int i = waitingChunksList.Count - 1; i >= 0; i--)
        {
            if (AreAllNeighborsReady(waitingChunksList[i]))
            {
                ScanAndGenerateStructures(waitingChunksList[i]);
                _lifecycleManager.RemoveChunkWaitingForDecoration(waitingChunksList[i]);
            }
        }
    }

    private bool AreAllNeighborsReady(Vector2Int chunkPosition)
    {
        for (int x = -1; x <= 1; x++)
        {
            for (int y = -1; y <= 1; y++)
            {
                Vector2Int neighborPosition = chunkPosition + new Vector2Int(x, y);
                MapGenerator chunk = _lifecycleManager.GetActiveChunk(neighborPosition);

                if (chunk == null || chunk.IsGenerating) return false;
            }
        }
        return true;
    }

    // Geração — determinística e com shuffle

    private void ScanAndGenerateStructures(Vector2Int chunkPosition)
    {
        // random determinístico por chunk — mesma seed → mesmas estruturas
        System.Random chunkRandom = new System.Random(HashChunkSeed(_worldSeed, chunkPosition.x, chunkPosition.y));

        // lista de índices embaralhada para varredura não-sequencial
        List<int> shuffledIndices = BuildShuffledIndices(_chunkSize.x * _chunkSize.y, chunkRandom);

        foreach (StructureData blueprint in _structuresList)
        {
            int placedCount = 0;

            foreach (int flatIndex in shuffledIndices)
            {
                if (placedCount >= blueprint.MaxPerChunk) break;

                if (chunkRandom.NextDouble() > blueprint.SpawnChance) continue;

                int x = flatIndex / _chunkSize.y;
                int y = flatIndex % _chunkSize.y;

                Vector3 worldPosition = _tileQuery.GetTileWorldPosition(chunkPosition, x, y);

                if (ValidateBlueprint(worldPosition, blueprint))
                {
                    worldPosition.x += (blueprint.StructureDimensions.x - 1) * _cachedCellSize / 2f;

                    Instantiate(blueprint.StructurePrefab, worldPosition, Quaternion.identity, _structuresContainer);
                    RegisterStructure(blueprint.StructureName, worldPosition, blueprint.IsolationRadius, chunkPosition); // passa chunkPosition
                    placedCount++;
                }
            }
        }
    }

    // Validação

    private bool ValidateBlueprint(Vector3 initialWorldPosition, StructureData blueprint)
    {
        foreach (var savedStructure in SavedStructuresList)
        {
            float distance = Vector3.Distance(savedStructure.structureWorldPosition, initialWorldPosition);
            float minimumRadius = Mathf.Max(savedStructure.isolationRadius, blueprint.IsolationRadius);
            if (distance < minimumRadius) return false;
        }

        for (int x = 0; x < blueprint.StructureDimensions.x; x++)
        {
            for (int y = 0; y < blueprint.StructureDimensions.y; y++)
            {
                Vector3 tilePosition = initialWorldPosition + new Vector3(x * _cachedCellSize, y * _cachedCellSize, 0);
                Tile tile = _tileQuery.GetTileAtWorldPosition(tilePosition);

                if (tile == null) return false;
                if (!IsTileLayerValidForBlueprint(blueprint, x, y, tile.Metadata.Layer)) return false;
            }
        }

        return true;
    }

    private bool IsTileLayerValidForBlueprint(StructureData blueprint, int localX, int localY, int tileLayer)
    {
        foreach (var layerOverride in blueprint.LayerOverridesList)
        {
            foreach (Vector2Int coordinate in layerOverride.LocalCoordinatesList)
            {
                if (new Vector2Int(localX, localY) == coordinate)
                    return tileLayer == layerOverride.Layer;
            }
        }
        return blueprint.ValidBaseLayersList.Contains(tileLayer);
    }

    // Helpers Privados

    private void RegisterStructure(string name, Vector3 position, float isolationRadius, Vector2Int chunkPosition)
    {
        SavedStructuresList.Add(new StructureSaveData
        {
            structureName = name,
            structureWorldPosition = position,
            isolationRadius = isolationRadius,
            chunkPosition = chunkPosition // NOVO
        });
    }

    /// <summary>
    /// Gera uma lista de índices 0..count-1 embaralhada com o random do chunk.
    /// Fisher-Yates in-place.
    /// </summary>
    private static List<int> BuildShuffledIndices(int count, System.Random random)
    {
        var indices = new List<int>(count);
        for (int i = 0; i < count; i++) indices.Add(i);

        for (int i = count - 1; i > 0; i--)
        {
            int j = random.Next(0, i + 1);
            int temp = indices[i];
            indices[i] = indices[j];
            indices[j] = temp;
        }

        return indices;
    }

    /// <summary>
    /// Mesma função de hash do WFCSolver, replicada aqui para manter
    /// o StructureGenerator independente de outros sistemas.
    /// </summary>
    private static int HashChunkSeed(int worldSeed, int chunkX, int chunkY)
    {
        uint hash = (uint)worldSeed * 2654435761u;
        hash ^= (uint)(chunkX * 1664525 + 1013904223);
        hash ^= (uint)(chunkY * 22695477 + 1664525);
        hash ^= hash >> 16;
        hash *= 0x45d9f3b;
        hash ^= hash >> 16;
        return (int)(hash & 0x7FFFFFFF);
    }
}