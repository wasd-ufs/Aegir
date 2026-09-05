using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;

namespace Aegir.Tests.World
{
    [TestFixture]
    public class WFCAlgorithmTests
    {
        private TilesetData _tilesetData;
        private Tile _waterTile;
        private Tile _sandTile;
        private Tile _grassTile;
        private ChunkCellGrid _grid;
        private Vector2Int _chunkSize;
        private WFCAlgorithm _wfc;
        private GameObject _ruleManagerHost;
        private RuleManager _ruleManager;
        private CompatibilityCache _cache;

        [SetUp]
        public void SetUp()
        {
            _chunkSize = new Vector2Int(3, 3);

            _waterTile = ScriptableObject.CreateInstance<Tile>();
            SetTileMetadata(_waterTile, 0, Tile.TileType.Block, Tile.TileDirection.None);

            _sandTile = ScriptableObject.CreateInstance<Tile>();
            SetTileMetadata(_sandTile, 1, Tile.TileType.Block, Tile.TileDirection.None);

            _grassTile = ScriptableObject.CreateInstance<Tile>();
            SetTileMetadata(_grassTile, 2, Tile.TileType.Block, Tile.TileDirection.None);

            _tilesetData = ScriptableObject.CreateInstance<TilesetData>();
            typeof(TilesetData).GetField("_tilesetList", BindingFlags.NonPublic | BindingFlags.Instance)
                ?.SetValue(_tilesetData, new List<Tile> { _waterTile, _sandTile, _grassTile });

            _ruleManagerHost = new GameObject("RuleManagerHost");
            _ruleManager = _ruleManagerHost.AddComponent<RuleManager>();
            typeof(RuleManager).GetField("_tilesetData", BindingFlags.NonPublic | BindingFlags.Instance)
                ?.SetValue(_ruleManager, _tilesetData);
            typeof(RuleManager).GetField("_blockingRulesList", BindingFlags.NonPublic | BindingFlags.Instance)
                ?.SetValue(_ruleManager, new List<RuleManager.TileRule>());

            _cache = new CompatibilityCache(_ruleManager, _tilesetData);

            _grid = new ChunkCellGrid(_chunkSize, _tilesetData);
            _grid.InitializeCells(null, null);

            _wfc = new WFCAlgorithm(_grid, _chunkSize, _cache, _tilesetData);
            _wfc.SetState(null, new System.Random(42), Vector2Int.zero);
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(_waterTile);
            Object.DestroyImmediate(_sandTile);
            Object.DestroyImmediate(_grassTile);
            Object.DestroyImmediate(_tilesetData);
            Object.DestroyImmediate(_ruleManagerHost);
        }

        private static void SetTileMetadata(Tile tile, int layer, Tile.TileType type, Tile.TileDirection direction)
        {
            var meta = new Tile.TileMetadata();
            object boxed = meta;
            typeof(Tile.TileMetadata).GetField("_layer", BindingFlags.NonPublic | BindingFlags.Instance)?.SetValue(boxed, layer);
            typeof(Tile.TileMetadata).GetField("_type", BindingFlags.NonPublic | BindingFlags.Instance)?.SetValue(boxed, type);
            typeof(Tile.TileMetadata).GetField("_direction", BindingFlags.NonPublic | BindingFlags.Instance)?.SetValue(boxed, direction);
            typeof(Tile).GetField("_metadata", BindingFlags.NonPublic | BindingFlags.Instance)?.SetValue(tile, boxed);
        }

        [Test]
        public void ChooseCell_ReturnsCellWithLowestEntropy()
        {
            // Todas as células iniciam com 3 opções (CountPossible == 3, não colapsadas)
            // Célula (2,2) terá 2 opções restantes (CountPossible == 2, ainda não colapsada, mas com menor entropia que as demais)
            Cell targetCell = _grid.CellsArray[2, 2];
            targetCell.PossibleBitsArray.Set(2, false); // Resta tiles 0 e 1 (2 opções)

            Cell chosen = _wfc.ChooseCell();

            Assert.IsNotNull(chosen);
            Assert.AreEqual(new Vector2Int(2, 2), chosen.Coordinates, "Deveria escolher a célula de menor entropia (2 possibilidades vs 3 das outras).");
        }

        [Test]
        public void ChooseCell_WhenAllCollapsed_ReturnsNull()
        {
            // Colapsar todas as células internas (1..chunkSize.x, 1..chunkSize.y)
            for (int x = 1; x <= _chunkSize.x; x++)
            {
                for (int y = 1; y <= _chunkSize.y; y++)
                {
                    _grid.CellsArray[x, y].CollapseCell(0);
                }
            }

            Cell chosen = _wfc.ChooseCell();
            Assert.IsNull(chosen, "Quando todas as células estão colapsadas, ChooseCell deve retornar null.");
        }

        [Test]
        public void Contradiction_DetectsEmptyCellAccurately()
        {
            Assert.IsFalse(_wfc.HasContradiction(), "Inicialmente a grade não deve ter contradições.");
            Assert.IsNull(_wfc.GetContradictionCell());

            // Forçar contradição em (1, 1)
            Cell contradictionTarget = _grid.CellsArray[1, 1];
            contradictionTarget.PossibleBitsArray.SetAll(false);

            Assert.IsTrue(_wfc.HasContradiction(), "Deve acusar contradição após uma célula esvaziar.");
            Assert.AreSame(contradictionTarget, _wfc.GetContradictionCell());
        }

        [Test]
        public void ApplyTargetLayerConstraints_FiltersCellOptionsBasedOnTargetMap()
        {
            // Mapa 5x5 (chunkSize 3x3 + 2 halo)
            int[,] targetMap = new int[5, 5];
            // Configurar tudo como água (layer 0), exceto (2,2) como terra (layer 1)
            for (int x = 0; x < 5; x++)
                for (int y = 0; y < 5; y++)
                    targetMap[x, y] = 0;

            targetMap[2, 2] = 1;

            _wfc.SetState(targetMap, new System.Random(123), Vector2Int.zero);

            // Antes da restrição, todas têm 3 opções (água, terra e grama)
            Assert.AreEqual(3, _grid.CellsArray[2, 2].CountPossible());

            // Testar o método de filtragem de camada diretamente em células
            MethodInfo restrictMethod = typeof(WFCAlgorithm).GetMethod("RestrictCellToTargetLayer", BindingFlags.NonPublic | BindingFlags.Instance);
            Assert.IsNotNull(restrictMethod);

            // Restringir célula (1,1) para camada 0 (água)
            bool changed = (bool)restrictMethod.Invoke(_wfc, new object[] { _grid.CellsArray[1, 1], 0 });
            Assert.IsTrue(changed);
            Assert.IsTrue(_grid.CellsArray[1, 1].PossibleBitsArray[0], "Tile de água (layer 0) deve permanecer.");
            Assert.IsFalse(_grid.CellsArray[1, 1].PossibleBitsArray[1], "Tile de terra (layer 1) deve ter sido removido.");
            Assert.IsFalse(_grid.CellsArray[1, 1].PossibleBitsArray[2], "Tile de grama (layer 2) deve ter sido removido.");

            // Restringir célula (2,2) para camada 1 (terra)
            bool changed2 = (bool)restrictMethod.Invoke(_wfc, new object[] { _grid.CellsArray[2, 2], 1 });
            Assert.IsTrue(changed2);
            Assert.IsFalse(_grid.CellsArray[2, 2].PossibleBitsArray[0], "Tile de água (layer 0) deve ter sido removido.");
            Assert.IsTrue(_grid.CellsArray[2, 2].PossibleBitsArray[1], "Tile de terra (layer 1) deve permanecer.");
            Assert.IsFalse(_grid.CellsArray[2, 2].PossibleBitsArray[2], "Tile de grama (layer 2) deve ter sido removido.");
        }
    }
}
