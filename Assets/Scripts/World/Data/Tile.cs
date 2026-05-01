using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Tilemaps;

[CreateAssetMenu(fileName = "New Tile", menuName = "WFC/Tile")]
public class Tile : ScriptableObject
{
    public enum TileType : byte
    {
        Block,
        Coast,
        Corner,
        InnerCorner
    }

    public enum TileDirection 
    { 
        North, 
        South, 
        West, 
        East, 
        NorthEast, 
        NorthWest, 
        SouthEast, 
        SouthWest, 
        None 
    }

    [Serializable]
    public struct CornerSockets
    {
        public int NorthWest;
        public int NorthEast;
        public int SouthWest;
        public int SouthEast;
    }

    [Serializable]
    public struct SpawnableCreature
    {
        [SerializeField] private GameObject _creaturePrefab;
        [SerializeField] private int _quantity;
        [SerializeField, Range(0, 1f)] private float _spawnChance;

        public GameObject CreaturePrefab => _creaturePrefab;
        public int Quantity => _quantity;
        public float SpawnChance => _spawnChance;
    }

    [Serializable]
    public struct TileMetadata
    {
        [SerializeField] private int _layer;
        [SerializeField] private TileType _type;
        [SerializeField] private TileDirection _direction;

        public CornerSockets Corners { get; set; }

        public int Layer => _layer;
        public TileType Type => _type;
        public TileDirection Direction => _direction;
    }

    [SerializeField] private TileBase _tilemapTile;
    [SerializeField] private float _weight = 1f;
    [SerializeField] private List<SpawnableCreature> _spawnableCreaturesList;
    [SerializeField] private TileMetadata _metadata;

    public TileBase TilemapTile => _tilemapTile;
    public float Weight => _weight;
    public List<SpawnableCreature> SpawnableCreaturesList => _spawnableCreaturesList;
    public TileMetadata Metadata => _metadata;

    private void OnValidate()
    {
        CornerSockets generatedCorners = GenerateCorners(_metadata.Layer, _metadata.Type, _metadata.Direction);
        _metadata.Corners = generatedCorners; 
    }

    private CornerSockets GenerateCorners(int baseLayer, TileType type, TileDirection direction)
    {
        int lowerLayer = baseLayer - 1;
        int upperLayer = baseLayer + 1;

        if (type == TileType.Block)
        {
            return new CornerSockets { NorthWest = baseLayer, NorthEast = baseLayer, SouthWest = baseLayer, SouthEast = baseLayer };
        }

        return (type, direction) switch
        {
            (TileType.Coast, TileDirection.North) => new CornerSockets { NorthWest = lowerLayer, NorthEast = lowerLayer, SouthWest = upperLayer, SouthEast = upperLayer },
            (TileType.Coast, TileDirection.South) => new CornerSockets { NorthWest = upperLayer, NorthEast = upperLayer, SouthWest = lowerLayer, SouthEast = lowerLayer },
            (TileType.Coast, TileDirection.East) => new CornerSockets { NorthWest = upperLayer, NorthEast = lowerLayer, SouthWest = upperLayer, SouthEast = lowerLayer },
            (TileType.Coast, TileDirection.West) => new CornerSockets { NorthWest = lowerLayer, NorthEast = upperLayer, SouthWest = lowerLayer, SouthEast = upperLayer },

            (TileType.Corner, TileDirection.NorthEast) => new CornerSockets { NorthWest = lowerLayer, NorthEast = lowerLayer, SouthWest = upperLayer, SouthEast = lowerLayer },
            (TileType.Corner, TileDirection.NorthWest) => new CornerSockets { NorthWest = lowerLayer, NorthEast = lowerLayer, SouthWest = lowerLayer, SouthEast = upperLayer },
            (TileType.Corner, TileDirection.SouthEast) => new CornerSockets { NorthWest = upperLayer, NorthEast = lowerLayer, SouthWest = lowerLayer, SouthEast = lowerLayer },
            (TileType.Corner, TileDirection.SouthWest) => new CornerSockets { NorthWest = lowerLayer, NorthEast = upperLayer, SouthWest = lowerLayer, SouthEast = lowerLayer },

            (TileType.InnerCorner, TileDirection.NorthEast) => new CornerSockets { NorthWest = upperLayer, NorthEast = upperLayer, SouthWest = lowerLayer, SouthEast = upperLayer },
            (TileType.InnerCorner, TileDirection.NorthWest) => new CornerSockets { NorthWest = upperLayer, NorthEast = upperLayer, SouthWest = upperLayer, SouthEast = lowerLayer },
            (TileType.InnerCorner, TileDirection.SouthEast) => new CornerSockets { NorthWest = lowerLayer, NorthEast = upperLayer, SouthWest = upperLayer, SouthEast = upperLayer },
            (TileType.InnerCorner, TileDirection.SouthWest) => new CornerSockets { NorthWest = upperLayer, NorthEast = lowerLayer, SouthWest = upperLayer, SouthEast = upperLayer },

            _ => new CornerSockets()
        };
    }

    public bool IsCompatibleWith(Tile neighbor, Vector2Int offsetDirection)
    {
        CornerSockets thisCorners = _metadata.Corners;
        CornerSockets neighborCorners = neighbor.Metadata.Corners;

        if (offsetDirection == Vector2Int.right)
            return thisCorners.NorthEast == neighborCorners.NorthWest && thisCorners.SouthEast == neighborCorners.SouthWest;

        if (offsetDirection == Vector2Int.left)
            return thisCorners.NorthWest == neighborCorners.NorthEast && thisCorners.SouthWest == neighborCorners.SouthEast;

        if (offsetDirection == Vector2Int.up)
            return thisCorners.NorthWest == neighborCorners.SouthWest && thisCorners.NorthEast == neighborCorners.SouthEast;

        if (offsetDirection == Vector2Int.down)
            return thisCorners.SouthWest == neighborCorners.NorthWest && thisCorners.SouthEast == neighborCorners.NorthEast;

        return false;
    }
}