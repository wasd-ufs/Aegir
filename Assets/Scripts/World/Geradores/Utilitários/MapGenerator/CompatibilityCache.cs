using UnityEngine;

public class CompatibilityCache
{
    private bool[,,] _cache;
    private RuleManager _ruleManager;
    private TilesetData _tilesetData;

    public CompatibilityCache(RuleManager ruleManager, TilesetData tilesetData)
    {
        _ruleManager = ruleManager;
        _tilesetData = tilesetData;
    }

    public void BuildCache()
    {
        if (_cache != null) return;

        int tileCount = _tilesetData.TilesetList.Count;
        _compatibilityCacheArray = new bool[tileCount, tileCount, 4];
        
        for (int a = 0; a < tileCount; a++)
        {
            for (int b = 0; b < tileCount; b++)
            {
                Tile tileA = _tilesetData.TilesetList[a];
                Tile tileB = _tilesetData.TilesetList[b];
                
                _cache[a, b, 0] = !_ruleManager.IsBlocked(tileA, tileB, Vector2Int.up);
                _cache[a, b, 1] = !_ruleManager.IsBlocked(tileA, tileB, Vector2Int.down);
                _cache[a, b, 2] = !_ruleManager.IsBlocked(tileA, tileB, Vector2Int.left);
                _cache[a, b, 3] = !_ruleManager.IsBlocked(tileA, tileB, Vector2Int.right);
            }
        }
    }

    public bool IsCompatible(int currentTileIndex, int neighborTileIndex, int directionIndex)
    {
        return _compatibilityCacheArray[currentTileIndex, neighborTileIndex, directionIndex];
    }
}