using UnityEngine;

namespace Aegir.World.V2.Terrain
{
    /// <summary>
    /// Sampler de terreno determinístico V2.
    /// Deriva diretamente do IslandMapSampler para garantir 100% de paridade matemática,
    /// topológica e de semente com o IslandLocator e IslandSettlementPlanner.
    /// </summary>
    public class FastNoiseSampler : IslandMapSampler
    {
        public FastNoiseSampler(int worldSeed) : base(worldSeed)
        {
        }

        public bool IsLand(int globalX, int globalY)
        {
            return Sample(globalX, globalY) >= ISLAND_EDGE_THRESHOLD;
        }

        public bool IsLand(float globalX, float globalY)
        {
            return Sample(globalX, globalY) >= ISLAND_EDGE_THRESHOLD;
        }
    }
}
