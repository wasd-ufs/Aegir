using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "TilesetData", menuName = "Scriptable Objects/TilesetData")]
public class TilesetData : ScriptableObject
{
    [SerializeField] private List<Tile> _tilesetList;

    public List<Tile> TilesetList => _tilesetList;
}