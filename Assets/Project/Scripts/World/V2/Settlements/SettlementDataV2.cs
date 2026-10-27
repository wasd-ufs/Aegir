using System;
using System.Collections.Generic;
using UnityEngine;

namespace Aegir.World.V2.Settlements
{
    [Serializable]
    public class PlannedStructureInstance
    {
        public StructureData Blueprint;
        public Vector2Int GlobalTileOrigin;
        public Vector2Int Dimensions;
        public Vector2Int ChunkPosition;
        public Vector2Int DoorGlobalCoordinate;
        public Vector3 WorldPosition;
    }

    [Serializable]
    public struct PlannedRoadCell
    {
        public Vector2Int GlobalCoordinate;
        public int Layer;
        public Tile.TileType TileType;
        public Tile.TileDirection Direction;
        public int SpecificTileIndex; // -1 se não especificado
    }

    public class IslandPlanV2
    {
        public Vector2Int CenterChunk;
        public Vector2Int CenterTile;
        public PlannedStructureInstance Harbor;
        public List<PlannedStructureInstance> Structures = new List<PlannedStructureInstance>();
        public Dictionary<Vector2Int, PlannedRoadCell> Roads = new Dictionary<Vector2Int, PlannedRoadCell>();
        public Dictionary<Vector2Int, List<PlannedStructureInstance>> StructuresByChunk = new Dictionary<Vector2Int, List<PlannedStructureInstance>>();
        public Dictionary<Vector2Int, List<PlannedRoadCell>> RoadsByChunk = new Dictionary<Vector2Int, List<PlannedRoadCell>>();
    }
}
