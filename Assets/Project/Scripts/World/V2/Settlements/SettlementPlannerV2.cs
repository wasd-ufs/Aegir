using System;
using System.Collections.Generic;
using UnityEngine;
using Aegir.World.V2.Terrain;

namespace Aegir.World.V2.Settlements
{
    /// <summary>
    /// Planejador determinístico de assentamentos V2.
    /// Gera malhas de vilas, portos e estruturas em terra firme antes do colapso do WFC,
    /// permitindo que o terreno e as conexões viárias sejam integrados de forma natural e sem remendos.
    /// </summary>
    public class SettlementPlannerV2
    {
        private readonly FastNoiseSampler _sampler;
        private readonly Vector2Int _chunkSize;
        private readonly int _worldSeed;
        private readonly List<StructureData> _structuresList;
        private readonly TilesetData _tilesetData;

        private readonly Dictionary<Vector2Int, IslandPlanV2> _planCache = new Dictionary<Vector2Int, IslandPlanV2>();
        private readonly Dictionary<Vector2Int, IslandPlanV2> _chunkToPlanCache = new Dictionary<Vector2Int, IslandPlanV2>();

        public SettlementPlannerV2(
            FastNoiseSampler sampler,
            Vector2Int chunkSize,
            int worldSeed,
            List<StructureData> structuresList,
            TilesetData tilesetData)
        {
            _sampler = sampler ?? throw new ArgumentNullException(nameof(sampler));
            _chunkSize = chunkSize;
            _worldSeed = worldSeed;
            _structuresList = structuresList ?? new List<StructureData>();
            _tilesetData = tilesetData;
        }

        public IslandPlanV2 GetPlanForChunk(Vector2Int chunkCoordinate)
        {
            if (_chunkToPlanCache.TryGetValue(chunkCoordinate, out var existingPlan))
            {
                return existingPlan;
            }

            // Descobre o centro da ilha para este chunk
            Vector2Int islandCenter = FindIslandCenterForChunk(chunkCoordinate);
            if (islandCenter == new Vector2Int(int.MinValue, int.MinValue))
            {
                _chunkToPlanCache[chunkCoordinate] = null;
                return null;
            }

            if (!_planCache.TryGetValue(islandCenter, out var plan))
            {
                plan = GeneratePlanForIsland(islandCenter);
                _planCache[islandCenter] = plan;
            }

            _chunkToPlanCache[chunkCoordinate] = plan;
            return plan;
        }

        private Vector2Int FindIslandCenterForChunk(Vector2Int chunkCoordinate)
        {
            // Amostra o centro do chunk para verificar se é terra
            int centerX = chunkCoordinate.x * _chunkSize.x + _chunkSize.x / 2;
            int centerY = chunkCoordinate.y * _chunkSize.y + _chunkSize.y / 2;

            if (!_sampler.IsLand(centerX, centerY))
            {
                // Se o centro não for terra, checa os 4 cantos
                bool anyLand = _sampler.IsLand(chunkCoordinate.x * _chunkSize.x, chunkCoordinate.y * _chunkSize.y) ||
                               _sampler.IsLand((chunkCoordinate.x + 1) * _chunkSize.x - 1, chunkCoordinate.y * _chunkSize.y) ||
                               _sampler.IsLand(chunkCoordinate.x * _chunkSize.x, (chunkCoordinate.y + 1) * _chunkSize.y - 1) ||
                               _sampler.IsLand((chunkCoordinate.x + 1) * _chunkSize.x - 1, (chunkCoordinate.y + 1) * _chunkSize.y - 1);

                if (!anyLand) return new Vector2Int(int.MinValue, int.MinValue);
            }

            // Clusteriza chunks de terra próximos para um centro comum
            int clusterSize = 4;
            int clusterX = Mathf.FloorToInt((float)chunkCoordinate.x / clusterSize) * clusterSize;
            int clusterY = Mathf.FloorToInt((float)chunkCoordinate.y / clusterSize) * clusterSize;

            return new Vector2Int(clusterX, clusterY);
        }

        private IslandPlanV2 GeneratePlanForIsland(Vector2Int islandCenterChunk)
        {
            var plan = new IslandPlanV2
            {
                CenterChunk = islandCenterChunk,
                CenterTile = new Vector2Int(
                    islandCenterChunk.x * _chunkSize.x + _chunkSize.x / 2,
                    islandCenterChunk.y * _chunkSize.y + _chunkSize.y / 2
                )
            };

            int seed = HashSeed(_worldSeed, islandCenterChunk.x, islandCenterChunk.y);
            var random = new System.Random(seed);

            // 1. Encontrar local ideal para o Porto na borda de transição Terra/Água
            Vector2Int? harborPos = FindShorelinePosition(plan.CenterTile, 30);
            if (harborPos.HasValue)
            {
                StructureData harborBlueprint = FindStructureBlueprint("Harbor") ?? FindStructureBlueprintByCategory(StructureCategory.Service);
                if (harborBlueprint != null)
                {
                    var harborInstance = new PlannedStructureInstance
                    {
                        Blueprint = harborBlueprint,
                        GlobalTileOrigin = harborPos.Value,
                        Dimensions = harborBlueprint.StructureDimensions,
                        ChunkPosition = WorldTileToChunk(harborPos.Value),
                        DoorGlobalCoordinate = harborPos.Value + harborBlueprint.DoorLocalCoordinate,
                        WorldPosition = new Vector3(harborPos.Value.x, harborPos.Value.y, 0)
                    };
                    plan.Harbor = harborInstance;
                    plan.Structures.Add(harborInstance);
                    AddStructureToChunkMap(plan, harborInstance);
                }
            }

            // 2. Traçar eixo principal de estrada (Porto -> Centro ou Cruzamento central)
            Vector2Int roadStart = harborPos.HasValue ? harborPos.Value : plan.CenterTile;
            Vector2Int roadEnd = plan.CenterTile;
            TraceRoadLine(plan, roadStart, roadEnd);

            // 3. Posicionar estruturas ao longo da malha viária
            if (_structuresList != null && _structuresList.Count > 0)
            {
                int structureAttempts = 6;
                for (int i = 0; i < structureAttempts; i++)
                {
                    StructureData blueprint = _structuresList[random.Next(0, _structuresList.Count)];
                    if (blueprint == null || blueprint.StructureName == "Harbor") continue;

                    Vector2Int candidateOrigin = roadStart + new Vector2Int(random.Next(-10, 11), random.Next(-10, 11));
                    if (CanPlaceStructure(plan, blueprint, candidateOrigin))
                    {
                        var instance = new PlannedStructureInstance
                        {
                            Blueprint = blueprint,
                            GlobalTileOrigin = candidateOrigin,
                            Dimensions = blueprint.StructureDimensions,
                            ChunkPosition = WorldTileToChunk(candidateOrigin),
                            DoorGlobalCoordinate = candidateOrigin + blueprint.DoorLocalCoordinate,
                            WorldPosition = new Vector3(candidateOrigin.x, candidateOrigin.y, 0)
                        };

                        plan.Structures.Add(instance);
                        AddStructureToChunkMap(plan, instance);

                        // Conecta a porta da estrutura à estrada
                        TraceRoadLine(plan, instance.DoorGlobalCoordinate, roadEnd);
                    }
                }
            }

            return plan;
        }

        private Vector2Int? FindShorelinePosition(Vector2Int centerTile, int maxRadius)
        {
            for (int r = 4; r <= maxRadius; r += 2)
            {
                for (int dx = -r; dx <= r; dx += 2)
                {
                    for (int dy = -r; dy <= r; dy += 2)
                    {
                        if (Math.Abs(dx) != r && Math.Abs(dy) != r) continue;

                        int gx = centerTile.x + dx;
                        int gy = centerTile.y + dy;

                        if (_sampler.IsLand(gx, gy))
                        {
                            // Checa se algum vizinho é água (borda)
                            if (!_sampler.IsLand(gx + 2, gy) || !_sampler.IsLand(gx - 2, gy) ||
                                !_sampler.IsLand(gx, gy + 2) || !_sampler.IsLand(gx, gy - 2))
                            {
                                return new Vector2Int(gx, gy);
                            }
                        }
                    }
                }
            }
            return null;
        }

        private void TraceRoadLine(IslandPlanV2 plan, Vector2Int from, Vector2Int to)
        {
            int cx = from.x;
            int cy = from.y;

            while (cx != to.x || cy != to.y)
            {
                AddRoadCell(plan, new Vector2Int(cx, cy));

                if (cx < to.x) cx++;
                else if (cx > to.x) cx--;
                else if (cy < to.y) cy++;
                else if (cy > to.y) cy--;
            }
            AddRoadCell(plan, to);
        }

        private void AddRoadCell(IslandPlanV2 plan, Vector2Int coordinate)
        {
            if (plan.Roads.ContainsKey(coordinate)) return;

            var roadCell = new PlannedRoadCell
            {
                GlobalCoordinate = coordinate,
                Layer = 4, // Camada de terra / via
                TileType = Tile.TileType.Block,
                Direction = Tile.TileDirection.None,
                SpecificTileIndex = -1
            };

            plan.Roads[coordinate] = roadCell;

            Vector2Int chunkPos = WorldTileToChunk(coordinate);
            if (!plan.RoadsByChunk.TryGetValue(chunkPos, out var list))
            {
                list = new List<PlannedRoadCell>();
                plan.RoadsByChunk[chunkPos] = list;
            }
            list.Add(roadCell);
        }

        private bool CanPlaceStructure(IslandPlanV2 plan, StructureData blueprint, Vector2Int origin)
        {
            Vector2Int dim = blueprint.StructureDimensions;

            // Checa se todas as células da estrutura estão em terra firme
            for (int x = 0; x < dim.x; x++)
            {
                for (int y = 0; y < dim.y; y++)
                {
                    int gx = origin.x + x;
                    int gy = origin.y + y;
                    if (!_sampler.IsLand(gx, gy)) return false;
                }
            }

            // Checa colisão com outras estruturas planejadas (respeitando isolation radius)
            foreach (var existing in plan.Structures)
            {
                float dist = Vector2.Distance(origin, existing.GlobalTileOrigin);
                float requiredDist = Mathf.Max(blueprint.IsolationRadius, existing.Blueprint.IsolationRadius);
                if (dist < requiredDist) return false;
            }

            return true;
        }

        private void AddStructureToChunkMap(IslandPlanV2 plan, PlannedStructureInstance structure)
        {
            if (!plan.StructuresByChunk.TryGetValue(structure.ChunkPosition, out var list))
            {
                list = new List<PlannedStructureInstance>();
                plan.StructuresByChunk[structure.ChunkPosition] = list;
            }
            list.Add(structure);
        }

        private Vector2Int WorldTileToChunk(Vector2Int globalTile)
        {
            int cx = Mathf.FloorToInt((float)globalTile.x / _chunkSize.x);
            int cy = Mathf.FloorToInt((float)globalTile.y / _chunkSize.y);
            return new Vector2Int(cx, cy);
        }

        private StructureData FindStructureBlueprint(string name)
        {
            return _structuresList.Find(s => s.StructureName.Equals(name, StringComparison.OrdinalIgnoreCase));
        }

        private StructureData FindStructureBlueprintByCategory(StructureCategory category)
        {
            return _structuresList.Find(s => s.Category == category);
        }

        private static int HashSeed(int seed, int x, int y)
        {
            uint hash = (uint)seed * 2654435761u;
            hash ^= (uint)(x * 1664525 + 1013904223);
            hash ^= (uint)(y * 22695477 + 1664525);
            hash ^= hash >> 16;
            hash *= 0x45d9f3b;
            hash ^= hash >> 16;
            return (int)(hash & 0x7FFFFFFF);
        }
    }
}
