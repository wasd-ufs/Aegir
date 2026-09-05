using System.Reflection;
using NUnit.Framework;
using UnityEngine;

namespace Aegir.Tests.World
{
    [TestFixture]
    public class TileCompatibilityTests
    {
        private Tile _tileA;
        private Tile _tileB;

        [SetUp]
        public void SetUp()
        {
            _tileA = ScriptableObject.CreateInstance<Tile>();
            _tileB = ScriptableObject.CreateInstance<Tile>();
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(_tileA);
            Object.DestroyImmediate(_tileB);
        }

        private static void SetCorners(Tile tile, Tile.CornerSockets sockets)
        {
            var meta = new Tile.TileMetadata { Corners = sockets };
            object boxed = meta;
            typeof(Tile).GetField("_metadata", BindingFlags.NonPublic | BindingFlags.Instance)?.SetValue(tile, boxed);
        }

        [Test]
        public void IsCompatibleWith_HorizontalRight_MatchesEastToWestSockets()
        {
            // Tile A (esquerda): NorthEast = 1, SouthEast = 0
            SetCorners(_tileA, new Tile.CornerSockets { NorthWest = 0, NorthEast = 1, SouthWest = 0, SouthEast = 0 });
            // Tile B (direita): NorthWest = 1, SouthWest = 0 -> compatível!
            SetCorners(_tileB, new Tile.CornerSockets { NorthWest = 1, NorthEast = 0, SouthWest = 0, SouthEast = 0 });

            Assert.IsTrue(_tileA.IsCompatibleWith(_tileB, Vector2Int.right));
            Assert.IsTrue(_tileB.IsCompatibleWith(_tileA, Vector2Int.left));

            // Modificar B para ter NorthWest = 2 -> incompatível
            SetCorners(_tileB, new Tile.CornerSockets { NorthWest = 2, NorthEast = 0, SouthWest = 0, SouthEast = 0 });
            Assert.IsFalse(_tileA.IsCompatibleWith(_tileB, Vector2Int.right));
        }

        [Test]
        public void IsCompatibleWith_VerticalUpDown_MatchesNorthToSouthSockets()
        {
            // Tile A (em baixo): NorthWest = 1, NorthEast = 2
            SetCorners(_tileA, new Tile.CornerSockets { NorthWest = 1, NorthEast = 2, SouthWest = 0, SouthEast = 0 });
            // Tile B (em cima): SouthWest = 1, SouthEast = 2 -> compatível!
            SetCorners(_tileB, new Tile.CornerSockets { NorthWest = 0, NorthEast = 0, SouthWest = 1, SouthEast = 2 });

            Assert.IsTrue(_tileA.IsCompatibleWith(_tileB, Vector2Int.up));
            Assert.IsTrue(_tileB.IsCompatibleWith(_tileA, Vector2Int.down));

            // Modificar B para SouthEast = 0 -> incompatível
            SetCorners(_tileB, new Tile.CornerSockets { NorthWest = 0, NorthEast = 0, SouthWest = 1, SouthEast = 0 });
            Assert.IsFalse(_tileA.IsCompatibleWith(_tileB, Vector2Int.up));
        }

        [Test]
        public void IsCompatibleWith_InvalidDirection_ReturnsFalse()
        {
            SetCorners(_tileA, new Tile.CornerSockets { NorthWest = 1, NorthEast = 1, SouthWest = 1, SouthEast = 1 });
            SetCorners(_tileB, new Tile.CornerSockets { NorthWest = 1, NorthEast = 1, SouthWest = 1, SouthEast = 1 });

            // Diagonal não é suportada diretamente por IsCompatibleWith
            Assert.IsFalse(_tileA.IsCompatibleWith(_tileB, new Vector2Int(1, 1)));
            Assert.IsFalse(_tileA.IsCompatibleWith(_tileB, Vector2Int.zero));
        }
    }
}
