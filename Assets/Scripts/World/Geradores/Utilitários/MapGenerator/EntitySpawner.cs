using UnityEngine;
using UnityEngine.Tilemaps;

/// <summary>
/// Responsável por ler as probabilidades de vida selvagem/NPCs de cada tile colapsado e 
/// instanciar os prefabs correspondentes.
/// </summary>
public class EntitySpawner : MonoBehaviour
{
    [SerializeField] private int _maxCreaturesPerChunk = 10;
    [SerializeField] private TilesetData _tilesetData;


    ///<summary>
    /// Itera sobre a matriz final já colapsada para invocar a criação das entidades correspondentes 
    ///</summary>
    public void SpawnEntities(ChunkCellGrid grid, Vector2Int chunkSize, Tilemap tilemap, Transform creaturesContainer, GameObject player, WorldGenerator worldGenerator)
    {
        int totalSpawnedCount = 0;

        for (int x = 1; x <= chunkSize.x; x++)
        {
            for (int y = 1; y <= chunkSize.y; y++)
            {
                if (totalSpawnedCount >= _maxCreaturesPerChunk) return;
                
                Cell cell = grid.CellsArray[x, y];
                
                if (!cell.IsCollapsed()) continue; 
                
                InstantiateCreatureForCell(cell, x, y, tilemap, creaturesContainer, player, worldGenerator, ref totalSpawnedCount);
            }
        }
    }

    /// <summary>
    /// Processa a lista de criaturas permitidas para o tile específico e as instancia no mundo, aplicando pequenas variações aleatórias na posição.
    /// </summary>
    private void InstantiateCreatureForCell(Cell cell, int cellX, int cellY, Tilemap tilemap, Transform creaturesContainer, GameObject player, WorldGenerator worldGenerator, ref int spawnCount)
    {
        int tileIndex = cell.CollapsedIndex();
        Vector3 basePosition = tilemap.GetCellCenterWorld(new Vector3Int(cellX - 1, cellY - 1, 0));
        Tile tile = _tilesetData.TilesetList[tileIndex];
        
        foreach (var spawnableEntry in tile.SpawnableCreaturesList)
        {
            if (Random.value > spawnableEntry.SpawnChance) continue;

            for (int j = 0; j < spawnableEntry.Quantity; j++)
            {
                Vector3 finalPosition = basePosition + new Vector3(
                    Random.Range(-0.2f, 0.2f),
                    Random.Range(-0.2f, 0.2f),
                    0
                );

                GameObject creatureInstance = Instantiate(spawnableEntry.CreaturePrefab, finalPosition, Quaternion.identity, creaturesContainer);
                
                NPCsMovement npcMovement = creatureInstance.GetComponent<NPCsMovement>();
                if (npcMovement != null)
                {
                    npcMovement.Setup(player, worldGenerator);
                }

                spawnCount++;
            }
        }
    }
}