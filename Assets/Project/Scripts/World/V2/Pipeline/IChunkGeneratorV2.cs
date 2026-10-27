using System.Collections.Generic;
using UnityEngine;
using Aegir.World.V2.Settlements;

namespace Aegir.World.V2.Pipeline
{
    public class ChunkResult
    {
        public Vector2Int ChunkCoordinate;
        public int[,] TileIndices; // [width, height]
        public List<PlannedStructureInstance> Structures = new List<PlannedStructureInstance>();
        public bool IsSuccess;
    }

    public interface IChunkGeneratorV2
    {
        ChunkResult GenerateChunk(Vector2Int chunkCoordinate, int worldSeed);
    }
}
