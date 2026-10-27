using System;
using System.Collections.Generic;
using UnityEngine;
using Aegir.World.V2.Core;
using Aegir.World.V2.Terrain;
using Aegir.World.V2.Settlements;

namespace Aegir.World.V2.Pipeline
{
    /// <summary>
    /// Pipeline unificado de geração de chunks V2.
    /// Combina autotiling dual-grid de vértices, malha viária e estruturas determinísticas
    /// via IslandSettlementPlanner, com reconciliação final de compatibilidade de sockets.
    /// </summary>
    public class ChunkPipelineV2 : IChunkGeneratorV2
    {
        private readonly Vector2Int _chunkSize;
        private readonly TilesetData _tilesetData;
        private readonly RuleManager _ruleManager;
        private readonly int _worldSeed;
        private readonly float _cellSize;

        private readonly FastNoiseSampler _noiseSampler;
        private readonly IslandMapSampler _islandSampler;
        private readonly IslandLocator _islandLocator;
        private readonly IslandSettlementPlanner _settlementPlanner;
        private readonly DeterministicLayerGenerator _layerGenerator;

        private readonly Dictionary<Vector2Int, Tile[,]> _chunkTilesCache = new Dictionary<Vector2Int, Tile[,]>();
        private readonly int _defaultWaterIndex;

        public ChunkPipelineV2(
            Vector2Int chunkSize,
            TilesetData tilesetData,
            RuleManager ruleManager,
            List<StructureData> structuresList,
            int worldSeed,
            float cellSize = 1f,
            float settlementChance = 0.5f,
            int minIslandChunks = 2)
        {
            _chunkSize = chunkSize;
            _tilesetData = tilesetData ?? throw new ArgumentNullException(nameof(tilesetData));
            _ruleManager = ruleManager;
            _worldSeed = worldSeed;
            _cellSize = cellSize > 0 ? cellSize : 1f;

            Tile waterTile = TileSocketResolver.FindTile(_tilesetData, 0, Tile.TileType.Block, Tile.TileDirection.None);
            int waterIdx = waterTile != null ? _tilesetData.TilesetList.FindIndex(t => ReferenceEquals(t, waterTile)) : 0;
            _defaultWaterIndex = waterIdx >= 0 ? waterIdx : 0;

            _noiseSampler = new FastNoiseSampler(worldSeed);
            _layerGenerator = new DeterministicLayerGenerator(_noiseSampler, chunkSize);

            _islandSampler = _noiseSampler;
            _islandLocator = new IslandLocator(_islandSampler, chunkSize);
            _settlementPlanner = new IslandSettlementPlanner(
                _islandSampler,
                _islandLocator,
                chunkSize,
                worldSeed,
                structuresList ?? new List<StructureData>(),
                GetTileAtGlobal,
                settlementChance,
                minIslandChunks
            );
        }

        public Tile GetTileAtGlobal(int globalX, int globalY)
        {
            int cx = Mathf.FloorToInt((float)globalX / _chunkSize.x);
            int cy = Mathf.FloorToInt((float)globalY / _chunkSize.y);
            Vector2Int chunkCoord = new Vector2Int(cx, cy);

            if (_chunkTilesCache.TryGetValue(chunkCoord, out var cachedTiles))
            {
                int lx = (globalX % _chunkSize.x + _chunkSize.x) % _chunkSize.x;
                int ly = (globalY % _chunkSize.y + _chunkSize.y) % _chunkSize.y;
                return cachedTiles[lx, ly];
            }

            // Avalia de forma determinística via vértices se a chunk ainda não estiver em cache
            int sw = _layerGenerator.CalculateSolidLayer(globalX, globalY, _worldSeed);
            int se = _layerGenerator.CalculateSolidLayer(globalX + 1, globalY, _worldSeed);
            int nw = _layerGenerator.CalculateSolidLayer(globalX, globalY + 1, _worldSeed);
            int ne = _layerGenerator.CalculateSolidLayer(globalX + 1, globalY + 1, _worldSeed);

            return TileSocketResolver.ResolveFromSockets(_tilesetData, nw, ne, sw, se);
        }

        public ChunkResult GenerateChunk(Vector2Int chunkCoordinate, int worldSeed)
        {
            var result = new ChunkResult
            {
                ChunkCoordinate = chunkCoordinate,
                TileIndices = new int[_chunkSize.x, _chunkSize.y],
                IsSuccess = false
            };

            int baseGX = chunkCoordinate.x * _chunkSize.x;
            int baseGY = chunkCoordinate.y * _chunkSize.y;

            // 1. Gerar terreno base com autotiling dual-grid (garante sockets perfeitos nas quinas e costas)
            Tile[,] tiles = _layerGenerator.GenerateTerrainTiles(chunkCoordinate, worldSeed, _tilesetData);

            // 2. Consultar o plano determinístico de assentamento da ilha
            IslandSettlementPlan plan = _settlementPlanner.GetPlanForChunk(chunkCoordinate);

            if (plan != null)
            {
                // 3. Esculpir caminhos / estradas no terreno
                if (plan.RoadTilesByChunk.TryGetValue(chunkCoordinate, out var roadTiles))
                {
                    foreach (PlannedRoadTile roadTile in roadTiles)
                    {
                        if (!roadTile.IsVisual) continue;

                        int lx = roadTile.Coordinate.x - baseGX;
                        int ly = roadTile.Coordinate.y - baseGY;

                        if (lx < 0 || lx >= _chunkSize.x || ly < 0 || ly >= _chunkSize.y) continue;

                        Tile existingTile = tiles[lx, ly];
                        // Estradas visuais só substituem terra firme (camada 4)
                        if (existingTile != null && existingTile.Metadata.Layer < 4) continue;

                        Tile targetRoad = TileSocketResolver.FindTile(_tilesetData, roadTile.Layer, roadTile.TileType, roadTile.Direction);
                        if (targetRoad != null)
                        {
                            tiles[lx, ly] = targetRoad;
                        }
                    }
                }

                // 4. Registrar estruturas planejadas para spawn
                if (plan.StructuresByChunk.TryGetValue(chunkCoordinate, out var structures))
                {
                    foreach (PlannedStructure planned in structures)
                    {
                        float posX = (planned.GlobalTileOrigin.x * _cellSize) + (planned.Dimensions.x * _cellSize) / 2f;
                        float posY = (planned.Blueprint.PivotMode == StructurePivotMode.BottomCenter)
                            ? (planned.GlobalTileOrigin.y * _cellSize)
                            : (planned.GlobalTileOrigin.y * _cellSize) + (planned.Dimensions.y * _cellSize) / 2f;

                        Vector3 worldPos = new Vector3(posX, posY, 0f);

                        result.Structures.Add(new PlannedStructureInstance
                        {
                            Blueprint = planned.Blueprint,
                            GlobalTileOrigin = planned.GlobalTileOrigin,
                            Dimensions = planned.Dimensions,
                            ChunkPosition = chunkCoordinate,
                            DoorGlobalCoordinate = planned.DoorGlobalCoordinate,
                            WorldPosition = worldPos
                        });
                    }
                }
            }

            // 5. Reconciliar compatibilidade de sockets nas transições via-costa e bordas
            ReconcileChunkSockets(chunkCoordinate, tiles);

            // 6. Armazenar em cache e exportar índices para o resultado final
            _chunkTilesCache[chunkCoordinate] = tiles;

            for (int x = 0; x < _chunkSize.x; x++)
            {
                for (int y = 0; y < _chunkSize.y; y++)
                {
                    Tile tile = tiles[x, y];
                    int tileIndex = tile != null ? _tilesetData.TilesetList.FindIndex(t => ReferenceEquals(t, tile)) : -1;
                    result.TileIndices[x, y] = tileIndex >= 0 ? tileIndex : _defaultWaterIndex;
                }
            }

            result.IsSuccess = true;
            return result;
        }

        private void ReconcileChunkSockets(Vector2Int chunkCoordinate, Tile[,] tiles)
        {
            int baseGX = chunkCoordinate.x * _chunkSize.x;
            int baseGY = chunkCoordinate.y * _chunkSize.y;
            Vector2Int[] cardinalDirs = { Vector2Int.up, Vector2Int.down, Vector2Int.left, Vector2Int.right };

            // 1. Resolver anomalias morfológicas de costas duplicadas
            for (int x = 0; x < _chunkSize.x; x++)
            {
                for (int y = 0; y < _chunkSize.y; y++)
                {
                    Tile current = tiles[x, y];
                    if (current == null || current.Metadata.Layer != 3 || current.Metadata.Type != Tile.TileType.Coast) continue;

                    if (current.Metadata.Direction == Tile.TileDirection.North && y > 0)
                    {
                        Tile below = tiles[x, y - 1];
                        if (below != null && below.Metadata.Layer == 3 && below.Metadata.Type == Tile.TileType.Coast && below.Metadata.Direction == Tile.TileDirection.North)
                        {
                            Tile sandBlock = TileSocketResolver.FindTile(_tilesetData, 2, Tile.TileType.Block, Tile.TileDirection.None);
                            if (sandBlock != null) tiles[x, y] = sandBlock;
                        }
                    }
                    else if (current.Metadata.Direction == Tile.TileDirection.South && y < _chunkSize.y - 1)
                    {
                        Tile above = tiles[x, y + 1];
                        if (above != null && above.Metadata.Layer == 3 && above.Metadata.Type == Tile.TileType.Coast && above.Metadata.Direction == Tile.TileDirection.South)
                        {
                            Tile sandBlock = TileSocketResolver.FindTile(_tilesetData, 2, Tile.TileType.Block, Tile.TileDirection.None);
                            if (sandBlock != null) tiles[x, y] = sandBlock;
                        }
                    }
                }
            }

            // 2. Loop de relaxação iterativa (Gauss-Seidel) para convergência de sockets
            const int maxPasses = 15;
            bool anyChanged;
            int pass = 0;

            do
            {
                anyChanged = false;
                pass++;

                for (int x = 0; x < _chunkSize.x; x++)
                {
                    for (int y = 0; y < _chunkSize.y; y++)
                    {
                        Tile current = tiles[x, y];
                        if (current == null) continue;

                        var neighborList = new List<(Vector2Int dir, Tile neighbor)>(4);
                        bool hasMismatch = false;

                        foreach (var dir in cardinalDirs)
                        {
                            int nx = x + dir.x;
                            int ny = y + dir.y;
                            Tile neighborTile = null;

                            if (nx >= 0 && nx < _chunkSize.x && ny >= 0 && ny < _chunkSize.y)
                            {
                                neighborTile = tiles[nx, ny];
                            }
                            else
                            {
                                int ngx = baseGX + nx;
                                int ngy = baseGY + ny;
                                int nsw = _layerGenerator.CalculateSolidLayer(ngx, ngy, _worldSeed);
                                int nse = _layerGenerator.CalculateSolidLayer(ngx + 1, ngy, _worldSeed);
                                int nnw = _layerGenerator.CalculateSolidLayer(ngx, ngy + 1, _worldSeed);
                                int nne = _layerGenerator.CalculateSolidLayer(ngx + 1, ngy + 1, _worldSeed);
                                neighborTile = TileSocketResolver.ResolveFromSockets(_tilesetData, nnw, nne, nsw, nse);
                            }

                            if (neighborTile != null)
                            {
                                neighborList.Add((dir, neighborTile));
                                if (TileSocketResolver.HasCompatibilityError(current, neighborTile, dir))
                                {
                                    hasMismatch = true;
                                }
                            }
                        }

                        if (hasMismatch)
                        {
                            Tile best = TileSocketResolver.FindCompatibleTile(current, neighborList, _tilesetData, allowRelaxed: true);
                            if (best != null && !ReferenceEquals(best, current))
                            {
                                tiles[x, y] = best;
                                anyChanged = true;
                            }
                        }
                    }
                }
            } while (anyChanged && pass < maxPasses);
        }
    }
}
