using System;
using System.Runtime.CompilerServices;

namespace Aegir.World.V2.Core
{
    /// <summary>
    /// Bitset de alto desempenho de 128 bits (struct de valor, zero alocações no Heap).
    /// Suporta até 128 tiles por tileset com operações bitwise ultra-rápidas (AND, OR, PopCount).
    /// </summary>
    public struct FastBitset128 : IEquatable<FastBitset128>
    {
        public ulong Part0;
        public ulong Part1;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public FastBitset128(ulong part0, ulong part1)
        {
            Part0 = part0;
            Part1 = part1;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static FastBitset128 CreateAllSet(int count)
        {
            if (count <= 0) return default;
            if (count <= 64)
            {
                ulong mask0 = count == 64 ? ~0UL : (1UL << count) - 1UL;
                return new FastBitset128(mask0, 0UL);
            }
            else
            {
                int remaining = Math.Min(count - 64, 64);
                ulong mask1 = remaining == 64 ? ~0UL : (1UL << remaining) - 1UL;
                return new FastBitset128(~0UL, mask1);
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static FastBitset128 SingleBit(int bitIndex)
        {
            if (bitIndex < 64)
            {
                return new FastBitset128(1UL << bitIndex, 0UL);
            }
            return new FastBitset128(0UL, 1UL << (bitIndex - 64));
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void SetBit(int bitIndex)
        {
            if (bitIndex < 64) Part0 |= (1UL << bitIndex);
            else Part1 |= (1UL << (bitIndex - 64));
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void ClearBit(int bitIndex)
        {
            if (bitIndex < 64) Part0 &= ~(1UL << bitIndex);
            else Part1 &= ~(1UL << (bitIndex - 64));
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool IsBitSet(int bitIndex)
        {
            if (bitIndex < 64) return (Part0 & (1UL << bitIndex)) != 0UL;
            return (Part1 & (1UL << (bitIndex - 64))) != 0UL;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void IntersectWith(in FastBitset128 other)
        {
            Part0 &= other.Part0;
            Part1 &= other.Part1;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void UnionWith(in FastBitset128 other)
        {
            Part0 |= other.Part0;
            Part1 |= other.Part1;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool Overlaps(in FastBitset128 other)
        {
            return ((Part0 & other.Part0) | (Part1 & other.Part1)) != 0UL;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool IsEmpty()
        {
            return (Part0 | Part1) == 0UL;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool HasSingleBit()
        {
            int count = CountBits();
            return count == 1;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public int CountBits()
        {
            return PopCount(Part0) + PopCount(Part1);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public int GetFirstSetBit()
        {
            if (Part0 != 0UL)
            {
                return TrailingZeroCount(Part0);
            }
            if (Part1 != 0UL)
            {
                return 64 + TrailingZeroCount(Part1);
            }
            return -1;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static int PopCount(ulong value)
        {
            // Software popcount de alta velocidade O(1) compatível com qualquer target Unity
            value -= (value >> 1) & 0x5555555555555555UL;
            value = (value & 0x3333333333333333UL) + ((value >> 2) & 0x3333333333333333UL);
            value = (value + (value >> 4)) & 0x0F0F0F0F0F0F0F0FUL;
            return (int)((value * 0x0101010101010101UL) >> 56);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
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

        public bool Equals(FastBitset128 other) => Part0 == other.Part0 && Part1 == other.Part1;
        public override bool Equals(object obj) => obj is FastBitset128 other && Equals(other);
        public override int GetHashCode() => unchecked((int)(Part0 ^ Part1));
        public static bool operator ==(FastBitset128 left, FastBitset128 right) => left.Equals(right);
        public static bool operator !=(FastBitset128 left, FastBitset128 right) => !left.Equals(right);
    }
}
