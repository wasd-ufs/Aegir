using UnityEngine;

/// <summary>
/// Responsável exclusivo pela matemática topológica do mapa.
/// Converte o ruído global e o IslandSampler numa matriz de camadas (TargetLayerMap)
/// que dita onde deve existir Terra, Areia, Água, etc.
///
/// Para adicionar novas camadas, edita apenas o <see cref="LayerStack"/> no Inspector.
/// Nenhuma alteração de código é necessária nesta classe.
/// </summary>
public class TargetLayerBuilder
{
    // Valor usado quando nenhuma LayerStack estiver configurada (fallback de segurança)
    private const int FALLBACK_WATER_VALUE = 0;
    private const int MIN_OCEAN_MASK_PADDING = 2;

    private readonly Vector2Int _chunkSize;
    private readonly int _oceanMaskPadding;
    private readonly int _minBeachRadius;
    private readonly int _maxBeachRadius;
    private readonly float _beachNoiseScale;

    private LayerStack _layerStack;
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

    /// <summary>
    /// Injeta a pilha de camadas configurada no Inspector.
    /// Se não for fornecida, o builder usará fallbacks hardcoded compatíveis com o sistema original.
    /// </summary>
    public void SetLayerStack(LayerStack layerStack) => _layerStack = layerStack;

    // Ponto de Entrada

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

    // Fase 1 — Mapa Sólido (sem transições)

    private void BuildSolidLayerMap(int padding, int maxEffectiveRadius)
    {
        int fullWidth  = _chunkSize.x + 2 + padding * 2;
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

    /// <summary>
    /// Classifica um ponto como terra ou água e, dentro da terra,
    /// determina a camada correta com base na distância à beira da ilha.
    ///
    /// Com LayerStack: suporta N camadas de terra ordenadas por distância à beira.
    ///   depthIndex 0 = beira (ex: SAND), 1 = interior próximo (ex: GRASS), 2 = interior profundo (ex: FOREST)...
    ///
    /// Sem LayerStack: comportamento original (SAND / GRASS).
    /// </summary>
    private int DetermineSolidLayer(Vector2 globalPosition, int maxEffectiveRadius)
    {
        int groupX = Mathf.FloorToInt(globalPosition.x) & ~1;
        int groupY = Mathf.FloorToInt(globalPosition.y) & ~1;

        if (!IsMathematicallyLand(groupX, groupY))
            return ChooseWaterSolidValue(globalPosition);

        float organicNoise = GetBeachNoise(groupX, groupY);
        int targetBeachWidth = Mathf.RoundToInt(Mathf.Lerp(_minBeachRadius, maxEffectiveRadius, organicNoise));
        int searchRadius = Mathf.Max(1, Mathf.CeilToInt(targetBeachWidth / 2f));

        // --- Com LayerStack: cálculo de distância multi-camada ---
        if (_layerStack != null && _layerStack.LandLayerCount > 1)
            return DetermineLandLayerByDistance(groupX, groupY, searchRadius);

        // --- Fallback original: SAND ou GRASS ---
        for (int offsetX = -searchRadius; offsetX <= searchRadius; offsetX++)
            for (int offsetY = -searchRadius; offsetY <= searchRadius; offsetY++)
            {
                if (offsetX == 0 && offsetY == 0) continue;
                if (!IsMathematicallyLand(groupX + offsetX * 2, groupY + offsetY * 2))
                    return GetLowestLandSolidValue();
            }

        return GetHighestLandSolidValue();
    }

    /// <summary>
    /// Versão multi-camada de terra: mede a distância mínima à beira da ilha
    /// e mapeia essa distância para o índice de profundidade na LayerStack.
    ///
    /// distância 0-1 tiles → depthIndex 0 (beira, ex: SAND)
    /// distância 2-3 tiles → depthIndex 1 (interior, ex: GRASS)
    /// distância 4+ tiles  → depthIndex 2 (profundo, ex: FOREST)
    /// etc.
    ///
    /// O raio de busca máximo é o mesmo searchRadius da lógica de praia original.
    /// Cada "banda" interior usa uma janela de 2 tiles para espelhar a convenção de valores pares.
    /// </summary>
    private int DetermineLandLayerByDistance(int groupX, int groupY, int searchRadius)
    {
        int landLayerCount = _layerStack.LandLayerCount;
        int maxSearchDepth = searchRadius * landLayerCount;

        int minDistanceToEdge = int.MaxValue;

        for (int offsetX = -maxSearchDepth; offsetX <= maxSearchDepth; offsetX++)
        {
            for (int offsetY = -maxSearchDepth; offsetY <= maxSearchDepth; offsetY++)
            {
                if (offsetX == 0 && offsetY == 0) continue;

                int checkX = groupX + offsetX * 2;
                int checkY = groupY + offsetY * 2;

                if (!IsMathematicallyLand(checkX, checkY))
                {
                    int distance = Mathf.Max(Mathf.Abs(offsetX), Mathf.Abs(offsetY)); // Chebyshev
                    if (distance < minDistanceToEdge)
                        minDistanceToEdge = distance;
                }
            }
        }

        if (minDistanceToEdge == int.MaxValue)
        {
            // Nenhuma borda encontrada no raio → camada mais interior
            return _layerStack.GetLandLayerByDepth(landLayerCount - 1).SolidValue;
        }

        // Mapeia distância → depthIndex
        // searchRadius define quantos tiles de "resolução" há por camada
        int depthIndex = Mathf.Min((minDistanceToEdge - 1) / Mathf.Max(searchRadius, 1), landLayerCount - 1);
        return _layerStack.GetLandLayerByDepth(depthIndex).SolidValue;
    }

    // Fase 2 — Mapa Visual (com transições)

    private int[,] BuildVisualLayerMap(int oceanMaskPadding)
    {
        int[,] targetLayerMap = new int[_chunkSize.x + 2, _chunkSize.y + 2];

        for (int localX = 0; localX <= _chunkSize.x + 1; localX++)
            for (int localY = 0; localY <= _chunkSize.y + 1; localY++)
                targetLayerMap[localX, localY] = CalculateVisualLayer(localX, localY, oceanMaskPadding);

        return targetLayerMap;
    }

    /// <summary>
    /// Para cada célula, verifica se algum vizinho pertence a uma camada 2 níveis abaixo.
    /// Se sim, esta célula é uma célula de transição — usa o TransitionValue.
    ///
    /// A regra "centerSolidLayer - 2" é universal para qualquer pilha de camadas,
    /// desde que a convenção de valores pares seja mantida.
    /// </summary>
    private int CalculateVisualLayer(int localX, int localY, int oceanMaskPadding)
    {
        int px = localX + oceanMaskPadding;
        int py = localY + oceanMaskPadding;
        int centerSolidLayer = _solidLayerMap[px, py];

        // A camada mais baixa nunca tem transição para baixo
        if (IsBottomLayer(centerSolidLayer)) return centerSolidLayer;

        int solidWidth  = _solidLayerMap.GetLength(0);
        int solidHeight = _solidLayerMap.GetLength(1);

        for (int offsetX = -1; offsetX <= 1; offsetX++)
        {
            for (int offsetY = -1; offsetY <= 1; offsetY++)
            {
                if (offsetX == 0 && offsetY == 0) continue;

                int neighborPX = px + offsetX;
                int neighborPY = py + offsetY;

                if (neighborPX < 0 || neighborPX >= solidWidth ||
                    neighborPY < 0 || neighborPY >= solidHeight) continue;

                // Regra universal: vizinho 2 degraus abaixo → esta célula é borda de transição
                if (_solidLayerMap[neighborPX, neighborPY] == centerSolidLayer - 2)
                    return GetTransitionValue(centerSolidLayer);
            }
        }

        return centerSolidLayer;
    }

    // Helpers — LayerStack com fallback para constantes originais

    /// <summary>
    /// Dado o SolidValue de uma camada superior, devolve o TransitionValue.
    /// Usa LayerStack se disponível; caso contrário usa a fórmula original (solidValue - 1).
    /// </summary>
    private int GetTransitionValue(int solidValue)
    {
        if (_layerStack != null) return _layerStack.GetTransitionValue(solidValue);
        return solidValue - 1; // Fallback: mantém convenção original
    }

    /// <summary>
    /// Dado um valor de altura amostrado, devolve o SolidValue da camada de água correcta.
    /// Usa LayerStack se disponível; caso contrário usa os thresholds hardcoded originais.
    /// </summary>
    private int ChooseWaterSolidValue(Vector2 globalPosition)
    {
        float sampledHeight = SampleIslandHeight(globalPosition.x, globalPosition.y);

        if (_layerStack != null) return _layerStack.GetWaterSolidValue(sampledHeight);

        // Fallback original
        if (sampledHeight > IslandMapSampler.WATER_EDGE_THRESHOLD) return 0;  // WATER_LAYER
        if (sampledHeight > IslandMapSampler.SEA_EDGE_THRESHOLD)   return -2; // SEA_LAYER
        return -4; // DEEP_SEA_LAYER
    }

    private int GetLowestLandSolidValue()
    {
        if (_layerStack != null)
        {
            var layer = _layerStack.LowestLandLayer;
            if (layer != null) return layer.SolidValue;
        }
        return 2; // SAND_LAYER original
    }

    private int GetHighestLandSolidValue()
    {
        if (_layerStack != null)
        {
            var layer = _layerStack.HighestLandLayer;
            if (layer != null) return layer.SolidValue;
        }
        return 4; // GRASS_LAYER original
    }

    /// <summary>
    /// A camada mais baixa da pilha (DeepSea ou equivalente) não tem transição para baixo.
    /// </summary>
    private bool IsBottomLayer(int solidValue)
    {
        if (_layerStack != null && _layerStack.Layers.Count > 0)
            return solidValue == _layerStack.Layers[0].SolidValue;

        return solidValue == -4; // DEEP_SEA_LAYER original
    }

    // Helpers — Ruído e Posições (inalterados)

    private bool IsMathematicallyLand(int groupX, int groupY)
    {
        if (SampleIslandHeight(groupX, groupY) >= IslandMapSampler.ISLAND_EDGE_THRESHOLD) return true;

        bool landUp    = SampleIslandHeight(groupX,     groupY + 2) >= IslandMapSampler.ISLAND_EDGE_THRESHOLD;
        bool landDown  = SampleIslandHeight(groupX,     groupY - 2) >= IslandMapSampler.ISLAND_EDGE_THRESHOLD;
        bool landRight = SampleIslandHeight(groupX + 2, groupY)     >= IslandMapSampler.ISLAND_EDGE_THRESHOLD;
        bool landLeft  = SampleIslandHeight(groupX - 2, groupY)     >= IslandMapSampler.ISLAND_EDGE_THRESHOLD;

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
        return new Vector2(
            _currentChunkCoordinate.x * _chunkSize.x + localX - 1,
            _currentChunkCoordinate.y * _chunkSize.y + localY - 1);
    }
}