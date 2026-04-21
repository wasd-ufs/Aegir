using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// ScriptableObject que agrupa todos os <see cref="Tile"/>s disponíveis no projeto.
/// Serve como fonte de dados central para o <see cref="MapGenerator"/> e o
/// <see cref="RuleManager"/>: o índice de cada tile nesta lista é o mesmo usado
/// nos <see cref="Cell.possible"/> BitArrays e nos arquivos de save (<c>.dat</c>).
/// </summary>
[CreateAssetMenu(fileName = "TilesetData", menuName = "Scriptable Objects/TilesetData")]
public class TilesetData : ScriptableObject
{
    /// <summary>
    /// Lista ordenada de todos os tiles. A ordem é importante — não deve ser
    /// alterada após chunks terem sido salvas em disco, pois os índices seriam invalidados.
    /// </summary>
    public List<Tile> tileset;
}