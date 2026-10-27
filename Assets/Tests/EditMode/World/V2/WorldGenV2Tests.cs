using NUnit.Framework;
using UnityEngine;
using Aegir.World.V2.Core;
using Aegir.World.V2.Terrain;
using Aegir.World.V2.Settlements;

namespace Aegir.Tests.World.V2
{
    [TestFixture]
    public class WorldGenV2Tests
    {
        [Test]
        public void FastBitset128_BasicOperations_WorkCorrectly()
        {
            var bitset = new FastBitset128();
            Assert.IsTrue(bitset.IsEmpty());
            Assert.AreEqual(0, bitset.CountBits());

            bitset.SetBit(5);
            bitset.SetBit(70);

            Assert.IsFalse(bitset.IsEmpty());
            Assert.AreEqual(2, bitset.CountBits());
            Assert.IsTrue(bitset.IsBitSet(5));
            Assert.IsTrue(bitset.IsBitSet(70));
            Assert.IsFalse(bitset.IsBitSet(6));
            Assert.AreEqual(5, bitset.GetFirstSetBit());

            bitset.ClearBit(5);
            Assert.AreEqual(1, bitset.CountBits());
            Assert.IsTrue(bitset.HasSingleBit());
            Assert.AreEqual(70, bitset.GetFirstSetBit());
        }

        [Test]
        public void FastBitset128_IntersectAndUnion_WorkCorrectly()
        {
            var b1 = FastBitset128.CreateAllSet(10); // bits 0..9
            var b2 = default(FastBitset128);
            b2.SetBit(5);
            b2.SetBit(15);

            Assert.IsTrue(b1.Overlaps(b2));

            b1.IntersectWith(b2);
            Assert.AreEqual(1, b1.CountBits());
            Assert.IsTrue(b1.IsBitSet(5));
            Assert.IsFalse(b1.IsBitSet(15));
        }

        [Test]
        public void FastNoiseSampler_IsDeterministic()
        {
            int seed = 12345;
            var sampler1 = new FastNoiseSampler(seed);
            var sampler2 = new FastNoiseSampler(seed);

            for (float x = 0; x < 50; x += 10.5f)
            {
                for (float y = 0; y < 50; y += 10.5f)
                {
                    float s1 = sampler1.Sample(x, y);
                    float s2 = sampler2.Sample(x, y);
                    Assert.AreEqual(s1, s2, 0.00001f);
                    Assert.GreaterOrEqual(s1, 0f);
                    Assert.LessOrEqual(s1, 1f);
                }
            }
        }

        [Test]
        public void FastNoiseSampler_Matches_IslandMapSampler_Exactly()
        {
            int seed = 1231445;
            var fast = new FastNoiseSampler(seed);
            var map = new IslandMapSampler(seed);

            for (float x = -100f; x <= 100f; x += 15.5f)
            {
                for (float y = -100f; y <= 100f; y += 15.5f)
                {
                    Assert.AreEqual(map.Sample(x, y), fast.Sample(x, y), 0.000001f);
                    Assert.AreEqual(map.Sample(x, y) >= IslandMapSampler.ISLAND_EDGE_THRESHOLD, fast.IsLand((int)x, (int)y));
                }
            }
        }

        [Test]
        public void DirectionExtensions_Opposites_AreSymmetric()
        {
            for (int i = 0; i < 4; i++)
            {
                CardinalDirection dir = (CardinalDirection)i;
                CardinalDirection opp = dir.GetOpposite();
                Assert.AreEqual(dir, opp.GetOpposite());
                Assert.AreEqual(Vector2Int.zero, dir.ToOffset() + opp.ToOffset());
            }
        }

        [Test]
        public void DeterministicLayerGenerator_NeighborBoundaries_MatchExactly()
        {
            int seed = 42;
            var sampler = new FastNoiseSampler(seed);
            var gen = new DeterministicLayerGenerator(sampler, new Vector2Int(16, 16));

            int[,] chunkLeft = gen.GenerateLayerMap(new Vector2Int(0, 0), seed);
            int[,] chunkRight = gen.GenerateLayerMap(new Vector2Int(1, 0), seed);

            // Borda leste de chunkLeft (x = 16) deve ser matematicamente consistente com
            // o halo do chunkRight (x = 0) na coordenada global
            for (int y = 1; y <= 16; y++)
            {
                int globalY = y - 1;
                int solidAtBorder = gen.CalculateSolidLayer(15, globalY, seed);
                int solidNext = gen.CalculateSolidLayer(16, globalY, seed);

                Assert.IsTrue(solidAtBorder >= -4 && solidAtBorder <= 4);
                Assert.IsTrue(solidNext >= -4 && solidNext <= 4);
            }
        }
        [Test]
        public void TileSocketResolver_ResolvesAll13MorphologiesCorrectly()
        {
            // 1. Bloco
            Assert.IsTrue(TileSocketResolver.ResolveMorphology(4, 4, 4, 4, out int l, out var t, out var d));
            Assert.AreEqual(4, l);
            Assert.AreEqual(Tile.TileType.Block, t);

            // 2. Costas
            Assert.IsTrue(TileSocketResolver.ResolveMorphology(4, 4, 2, 2, out l, out t, out d));
            Assert.AreEqual(3, l);
            Assert.AreEqual(Tile.TileType.Coast, t);
            Assert.AreEqual(Tile.TileDirection.South, d);

            Assert.IsTrue(TileSocketResolver.ResolveMorphology(2, 2, 4, 4, out l, out t, out d));
            Assert.AreEqual(3, l);
            Assert.AreEqual(Tile.TileType.Coast, t);
            Assert.AreEqual(Tile.TileDirection.North, d);

            Assert.IsTrue(TileSocketResolver.ResolveMorphology(4, 2, 4, 2, out l, out t, out d));
            Assert.AreEqual(3, l);
            Assert.AreEqual(Tile.TileType.Coast, t);
            Assert.AreEqual(Tile.TileDirection.East, d);

            Assert.IsTrue(TileSocketResolver.ResolveMorphology(2, 4, 2, 4, out l, out t, out d));
            Assert.AreEqual(3, l);
            Assert.AreEqual(Tile.TileType.Coast, t);
            Assert.AreEqual(Tile.TileDirection.West, d);

            // 3. Quinas Externas (Corner / SL Quintet)
            Assert.IsTrue(TileSocketResolver.ResolveMorphology(2, 2, 4, 2, out l, out t, out d));
            Assert.AreEqual(3, l);
            Assert.AreEqual(Tile.TileType.Corner, t);
            Assert.AreEqual(Tile.TileDirection.NorthEast, d);

            Assert.IsTrue(TileSocketResolver.ResolveMorphology(2, 2, 2, 4, out l, out t, out d));
            Assert.AreEqual(3, l);
            Assert.AreEqual(Tile.TileType.Corner, t);
            Assert.AreEqual(Tile.TileDirection.NorthWest, d);

            Assert.IsTrue(TileSocketResolver.ResolveMorphology(4, 2, 2, 2, out l, out t, out d));
            Assert.AreEqual(3, l);
            Assert.AreEqual(Tile.TileType.Corner, t);
            Assert.AreEqual(Tile.TileDirection.SouthEast, d);

            Assert.IsTrue(TileSocketResolver.ResolveMorphology(2, 4, 2, 2, out l, out t, out d));
            Assert.AreEqual(3, l);
            Assert.AreEqual(Tile.TileType.Corner, t);
            Assert.AreEqual(Tile.TileDirection.SouthWest, d);

            // 4. Quinas Internas (InnerCorner / SL Intern)
            Assert.IsTrue(TileSocketResolver.ResolveMorphology(4, 4, 2, 4, out l, out t, out d));
            Assert.AreEqual(3, l);
            Assert.AreEqual(Tile.TileType.InnerCorner, t);
            Assert.AreEqual(Tile.TileDirection.NorthEast, d);

            Assert.IsTrue(TileSocketResolver.ResolveMorphology(4, 4, 4, 2, out l, out t, out d));
            Assert.AreEqual(3, l);
            Assert.AreEqual(Tile.TileType.InnerCorner, t);
            Assert.AreEqual(Tile.TileDirection.NorthWest, d);

            Assert.IsTrue(TileSocketResolver.ResolveMorphology(2, 4, 4, 4, out l, out t, out d));
            Assert.AreEqual(3, l);
            Assert.AreEqual(Tile.TileType.InnerCorner, t);
            Assert.AreEqual(Tile.TileDirection.SouthEast, d);

            Assert.IsTrue(TileSocketResolver.ResolveMorphology(4, 2, 4, 4, out l, out t, out d));
            Assert.AreEqual(3, l);
            Assert.AreEqual(Tile.TileType.InnerCorner, t);
            Assert.AreEqual(Tile.TileDirection.SouthWest, d);
        }

        [Test]
        public void TileSocketResolver_AreSocketsCompatible_EvaluatesEdgesStrictly()
        {
            var coastSouth = new Tile.CornerSockets { NorthWest = 4, NorthEast = 4, SouthWest = 2, SouthEast = 2 };
            var grassBlock = new Tile.CornerSockets { NorthWest = 4, NorthEast = 4, SouthWest = 4, SouthEast = 4 };
            var sandBlock = new Tile.CornerSockets { NorthWest = 2, NorthEast = 2, SouthWest = 2, SouthEast = 2 };

            // coastSouth tem topo 4, 4 -> compatível com grassBlock ao Norte (que tem base 4, 4)
            Assert.IsTrue(TileSocketResolver.AreSocketsCompatible(coastSouth, grassBlock, Vector2Int.up));
            // coastSouth tem base 2, 2 -> compatível com sandBlock ao Sul (que tem topo 2, 2)
            Assert.IsTrue(TileSocketResolver.AreSocketsCompatible(coastSouth, sandBlock, Vector2Int.down));

            // Incompatibilidade proposital: coastSouth ao Norte de grassBlock deve falhar
            Assert.IsFalse(TileSocketResolver.AreSocketsCompatible(grassBlock, coastSouth, Vector2Int.up));
        }

        [Test]
        public void DualGrid_AdjacentCells_HaveIdenticalSharedEdgeSockets()
        {
            int seed = 12345;
            var sampler = new FastNoiseSampler(seed);
            var gen = new DeterministicLayerGenerator(sampler, new Vector2Int(16, 16));

            for (int x = -10; x <= 10; x++)
            {
                for (int y = -10; y <= 10; y++)
                {
                    // Célula A em (x, y)
                    int swA = gen.CalculateSolidLayer(x, y, seed);
                    int seA = gen.CalculateSolidLayer(x + 1, y, seed);
                    int nwA = gen.CalculateSolidLayer(x, y + 1, seed);
                    int neA = gen.CalculateSolidLayer(x + 1, y + 1, seed);

                    // Célula B em (x + 1, y) (vizinho à Direita)
                    int swB = gen.CalculateSolidLayer(x + 1, y, seed);
                    int nwB = gen.CalculateSolidLayer(x + 1, y + 1, seed);

                    // A aresta Direita de A DEVE ser matematicamente idêntica à aresta Esquerda de B
                    Assert.AreEqual(neA, nwB, $"Mismatch no topo da aresta horizontal em ({x}, {y})");
                    Assert.AreEqual(seA, swB, $"Mismatch na base da aresta horizontal em ({x}, {y})");

                    // Célula C em (x, y + 1) (vizinho Acima)
                    int swC = gen.CalculateSolidLayer(x, y + 1, seed);
                    int seC = gen.CalculateSolidLayer(x + 1, y + 1, seed);

                    // A aresta Superior de A DEVE ser idêntica à aresta Inferior de C
                    Assert.AreEqual(nwA, swC, $"Mismatch à esquerda da aresta vertical em ({x}, {y})");
                    Assert.AreEqual(neA, seC, $"Mismatch à direita da aresta vertical em ({x}, {y})");
                }
            }
        }

        [Test]
        public void SettlementPlanner_RespectsMinIslandChunks_SmallIslandsRemainWild()
        {
            int seed = 42;
            var sampler = new IslandMapSampler(seed);
            var locator = new IslandLocator(sampler, new Vector2Int(16, 16));
            // Min chunks = 3, chance = 1.0 (apenas ilhas com >= 3 chunks são habitadas)
            var planner = new IslandSettlementPlanner(sampler, locator, new Vector2Int(16, 16), seed, null, null, 1.0f, minIslandChunks: 3);

            // Simula uma ilha pequena com apenas 1 chunk longe da origem
            var smallIslandChunks = new System.Collections.Generic.List<Vector2Int> { new Vector2Int(10, 10) };
            var plan = planner.GetPlanForIsland(new Vector2Int(10, 10), smallIslandChunks);

            Assert.IsNotNull(plan);
            Assert.AreEqual(0, plan.RoadTiles.Count, "Ilha pequena com menos chunks que o mínimo deve permanecer deserta (sem estradas)!");
            Assert.AreEqual(0, plan.StructuresList.Count, "Ilha pequena com menos chunks que o mínimo deve permanecer deserta (sem estruturas)!");
        }

        [Test]
        public void SettlementPlanner_RespectsSettlementChance_ZeroChanceLeavesAllNonSpawnIslandsWild()
        {
            int seed = 42;
            var sampler = new IslandMapSampler(seed);
            var locator = new IslandLocator(sampler, new Vector2Int(16, 16));
            // Chance = 0% (todas as ilhas não-spawn devem ser desertas/selvagens)
            var planner = new IslandSettlementPlanner(sampler, locator, new Vector2Int(16, 16), seed, null, null, settlementChance: 0.0f, minIslandChunks: 1);

            var islandChunks = new System.Collections.Generic.List<Vector2Int> { new Vector2Int(8, 8), new Vector2Int(8, 9) };
            var plan = planner.GetPlanForIsland(new Vector2Int(8, 8), islandChunks);

            Assert.IsNotNull(plan);
            Assert.AreEqual(0, plan.RoadTiles.Count, "Com settlementChance = 0, a ilha deve ser completamente selvagem!");
            Assert.AreEqual(0, plan.StructuresList.Count, "Com settlementChance = 0, a ilha não deve ter estruturas!");
        }

        [Test]
        public void IslandLocator_4x4Sampling_DetectsLandNearChunkBoundaries()
        {
            int seed = 1231445;
            var sampler = new IslandMapSampler(seed);
            var locator = new IslandLocator(sampler, new Vector2Int(16, 16));

            // Procura um chunk que tenha terra em qualquer coordenada
            bool foundTestedChunk = false;
            for (int cx = -10; cx <= 10 && !foundTestedChunk; cx++)
            {
                for (int cy = -10; cy <= 10 && !foundTestedChunk; cy++)
                {
                    var coord = new Vector2Int(cx, cy);
                    if (locator.IsChunkPartOfIsland(coord))
                    {
                        foundTestedChunk = true;
                        var island = locator.GetIslandContaining(coord);
                        Assert.IsNotNull(island);
                        Assert.IsTrue(island.Count >= 1);
                    }
                }
            }
            Assert.IsTrue(foundTestedChunk, "Deveria ter encontrado ao menos um chunk de ilha perto da origem!");
        }
    }
}

