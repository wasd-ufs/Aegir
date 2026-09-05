using System.Collections;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

namespace Aegir.Tests.World
{
    [TestFixture]
    public class CellTests
    {
        private const int TileCount = 10;
        private Cell _cell;

        [SetUp]
        public void SetUp()
        {
            _cell = new Cell(TileCount, new Vector2Int(2, 3));
        }

        [Test]
        public void InitialState_AllBitsActive_CoordinatesMatch()
        {
            Assert.AreEqual(new Vector2Int(2, 3), _cell.Coordinates);
            Assert.AreEqual(TileCount, _cell.CountPossible());
            Assert.IsFalse(_cell.IsCollapsed());
            Assert.IsFalse(_cell.IsEmpty());
        }

        [TestCase(0)]
        [TestCase(4)]
        [TestCase(9)]
        public void CollapseCell_SingleIndex_BecomesCollapsedWithCorrectIndex(int targetIndex)
        {
            _cell.CollapseCell(targetIndex);

            Assert.IsTrue(_cell.IsCollapsed());
            Assert.IsFalse(_cell.IsEmpty());
            Assert.AreEqual(1, _cell.CountPossible());
            Assert.AreEqual(targetIndex, _cell.CollapsedIndex());
        }

        [Test]
        public void PossibleIndices_ReturnsExactIndicesActive()
        {
            _cell.PossibleBitsArray.SetAll(false);
            _cell.PossibleBitsArray[1] = true;
            _cell.PossibleBitsArray[5] = true;
            _cell.PossibleBitsArray[8] = true;

            List<int> indices = _cell.PossibleIndices();

            Assert.AreEqual(3, indices.Count);
            CollectionAssert.AreEqual(new[] { 1, 5, 8 }, indices);
        }

        [Test]
        public void IsEmpty_WhenNoBitsActive_ReturnsTrue()
        {
            _cell.PossibleBitsArray.SetAll(false);

            Assert.IsTrue(_cell.IsEmpty());
            Assert.IsFalse(_cell.IsCollapsed());
            Assert.AreEqual(0, _cell.CountPossible());
            Assert.AreEqual(-1, _cell.CollapsedIndex());
        }

        [Test]
        public void CopyFrom_CopiesBitStateAccurately()
        {
            BitArray externalBits = new BitArray(TileCount, false);
            externalBits[3] = true;
            externalBits[7] = true;

            _cell.CopyFrom(externalBits);

            Assert.AreEqual(2, _cell.CountPossible());
            Assert.IsTrue(_cell.PossibleBitsArray[3]);
            Assert.IsTrue(_cell.PossibleBitsArray[7]);
            Assert.IsFalse(_cell.PossibleBitsArray[0]);
        }
    }
}
