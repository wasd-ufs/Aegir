using System;
using UnityEngine;

namespace Aegir.World.V2.Core
{
    /// <summary>
    /// Grade WFC contígua em memória para um chunk (dimensões locais + 1 célula de halo em cada borda).
    /// Elimina alocações individuais por célula, mantendo os estados de entropia em arrays planos.
    /// </summary>
    public class WfcGridV2
    {
        public readonly int InnerWidth;
        public readonly int InnerHeight;
        public readonly int GridWidth;
        public readonly int GridHeight;

        private readonly int _tileCount;
        private readonly FastBitset128[] _cells;
        private readonly FastBitset128[] _haloSnapshot;
        private readonly FastBitset128 _allPossibleMask;

        public FastBitset128[] Cells => _cells;

        public WfcGridV2(Vector2Int chunkSize, int tileCount)
        {
            InnerWidth = chunkSize.x;
            InnerHeight = chunkSize.y;
            GridWidth = InnerWidth + 2;
            GridHeight = InnerHeight + 2;

            _tileCount = tileCount;
            _allPossibleMask = FastBitset128.CreateAllSet(tileCount);

            int totalCells = GridWidth * GridHeight;
            _cells = new FastBitset128[totalCells];
            _haloSnapshot = new FastBitset128[totalCells];
        }

        public int ToIndex(int x, int y)
        {
            return x * GridHeight + y;
        }

        public void FromIndex(int index, out int x, out int y)
        {
            x = index / GridHeight;
            y = index % GridHeight;
        }

        public bool IsInsideBounds(int x, int y)
        {
            return x >= 0 && x < GridWidth && y >= 0 && y < GridHeight;
        }

        public bool IsInner(int x, int y)
        {
            return x >= 1 && x <= InnerWidth && y >= 1 && y <= InnerHeight;
        }

        public void ResetAll()
        {
            for (int i = 0; i < _cells.Length; i++)
            {
                _cells[i] = _allPossibleMask;
            }
        }

        public void SnapshotHalo()
        {
            Array.Copy(_cells, _haloSnapshot, _cells.Length);
        }

        public void RestoreFromHaloSnapshot()
        {
            // Restaura as bordas do halo a partir do snapshot e reseta as células internas
            for (int x = 0; x < GridWidth; x++)
            {
                for (int y = 0; y < GridHeight; y++)
                {
                    int idx = ToIndex(x, y);
                    if (IsInner(x, y))
                    {
                        _cells[idx] = _allPossibleMask;
                    }
                    else
                    {
                        _cells[idx] = _haloSnapshot[idx];
                    }
                }
            }
        }

        public void CollapseCell(int x, int y, int tileIndex)
        {
            int idx = ToIndex(x, y);
            _cells[idx] = FastBitset128.SingleBit(tileIndex);
        }

        public bool IsCollapsed(int x, int y)
        {
            return _cells[ToIndex(x, y)].HasSingleBit();
        }

        public int GetCollapsedIndex(int x, int y)
        {
            return _cells[ToIndex(x, y)].GetFirstSetBit();
        }

        public int CountPossible(int x, int y)
        {
            return _cells[ToIndex(x, y)].CountBits();
        }
    }
}
