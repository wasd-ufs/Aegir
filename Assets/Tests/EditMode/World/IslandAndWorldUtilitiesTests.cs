using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

namespace Aegir.Tests.World
{
    [TestFixture]
    public class IslandAndWorldUtilitiesTests
    {
        [Test]
        public void IslandMapSampler_DeterministicOutput_SameSeedGivesSameHeight()
        {
            IslandMapSampler samplerA = new IslandMapSampler(12345);
            IslandMapSampler samplerB = new IslandMapSampler(12345);

            float sampleA1 = samplerA.Sample(10.5f, 20.3f);
            float sampleB1 = samplerB.Sample(10.5f, 20.3f);

            Assert.AreEqual(sampleA1, sampleB1, 0.0001f);
        }

        [Test]
        public void IslandMapSampler_OutputIsClampedBetweenZeroAndOne()
        {
            IslandMapSampler sampler = new IslandMapSampler(42);

            for (int x = -100; x <= 100; x += 25)
            {
                for (int y = -100; y <= 100; y += 25)
                {
                    float value = sampler.Sample(x, y);
                    Assert.GreaterOrEqual(value, 0.0f);
                    Assert.LessOrEqual(value, 1.0f);
                }
            }
        }

        [Test]
        public void ChunkGenerationQueue_EnqueueAndSort_ClosestChunkDequeuedFirst()
        {
            ChunkGenerationQueue queue = new ChunkGenerationQueue();
            Vector2Int playerPos = new Vector2Int(0, 0);

            queue.EnqueueChunk(new Vector2Int(10, 10), playerPos); // distance^2 = 200
            queue.EnqueueChunk(new Vector2Int(1, 1), playerPos);   // distance^2 = 2
            queue.EnqueueChunk(new Vector2Int(5, 0), playerPos);   // distance^2 = 25

            Assert.IsTrue(queue.TryGetNext(out Vector2Int first));
            Assert.AreEqual(new Vector2Int(1, 1), first);

            Assert.IsTrue(queue.TryGetNext(out Vector2Int second));
            Assert.AreEqual(new Vector2Int(5, 0), second);

            Assert.IsTrue(queue.TryGetNext(out Vector2Int third));
            Assert.AreEqual(new Vector2Int(10, 10), third);

            Assert.IsFalse(queue.TryGetNext(out _));
        }

        [Test]
        public void ChunkPersistence_SaveAndLoad_RecoversOriginalBytes()
        {
            ChunkPersistence persistence = new ChunkPersistence();
            Vector2Int testCoord = new Vector2Int(9999, 9999);
            byte[] testData = new byte[] { 0, 1, 2, 3, 4, 255 };

            try
            {
                persistence.SaveChunkToDisk(testCoord, testData);
                byte[] loaded = persistence.LoadChunkFromDisk(testCoord);

                Assert.IsNotNull(loaded);
                CollectionAssert.AreEqual(testData, loaded);
            }
            finally
            {
                // Limpeza
                string path = Application.persistentDataPath + $"/map_data/chunk_{testCoord.x}_{testCoord.y}.dat";
                if (System.IO.File.Exists(path))
                    System.IO.File.Delete(path);
            }
        }

        [Test]
        public void IslandLocator_InvalidRadiusRange_ReturnsEmptyList()
        {
            IslandMapSampler sampler = new IslandMapSampler(123);
            IslandLocator locator = new IslandLocator(sampler, new Vector2Int(16, 16));

            List<Vector2Int> islands = locator.FindIslandsInRange(Vector2Int.zero, 10, 2);
            Assert.IsEmpty(islands, "Quando minRadius > maxRadius, a busca não deve retornar nenhum chunk.");
        }

        [Test]
        public void IslandLocator_AlwaysExcludesOriginZeroZero()
        {
            IslandMapSampler sampler = new IslandMapSampler(123);
            IslandLocator locator = new IslandLocator(sampler, new Vector2Int(16, 16));

            List<Vector2Int> islands = locator.FindIslandsInRange(Vector2Int.zero, 0, 10);
            Assert.IsFalse(islands.Contains(Vector2Int.zero), "O chunk (0,0) nunca deve ser retornado como ilha.");
        }
    }
}
