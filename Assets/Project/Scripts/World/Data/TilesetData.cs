using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Contentor global (ScriptableObject) que armazena a coleção completa de tiles disponíveis.
/// Serve como base de dados primária para o gerador WFC consultar as peças permitidas no mapa.
/// </summary>
[CreateAssetMenu(fileName = "TilesetData", menuName = "Scriptable Objects/TilesetData")]
public class TilesetData : ScriptableObject
{
    [SerializeField] private List<Tile> _tilesetList;

    public List<Tile> TilesetList => _tilesetList;
}