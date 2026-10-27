using System;
using UnityEngine;

namespace Aegir.World.V2.Core
{
    /// <summary>
    /// Matriz de compatibilidade otimizada com Bitmasks pré-calculadas.
    /// Utiliza TileSocketResolver para avaliar rigorosamente os CornerSockets de cada tile.
    /// </summary>
    public class CompatibilityMatrixV2
    {
        private readonly int _tileCount;
        private readonly FastBitset128[] _validNeighborMasks;
        private readonly float[] _tileWeights;
        private readonly int[] _tileLayers;

        public int TileCount => _tileCount;

        public CompatibilityMatrixV2(TilesetData tilesetData, RuleManager ruleManager)
        {
            if (tilesetData == null || tilesetData.TilesetList == null)
            {
                throw new ArgumentNullException(nameof(tilesetData));
            }

            _tileCount = tilesetData.TilesetList.Count;
            _validNeighborMasks = new FastBitset128[_tileCount * 4];
            _tileWeights = new float[_tileCount];
            _tileLayers = new int[_tileCount];

            BuildMatrix(tilesetData, ruleManager);
        }

        private void BuildMatrix(TilesetData tilesetData, RuleManager ruleManager)
        {
            var tiles = tilesetData.TilesetList;

            // Extrair pesos e camadas
            for (int i = 0; i < _tileCount; i++)
            {
                Tile tile = tiles[i];
                _tileWeights[i] = tile != null ? Mathf.Max(tile.Weight, 0.0001f) : 1f;
                _tileLayers[i] = tile != null ? tile.Metadata.Layer : 0;
            }

            // Pré-computar máscaras de compatibilidade com TileSocketResolver
            for (int a = 0; a < _tileCount; a++)
            {
                Tile tileA = tiles[a];
                if (tileA == null) continue;

                for (int dir = 0; dir < 4; dir++)
                {
                    CardinalDirection direction = (CardinalDirection)dir;
                    Vector2Int offset = direction.ToOffset();
                    FastBitset128 mask = default;

                    for (int b = 0; b < _tileCount; b++)
                    {
                        Tile tileB = tiles[b];
                        if (tileB == null) continue;

                        bool isCompatible = TileSocketResolver.AreTilesCompatible(tileA, tileB, offset, ruleManager);

                        if (isCompatible)
                        {
                            mask.SetBit(b);
                        }
                    }

                    _validNeighborMasks[a * 4 + dir] = mask;
                }
            }
        }

        public FastBitset128 GetAllowedNeighborsMask(int tileIndex, CardinalDirection direction)
        {
            if ((uint)tileIndex >= (uint)_tileCount) return default;
            return _validNeighborMasks[tileIndex * 4 + (int)direction];
        }

        public float GetTileWeight(int tileIndex)
        {
            if ((uint)tileIndex >= (uint)_tileCount) return 0f;
            return _tileWeights[tileIndex];
        }

        public int GetTileLayer(int tileIndex)
        {
            if ((uint)tileIndex >= (uint)_tileCount) return 0;
            return _tileLayers[tileIndex];
        }

        public FastBitset128 GetTilesForLayer(int targetLayer)
        {
            FastBitset128 mask = default;
            for (int i = 0; i < _tileCount; i++)
            {
                if (_tileLayers[i] == targetLayer)
                {
                    mask.SetBit(i);
                }
            }
            return mask;
        }
    }
}
