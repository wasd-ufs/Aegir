using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEngine;

namespace Aegir.Tests.World
{
    [TestFixture]
    public class IslandSettlementPlannerTests
    {
        private StructureData CreateMockBlueprint(string name, Vector2Int dims, StructureCategory category, float weight)
        {
            StructureData data;
            try
            {
                data = ScriptableObject.CreateInstance<StructureData>();
            }
            catch
            {
                data = (StructureData)System.Runtime.Serialization.FormatterServices.GetUninitializedObject(typeof(StructureData));
            }

            typeof(UnityEngine.Object).GetField("m_InstanceID", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)
                ?.SetValue(data, ++_nextInstanceId);
            typeof(StructureData).GetField("_structureName", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)
                ?.SetValue(data, name);
            typeof(StructureData).GetField("_structureDimensions", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)
                ?.SetValue(data, dims);
            typeof(StructureData).GetField("_category", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)
                ?.SetValue(data, category);
            typeof(StructureData).GetField("_weight", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)
                ?.SetValue(data, weight);
            typeof(StructureData).GetField("_pivotMode", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)
                ?.SetValue(data, StructurePivotMode.BottomCenter);
            typeof(StructureData).GetField("_doorLocalCoordinate", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)
                ?.SetValue(data, new Vector2Int(dims.x / 2, 0));

            return data;
        }

        public class MockDeterministicIslandSampler : IslandMapSampler
        {
            private readonly int _shoreY;
            public MockDeterministicIslandSampler(int shoreY = 20) : base(0)
            {
                _shoreY = shoreY;
            }

            public override float Sample(float globalX, float globalY)
            {
                if (globalY < _shoreY - 5) return 0.20f;
                if (globalY < _shoreY) return 0.40f;
                return 0.85f;
            }
        }

        public class MockRadialIslandSampler : IslandMapSampler
        {
            private readonly Vector2 _center;
            private readonly float _radius;
            public MockRadialIslandSampler(Vector2 center, float radius) : base(0)
            {
                _center = center;
                _radius = radius;
            }

            public override float Sample(float globalX, float globalY)
            {
                float dist = Vector2.Distance(new Vector2(globalX, globalY), _center);
                if (dist > _radius + 6f) return 0.20f;
                if (dist > _radius) return 0.40f;
                return 0.85f;
            }
        }

        [Test]
        public void SettlementPlanner_IsStrictlyDeterministic_SameSeedGivesSamePlan()
        {
            int seed = 42;
            Vector2Int chunkSize = new Vector2Int(20, 20);
            IslandMapSampler sampler1 = new MockDeterministicIslandSampler(20);
            IslandMapSampler sampler2 = new MockDeterministicIslandSampler(20);
            IslandLocator locator1 = new IslandLocator(sampler1, chunkSize);
            IslandLocator locator2 = new IslandLocator(sampler2, chunkSize);

            var house = CreateMockBlueprint("House", new Vector2Int(2, 2), StructureCategory.Residential, 1f);
            var harbor = CreateMockBlueprint("Harbor", new Vector2Int(3, 3), StructureCategory.Harbor, 1f);
            var list = new List<StructureData> { house, harbor };

            var plannerA = new IslandSettlementPlanner(sampler1, locator1, chunkSize, seed, list);
            var plannerB = new IslandSettlementPlanner(sampler2, locator2, chunkSize, seed, list);

            Vector2Int testIslandChunk = new Vector2Int(2, 2);
            var planA = plannerA.GetPlanForIsland(testIslandChunk);
            var planB = plannerB.GetPlanForIsland(testIslandChunk);

            Assert.IsNotNull(planA);
            Assert.IsNotNull(planB);
            Assert.AreEqual(planA.RoadTiles.Count, planB.RoadTiles.Count);
            Assert.AreEqual(planA.StructuresList.Count, planB.StructuresList.Count);

            for (int i = 0; i < planA.StructuresList.Count; i++)
            {
                Assert.AreEqual(planA.StructuresList[i].GlobalTileOrigin, planB.StructuresList[i].GlobalTileOrigin);
                Assert.AreEqual(planA.StructuresList[i].Dimensions, planB.StructuresList[i].Dimensions);
                Assert.AreEqual(planA.StructuresList[i].Blueprint.StructureName, planB.StructuresList[i].Blueprint.StructureName);
            }
        }

        [Test]
        public void SettlementPlanner_StructuresOnlySpawnIfConnectedToRoad()
        {
            int seed = 123;
            Vector2Int chunkSize = new Vector2Int(20, 20);
            IslandMapSampler sampler = new MockDeterministicIslandSampler(20);
            IslandLocator locator = new IslandLocator(sampler, chunkSize);

            var house = CreateMockBlueprint("House", new Vector2Int(2, 2), StructureCategory.Residential, 1f);
            var shop = CreateMockBlueprint("Shop", new Vector2Int(3, 2), StructureCategory.Service, 2f);
            var harbor = CreateMockBlueprint("Harbor", new Vector2Int(3, 3), StructureCategory.Harbor, 1f);
            var list = new List<StructureData> { house, shop, harbor };

            var planner = new IslandSettlementPlanner(sampler, locator, chunkSize, seed, list);
            var plan = planner.GetPlanForIsland(new Vector2Int(2, 2));

            Assert.IsTrue(plan.RoadTiles.Count > 0, "A ilha deveria ter gerado estradas!");

            foreach (var planned in plan.StructuresList)
            {
                Vector2Int doorTile = planned.GlobalTileOrigin + planned.Blueprint.DoorLocalCoordinate;
                Vector2Int frontTile = doorTile + Vector2Int.down;

                Assert.IsTrue(plan.RoadTiles.ContainsKey(frontTile),
                    $"A estrutura {planned.Blueprint.StructureName} em {planned.GlobalTileOrigin} DEVE ter sua porta {doorTile} conectada Ã  estrada em {frontTile}!");
            }
        }

        [Test]
        public void SettlementPlanner_RoadContainsOnlyCoastTransitionsWithoutSandCenter()
        {
            int seed = 555;
            Vector2Int chunkSize = new Vector2Int(20, 20);
            IslandMapSampler sampler = new MockDeterministicIslandSampler(20);
            IslandLocator locator = new IslandLocator(sampler, chunkSize);

            var house = CreateMockBlueprint("House", new Vector2Int(2, 2), StructureCategory.Residential, 1f);
            var planner = new IslandSettlementPlanner(sampler, locator, chunkSize, seed, new List<StructureData> { house });
            var plan = planner.GetPlanForIsland(new Vector2Int(2, 2));

            bool hasVisualSandCenter = false;
            bool hasCoastSouth = false;
            bool hasCoastNorth = false;

            foreach (var tile in plan.RoadTiles.Values)
            {
                if (tile.IsVisual && (tile.Layer == 2 || tile.TileType == Tile.TileType.Block))
                    hasVisualSandCenter = true;

                if (tile.IsVisual && tile.Layer == 3 && tile.TileType == Tile.TileType.Coast && tile.Direction == Tile.TileDirection.South)
                    hasCoastSouth = true;

                if (tile.IsVisual && tile.Layer == 3 && tile.TileType == Tile.TileType.Coast && tile.Direction == Tile.TileDirection.North)
                    hasCoastNorth = true;
            }

            Assert.IsFalse(hasVisualSandCenter, "REGRA DO USUÃRIO: O bloco central de areia (Layer 2 Block) deve ser retirado das estradas visuais!");
            Assert.IsTrue(hasCoastSouth, "A estrada deve ter transiÃ§Ã£o Coast South na lateral superior!");
            Assert.IsTrue(hasCoastNorth, "A estrada deve ter transiÃ§Ã£o Coast North na lateral inferior!");
        }

        [Test]
        public void SettlementPlanner_RoadsOnSandAreNonVisualAndBackingBlocksAreLayer4()
        {
            int seed = 42;
            Vector2Int chunkSize = new Vector2Int(20, 20);
            IslandMapSampler sampler = new MockDeterministicIslandSampler(20);
            IslandLocator locator = new IslandLocator(sampler, chunkSize);

            var house = CreateMockBlueprint("House", new Vector2Int(2, 2), StructureCategory.Residential, 1f);
            var harbor = CreateMockBlueprint("Harbor", new Vector2Int(3, 3), StructureCategory.Harbor, 1f);
            var planner = new IslandSettlementPlanner(sampler, locator, chunkSize, seed, new List<StructureData> { house, harbor });
            var plan = planner.GetPlanForIsland(new Vector2Int(2, 2));

            foreach (var kvp in plan.RoadTiles)
            {
                float h = sampler.Sample(kvp.Key.x, kvp.Key.y);
                if (h < IslandMapSampler.ISLAND_EDGE_THRESHOLD)
                {
                    Assert.IsFalse(kvp.Value.IsVisual,
                        $"O tile em {kvp.Key} possui altura {h} (areia) e DEVE ser nÃ£o-visual!");
                }
            }

            foreach (var kvp in plan.RoadTiles)
            {
                if (!kvp.Value.IsVisual) continue;

                Assert.IsTrue(sampler.Sample(kvp.Key.x, kvp.Key.y) >= IslandMapSampler.ISLAND_EDGE_THRESHOLD,
                    $"Tiles visuais de estrada sÃ³ podem existir sobre a camada 4!");
            }

            if (plan.Harbor != null)
            {
                Vector2Int doorTile = plan.Harbor.GlobalTileOrigin + plan.Harbor.Blueprint.DoorLocalCoordinate;
                Vector2Int frontTile = doorTile + Vector2Int.down;
                Assert.IsTrue(plan.RoadTiles.ContainsKey(frontTile),
                    "O porto na areia deve estar conectado Ã  malha viÃ¡ria!");
            }
        }

        [Test]
        public void SettlementPlanner_GeneratesCornersAndInnerCornersAtJunctionsAndTerminals()
        {
            int seed = 555;
            Vector2Int chunkSize = new Vector2Int(20, 20);
            IslandMapSampler sampler = new MockDeterministicIslandSampler(20);
            IslandLocator locator = new IslandLocator(sampler, chunkSize);

            var house = CreateMockBlueprint("House", new Vector2Int(2, 2), StructureCategory.Residential, 1f);
            var harbor = CreateMockBlueprint("Harbor", new Vector2Int(3, 3), StructureCategory.Harbor, 1f);
            var planner = new IslandSettlementPlanner(sampler, locator, chunkSize, seed, new List<StructureData> { house, harbor });
            var plan = planner.GetPlanForIsland(new Vector2Int(2, 2));

            bool hasCorner = false;
            bool hasInnerCorner = false;

            foreach (var tile in plan.RoadTiles.Values)
            {
                if (tile.Layer == 3 && tile.TileType == Tile.TileType.Corner)
                    hasCorner = true;

                if (tile.Layer == 3 && tile.TileType == Tile.TileType.InnerCorner)
                    hasInnerCorner = true;
            }

            Assert.IsTrue(hasCorner, "As estradas devem gerar tiles de quina externa (TileType.Corner / SL Quintet) nos pontos de encontro!");
            Assert.IsTrue(hasInnerCorner, "As estradas devem gerar tiles de quina interna/canto (TileType.InnerCorner / SL Intern) nas pontas e curvas!");
        }

        [Test]
        public void Autotiling_CorrectlyResolvesAllThirteenTopologicalTileTypes()
        {
            Assert.IsTrue(IslandSettlementPlanner.TryResolveRoadTile(2, 2, 2, 2, out var block));
            Assert.AreEqual(2, block.Layer);
            Assert.AreEqual(Tile.TileType.Block, block.TileType);

            Assert.IsTrue(IslandSettlementPlanner.TryResolveRoadTile(4, 4, 2, 2, out var coastS));
            Assert.AreEqual(Tile.TileType.Coast, coastS.TileType);
            Assert.AreEqual(Tile.TileDirection.South, coastS.Direction);

            Assert.IsTrue(IslandSettlementPlanner.TryResolveRoadTile(2, 2, 4, 4, out var coastN));
            Assert.AreEqual(Tile.TileType.Coast, coastN.TileType);
            Assert.AreEqual(Tile.TileDirection.North, coastN.Direction);

            Assert.IsTrue(IslandSettlementPlanner.TryResolveRoadTile(4, 2, 4, 2, out var coastE));
            Assert.AreEqual(Tile.TileType.Coast, coastE.TileType);
            Assert.AreEqual(Tile.TileDirection.East, coastE.Direction);

            Assert.IsTrue(IslandSettlementPlanner.TryResolveRoadTile(2, 4, 2, 4, out var coastW));
            Assert.AreEqual(Tile.TileType.Coast, coastW.TileType);
            Assert.AreEqual(Tile.TileDirection.West, coastW.Direction);

            Assert.IsTrue(IslandSettlementPlanner.TryResolveRoadTile(2, 2, 4, 2, out var cornerNE));
            Assert.AreEqual(Tile.TileType.Corner, cornerNE.TileType);
            Assert.AreEqual(Tile.TileDirection.NorthEast, cornerNE.Direction);

            Assert.IsTrue(IslandSettlementPlanner.TryResolveRoadTile(2, 2, 2, 4, out var cornerNW));
            Assert.AreEqual(Tile.TileType.Corner, cornerNW.TileType);
            Assert.AreEqual(Tile.TileDirection.NorthWest, cornerNW.Direction);

            Assert.IsTrue(IslandSettlementPlanner.TryResolveRoadTile(4, 2, 2, 2, out var cornerSE));
            Assert.AreEqual(Tile.TileType.Corner, cornerSE.TileType);
            Assert.AreEqual(Tile.TileDirection.SouthEast, cornerSE.Direction);

            Assert.IsTrue(IslandSettlementPlanner.TryResolveRoadTile(2, 4, 2, 2, out var cornerSW));
            Assert.AreEqual(Tile.TileType.Corner, cornerSW.TileType);
            Assert.AreEqual(Tile.TileDirection.SouthWest, cornerSW.Direction);

            Assert.IsTrue(IslandSettlementPlanner.TryResolveRoadTile(4, 4, 2, 4, out var innerNE));
            Assert.AreEqual(Tile.TileType.InnerCorner, innerNE.TileType);
            Assert.AreEqual(Tile.TileDirection.NorthEast, innerNE.Direction);

            Assert.IsTrue(IslandSettlementPlanner.TryResolveRoadTile(4, 4, 4, 2, out var innerNW));
            Assert.AreEqual(Tile.TileType.InnerCorner, innerNW.TileType);
            Assert.AreEqual(Tile.TileDirection.NorthWest, innerNW.Direction);

            Assert.IsTrue(IslandSettlementPlanner.TryResolveRoadTile(2, 4, 4, 4, out var innerSE));
            Assert.AreEqual(Tile.TileType.InnerCorner, innerSE.TileType);
            Assert.AreEqual(Tile.TileDirection.SouthEast, innerSE.Direction);

            Assert.IsTrue(IslandSettlementPlanner.TryResolveRoadTile(4, 2, 4, 4, out var innerSW));
            Assert.AreEqual(Tile.TileType.InnerCorner, innerSW.TileType);
            Assert.AreEqual(Tile.TileDirection.SouthWest, innerSW.Direction);
        }

        [Test]
        public void SettlementPlanner_RoadsGrowWithVariedLengthsAndNodeBranching()
        {
            int seed = 42;
            Vector2Int chunkSize = new Vector2Int(20, 20);
            IslandMapSampler sampler = new MockDeterministicIslandSampler(20);
            IslandLocator locator = new IslandLocator(sampler, chunkSize);

            var house = CreateMockBlueprint("House", new Vector2Int(2, 2), StructureCategory.Residential, 1f);
            var planner = new IslandSettlementPlanner(sampler, locator, chunkSize, seed, new List<StructureData> { house });
            var plan = planner.GetPlanForIsland(new Vector2Int(2, 2));

            Assert.IsFalse(plan.RoadTiles.Values.Any(t => t.Layer == 2 || t.TileType == Tile.TileType.Block),
                "Estradas nÃ£o devem conter blocos de areia centrais (Layer 2)!");

            var tilesByY = new Dictionary<int, List<int>>();
            foreach (var tile in plan.RoadTiles.Values)
            {
                if (!tilesByY.TryGetValue(tile.Coordinate.y, out var xs))
                {
                    xs = new List<int>();
                    tilesByY[tile.Coordinate.y] = xs;
                }
                xs.Add(tile.Coordinate.x);
            }

            var segmentLengths = new HashSet<int>();
            foreach (var kvp in tilesByY)
            {
                segmentLengths.Add(kvp.Value.Count);
            }

            Assert.IsTrue(segmentLengths.Count >= 2,
                $"As estradas devem possuir variedade de comprimento! Comprimentos encontrados: {string.Join(", ", segmentLengths)}");
        }

        [Test]
        public void SettlementPlanner_GeneratesMultiBlockGridAcrossEntireIsland()
        {
            int seed = 42;
            Vector2Int chunkSize = new Vector2Int(20, 20);
            IslandMapSampler sampler = new MockDeterministicIslandSampler(20);
            IslandLocator locator = new IslandLocator(sampler, chunkSize);

            var house = CreateMockBlueprint("House", new Vector2Int(2, 2), StructureCategory.Residential, 1f);
            var planner = new IslandSettlementPlanner(sampler, locator, chunkSize, seed, new List<StructureData> { house });
            var plan = planner.GetPlanForIsland(new Vector2Int(2, 2));

            var distinctYs = new HashSet<int>();
            var distinctXs = new HashSet<int>();

            foreach (var roadTile in plan.RoadTiles.Values)
            {
                if (roadTile.TileType == Tile.TileType.Coast || roadTile.TileType == Tile.TileType.Corner)
                {
                    distinctYs.Add(roadTile.Coordinate.y);
                    distinctXs.Add(roadTile.Coordinate.x);
                }
            }

            Assert.IsTrue(distinctYs.Count >= 2, $"A malha viÃ¡ria deve conter mÃºltiplos eixos horizontais de ruas! Encontrados: {distinctYs.Count}");
            Assert.IsTrue(distinctXs.Count >= 2, $"A malha viÃ¡ria deve conter mÃºltiplos eixos verticais de avenidas! Encontrados: {distinctXs.Count}");
        }

        [Test]
        public void SettlementPlanner_GeneratesCrossroadsWithCorners()
        {
            int seed = 777;
            Vector2Int chunkSize = new Vector2Int(20, 20);
            IslandMapSampler sampler = new MockDeterministicIslandSampler(20);
            IslandLocator locator = new IslandLocator(sampler, chunkSize);

            var house = CreateMockBlueprint("House", new Vector2Int(2, 2), StructureCategory.Residential, 1f);
            var planner = new IslandSettlementPlanner(sampler, locator, chunkSize, seed, new List<StructureData> { house });
            var plan = planner.GetPlanForIsland(new Vector2Int(2, 2));

            var cornerDirections = new HashSet<Tile.TileDirection>();
            foreach (var tile in plan.RoadTiles.Values)
            {
                if (tile.Layer == 3 && tile.TileType == Tile.TileType.Corner)
                {
                    cornerDirections.Add(tile.Direction);
                }
            }

            Assert.IsTrue(cornerDirections.Count >= 2, $"Cruzamentos devem gerar quinas (SL Quintet) em mÃºltiplos quadrantes! Encontradas: {cornerDirections.Count}");
        }

        [Test]
        public void SettlementPlanner_PopulatesStructuresAcrossMultipleBlocks()
        {
            int seed = 123;
            Vector2Int chunkSize = new Vector2Int(20, 20);
            IslandMapSampler sampler = new MockDeterministicIslandSampler(20);
            IslandLocator locator = new IslandLocator(sampler, chunkSize);

            var house = CreateMockBlueprint("House", new Vector2Int(2, 2), StructureCategory.Residential, 1f);
            var shop = CreateMockBlueprint("Shop", new Vector2Int(3, 2), StructureCategory.Service, 2f);
            var list = new List<StructureData> { house, shop };

            var planner = new IslandSettlementPlanner(sampler, locator, chunkSize, seed, list);
            var plan = planner.GetPlanForIsland(new Vector2Int(2, 2));

            Assert.IsTrue(plan.StructuresList.Count > 0, "Deveriam ter sido geradas estruturas!");

            var distinctLotYs = new HashSet<int>();
            foreach (var s in plan.StructuresList)
            {
                distinctLotYs.Add(s.GlobalTileOrigin.y);
            }

            Assert.IsTrue(distinctLotYs.Count >= 2, $"As estruturas devem estar distribuÃ­das em mÃºltiplos quarteirÃµes/ruas (Y distintos)! Encontrados: {distinctLotYs.Count}");
        }

        [Test]
        public void SettlementPlanner_DoesNotPlaceOversizedStructures()
        {
            int seed = 999;
            Vector2Int chunkSize = new Vector2Int(20, 20);
            IslandMapSampler sampler = new MockDeterministicIslandSampler(20);
            IslandLocator locator = new IslandLocator(sampler, chunkSize);

            var colossalBuilding = CreateMockBlueprint("Colossal", new Vector2Int(50, 50), StructureCategory.Residential, 10f);
            var smallHouse = CreateMockBlueprint("SmallHouse", new Vector2Int(2, 2), StructureCategory.Residential, 1f);
            var list = new List<StructureData> { colossalBuilding, smallHouse };

            var planner = new IslandSettlementPlanner(sampler, locator, chunkSize, seed, list);
            var plan = planner.GetPlanForIsland(new Vector2Int(3, 3));

            foreach (var planned in plan.StructuresList)
            {
                Assert.AreNotEqual("Colossal", planned.Blueprint.StructureName, "Estrutura colossal nÃ£o deveria ter sido posicionada!");
            }
        }

        [Test]
        public void SettlementPlanner_RoadsCrossIntoNeighborChunks()
        {
            int seed = 12345;
            Vector2Int chunkSize = new Vector2Int(20, 20);
            IslandMapSampler sampler = new MockRadialIslandSampler(new Vector2(30, 30), 25f);
            IslandLocator locator = new IslandLocator(sampler, chunkSize);

            var house = CreateMockBlueprint("House", new Vector2Int(2, 2), StructureCategory.Residential, 1f);
            var list = new List<StructureData> { house };

            var planner = new IslandSettlementPlanner(sampler, locator, chunkSize, seed, list);
            var plan = planner.GetPlanForChunk(new Vector2Int(1, 1));

            Assert.IsNotNull(plan, "O plano da ilha nÃ£o pode ser nulo!");
            Assert.IsTrue(plan.RoadTilesByChunk.Keys.Count > 1,
                $"As estradas devem atravessar as bordas da chunk e continuar nas chunks vizinhas! Chunks com estradas: {plan.RoadTilesByChunk.Keys.Count}");

            var neighborPlan = planner.GetPlanForChunk(new Vector2Int(0, 1));
            Assert.AreSame(plan, neighborPlan, "Chunks da mesma ilha devem compartilhar a mesma instÃ¢ncia de plano!");
        }

        [Test]
        public void SettlementPlanner_AllRoadTilesHaveMatchingSocketsWithNeighbors()
        {
            int seed = 42;
            Vector2Int chunkSize = new Vector2Int(20, 20);
            IslandMapSampler sampler = new MockDeterministicIslandSampler(20);
            IslandLocator locator = new IslandLocator(sampler, chunkSize);

            var house = CreateMockBlueprint("House", new Vector2Int(2, 2), StructureCategory.Residential, 1f);
            var list = new List<StructureData> { house };

            var planner = new IslandSettlementPlanner(sampler, locator, chunkSize, seed, list);
            var plan = planner.GetPlanForIsland(new Vector2Int(2, 2));

            var allRoads = plan.RoadTiles;

            Assert.IsTrue(allRoads.Count > 0, "Deveriam existir estradas planejadas!");

            foreach (var kvp in allRoads)
            {
                Vector2Int pos = kvp.Key;
                var road = kvp.Value;
                if (!road.IsVisual) continue;

                if (allRoads.TryGetValue(pos + Vector2Int.up, out var northRoad))
                {
                    Assert.AreEqual(road.Corners.NorthWest, northRoad.Corners.SouthWest, $"Incompatibilidade de socket em {pos} com Norte (NW/SW)!");
                    Assert.AreEqual(road.Corners.NorthEast, northRoad.Corners.SouthEast, $"Incompatibilidade de socket em {pos} com Norte (NE/SE)!");
                }

                if (allRoads.TryGetValue(pos + Vector2Int.down, out var southRoad))
                {
                    Assert.AreEqual(road.Corners.SouthWest, southRoad.Corners.NorthWest, $"Incompatibilidade de socket em {pos} com Sul (SW/NW)!");
                    Assert.AreEqual(road.Corners.SouthEast, southRoad.Corners.NorthEast, $"Incompatibilidade de socket em {pos} com Sul (SE/NE)!");
                }

                if (allRoads.TryGetValue(pos + Vector2Int.left, out var westRoad))
                {
                    Assert.AreEqual(road.Corners.NorthWest, westRoad.Corners.NorthEast, $"Incompatibilidade de socket em {pos} com Oeste (NW/NE)!");
                    Assert.AreEqual(road.Corners.SouthWest, westRoad.Corners.SouthEast, $"Incompatibilidade de socket em {pos} com Oeste (SW/SE)!");
                }

                if (allRoads.TryGetValue(pos + Vector2Int.right, out var eastRoad))
                {
                    Assert.AreEqual(road.Corners.NorthEast, eastRoad.Corners.NorthWest, $"Incompatibilidade de socket em {pos} com Leste (NE/NW)!");
                    Assert.AreEqual(road.Corners.SouthEast, eastRoad.Corners.SouthWest, $"Incompatibilidade de socket em {pos} com Leste (SE/SW)!");
                }

                bool hasNorth = allRoads.ContainsKey(pos + Vector2Int.up);
                bool hasSouth = allRoads.ContainsKey(pos + Vector2Int.down);
                bool hasWest = allRoads.ContainsKey(pos + Vector2Int.left);
                bool hasEast = allRoads.ContainsKey(pos + Vector2Int.right);

                if (!hasNorth && !hasWest)
                    Assert.AreEqual(4, road.Corners.NorthWest, $"Canto NW em {pos} expõe estrada sem vizinho a Norte ou Oeste!");
                if (!hasNorth && !hasEast)
                    Assert.AreEqual(4, road.Corners.NorthEast, $"Canto NE em {pos} expõe estrada sem vizinho a Norte ou Leste!");
                if (!hasSouth && !hasWest)
                    Assert.AreEqual(4, road.Corners.SouthWest, $"Canto SW em {pos} expõe estrada sem vizinho a Sul ou Oeste!");
                if (!hasSouth && !hasEast)
                    Assert.AreEqual(4, road.Corners.SouthEast, $"Canto SE em {pos} expõe estrada sem vizinho a Sul ou Leste!");
            }
        }

        [Test]
        public void SettlementPlanner_StructureLotteryAllowsEmptyLots()
        {
            var house = CreateMockBlueprint("House", new Vector2Int(2, 2), StructureCategory.Residential, 1f);
            var list = new List<StructureData> { house };

            int nullCount = 0;
            int totalTrials = 100;

            for (int i = 0; i < totalTrials; i++)
            {
                var prng = new System.Random(i * 37 + 11);
                var chosen = IslandSettlementPlanner.SelectWeightedBlueprint(list, prng, emptyWeight: 0.25f);
                if ((object)chosen == null)
                    nullCount++;
            }

            Assert.IsTrue(nullCount > 0, "O sorteio de estruturas deve permitir a possibilidade de nÃ£o haver nenhuma (lote vazio)!");
            Assert.IsTrue(nullCount < totalTrials, "O sorteio de estruturas tambÃ©m deve selecionar blueprints vÃ¡lidos!");
        }

        [Test]
        public void SmartTile_DerivesCoastNorthFromNorthNeighborExposingSockets()
        {
            var roadTiles = new Dictionary<Vector2Int, PlannedRoadTile>();

            roadTiles[new Vector2Int(0, 1)] = new PlannedRoadTile
            {
                Coordinate = new Vector2Int(0, 1),
                Layer = 3, TileType = Tile.TileType.Coast, Direction = Tile.TileDirection.South,
                IsVisual = true,
                Corners = new Tile.CornerSockets { NorthWest = 4, NorthEast = 4, SouthWest = 2, SouthEast = 2 }
            };

            var sampler = new MockDeterministicIslandSampler(shoreY: -100);
            var derived = IslandSettlementPlanner.DeriveRoadTileFromNeighborSockets(
                new Vector2Int(0, 0), roadTiles, sampler);

            Assert.IsNotNull(derived, "Deve derivar Coast-North a partir do vizinho Norte que expÃµe SW=2, SE=2.");
            Assert.AreEqual(Tile.TileType.Coast, derived.TileType);
            Assert.AreEqual(Tile.TileDirection.North, derived.Direction);
        }

        [Test]
        public void SmartTile_ReturnsNullWhenTwoNeighborsConflictOnSameCorner()
        {
            var roadTiles = new Dictionary<Vector2Int, PlannedRoadTile>();

            roadTiles[new Vector2Int(0, 1)] = new PlannedRoadTile
            {
                Coordinate = new Vector2Int(0, 1),
                Layer = 3, TileType = Tile.TileType.Coast, Direction = Tile.TileDirection.South,
                IsVisual = true,
                Corners = new Tile.CornerSockets { NorthWest = 4, NorthEast = 4, SouthWest = 2, SouthEast = 2 }
            };

            roadTiles[new Vector2Int(-1, 0)] = new PlannedRoadTile
            {
                Coordinate = new Vector2Int(-1, 0),
                Layer = 3, TileType = Tile.TileType.InnerCorner, Direction = Tile.TileDirection.NorthWest,
                IsVisual = true,
                Corners = new Tile.CornerSockets { NorthWest = 4, NorthEast = 4, SouthWest = 4, SouthEast = 2 }
            };

            var sampler = new MockDeterministicIslandSampler(shoreY: -100);
            var derived = IslandSettlementPlanner.DeriveRoadTileFromNeighborSockets(
                new Vector2Int(0, 0), roadTiles, sampler);

            Assert.IsNull(derived, "Conflito topolÃ³gico nos sockets deve retornar null!");
        }

        [Test]
        public void SmartTile_ReturnsNullWhenNoRoadNeighborsExist()
        {
            var roadTiles = new Dictionary<Vector2Int, PlannedRoadTile>();
            var sampler = new MockDeterministicIslandSampler(shoreY: -100);

            var derived = IslandSettlementPlanner.DeriveRoadTileFromNeighborSockets(
                new Vector2Int(5, 5), roadTiles, sampler);

            Assert.IsNull(derived, "Sem vizinhos de estrada, não deve derivar nenhum tile!");
        }

        [Test]
        public void SettlementPlanner_NodesOnlySpawnOnEvenCellsWithLayerAtLeastFour()
        {
            int[] testSeeds = { 42, 12345, 999 };
            Vector2Int chunkSize = new Vector2Int(20, 20);

            foreach (int seed in testSeeds)
            {
                IslandMapSampler sampler = new MockDeterministicIslandSampler(20);
                IslandLocator locator = new IslandLocator(sampler, chunkSize);
                var house = CreateMockBlueprint("House", new Vector2Int(2, 2), StructureCategory.Residential, 1f);
                var harbor = CreateMockBlueprint("Harbor", new Vector2Int(3, 3), StructureCategory.Harbor, 1f);

                var planner = new IslandSettlementPlanner(sampler, locator, chunkSize, seed, new List<StructureData> { house, harbor });
                var plan = planner.GetPlanForIsland(new Vector2Int(2, 2));

                Assert.IsNotNull(plan.RoadNodes, "A malha viária deve possuir nós registrados!");
                Assert.IsTrue(plan.RoadNodes.Count > 0, "Deveriam existir nós gerados na malha viária!");

                foreach (Vector2Int node in plan.RoadNodes)
                {
                    Assert.AreEqual(0, node.x % 2, $"Nó em {node} (seed {seed}) DEVE ter coordenada X par!");
                    Assert.AreEqual(0, node.y % 2, $"Nó em {node} (seed {seed}) DEVE ter coordenada Y par!");
                    float height = sampler.Sample(node.x, node.y);
                    Assert.IsTrue(height >= IslandMapSampler.ISLAND_EDGE_THRESHOLD,
                        $"Nó em {node} (seed {seed}) possui altura {height} e DEVE estar na camada >= 4 (terra firme)!");
                }
            }
        }

        private static int _nextInstanceId = 1000;
        private Tile CreateMockTile(int layer, Tile.TileType type, Tile.TileDirection direction, Tile.CornerSockets corners, float weight = 1f)
        {
            Tile tile;
            try
            {
                tile = ScriptableObject.CreateInstance<Tile>();
            }
            catch
            {
                tile = (Tile)System.Runtime.Serialization.FormatterServices.GetUninitializedObject(typeof(Tile));
            }

            typeof(UnityEngine.Object).GetField("m_InstanceID", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)?.SetValue(tile, ++_nextInstanceId);

            var metadata = new Tile.TileMetadata();
            object boxed = metadata;
            typeof(Tile.TileMetadata).GetField("_layer", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)?.SetValue(boxed, layer);
            typeof(Tile.TileMetadata).GetField("_type", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)?.SetValue(boxed, type);
            typeof(Tile.TileMetadata).GetField("_direction", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)?.SetValue(boxed, direction);
            metadata = (Tile.TileMetadata)boxed;
            metadata.Corners = corners;

            typeof(Tile).GetField("_metadata", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)?.SetValue(tile, metadata);
            typeof(Tile).GetField("_weight", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)?.SetValue(tile, weight);

            return tile;
        }

        private TilesetData CreateMockTilesetData(List<Tile> tiles)
        {
            TilesetData data;
            try
            {
                data = ScriptableObject.CreateInstance<TilesetData>();
            }
            catch
            {
                data = (TilesetData)System.Runtime.Serialization.FormatterServices.GetUninitializedObject(typeof(TilesetData));
            }

            typeof(TilesetData).GetField("_tilesetList", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)?.SetValue(data, tiles);
            return data;
        }

        [Test]
        public void FindCompatibleTile_WhenCurrentTileIsIncompatible_FindsMatchingCandidate()
        {
            var grassBlock = CreateMockTile(4, Tile.TileType.Block, Tile.TileDirection.None,
                new Tile.CornerSockets { NorthWest = 4, NorthEast = 4, SouthWest = 4, SouthEast = 4 });
            var sandBlock = CreateMockTile(2, Tile.TileType.Block, Tile.TileDirection.None,
                new Tile.CornerSockets { NorthWest = 2, NorthEast = 2, SouthWest = 2, SouthEast = 2 });
            var coastSouth = CreateMockTile(3, Tile.TileType.Coast, Tile.TileDirection.South,
                new Tile.CornerSockets { NorthWest = 4, NorthEast = 4, SouthWest = 2, SouthEast = 2 });
            var coastNorth = CreateMockTile(3, Tile.TileType.Coast, Tile.TileDirection.North,
                new Tile.CornerSockets { NorthWest = 2, NorthEast = 2, SouthWest = 4, SouthEast = 4 });

            var tileset = CreateMockTilesetData(new List<Tile> { grassBlock, sandBlock, coastSouth, coastNorth });

            var neighbors = new List<(Vector2Int dir, Tile neighbor)>
            {
                (Vector2Int.up, grassBlock),
                (Vector2Int.down, sandBlock)
            };

            Assert.IsFalse(grassBlock.IsCompatibleWith(sandBlock, Vector2Int.down));

            Assert.AreEqual(4, coastSouth.Metadata.Corners.NorthWest, "coastSouth NW deve ser 4");
            Assert.AreEqual(2, coastSouth.Metadata.Corners.SouthWest, "coastSouth SW deve ser 2");
            Assert.IsTrue(coastSouth.IsCompatibleWith(grassBlock, Vector2Int.up), "coastSouth deve ser compatível com grassBlock ao Norte");
            Assert.IsTrue(coastSouth.IsCompatibleWith(sandBlock, Vector2Int.down), "coastSouth deve ser compatível com sandBlock ao Sul");

            Tile result = StructureGenerator.FindCompatibleTile(grassBlock, neighbors, tileset);

            Assert.IsTrue((object)result != null, "Resultado de FindCompatibleTile não deve ser nulo!");
            Assert.IsTrue(ReferenceEquals(coastSouth, result), "Deveria encontrar coastSouth que encaixa com grama ao Norte e areia ao Sul!");
        }

        [Test]
        public void FindCompatibleTile_WhenCurrentTileIsCompatible_PreservesTile()
        {
            var grassBlock = CreateMockTile(4, Tile.TileType.Block, Tile.TileDirection.None,
                new Tile.CornerSockets { NorthWest = 4, NorthEast = 4, SouthWest = 4, SouthEast = 4 });
            var sandBlock = CreateMockTile(2, Tile.TileType.Block, Tile.TileDirection.None,
                new Tile.CornerSockets { NorthWest = 2, NorthEast = 2, SouthWest = 2, SouthEast = 2 });

            var tileset = CreateMockTilesetData(new List<Tile> { grassBlock, sandBlock });

            var neighbors = new List<(Vector2Int dir, Tile neighbor)>
            {
                (Vector2Int.up, grassBlock),
                (Vector2Int.down, grassBlock),
                (Vector2Int.left, grassBlock),
                (Vector2Int.right, grassBlock)
            };

            Tile result = StructureGenerator.FindCompatibleTile(grassBlock, neighbors, tileset);
            Assert.IsTrue(ReferenceEquals(grassBlock, result), "Deveria preservar grassBlock pois já é compatível!");
        }

        [Test]
        public void SettlementPlanner_ChunkWithStructures_ResolvesTileCompatibility()
        {
            var grassBlock = CreateMockTile(4, Tile.TileType.Block, Tile.TileDirection.None,
                new Tile.CornerSockets { NorthWest = 4, NorthEast = 4, SouthWest = 4, SouthEast = 4 });
            var sandBlock = CreateMockTile(2, Tile.TileType.Block, Tile.TileDirection.None,
                new Tile.CornerSockets { NorthWest = 2, NorthEast = 2, SouthWest = 2, SouthEast = 2 });
            var coastSouth = CreateMockTile(3, Tile.TileType.Coast, Tile.TileDirection.South,
                new Tile.CornerSockets { NorthWest = 4, NorthEast = 4, SouthWest = 2, SouthEast = 2 });

            var tileset = CreateMockTilesetData(new List<Tile> { grassBlock, sandBlock, coastSouth });

            Vector2Int chunkSize = new Vector2Int(3, 3);
            MapGenerator activeChunk = (MapGenerator)System.Runtime.Serialization.FormatterServices.GetUninitializedObject(typeof(MapGenerator));
            var grid = new ChunkCellGrid(chunkSize, tileset);
            grid.InitializeCells(null, null);

            typeof(MapGenerator).GetField("_grid", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance).SetValue(activeChunk, grid);
            typeof(MapGenerator).GetField("_chunkSize", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance).SetValue(activeChunk, chunkSize);
            typeof(MapGenerator).GetField("_tilesetData", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance).SetValue(activeChunk, tileset);

            for (int x = 0; x < 3; x++)
            {
                activeChunk.SetTileAt(x, 2, 0);
                activeChunk.SetTileAt(x, 0, 1);
            }
            activeChunk.SetTileAt(0, 1, 2);
            activeChunk.SetTileAt(2, 1, 2);
            activeChunk.SetTileAt(1, 1, 0);

            Assert.IsFalse(activeChunk.GetTileAt(1, 1).IsCompatibleWith(activeChunk.GetTileAt(1, 0), Vector2Int.down));

            StructureGenerator structGen = (StructureGenerator)System.Runtime.Serialization.FormatterServices.GetUninitializedObject(typeof(StructureGenerator));
            typeof(StructureGenerator).GetField("_chunkSize", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance).SetValue(structGen, chunkSize);
            typeof(StructureGenerator).GetField("_tilesetData", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance).SetValue(structGen, tileset);

            structGen.ResolveTileCompatibilityInChunk(Vector2Int.zero, activeChunk);

            Tile resolvedTile = activeChunk.GetTileAt(1, 1);
            Assert.IsTrue(ReferenceEquals(coastSouth, resolvedTile), "O tile em (1, 1) deve ser substituído por coastSouth!");
            Assert.IsTrue(resolvedTile.IsCompatibleWith(activeChunk.GetTileAt(1, 0), Vector2Int.down), "Deve ser compatível com o Sul (sandBlock)!");
            Assert.IsTrue(resolvedTile.IsCompatibleWith(activeChunk.GetTileAt(1, 2), Vector2Int.up), "Deve ser compatível com o Norte (grassBlock)!");
            Assert.IsTrue(resolvedTile.IsCompatibleWith(activeChunk.GetTileAt(0, 1), Vector2Int.left), "Deve ser compatível com o Oeste (coastSouth)!");
            Assert.IsTrue(resolvedTile.IsCompatibleWith(activeChunk.GetTileAt(2, 1), Vector2Int.right), "Deve ser compatível com o Leste (coastSouth)!");
        }

        [Test]
        public void ScanAndGenerateStructures_AllChunksWithoutStructures_ResolveCompatibility()
        {
            var grassBlock = CreateMockTile(4, Tile.TileType.Block, Tile.TileDirection.None,
                new Tile.CornerSockets { NorthWest = 4, NorthEast = 4, SouthWest = 4, SouthEast = 4 });
            var sandBlock = CreateMockTile(2, Tile.TileType.Block, Tile.TileDirection.None,
                new Tile.CornerSockets { NorthWest = 2, NorthEast = 2, SouthWest = 2, SouthEast = 2 });
            var coastSouth = CreateMockTile(3, Tile.TileType.Coast, Tile.TileDirection.South,
                new Tile.CornerSockets { NorthWest = 4, NorthEast = 4, SouthWest = 2, SouthEast = 2 });

            var tileset = CreateMockTilesetData(new List<Tile> { grassBlock, sandBlock, coastSouth });

            Vector2Int chunkSize = new Vector2Int(3, 3);
            MapGenerator activeChunk = (MapGenerator)System.Runtime.Serialization.FormatterServices.GetUninitializedObject(typeof(MapGenerator));
            var grid = new ChunkCellGrid(chunkSize, tileset);
            grid.InitializeCells(null, null);

            typeof(MapGenerator).GetField("_grid", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance).SetValue(activeChunk, grid);
            typeof(MapGenerator).GetField("_chunkSize", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance).SetValue(activeChunk, chunkSize);
            typeof(MapGenerator).GetField("_tilesetData", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance).SetValue(activeChunk, tileset);

            for (int x = 0; x < 3; x++)
            {
                activeChunk.SetTileAt(x, 2, 0);
                activeChunk.SetTileAt(x, 0, 1);
            }
            activeChunk.SetTileAt(0, 1, 2);
            activeChunk.SetTileAt(2, 1, 2);
            activeChunk.SetTileAt(1, 1, 0);

            StructureGenerator structGen = (StructureGenerator)System.Runtime.Serialization.FormatterServices.GetUninitializedObject(typeof(StructureGenerator));
            typeof(StructureGenerator).GetField("_chunkSize", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance).SetValue(structGen, chunkSize);
            typeof(StructureGenerator).GetField("_tilesetData", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance).SetValue(structGen, tileset);

            ChunkLifecycleManager lifecycleManager = (ChunkLifecycleManager)System.Runtime.Serialization.FormatterServices.GetUninitializedObject(typeof(ChunkLifecycleManager));
            var activeChunksDict = new Dictionary<Vector2Int, MapGenerator>
            {
                { Vector2Int.zero, activeChunk }
            };
            typeof(ChunkLifecycleManager).GetField("_activeChunksDictionary", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)
                ?.SetValue(lifecycleManager, activeChunksDict);
            typeof(StructureGenerator).GetField("_lifecycleManager", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)
                ?.SetValue(structGen, lifecycleManager);

            var scanMethod = typeof(StructureGenerator).GetMethod("ScanAndGenerateStructures", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            scanMethod.Invoke(structGen, new object[] { Vector2Int.zero });

            Tile resolvedTile = activeChunk.GetTileAt(1, 1);
            Assert.IsTrue(ReferenceEquals(coastSouth, resolvedTile), "Mesmo sem plano de estruturas, toda chunk deve passar pela resolução final de compatibilidade!");
        }

        [Test]
        public void ResolveTileCompatibility_NeverLeavesSandAdjacentToWater()
        {
            var waterBlock = CreateMockTile(0, Tile.TileType.Block, Tile.TileDirection.None,
                new Tile.CornerSockets { NorthWest = 0, NorthEast = 0, SouthWest = 0, SouthEast = 0 });
            var coastSouth = CreateMockTile(1, Tile.TileType.Coast, Tile.TileDirection.South,
                new Tile.CornerSockets { NorthWest = 2, NorthEast = 2, SouthWest = 0, SouthEast = 0 });
            var sandBlock = CreateMockTile(2, Tile.TileType.Block, Tile.TileDirection.None,
                new Tile.CornerSockets { NorthWest = 2, NorthEast = 2, SouthWest = 2, SouthEast = 2 });

            var tileset = CreateMockTilesetData(new List<Tile> { waterBlock, coastSouth, sandBlock });

            Vector2Int chunkSize = new Vector2Int(3, 3);
            MapGenerator activeChunk = (MapGenerator)System.Runtime.Serialization.FormatterServices.GetUninitializedObject(typeof(MapGenerator));
            var grid = new ChunkCellGrid(chunkSize, tileset);
            grid.InitializeCells(null, null);

            typeof(MapGenerator).GetField("_grid", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance).SetValue(activeChunk, grid);
            typeof(MapGenerator).GetField("_chunkSize", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance).SetValue(activeChunk, chunkSize);
            typeof(MapGenerator).GetField("_tilesetData", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance).SetValue(activeChunk, tileset);

            for (int x = 0; x < 3; x++)
            {
                activeChunk.SetTileAt(x, 0, 0);
                activeChunk.SetTileAt(x, 1, 2);
                activeChunk.SetTileAt(x, 2, 2);
            }

            for (int x = 0; x < 3; x++)
            {
                Assert.AreEqual(2, activeChunk.GetTileAt(x, 1).Metadata.Layer, "Inicialmente linha 1 deve ser areia (camada 2).");
                Assert.IsFalse(activeChunk.GetTileAt(x, 1).IsCompatibleWith(activeChunk.GetTileAt(x, 0), Vector2Int.down),
                    "Areia pura não pode ser compatível com água!");
            }

            StructureGenerator structGen = (StructureGenerator)System.Runtime.Serialization.FormatterServices.GetUninitializedObject(typeof(StructureGenerator));
            typeof(StructureGenerator).GetField("_chunkSize", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance).SetValue(structGen, chunkSize);
            typeof(StructureGenerator).GetField("_tilesetData", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance).SetValue(structGen, tileset);

            structGen.ResolveTileCompatibilityInChunk(Vector2Int.zero, activeChunk);

            Vector2Int[] cardinalDirs = { Vector2Int.up, Vector2Int.down, Vector2Int.left, Vector2Int.right };
            for (int y = 0; y < 3; y++)
            {
                for (int x = 0; x < 3; x++)
                {
                    Tile tile = activeChunk.GetTileAt(x, y);
                    if (tile.Metadata.Layer == 2)
                    {
                        foreach (var dir in cardinalDirs)
                        {
                            int nx = x + dir.x;
                            int ny = y + dir.y;
                            if (nx >= 0 && nx < 3 && ny >= 0 && ny < 3)
                            {
                                Tile nTile = activeChunk.GetTileAt(nx, ny);
                                Assert.AreNotEqual(0, nTile.Metadata.Layer,
                                    $"Bloco de areia em ({x}, {y}) NUNCA pode ter vizinho de água em ({nx}, {ny})!");
                            }
                        }
                    }
                }
            }

            for (int x = 0; x < 3; x++)
            {
                Tile resolved = activeChunk.GetTileAt(x, 1);
                Assert.AreEqual(1, resolved.Metadata.Layer, $"Tile em ({x}, 1) deve ser transição (camada 1)!");
                Assert.IsTrue(ReferenceEquals(coastSouth, resolved), $"Tile em ({x}, 1) deve ser coastSouth!");
                Assert.IsTrue(resolved.IsCompatibleWith(activeChunk.GetTileAt(x, 0), Vector2Int.down),
                    "Deve ser 100% compatível com a água ao Sul!");
            }
        }

        [Test]
        public void ScanAndGenerateStructures_WithHarborPlan_NeverLeavesSandAdjacentToWater()
        {
            var waterBlock = CreateMockTile(0, Tile.TileType.Block, Tile.TileDirection.None,
                new Tile.CornerSockets { NorthWest = 0, NorthEast = 0, SouthWest = 0, SouthEast = 0 });
            var coastSouth = CreateMockTile(1, Tile.TileType.Coast, Tile.TileDirection.South,
                new Tile.CornerSockets { NorthWest = 2, NorthEast = 2, SouthWest = 0, SouthEast = 0 });
            var sandBlock = CreateMockTile(2, Tile.TileType.Block, Tile.TileDirection.None,
                new Tile.CornerSockets { NorthWest = 2, NorthEast = 2, SouthWest = 2, SouthEast = 2 });

            var tileset = CreateMockTilesetData(new List<Tile> { waterBlock, coastSouth, sandBlock });

            Vector2Int chunkSize = new Vector2Int(4, 4);
            MapGenerator activeChunk = (MapGenerator)System.Runtime.Serialization.FormatterServices.GetUninitializedObject(typeof(MapGenerator));
            var grid = new ChunkCellGrid(chunkSize, tileset);
            grid.InitializeCells(null, null);

            typeof(MapGenerator).GetField("_grid", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance).SetValue(activeChunk, grid);
            typeof(MapGenerator).GetField("_chunkSize", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance).SetValue(activeChunk, chunkSize);
            typeof(MapGenerator).GetField("_tilesetData", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance).SetValue(activeChunk, tileset);

            for (int x = 0; x < 4; x++)
            {
                activeChunk.SetTileAt(x, 0, 0);
                activeChunk.SetTileAt(x, 1, 1);
                activeChunk.SetTileAt(x, 2, 2);
                activeChunk.SetTileAt(x, 3, 2);
            }

            var harborBp = CreateMockBlueprint("Harbor", new Vector2Int(2, 2), StructureCategory.Harbor, 1f);

            var plan = new IslandSettlementPlan
            {
                Harbor = new PlannedStructure
                {
                    Blueprint = harborBp,
                    GlobalTileOrigin = new Vector2Int(1, 1),
                    Dimensions = new Vector2Int(2, 2)
                }
            };
            plan.StructuresByChunk[Vector2Int.zero] = new List<PlannedStructure> { plan.Harbor };

            StructureGenerator structGen = (StructureGenerator)System.Runtime.Serialization.FormatterServices.GetUninitializedObject(typeof(StructureGenerator));
            typeof(StructureGenerator).GetField("_chunkSize", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance).SetValue(structGen, chunkSize);
            typeof(StructureGenerator).GetField("_tilesetData", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance).SetValue(structGen, tileset);

            var carveMethod = typeof(StructureGenerator).GetMethod("CarveRoadsInChunk", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            carveMethod.Invoke(structGen, new object[] { Vector2Int.zero, activeChunk, plan });
            structGen.ResolveTileCompatibilityInChunk(Vector2Int.zero, activeChunk);

            Vector2Int[] cardinalDirs = { Vector2Int.up, Vector2Int.down, Vector2Int.left, Vector2Int.right };
            for (int y = 0; y < 4; y++)
            {
                for (int x = 0; x < 4; x++)
                {
                    Tile tile = activeChunk.GetTileAt(x, y);
                    if (tile.Metadata.Layer == 2)
                    {
                        foreach (var dir in cardinalDirs)
                        {
                            int nx = x + dir.x;
                            int ny = y + dir.y;
                            if (nx >= 0 && nx < 4 && ny >= 0 && ny < 4)
                            {
                                Tile nTile = activeChunk.GetTileAt(nx, ny);
                                Assert.AreNotEqual(0, nTile.Metadata.Layer,
                                    $"Bloco de areia em ({x}, {y}) sob o porto NUNCA pode ser adjacente a água em ({nx}, {ny})!");
                            }
                        }
                    }
                }
            }
        }

        [Test]
        public void ResolveTileCompatibility_FixesCuttingRoadTilesUntilZeroErrors()
        {
            var grassBlock = CreateMockTile(4, Tile.TileType.Block, Tile.TileDirection.None,
                new Tile.CornerSockets { NorthWest = 4, NorthEast = 4, SouthWest = 4, SouthEast = 4 });
            var sandBlock = CreateMockTile(2, Tile.TileType.Block, Tile.TileDirection.None,
                new Tile.CornerSockets { NorthWest = 2, NorthEast = 2, SouthWest = 2, SouthEast = 2 });

            var coastSouth = CreateMockTile(3, Tile.TileType.Coast, Tile.TileDirection.South,
                new Tile.CornerSockets { NorthWest = 4, NorthEast = 4, SouthWest = 2, SouthEast = 2 });
            var coastNorth = CreateMockTile(3, Tile.TileType.Coast, Tile.TileDirection.North,
                new Tile.CornerSockets { NorthWest = 2, NorthEast = 2, SouthWest = 4, SouthEast = 4 });
            var coastEast = CreateMockTile(3, Tile.TileType.Coast, Tile.TileDirection.East,
                new Tile.CornerSockets { NorthWest = 4, NorthEast = 2, SouthWest = 4, SouthEast = 2 });
            var coastWest = CreateMockTile(3, Tile.TileType.Coast, Tile.TileDirection.West,
                new Tile.CornerSockets { NorthWest = 2, NorthEast = 4, SouthWest = 2, SouthEast = 4 });

            var innerNW = CreateMockTile(3, Tile.TileType.InnerCorner, Tile.TileDirection.NorthWest,
                new Tile.CornerSockets { NorthWest = 4, NorthEast = 4, SouthWest = 4, SouthEast = 2 });
            var innerNE = CreateMockTile(3, Tile.TileType.InnerCorner, Tile.TileDirection.NorthEast,
                new Tile.CornerSockets { NorthWest = 4, NorthEast = 4, SouthWest = 2, SouthEast = 4 });
            var innerSW = CreateMockTile(3, Tile.TileType.InnerCorner, Tile.TileDirection.SouthWest,
                new Tile.CornerSockets { NorthWest = 4, NorthEast = 2, SouthWest = 4, SouthEast = 4 });
            var innerSE = CreateMockTile(3, Tile.TileType.InnerCorner, Tile.TileDirection.SouthEast,
                new Tile.CornerSockets { NorthWest = 2, NorthEast = 4, SouthWest = 4, SouthEast = 4 });

            var tilesList = new List<Tile> {
                grassBlock, sandBlock,
                coastSouth, coastNorth, coastEast, coastWest,
                innerNW, innerNE, innerSW, innerSE
            };
            var tileset = CreateMockTilesetData(tilesList);

            Vector2Int chunkSize = new Vector2Int(3, 3);
            MapGenerator activeChunk = (MapGenerator)System.Runtime.Serialization.FormatterServices.GetUninitializedObject(typeof(MapGenerator));
            var grid = new ChunkCellGrid(chunkSize, tileset);
            grid.InitializeCells(null, null);

            typeof(MapGenerator).GetField("_grid", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance).SetValue(activeChunk, grid);
            typeof(MapGenerator).GetField("_chunkSize", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance).SetValue(activeChunk, chunkSize);
            typeof(MapGenerator).GetField("_tilesetData", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance).SetValue(activeChunk, tileset);

            for (int x = 0; x < 3; x++)
            {
                activeChunk.SetTileAt(x, 2, tilesList.IndexOf(grassBlock));
                activeChunk.SetTileAt(x, 0, tilesList.IndexOf(coastNorth));
            }
            activeChunk.SetTileAt(0, 1, tilesList.IndexOf(coastSouth));
            activeChunk.SetTileAt(2, 1, tilesList.IndexOf(coastSouth));
            activeChunk.SetTileAt(1, 1, tilesList.IndexOf(coastEast));

            Assert.IsTrue(StructureGenerator.HasCompatibilityError(activeChunk.GetTileAt(1, 1), activeChunk.GetTileAt(0, 1), Vector2Int.left),
                "O tile em (1, 1) deve inicialmente ter erro de compatibilidade com o vizinho Oeste!");

            StructureGenerator structGen = (StructureGenerator)System.Runtime.Serialization.FormatterServices.GetUninitializedObject(typeof(StructureGenerator));
            typeof(StructureGenerator).GetField("_chunkSize", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance).SetValue(structGen, chunkSize);
            typeof(StructureGenerator).GetField("_tilesetData", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance).SetValue(structGen, tileset);

            structGen.ResolveTileCompatibilityInChunk(Vector2Int.zero, activeChunk);

            Tile resolvedTile = activeChunk.GetTileAt(1, 1);
            Assert.IsTrue(ReferenceEquals(coastSouth, resolvedTile),
                $"O tile em (1, 1) deveria ter sido transformado em coastSouth, mas era {resolvedTile.Metadata.Type}_{resolvedTile.Metadata.Direction}!");

            Vector2Int[] cardinalDirs = { Vector2Int.up, Vector2Int.down, Vector2Int.left, Vector2Int.right };
            for (int y = 0; y < 3; y++)
            {
                for (int x = 0; x < 3; x++)
                {
                    Tile current = activeChunk.GetTileAt(x, y);
                    foreach (var dir in cardinalDirs)
                    {
                        int nx = x + dir.x;
                        int ny = y + dir.y;
                        if (nx >= 0 && nx < 3 && ny >= 0 && ny < 3)
                        {
                            Tile neighbor = activeChunk.GetTileAt(nx, ny);
                            Assert.IsFalse(StructureGenerator.HasCompatibilityError(current, neighbor, dir),
                                $"Ainda restou erro de compatibilidade entre ({x}, {y}) e ({nx}, {ny})!");
                        }
                    }
                }
            }
        }

        [Test]
        public void SettlementPlanner_RoadMeetingTransition_ClosesRoadWithInnerCornersTowardTransition()
        {
            int seed = 42;
            Vector2Int chunkSize = new Vector2Int(20, 20);
            IslandMapSampler sampler = new MockDeterministicIslandSampler(20);
            IslandLocator locator = new IslandLocator(sampler, chunkSize);

            var house = CreateMockBlueprint("House", new Vector2Int(2, 2), StructureCategory.Residential, 1f);
            var harbor = CreateMockBlueprint("Harbor", new Vector2Int(3, 3), StructureCategory.Harbor, 1f);
            var planner = new IslandSettlementPlanner(sampler, locator, chunkSize, seed, new List<StructureData> { house, harbor });
            var plan = planner.GetPlanForIsland(new Vector2Int(2, 2));

            Assert.IsNotNull(plan.Harbor, "O porto deve estar planejado na ilha!");

            var harborRoadCoords = plan.RoadTiles.Where(kvp => !kvp.Value.IsVisual || kvp.Value.Layer == 2).Select(kvp => kvp.Key).ToHashSet();
            Assert.IsTrue(harborRoadCoords.Count > 0, "Deveriam existir tiles de estrada não-visual sobre a areia!");

            var meetingTiles = new List<PlannedRoadTile>();
            Vector2Int[] cardinalDirs = { Vector2Int.up, Vector2Int.down, Vector2Int.left, Vector2Int.right };

            foreach (var kvp in plan.RoadTiles)
            {
                if (!kvp.Value.IsVisual) continue;

                Vector2Int pos = kvp.Key;
                foreach (var dir in cardinalDirs)
                {
                    Vector2Int neighborPos = pos + dir;
                    if (harborRoadCoords.Contains(neighborPos))
                    {
                        meetingTiles.Add(kvp.Value);
                        break;
                    }
                }
            }

            Assert.IsTrue(meetingTiles.Count > 0, "Deveria haver pelo menos um tile visual de estrada encontrando a transição/areia!");

            foreach (var meetingTile in meetingTiles)
            {
                Assert.AreEqual(Tile.TileType.InnerCorner, meetingTile.TileType,
                    $"Ao encontrar a transição/areia em {meetingTile.Coordinate}, a estrada DEVE fechar com InnerCorner!");

                if (harborRoadCoords.Contains(meetingTile.Coordinate + Vector2Int.down))
                {
                    Assert.AreEqual(4, meetingTile.Corners.SouthWest, "As corners inferiores voltadas para a transição/areia devem fechar com grama (SW=4)!");
                    Assert.AreEqual(4, meetingTile.Corners.SouthEast, "As corners inferiores voltadas para a transição/areia devem fechar com grama (SE=4)!");
                }

                if (harborRoadCoords.Contains(meetingTile.Coordinate + Vector2Int.up))
                {
                    Assert.AreEqual(4, meetingTile.Corners.NorthWest, "As corners superiores voltadas para a transição/areia devem fechar com grama (NW=4)!");
                    Assert.AreEqual(4, meetingTile.Corners.NorthEast, "As corners superiores voltadas para a transição/areia devem fechar com grama (NE=4)!");
                }
            }
        }

        [Test]
        public void SettlementPlanner_RoadClosesAtLowerLayerElements_AndCarvesOnlyOnLayerAtLeastFour()
        {
            var grassBlock = CreateMockTile(4, Tile.TileType.Block, Tile.TileDirection.None,
                new Tile.CornerSockets { NorthWest = 4, NorthEast = 4, SouthWest = 4, SouthEast = 4 });
            var coastTransition = CreateMockTile(3, Tile.TileType.Coast, Tile.TileDirection.South,
                new Tile.CornerSockets { NorthWest = 4, NorthEast = 4, SouthWest = 2, SouthEast = 2 });
            var sandBlock = CreateMockTile(2, Tile.TileType.Block, Tile.TileDirection.None,
                new Tile.CornerSockets { NorthWest = 2, NorthEast = 2, SouthWest = 2, SouthEast = 2 });
            var waterBlock = CreateMockTile(0, Tile.TileType.Block, Tile.TileDirection.None,
                new Tile.CornerSockets { NorthWest = 0, NorthEast = 0, SouthWest = 0, SouthEast = 0 });

            var tilesList = new List<Tile> { waterBlock, coastTransition, sandBlock, grassBlock };
            var tileset = CreateMockTilesetData(tilesList);

            Tile CellProvider(int gx, int gy)
            {
                if (gx == 0) return waterBlock;
                if (gx == 1) return sandBlock;
                if (gx == 2) return coastTransition;
                return grassBlock;
            }

            var planner = new IslandSettlementPlanner(null, null, new Vector2Int(10, 10), 42, new List<StructureData>(), CellProvider);

            Assert.IsTrue(planner.IsCellLand(3, 0), "Célula de camada 4 (grama) DEVE ser considerada terra firme (>= 4)!");

            Assert.IsFalse(planner.IsCellLand(2, 0), "Célula de camada 3 (transição para areia) é elemento de camada inferior onde a estrada fecha!");
            Assert.IsFalse(planner.IsCellLand(1, 0), "Célula de camada 2 (areia) é elemento de camada inferior!");
            Assert.IsFalse(planner.IsCellLand(0, 0), "Célula de camada 0 (água) não é terra!");

            Vector2Int chunkSize = new Vector2Int(4, 1);
            MapGenerator activeChunk = (MapGenerator)System.Runtime.Serialization.FormatterServices.GetUninitializedObject(typeof(MapGenerator));
            var grid = new ChunkCellGrid(chunkSize, tileset);
            grid.InitializeCells(null, null);

            typeof(MapGenerator).GetField("_grid", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance).SetValue(activeChunk, grid);
            typeof(MapGenerator).GetField("_chunkSize", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance).SetValue(activeChunk, chunkSize);
            typeof(MapGenerator).GetField("_tilesetData", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance).SetValue(activeChunk, tileset);

            activeChunk.SetTileAt(0, 0, tilesList.IndexOf(waterBlock));
            activeChunk.SetTileAt(1, 0, tilesList.IndexOf(sandBlock));
            activeChunk.SetTileAt(2, 0, tilesList.IndexOf(coastTransition));
            activeChunk.SetTileAt(3, 0, tilesList.IndexOf(grassBlock));

            var plan = new IslandSettlementPlan();
            for (int x = 0; x < 4; x++)
            {
                plan.RoadTiles[new Vector2Int(x, 0)] = new PlannedRoadTile
                {
                    Coordinate = new Vector2Int(x, 0),
                    Layer = 3,
                    TileType = Tile.TileType.Coast,
                    Direction = Tile.TileDirection.South,
                    IsVisual = true
                };
            }
            plan.RoadTilesByChunk[Vector2Int.zero] = plan.RoadTiles.Values.ToList();

            StructureGenerator structGen = (StructureGenerator)System.Runtime.Serialization.FormatterServices.GetUninitializedObject(typeof(StructureGenerator));
            typeof(StructureGenerator).GetField("_chunkSize", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance).SetValue(structGen, chunkSize);
            typeof(StructureGenerator).GetField("_tilesetData", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance).SetValue(structGen, tileset);

            var carveMethod = typeof(StructureGenerator).GetMethod("CarveRoadsInChunk", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            carveMethod.Invoke(structGen, new object[] { Vector2Int.zero, activeChunk, plan });

            Assert.AreEqual(3, activeChunk.GetTileAt(3, 0).Metadata.Layer, "Célula x=3 (Layer 4) deve ser esculpida!");

            Assert.AreEqual(3, activeChunk.GetTileAt(2, 0).Metadata.Layer, "Célula x=2 (Layer 3 - transição) NÃO pode ser esculpida!");
            Assert.IsTrue(ReferenceEquals(coastTransition, activeChunk.GetTileAt(2, 0)));

            Assert.AreEqual(2, activeChunk.GetTileAt(1, 0).Metadata.Layer, "Célula x=1 (Layer 2 - areia) NÃO pode ser esculpida!");
            Assert.IsTrue(ReferenceEquals(sandBlock, activeChunk.GetTileAt(1, 0)));

            Assert.AreEqual(0, activeChunk.GetTileAt(0, 0).Metadata.Layer, "Célula x=0 (Layer 0 - água) NÃO pode ser esculpida!");
        }

        [Test]
        public void SettlementPlanner_RoadTerminatingAtCoastSouth_HasZeroSocketErrorsAndMatchesCleanly()
        {
            var coastSouth = CreateMockTile(3, Tile.TileType.Coast, Tile.TileDirection.South,
                new Tile.CornerSockets { NorthWest = 4, NorthEast = 4, SouthWest = 2, SouthEast = 2 });
            var grassBlock = CreateMockTile(4, Tile.TileType.Block, Tile.TileDirection.None,
                new Tile.CornerSockets { NorthWest = 4, NorthEast = 4, SouthWest = 4, SouthEast = 4 });

            Assert.AreEqual(4, coastSouth.Metadata.Corners.NorthWest);
            Assert.AreEqual(4, coastSouth.Metadata.Corners.NorthEast);

            var visualCells = new HashSet<Vector2Int>
            {
                new Vector2Int(4, 1), new Vector2Int(5, 1),
                new Vector2Int(4, 2), new Vector2Int(5, 2)
            };

            int sw4 = (visualCells.Contains(new Vector2Int(3, 0)) && visualCells.Contains(new Vector2Int(4, 0)) && visualCells.Contains(new Vector2Int(3, 1)) && visualCells.Contains(new Vector2Int(4, 1))) ? 2 : 4;
            int se4 = (visualCells.Contains(new Vector2Int(4, 0)) && visualCells.Contains(new Vector2Int(5, 0)) && visualCells.Contains(new Vector2Int(4, 1)) && visualCells.Contains(new Vector2Int(5, 1))) ? 2 : 4;
            int nw4 = (visualCells.Contains(new Vector2Int(3, 1)) && visualCells.Contains(new Vector2Int(4, 1)) && visualCells.Contains(new Vector2Int(3, 2)) && visualCells.Contains(new Vector2Int(4, 2))) ? 2 : 4;
            int ne4 = (visualCells.Contains(new Vector2Int(4, 1)) && visualCells.Contains(new Vector2Int(5, 1)) && visualCells.Contains(new Vector2Int(4, 2)) && visualCells.Contains(new Vector2Int(5, 2))) ? 2 : 4;

            sw4 = 4; se4 = 4;
            nw4 = 4; ne4 = 2;

            bool resolved4 = IslandSettlementPlanner.TryResolveRoadTile(nw4, ne4, sw4, se4, out var tile4);
            Assert.IsTrue(resolved4, "Célula (4, 1) deve resolver como road tile!");
            Assert.AreEqual(Tile.TileType.InnerCorner, tile4.TileType);
            Assert.AreEqual(Tile.TileDirection.SouthWest, tile4.Direction);
            Assert.AreEqual(4, tile4.Corners.SouthWest, "SW de (4, 1) deve ser grama (4)!");
            Assert.AreEqual(4, tile4.Corners.SouthEast, "SE de (4, 1) deve ser grama (4)!");

            int sw5 = 4; int se5 = 4;
            int nw5 = 2; int ne5 = 4;

            bool resolved5 = IslandSettlementPlanner.TryResolveRoadTile(nw5, ne5, sw5, se5, out var tile5);
            Assert.IsTrue(resolved5, "Célula (5, 1) deve resolver como road tile!");
            Assert.AreEqual(Tile.TileType.InnerCorner, tile5.TileType);
            Assert.AreEqual(Tile.TileDirection.SouthEast, tile5.Direction);
            Assert.AreEqual(4, tile5.Corners.SouthWest, "SW de (5, 1) deve ser grama (4)!");
            Assert.AreEqual(4, tile5.Corners.SouthEast, "SE de (5, 1) deve ser grama (4)!");

            Assert.AreEqual(tile4.Corners.SouthWest, coastSouth.Metadata.Corners.NorthWest, "SW de (4,1) deve ser igual a NW de Coast South!");
            Assert.AreEqual(tile4.Corners.SouthEast, coastSouth.Metadata.Corners.NorthEast, "SE de (4,1) deve ser igual a NE de Coast South!");

            Assert.AreEqual(tile5.Corners.SouthWest, coastSouth.Metadata.Corners.NorthWest, "SW de (5,1) deve ser igual a NW de Coast South!");
            Assert.AreEqual(tile5.Corners.SouthEast, coastSouth.Metadata.Corners.NorthEast, "SE de (5,1) deve ser igual a NE de Coast South!");

            Assert.AreEqual(tile4.Corners.NorthEast, tile5.Corners.NorthWest, "Interior da estrada deve coincidir (2 == 2)!");
            Assert.AreEqual(tile4.Corners.SouthEast, tile5.Corners.SouthWest, "Borda de grama deve coincidir (4 == 4)!");
        }
    }
}
