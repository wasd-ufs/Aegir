using UnityEngine;

/// <summary>
/// Motor matemático responsável por "esculpir" o formato global do arquipélago.
/// Utiliza Fractional Brownian Motion (FBM) combinado com Domain Warping 
/// para gerar costas orgânicas, penínsulas retorcidas e continentes naturais.
/// </summary>
public class IslandMapSampler
{
    #region Limites do Terreno (Nível do Mar)

    /// <summary>
    /// O "nível da água". Qualquer valor de ruído gerado abaixo disto torna-se oceano. 
    /// Aumentar este valor submerge a ilha (deixando-a menor). Diminuir faz a ilha crescer e ligar-se a outras.
    /// </summary>
    public const float ISLAND_EDGE_THRESHOLD = 0.62f;

    /// <summary>
    /// Constante de leitura pública usada pelo WFC para identificar onde a terra firme começa.
    /// </summary>
    public const float LAND_THRESHOLD = ISLAND_EDGE_THRESHOLD;

    #endregion

    #region Configurações do Ruído Principal (FBM)

    /// <summary>
    /// O "Zoom" da câmara sobre o mapa. 
    /// - Valores baixos (ex: 0.005f): Continentes massivos e contínuos.
    /// - Valores altos (ex: 0.05f): Milhares de ilhotas minúsculas e fragmentadas.
    /// </summary>
    private const float BASE_FREQUENCY = 0.01f;

    /// <summary>
    /// Número de camadas de detalhes sobrepostas. 
    /// 1 = Bordas perfeitamente suaves. 
    /// 2 ou 3 = Bordas com reentrâncias, micro-baías e irregularidades rochosas. (Cuidado: valores > 4 custam muito processamento).
    /// </summary>
    private const int FBM_OCTAVES = 2;

    /// <summary>
    /// O impacto das camadas de detalhe (Oitavas) no formato global.
    /// Com 0.2f, os micro-detalhes alteram apenas 20% do formato da ilha principal, mantendo a coerência.
    /// </summary>
    private const float FBM_PERSISTENCE = 0.2f;

    /// <summary>
    /// A velocidade com que o ruído encolhe a cada nova oitava. 
    /// O valor 1.8f garante que os detalhes da segunda oitava sejam quase o dobro mais pequenos que os da primeira.
    /// </summary>
    private const float FBM_LACUNARITY = 1.8f;

    /// <summary>
    /// Força o achatamento dos vales (oceanos) e a elevação dos picos.
    /// Exponentes > 1.0f puxam os valores mornos do Perlin Noise para baixo, forçando o mar a ser mais vasto e ilhas mais isoladas.
    /// </summary>
    private const float ISLAND_EXPONENT = 1.25f;

    #endregion

    #region Configurações de Distorção (Domain Warping)

    /// <summary>
    /// O tamanho das "correntes" que entortam a ilha. 
    /// Define o quão grandes são as dobras que puxam e empurram as penínsulas.
    /// </summary>
    private const float WARP_FREQUENCY = 0.003f;

    /// <summary>
    /// A força bruta com que o terreno é "derretido" ou esticado. 
    /// Sem isto, as ilhas seriam apenas "bolhas" redondas. Com isto, elas ganham formas curvas, como braços espirais ou ganchos.
    /// </summary>
    private const float WARP_STRENGTH = 0.003f;

    // Valores arbitrários para garantir que o ruído de distorção não é idêntico ao ruído do terreno
    private const float WARP_X_OFFSET = 31.7f;
    private const float WARP_Y_OFFSET = 71.3f;
    
    // Matemática de Hash para espalhar a Seed do mundo
    private const float HASH_OFFSET_BASE = 13f;
    private const uint HASH_OFFSET_RANGE = 984u;

    #endregion

    #region Estado Interno

    private readonly float _offsetX;
    private readonly float _offsetY;
    private readonly float _warpOffsetX;
    private readonly float _warpOffsetY;

    #endregion

    #region Construtor e API Pública

    public IslandMapSampler(int seed)
    {
        _offsetX = HashSeed(seed, 0);
        _offsetY = HashSeed(seed, 1);
        _warpOffsetX = HashSeed(seed, 2);
        _warpOffsetY = HashSeed(seed, 3);
    }

    /// <summary>
    /// Consulta o motor matemático para saber a altitude de um ponto exato do mundo.
    /// </summary>
    public float Sample(float globalX, float globalY)
    {
        // 1. Domain Warping: Antes de ler o mapa, "entortamos" as coordenadas 
        // para fingir que a grelha do mundo é feita de gelatina.
        Vector2 warpedPosition = CalculateWarpedPosition(globalX, globalY);

        // 2. Lemos o ruído FBM já na coordenada distorcida
        float heightValue = SampleFbm(
            warpedPosition.x * BASE_FREQUENCY + _offsetX,
            warpedPosition.y * BASE_FREQUENCY + _offsetY);

        // 3. Aplicamos o contraste final (Exponente) para separar bem o que é terra do que é mar
        return Mathf.Clamp01(Mathf.Pow(heightValue, ISLAND_EXPONENT));
    }

    #endregion

    #region Lógica: Warping

    private Vector2 CalculateWarpedPosition(float globalX, float globalY)
    {
        // Geramos duas forças diferentes: uma que empurra na horizontal (warpX) e outra na vertical (warpY)
        float warpX = SampleWarpNoise(globalX, globalY, 0f, 0f);
        float warpY = SampleWarpNoise(globalX, globalY, WARP_X_OFFSET, WARP_Y_OFFSET);
        
        float warpScale = WARP_STRENGTH / BASE_FREQUENCY;

        return new Vector2(
            globalX + RemapToSignedRange(warpX) * warpScale,
            globalY + RemapToSignedRange(warpY) * warpScale);
    }

    private float SampleWarpNoise(float globalX, float globalY, float offsetX, float offsetY)
    {
        return Mathf.PerlinNoise(
            globalX * WARP_FREQUENCY + _warpOffsetX + offsetX,
            globalY * WARP_FREQUENCY + _warpOffsetY + offsetY);
    }

    private float RemapToSignedRange(float value)
    {
        // O Perlin devolve de 0.0 a 1.0. 
        // Convertendo para -1.0 a 1.0, permitimos que a distorção empurre a terra tanto para a esquerda como para a direita.
        return value * 2f - 1f;
    }

    #endregion

    #region Lógica: FBM

    private float SampleFbm(float x, float y)
    {
        float totalValue = 0f;
        float amplitude = 1f;
        float frequency = 1f;
        float maxValue = 0f; // Usado para normalizar o valor final de volta para 0.0 ~ 1.0

        for (int octaveIndex = 0; octaveIndex < FBM_OCTAVES; octaveIndex++)
        {
            totalValue += Mathf.PerlinNoise(x * frequency, y * frequency) * amplitude;
            maxValue += amplitude;

            amplitude *= FBM_PERSISTENCE; // A próxima camada será mais fraca
            frequency *= FBM_LACUNARITY;  // A próxima camada terá detalhes mais finos
        }

        return totalValue / maxValue;
    }

    #endregion

    #region Lógica: Hashing

    private static float HashSeed(int seed, int channel)
    {
        // Algoritmo clássico de hash rápido para garantir que cada mundo gerado 
        // comece num ponto completamente aleatório do mapa infinito do Perlin Noise.
        uint hash = (uint)(seed * 1664525 + channel * 22695477 + 1013904223);
        hash ^= hash >> 16;
        hash *= 0x45d9f3b;
        hash ^= hash >> 16;

        return HASH_OFFSET_BASE + hash % HASH_OFFSET_RANGE;
    }

    #endregion
}