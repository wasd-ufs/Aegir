using UnityEditor.PackageManager.UI;
using UnityEngine;

/// <summary>
/// Responsável exclusivo pela matemática topológica do mapa.
/// Converte o ruído global e o IslandSampler numa matriz de camadas (TargetLayerMap) 
/// que dita onde deve existir Terra, Areia ou Água.
/// </summary>
public class TargetLayerBuilder
{
    private const int DEEP_SEA_LAYER = -4;
    private const int DEEP_SEA_TO_SEA_TRANSITION_LAYER = -3;
    private const int SEA_LAYER = -2;
    private const int SEA_TO_WATER_TRANSITION_LAYER = -1;
    private const int WATER_LAYER = 0;
    private const int WATER_TO_SAND_TRANSITION_LAYER = 1;
    private const int SAND_LAYER = 2;
    private const int SAND_TO_GRASS_TRANSITION_LAYER = 3;
    private const int GRASS_LAYER = 4;
    private const int MIN_OCEAN_MASK_PADDING = 2;

    private readonly Vector2Int _chunkSize;
    private readonly int _oceanMaskPadding;
    private readonly int _minBeachRadius;
    private readonly int _maxBeachRadius;
    private readonly float _beachNoiseScale;

    private IslandMapSampler _islandMapSampler;
    private Vector2Int _currentChunkCoordinate;
    private float _currentNoiseScale;
    private int _worldSeed;
    private int[,] _solidLayerMap;

    public TargetLayerBuilder(Vector2Int chunkSize, int oceanMaskPadding, int minBeachRadius, int maxBeachRadius, float beachNoiseScale)
    {
        _chunkSize = chunkSize;
        _oceanMaskPadding = oceanMaskPadding;
        _minBeachRadius = minBeachRadius;
        _maxBeachRadius = maxBeachRadius;
        _beachNoiseScale = beachNoiseScale;
    }

    public void SetSampler(IslandMapSampler sampler) => _islandMapSampler = sampler;

    public int[,] Build(Vector2Int chunkCoordinate, float noiseScale, int worldSeed)
    {
        _currentChunkCoordinate = chunkCoordinate;
        _currentNoiseScale = noiseScale;
        _worldSeed = worldSeed;

        int oceanMaskPadding = Mathf.Max(_oceanMaskPadding, MIN_OCEAN_MASK_PADDING);
        int maxEffectiveRadius = Mathf.Clamp(_maxBeachRadius, 1, oceanMaskPadding);

        BuildSolidLayerMap(oceanMaskPadding, maxEffectiveRadius);
        return BuildVisualLayerMap(oceanMaskPadding);
    }

    private void BuildSolidLayerMap(int padding, int maxEffectiveRadius)
    {
        int fullWidth = _chunkSize.x + 2 + padding * 2;
        int fullHeight = _chunkSize.y + 2 + padding * 2;
        _solidLayerMap = new int[fullWidth, fullHeight];

        for (int px = 0; px < fullWidth; px++)
        {
            for (int py = 0; py < fullHeight; py++)
            {
                int localX = px - padding;
                int localY = py - padding;
                
                Vector2 globalPosition = GetGlobalPosition(localX, localY);
                _solidLayerMap[px, py] = DetermineSolidLayer(globalPosition, maxEffectiveRadius);
            }
        }
    }

    private int DetermineSolidLayer(Vector2 globalPosition, int maxEffectiveRadius)
    {
        int groupX = Mathf.FloorToInt(globalPosition.x) & ~1;
        int groupY = Mathf.FloorToInt(globalPosition.y) & ~1;
        
        if (!IsMathematicallyLand(groupX, groupY)) return ChooseWaterProfundity(globalPosition);

        float organicNoise = GetBeachNoise(groupX, groupY);
        int targetBeachWidth = Mathf.RoundToInt(Mathf.Lerp(_minBeachRadius, maxEffectiveRadius, organicNoise));
        int searchRadius = Mathf.Max(1, Mathf.CeilToInt(targetBeachWidth / 2f));

        for (int offsetX = -searchRadius; offsetX <= searchRadius; offsetX++)
        {
            for (int offsetY = -searchRadius; offsetY <= searchRadius; offsetY++)
            {
                if (offsetX == 0 && offsetY == 0) continue;
                
                if (!IsMathematicallyLand(groupX + offsetX * 2, groupY + offsetY * 2))
                    return SAND_LAYER;
            }
        }
        return GRASS_LAYER;
    }

    private int[,] BuildVisualLayerMap(int oceanMaskPadding)
    {
        int[,] targetLayerMap = new int[_chunkSize.x + 2, _chunkSize.y + 2];

        for (int localX = 0; localX <= _chunkSize.x + 1; localX++)
        {
            for (int localY = 0; localY <= _chunkSize.y + 1; localY++)
            {
                targetLayerMap[localX, localY] = CalculateVisualLayer(localX, localY, oceanMaskPadding);
            }
        }
        return targetLayerMap;
    }

    private int CalculateVisualLayer(int localX, int localY, int oceanMaskPadding)
    {
        int px = localX + oceanMaskPadding;
        int py = localY + oceanMaskPadding;
        int centerSolidLayer = _solidLayerMap[px, py];
        
        if (centerSolidLayer == DEEP_SEA_LAYER) return DEEP_SEA_LAYER;

        int solidWidth = _solidLayerMap.GetLength(0);
        int solidHeight = _solidLayerMap.GetLength(1);

        for (int offsetX = -1; offsetX <= 1; offsetX++)
        {
            for (int offsetY = -1; offsetY <= 1; offsetY++)
            {
                if (offsetX == 0 && offsetY == 0) continue;

                int neighborPX = px + offsetX;
                int neighborPY = py + offsetY;

                if (neighborPX < 0 || neighborPX >= solidWidth || neighborPY < 0 || neighborPY >= solidHeight) continue;

                if (_solidLayerMap[neighborPX, neighborPY] == centerSolidLayer - 2)
                    return GetTransitionLayer(centerSolidLayer);
            }
        }
        return centerSolidLayer;
    }

    private int GetTransitionLayer(int upperSolidLayer)
    {
        return upperSolidLayer switch
        {
            SEA_LAYER => DEEP_SEA_TO_SEA_TRANSITION_LAYER,
            WATER_LAYER => SEA_TO_WATER_TRANSITION_LAYER,
            SAND_LAYER => WATER_TO_SAND_TRANSITION_LAYER,
            GRASS_LAYER => SAND_TO_GRASS_TRANSITION_LAYER,
            _ => upperSolidLayer
        };
    }

    private bool IsMathematicallyLand(int groupX, int groupY)
    {
        if (SampleIslandHeight(groupX, groupY) >= IslandMapSampler.ISLAND_EDGE_THRESHOLD) return true;

        bool landUp = SampleIslandHeight(groupX, groupY + 2) >= IslandMapSampler.ISLAND_EDGE_THRESHOLD;
        bool landDown = SampleIslandHeight(groupX, groupY - 2) >= IslandMapSampler.ISLAND_EDGE_THRESHOLD;
        bool landRight = SampleIslandHeight(groupX + 2, groupY) >= IslandMapSampler.ISLAND_EDGE_THRESHOLD;
        bool landLeft = SampleIslandHeight(groupX - 2, groupY) >= IslandMapSampler.ISLAND_EDGE_THRESHOLD;

        return (landUp && landRight) || (landRight && landDown) || (landDown && landLeft) || (landLeft && landUp);
    }

    private float GetBeachNoise(float globalX, float globalY)
    {
        uint seedHash = (uint)(_worldSeed * 198491317u);
        float safeOffset = 42.1337f + (seedHash % 876u) * 0.73f;
        float rawNoise = Mathf.PerlinNoise(globalX * _beachNoiseScale + safeOffset, globalY * _beachNoiseScale + safeOffset);
        return Mathf.Clamp01((rawNoise - 0.2f) * 1.66f);
    }

    private float SampleIslandHeight(float globalX, float globalY)
    {
        if (_islandMapSampler != null) return _islandMapSampler.Sample(globalX, globalY);
        uint seedHash = (uint)(_worldSeed * 2654435761u);
        float safeOffset = 13f + seedHash % 984u;
        return Mathf.PerlinNoise(globalX * _currentNoiseScale + safeOffset, globalY * _currentNoiseScale + safeOffset);
    }

    private Vector2 GetGlobalPosition(int localX, int localY)
    {
        return new Vector2(_currentChunkCoordinate.x * _chunkSize.x + localX - 1, _currentChunkCoordinate.y * _chunkSize.y + localY - 1);
    }

    private int ChooseWaterProfundity(Vector2 globalPosition)
    {
        float threshold = SampleIslandHeight(globalPosition.x, globalPosition.y);

        if (threshold > IslandMapSampler.WATER_EDGE_THRESHOLD) return WATER_LAYER;
        else if (threshold > IslandMapSampler.SEA_EDGE_THRESHOLD) return SEA_LAYER;
        else return DEEP_SEA_LAYER;
    }
}