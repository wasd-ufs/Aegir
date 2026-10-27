using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Tilemaps;
using Aegir.World.V2.Pipeline;

namespace Aegir.World.V2.Runtime
{
    /// <summary>
    /// Componente de visualização e apresentação de um chunk V2.
    /// Suporta reciclagem via Object Pooling, renderização em Tilemap e gerenciamento de estruturas filhas.
    /// </summary>
    public class ChunkViewV2 : MonoBehaviour
    {
        [SerializeField] private Tilemap _tilemap;

        public Tilemap Tilemap => _tilemap;
        public Vector2Int ChunkCoordinate { get; private set; }

        private readonly List<GameObject> _spawnedStructures = new List<GameObject>();
        private TilesetData _tilesetData;

        public void Initialize(Tilemap tilemap, TilesetData tilesetData)
        {
            _tilemap = tilemap;
            _tilesetData = tilesetData;
        }

        public void ApplyChunkResult(ChunkResult result, TilesetData tilesetData, Transform structuresContainer)
        {
            _tilesetData = tilesetData;
            ChunkCoordinate = result.ChunkCoordinate;

            ClearSpawnedStructures();

            if (_tilemap != null && tilesetData != null)
            {
                _tilemap.ClearAllTiles();

                int width = result.TileIndices.GetLength(0);
                int height = result.TileIndices.GetLength(1);

                for (int x = 0; x < width; x++)
                {
                    for (int y = 0; y < height; y++)
                    {
                        int tileIndex = result.TileIndices[x, y];
                        if (tileIndex >= 0 && tileIndex < tilesetData.TilesetList.Count)
                        {
                            Tile tile = tilesetData.TilesetList[tileIndex];
                            if (tile != null && tile.TilemapTile != null)
                            {
                                _tilemap.SetTile(new Vector3Int(x, y, 0), tile.TilemapTile);
                            }
                        }
                    }
                }
            }

            // Instancia estruturas planejadas
            if (result.Structures != null && structuresContainer != null)
            {
                foreach (var structure in result.Structures)
                {
                    if (structure.Blueprint != null && structure.Blueprint.StructurePrefab != null)
                    {
                        GameObject instance = Instantiate(
                            structure.Blueprint.StructurePrefab,
                            structure.WorldPosition,
                            Quaternion.identity,
                            structuresContainer
                        );
                        _spawnedStructures.Add(instance);
                    }
                }
            }
        }

        public void ClearSpawnedStructures()
        {
            for (int i = _spawnedStructures.Count - 1; i >= 0; i--)
            {
                if (_spawnedStructures[i] != null)
                {
                    if (Application.isPlaying) Destroy(_spawnedStructures[i]);
                    else DestroyImmediate(_spawnedStructures[i]);
                }
            }
            _spawnedStructures.Clear();
        }

        public void ResetForPool()
        {
            ClearSpawnedStructures();
            if (_tilemap != null)
            {
                _tilemap.ClearAllTiles();
            }
            gameObject.SetActive(false);
        }
    }
}
