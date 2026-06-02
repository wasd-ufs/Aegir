using UnityEngine;

/// <summary>
/// Amostra o mapa de altura usado para definir a forma geral das ilhas.
/// A praia e as transições visuais são calculadas pelo WFCSolver a partir deste mapa base.
/// </summary>
public class IslandMapSampler
{
    // =========================================================================
    // Constantes Públicas — Limiares
    // =========================================================================

    /// <summary>Valor mínimo para uma posição ser considerada parte da ilha.</summary>
    public const float ISLAND_EDGE_THRESHOLD = 0.62f;


    /// <summary>Valor usado por outros sistemas para detectar terra.</summary>
    public const float LAND_THRESHOLD = ISLAND_EDGE_THRESHOLD;


    // =========================================================================
    // Constantes Privadas — FBM
    // =========================================================================

    private const int FBM_OCTAVES = 2;
    private const float FBM_PERSISTENCE = 0.2f;
    private const float FBM_LACUNARITY = 1.8f;
    private const float BASE_FREQUENCY = 0.01f;
    private const float WARP_FREQUENCY = 0.003f;
    private const float WARP_STRENGTH = 0.003f;
    private const float ISLAND_EXPONENT = 1.25f;
    private const float WARP_X_OFFSET = 31.7f;
    private const float WARP_Y_OFFSET = 71.3f;
    private const float HASH_OFFSET_BASE = 13f;
    private const uint HASH_OFFSET_RANGE = 984u;

    // =========================================================================
    // Campos Privados
    // =========================================================================

    private readonly float _offsetX;
    private readonly float _offsetY;
    private readonly float _warpOffsetX;
    private readonly float _warpOffsetY;

    // =========================================================================
    // Inicialização
    // =========================================================================

    public IslandMapSampler(int seed)
    {
        _offsetX = HashSeed(seed, 0);
        _offsetY = HashSeed(seed, 1);
        _warpOffsetX = HashSeed(seed, 2);
        _warpOffsetY = HashSeed(seed, 3);
    }

    // =========================================================================
    // API Pública
    // =========================================================================

    /// <summary>
    /// Retorna o valor normalizado do mapa de altura para uma posição global.
    /// Valores maiores indicam regiões mais internas ou elevadas da ilha.
    /// </summary>
    public float Sample(float globalX, float globalY)
    {
        Vector2 warpedPosition = CalculateWarpedPosition(globalX, globalY);

        float heightValue = SampleFbm(
            warpedPosition.x * BASE_FREQUENCY + _offsetX,
            warpedPosition.y * BASE_FREQUENCY + _offsetY);

        return Mathf.Clamp01(Mathf.Pow(heightValue, ISLAND_EXPONENT));
    }

    // =========================================================================
    // Helpers Privados — Domain Warp
    // =========================================================================

    /// <summary>
    /// Aplica uma distorção suave nas coordenadas antes de amostrar o ruído principal.
    /// </summary>
    private Vector2 CalculateWarpedPosition(float globalX, float globalY)
    {
        float warpX = SampleWarpNoise(globalX, globalY, 0f, 0f);
        float warpY = SampleWarpNoise(globalX, globalY, WARP_X_OFFSET, WARP_Y_OFFSET);
        float warpScale = WARP_STRENGTH / BASE_FREQUENCY;

        return new Vector2(
            globalX + RemapToSignedRange(warpX) * warpScale,
            globalY + RemapToSignedRange(warpY) * warpScale);
    }

    /// <summary>
    /// Amostra um canal do ruído de distorção com deslocamento opcional.
    /// </summary>
    private float SampleWarpNoise(float globalX, float globalY, float offsetX, float offsetY)
    {
        return Mathf.PerlinNoise(
            globalX * WARP_FREQUENCY + _warpOffsetX + offsetX,
            globalY * WARP_FREQUENCY + _warpOffsetY + offsetY);
    }

    /// <summary>
    /// Converte um valor de [0, 1] para [-1, 1].
    /// </summary>
    private float RemapToSignedRange(float value)
    {
        return value * 2f - 1f;
    }

    // =========================================================================
    // Helpers Privados — FBM
    // =========================================================================

    /// <summary>
    /// Soma múltiplas oitavas de Perlin Noise e normaliza o resultado para [0, 1].
    /// </summary>
    private float SampleFbm(float x, float y)
    {
        float totalValue = 0f;
        float amplitude = 1f;
        float frequency = 1f;
        float maxValue = 0f;

        for (int octaveIndex = 0; octaveIndex < FBM_OCTAVES; octaveIndex++)
        {
            totalValue += Mathf.PerlinNoise(x * frequency, y * frequency) * amplitude;
            maxValue += amplitude;

            amplitude *= FBM_PERSISTENCE;
            frequency *= FBM_LACUNARITY;
        }

        return totalValue / maxValue;
    }

    /// <summary>
    /// Gera um deslocamento determinístico a partir da seed e de um canal.
    /// </summary>
    private static float HashSeed(int seed, int channel)
    {
        uint hash = (uint)(seed * 1664525 + channel * 22695477 + 1013904223);
        hash ^= hash >> 16;
        hash *= 0x45d9f3b;
        hash ^= hash >> 16;

        return HASH_OFFSET_BASE + hash % HASH_OFFSET_RANGE;
    }
}
