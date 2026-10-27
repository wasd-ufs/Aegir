using System;
using UnityEngine;

namespace Aegir.World.V2.Core
{
    /// <summary>
    /// Motor WFC de alta velocidade com zero alocações por passo de propagação.
    /// Utiliza fila circular de inteiros e bitsets de 128 bits para colapso de entropia.
    /// </summary>
    public class FastWfcSolver
    {
        private readonly WfcGridV2 _grid;
        private readonly CompatibilityMatrixV2 _compatibilityMatrix;
        private readonly int _tileCount;

        // Fila circular pré-alocada para propagação BFS
        private readonly int[] _propagationQueue;
        private readonly bool[] _inQueueFlags;
        private int _queueHead;
        private int _queueTail;
        private int _queueCount;

        // Buffer pré-alocado para escolha de células de menor entropia
        private readonly int[] _candidateBuffer;

        public FastWfcSolver(WfcGridV2 grid, CompatibilityMatrixV2 compatibilityMatrix)
        {
            _grid = grid ?? throw new ArgumentNullException(nameof(grid));
            _compatibilityMatrix = compatibilityMatrix ?? throw new ArgumentNullException(nameof(compatibilityMatrix));
            _tileCount = compatibilityMatrix.TileCount;

            int totalCells = _grid.GridWidth * _grid.GridHeight;
            _propagationQueue = new int[totalCells * 4];
            _inQueueFlags = new bool[totalCells];
            _candidateBuffer = new int[totalCells];
        }

        public void EnqueueCell(int x, int y)
        {
            int cellIndex = _grid.ToIndex(x, y);
            if (_inQueueFlags[cellIndex]) return;

            _inQueueFlags[cellIndex] = true;
            _propagationQueue[_queueTail] = cellIndex;
            _queueTail = (_queueTail + 1) % _propagationQueue.Length;
            _queueCount++;
        }

        private int DequeueCell()
        {
            if (_queueCount == 0) return -1;
            int cellIndex = _propagationQueue[_queueHead];
            _queueHead = (_queueHead + 1) % _propagationQueue.Length;
            _queueCount--;
            _inQueueFlags[cellIndex] = false;
            return cellIndex;
        }

        public bool PropagateAll()
        {
            while (_queueCount > 0)
            {
                int currentCellIndex = DequeueCell();
                _grid.FromIndex(currentCellIndex, out int cx, out int cy);
                FastBitset128 currentMask = _grid.Cells[currentCellIndex];

                if (currentMask.IsEmpty())
                {
                    return false; // Contradição detectada
                }

                // Propaga para os 4 vizinhos cardinais
                for (int dir = 0; dir < 4; dir++)
                {
                    CardinalDirection direction = (CardinalDirection)dir;
                    Vector2Int offset = direction.ToOffset();
                    int nx = cx + offset.x;
                    int ny = cy + offset.y;

                    if (!_grid.IsInsideBounds(nx, ny)) continue;

                    int neighborIndex = _grid.ToIndex(nx, ny);
                    FastBitset128 neighborMask = _grid.Cells[neighborIndex];
                    if (neighborMask.HasSingleBit()) continue; // Já colapsado

                    // Calcula a união dos vizinhos suportados por todos os bits ativos na célula atual
                    FastBitset128 allowedNeighbors = default;

                    // Itera apenas sobre os bits ativos de currentMask
                    ulong p0 = currentMask.Part0;
                    while (p0 != 0UL)
                    {
                        int bit = TrailingZeroCount(p0);
                        allowedNeighbors.UnionWith(_compatibilityMatrix.GetAllowedNeighborsMask(bit, direction));
                        p0 &= p0 - 1UL; // Limpa o bit menos significativo
                    }

                    ulong p1 = currentMask.Part1;
                    while (p1 != 0UL)
                    {
                        int bit = 64 + TrailingZeroCount(p1);
                        allowedNeighbors.UnionWith(_compatibilityMatrix.GetAllowedNeighborsMask(bit, direction));
                        p1 &= p1 - 1UL;
                    }

                    // Reduz as possibilidades do vizinho
                    FastBitset128 newNeighborMask = neighborMask;
                    newNeighborMask.IntersectWith(allowedNeighbors);

                    if (newNeighborMask != neighborMask)
                    {
                        if (newNeighborMask.IsEmpty())
                        {
                            return false; // Contradição
                        }

                        _grid.Cells[neighborIndex] = newNeighborMask;
                        EnqueueCell(nx, ny);
                    }
                }
            }

            return true;
        }

        public bool ApplyTargetLayers(int[,] targetLayerMap)
        {
            for (int x = 0; x < _grid.GridWidth; x++)
            {
                for (int y = 0; y < _grid.GridHeight; y++)
                {
                    int targetLayer = targetLayerMap[x, y];
                    FastBitset128 layerMask = _compatibilityMatrix.GetTilesForLayer(targetLayer);

                    int idx = _grid.ToIndex(x, y);
                    FastBitset128 current = _grid.Cells[idx];
                    current.IntersectWith(layerMask);

                    if (current.IsEmpty())
                    {
                        return false;
                    }

                    if (current != _grid.Cells[idx])
                    {
                        _grid.Cells[idx] = current;
                        EnqueueCell(x, y);
                    }
                }
            }

            return PropagateAll();
        }

        public int FindLowestEntropyCell(System.Random random)
        {
            int minPossibilities = int.MaxValue;
            int candidateCount = 0;

            for (int x = 1; x <= _grid.InnerWidth; x++)
            {
                for (int y = 1; y <= _grid.InnerHeight; y++)
                {
                    int idx = _grid.ToIndex(x, y);
                    int count = _grid.Cells[idx].CountBits();

                    if (count <= 1) continue; // Já colapsado ou vazio

                    if (count < minPossibilities)
                    {
                        minPossibilities = count;
                        candidateCount = 0;
                        _candidateBuffer[candidateCount++] = idx;
                    }
                    else if (count == minPossibilities)
                    {
                        _candidateBuffer[candidateCount++] = idx;
                    }
                }
            }

            if (candidateCount == 0) return -1;
            return _candidateBuffer[random.Next(0, candidateCount)];
        }

        public bool CollapseCellWeighted(int cellIndex, System.Random random)
        {
            FastBitset128 mask = _grid.Cells[cellIndex];
            float totalWeight = 0f;

            ulong p0 = mask.Part0;
            while (p0 != 0UL)
            {
                int bit = TrailingZeroCount(p0);
                totalWeight += _compatibilityMatrix.GetTileWeight(bit);
                p0 &= p0 - 1UL;
            }

            ulong p1 = mask.Part1;
            while (p1 != 0UL)
            {
                int bit = 64 + TrailingZeroCount(p1);
                totalWeight += _compatibilityMatrix.GetTileWeight(bit);
                p1 &= p1 - 1UL;
            }

            if (totalWeight <= 0f) return false;

            float roll = (float)(random.NextDouble() * totalWeight);
            int chosenTileIndex = -1;

            p0 = mask.Part0;
            while (p0 != 0UL)
            {
                int bit = TrailingZeroCount(p0);
                roll -= _compatibilityMatrix.GetTileWeight(bit);
                if (roll <= 0f) { chosenTileIndex = bit; break; }
                p0 &= p0 - 1UL;
            }

            if (chosenTileIndex == -1)
            {
                p1 = mask.Part1;
                while (p1 != 0UL)
                {
                    int bit = 64 + TrailingZeroCount(p1);
                    roll -= _compatibilityMatrix.GetTileWeight(bit);
                    if (roll <= 0f) { chosenTileIndex = bit; break; }
                    p1 &= p1 - 1UL;
                }
            }

            if (chosenTileIndex == -1) chosenTileIndex = mask.GetFirstSetBit();

            _grid.Cells[cellIndex] = FastBitset128.SingleBit(chosenTileIndex);
            _grid.FromIndex(cellIndex, out int cx, out int cy);
            EnqueueCell(cx, cy);

            return PropagateAll();
        }

        public bool SolveSynchronous(int[,] targetLayerMap, int seed, int maxAttempts = 5)
        {
            var random = new System.Random(seed);

            for (int attempt = 0; attempt < maxAttempts; attempt++)
            {
                _grid.ResetAll();
                _queueHead = 0;
                _queueTail = 0;
                _queueCount = 0;
                Array.Clear(_inQueueFlags, 0, _inQueueFlags.Length);

                if (!ApplyTargetLayers(targetLayerMap))
                {
                    continue;
                }

                bool success = true;
                while (true)
                {
                    int chosenCell = FindLowestEntropyCell(random);
                    if (chosenCell == -1) break; // Todas colapsadas!

                    if (!CollapseCellWeighted(chosenCell, random))
                    {
                        success = false;
                        break;
                    }
                }

                if (success)
                {
                    return true;
                }
            }

            return false;
        }

        private static int TrailingZeroCount(ulong value)
        {
            if (value == 0UL) return 64;
            int count = 0;
            if ((value & 0xFFFFFFFFUL) == 0UL) { count += 32; value >>= 32; }
            if ((value & 0xFFFFUL) == 0UL) { count += 16; value >>= 16; }
            if ((value & 0xFFUL) == 0UL) { count += 8; value >>= 8; }
            if ((value & 0xFUL) == 0UL) { count += 4; value >>= 4; }
            if ((value & 0x3UL) == 0UL) { count += 2; value >>= 2; }
            if ((value & 0x1UL) == 0UL) { count += 1; }
            return count;
        }
    }
}
