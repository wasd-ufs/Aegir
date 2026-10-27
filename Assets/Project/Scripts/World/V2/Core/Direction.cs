using UnityEngine;

namespace Aegir.World.V2.Core
{
    /// <summary>
    /// Representa as direções cardinais e diagonais com índices otimizados para lookup em tabelas.
    /// </summary>
    public enum CardinalDirection : byte
    {
        Up = 0,
        Down = 1,
        Left = 2,
        Right = 3
    }

    public static class DirectionExtensions
    {
        public static readonly Vector2Int[] CardinalOffsets = new Vector2Int[]
        {
            Vector2Int.up,      // 0: Up (0, 1)
            Vector2Int.down,    // 1: Down (0, -1)
            Vector2Int.left,    // 2: Left (-1, 0)
            Vector2Int.right    // 3: Right (1, 0)
        };

        public static readonly CardinalDirection[] OppositeDirections = new CardinalDirection[]
        {
            CardinalDirection.Down,  // Inverso de Up
            CardinalDirection.Up,    // Inverso de Down
            CardinalDirection.Right, // Inverso de Left
            CardinalDirection.Left   // Inverso de Right
        };

        public static Vector2Int ToOffset(this CardinalDirection direction)
        {
            return CardinalOffsets[(int)direction];
        }

        public static CardinalDirection GetOpposite(this CardinalDirection direction)
        {
            return OppositeDirections[(int)direction];
        }

        public static int ToIndex(this CardinalDirection direction)
        {
            return (int)direction;
        }

        public static CardinalDirection FromOffset(Vector2Int offset)
        {
            if (offset == Vector2Int.up) return CardinalDirection.Up;
            if (offset == Vector2Int.down) return CardinalDirection.Down;
            if (offset == Vector2Int.left) return CardinalDirection.Left;
            return CardinalDirection.Right;
        }
    }
}
