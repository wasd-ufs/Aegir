using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Representa uma célula na grade do WFC (Wave Function Collapse).
/// Cada célula mantém um <see cref="BitArray"/> indicando quais tiles do tileset
/// ainda são possíveis para aquela posição. Quando restrar apenas um bit ativo,
/// a célula é considerada colapsada.
/// </summary>
public class Cell
{
    // -------------------------------------------------------------------------
    // Campos
    // -------------------------------------------------------------------------

    /// <summary>
    /// Um bit por tile do tileset — <c>1</c> = ainda possível, <c>0</c> = eliminado.
    /// </summary>
    public BitArray possible;

    /// <summary>
    /// Coordenadas da célula na grade local do chunk (inclui bordas do halo).
    /// </summary>
    public Vector2Int coordinates;

    /// <summary>
    /// Cache de <c>tileset.Count</c> para evitar referência externa frequente.
    /// </summary>
    private int tileCount;

    // -------------------------------------------------------------------------
    // Construtor
    // -------------------------------------------------------------------------

    /// <summary>
    /// Cria uma célula com todos os tiles marcados como possíveis.
    /// </summary>
    /// <param name="tileCount">Número total de tiles no tileset.</param>
    /// <param name="coords">Posição da célula na grade.</param>
    public Cell(int tileCount, Vector2Int coords)
    {
        this.tileCount = tileCount;
        this.coordinates = coords;
        possible = new BitArray(tileCount, true); // Começa com todos possíveis
    }

    // -------------------------------------------------------------------------
    // Estado
    // -------------------------------------------------------------------------

    /// <summary>Retorna <c>true</c> se apenas um tile ainda é possível.</summary>
    public bool isCollapsed() => CountPossible() == 1;

    /// <summary>Retorna <c>true</c> se nenhum tile é mais possível (contradição).</summary>
    public bool isEmpty() => CountPossible() == 0;

    /// <summary>Conta quantos tiles ainda estão marcados como possíveis.</summary>
    public int CountPossible()
    {
        int count = 0;
        for (int i = 0; i < possible.Count; i++)
            if (possible[i]) count++;
        return count;
    }

    // -------------------------------------------------------------------------
    // Colapso
    // -------------------------------------------------------------------------

    /// <summary>
    /// Retorna o índice do único tile possível.
    /// Deve ser chamado somente quando a célula estiver colapsada.
    /// </summary>
    /// <returns>Índice do tile, ou <c>-1</c> se a célula não estiver colapsada.</returns>
    public int CollapsedIndex()
    {
        for (int i = 0; i < possible.Count; i++)
            if (possible[i]) return i;
        return -1;
    }

    /// <summary>
    /// Colapsa a célula para um tile específico, zerando todos os demais bits.
    /// </summary>
    /// <param name="tileIndex">Índice do tile escolhido no tileset.</param>
    public void CollapseCell(int tileIndex)
    {
        possible.SetAll(false);
        possible[tileIndex] = true;
    }

    // -------------------------------------------------------------------------
    // Consulta
    // -------------------------------------------------------------------------

    /// <summary>Retorna uma lista com todos os índices de tiles ainda possíveis.</summary>
    public List<int> PossibleIndices()
    {
        var result = new List<int>();
        for (int i = 0; i < possible.Count; i++)
            if (possible[i]) result.Add(i);
        return result;
    }

    /// <summary>
    /// Substitui o estado de possibilidades pelo conteúdo de outro <see cref="BitArray"/>.
    /// Usado ao restaurar o snapshot do halo durante um reinício de geração.
    /// </summary>
    /// <param name="other">BitArray de origem para a cópia.</param>
    public void CopyFrom(BitArray other)
    {
        possible = new BitArray(other);
    }
}