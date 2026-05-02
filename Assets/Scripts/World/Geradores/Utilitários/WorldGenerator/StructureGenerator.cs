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
    }

    public List<StructureSaveData> SavedStructuresList { get; private set; } = new List<StructureSaveData>();

    private WorldTileQuery _tileQuery;
    private Vector2Int _chunkSize;
    private float _cachedCellSize;
    private ChunkLifecycleManager _lifecycleManager;

    public void Setup(WorldTileQuery tileQuery, ChunkLifecycleManager lifecycleManager, Vector2Int chunkSize, float cachedCellSize)
    {
        _tileQuery = tileQuery;
        _lifecycleManager = lifecycleManager;
        _chunkSize = chunkSize;
        _cachedCellSize = cachedCellSize;
    }

    ///<summary>
    /// Ponto de entrada que verifica quais os chunks que já têm todos os vizinhos prontos 
    /// para começarem a receber as decorações 
    ///</summary>

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

    /// <summary>
    /// Verifica se os 8 chunks adjacentes já terminaram de ser gerados para evitar que decorações sejam cortadas nas bordas.
    /// </summary>
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

    /// <summary>
    /// Percorre a grelha do chunk e tenta posicionar cada blueprint da lista, respeitando as probabilidades de spawn.
    /// </summary>
    private void ScanAndGenerateStructures(Vector2Int chunkPosition)
    {
        foreach (StructureData blueprint in _structuresList)
        {
            if (UnityEngine.Random.value > blueprint.SpawnChance) continue;

            bool hasGenerated = false;

            for (int x = 0; x < _chunkSize.x && !hasGenerated; x++)
            {
                for (int y = 0; y < _chunkSize.y && !hasGenerated; y++)
                {
                    Vector3 worldPosition = _tileQuery.GetTileWorldPosition(chunkPosition, x, y);

                    if (ValidateBlueprint(worldPosition, blueprint))
                    {
                        worldPosition.x += (blueprint.StructureDimensions.x - 1) * _cachedCellSize / 2f;
                        worldPosition.y += (blueprint.StructureDimensions.y - 1) * _cachedCellSize / 2f;

                        Instantiate(blueprint.StructurePrefab, worldPosition, Quaternion.identity, _structuresContainer);
                        RegisterStructure(blueprint.StructureName, worldPosition, blueprint.IsolationRadius);
                        hasGenerated = true;
                    }
                }
            }
        }
    }

    /// <summary>
    /// Garante que a estrutura cabe no local desejado, respeitando as camadas exigidas do chão e o raio de isolamento de outras estruturas.
    /// </summary>
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
                {
                    return tileLayer == layerOverride.Layer;
                }
            }
        }
        return blueprint.ValidBaseLayersList.Contains(tileLayer);
    }

    private void RegisterStructure(string name, Vector3 position, float isolationRadius)
    {
        SavedStructuresList.Add(new StructureSaveData
        {
            structureName = name,
            structureWorldPosition = position,
            isolationRadius = isolationRadius
        });
    }
}