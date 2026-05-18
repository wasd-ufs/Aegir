using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Representa um espaço individual na grelha do algoritmo Wave Function Collapse (WFC).
/// Controla os estados possíveis (entropia) de um tile nesta coordenada e verifica se o mesmo já colapsou.
/// </summary>
public class Cell
{
    public BitArray PossibleBitsArray { get; private set; }
    public Vector2Int Coordinates { get; private set; }

    private int _tileCount;

    public Cell(int tileCount, Vector2Int coordinates)
    {
        _tileCount = tileCount;
        Coordinates = coordinates;
        PossibleBitsArray = new BitArray(tileCount, true); 
    }

    public bool IsCollapsed() => CountPossible() == 1;

    public bool IsEmpty() => CountPossible() == 0;

    public int CountPossible()
    {
        int count = 0;
        for (int i = 0; i < PossibleBitsArray.Count; i++)
        {
            if (PossibleBitsArray[i]) count++;
        }
        return count;
    }

    public int CollapsedIndex()
    {
        for (int i = 0; i < PossibleBitsArray.Count; i++)
        {
            if (PossibleBitsArray[i]) return i;
        }
        return -1;
    }

    public void CollapseCell(int tileIndex)
    {
        PossibleBitsArray.SetAll(false);
        PossibleBitsArray[tileIndex] = true;
    }

    public List<int> PossibleIndices()
    {
        var resultList = new List<int>();
        for (int i = 0; i < PossibleBitsArray.Count; i++)
        {
            if (PossibleBitsArray[i]) resultList.Add(i);
        }
        return resultList;
    }

    public void CopyFrom(BitArray other)
    {
        PossibleBitsArray = new BitArray(other);
    }
}