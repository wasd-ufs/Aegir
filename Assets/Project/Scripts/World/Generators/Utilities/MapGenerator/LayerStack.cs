using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Contentor da pilha completa de camadas de terreno.
/// Criado como ScriptableObject e referenciado pelo WFCSolver no Inspector.
///
/// A pilha deve estar ordenada do MAIS FUNDO para o MAIS ALTO:
///   DeepSea (-4) → Sea (-2) → Water (0) → Sand (2) → Grass (4) → ...
///
/// Para adicionar uma nova camada (ex: FOREST = 6):
///   1. Crie um novo LayerDefinition asset no Inspector
///   2. Adicione-o a esta lista na posição correcta (acima de Grass)
///   3. Zero alterações de código necessárias.
/// </summary>
[CreateAssetMenu(fileName = "LayerStack", menuName = "WFC/Layer Stack")]
public class LayerStack : ScriptableObject
{
    [Tooltip("Pilha de camadas ordenada do mais profundo (índice 0) para o mais alto.")]
    [SerializeField] private List<LayerDefinition> _layers = new List<LayerDefinition>();

    /// <summary>Todas as camadas definidas, da mais baixa para a mais alta.</summary>
    public IReadOnlyList<LayerDefinition> Layers => _layers;

    /// <summary>A camada de terra mais baixa (normalmente SAND). Usada como fallback de praia.</summary>
    public LayerDefinition LowestLandLayer
    {
        get
        {
            foreach (var layer in _layers)
                if (layer.IsLandLayer) return layer;
            return null;
        }
    }

    /// <summary>A camada de terra mais alta (normalmente GRASS, FOREST...). Usada como interior de ilha.</summary>
    public LayerDefinition HighestLandLayer
    {
        get
        {
            LayerDefinition highest = null;
            foreach (var layer in _layers)
                if (layer.IsLandLayer) highest = layer;
            return highest;
        }
    }

    /// <summary>
    /// Dado o SolidValue de uma camada, devolve o TransitionValue correspondente.
    /// Retorna o próprio solidValue se não encontrar (fallback seguro).
    /// </summary>
    public int GetTransitionValue(int solidValue)
    {
        foreach (var layer in _layers)
            if (layer.SolidValue == solidValue) return layer.TransitionValue;
        return solidValue;
    }

    /// <summary>
    /// Dada uma altura amostrada pelo IslandMapSampler, devolve o SolidValue
    /// da camada de água correspondente. Itera da mais rasa para a mais funda.
    /// </summary>
    public int GetWaterSolidValue(float sampledHeight)
    {
        // Percorre as camadas de água do mais raso para o mais fundo.
        // A primeira cujo threshold NÃO seja satisfeito é a camada correcta.
        // (Equivalente ao if/else chain original.)
        LayerDefinition deepest = null;

        for (int i = _layers.Count - 1; i >= 0; i--)
        {
            var layer = _layers[i];
            if (layer.IsLandLayer) continue;

            if (sampledHeight > layer.HeightThreshold) return layer.SolidValue;
            deepest = layer;
        }

        // Nenhum threshold satisfeito → camada mais funda
        return deepest != null ? deepest.SolidValue : 0;
    }

    /// <summary>
    /// Determina a camada sólida de terra para uma célula com base no número
    /// de camadas de terra disponíveis e na distância à beira da ilha.
    /// 
    /// A lógica de distância (beach radius) continua no TargetLayerBuilder —
    /// este método apenas mapeia "índice de profundidade dentro da terra" → LayerDefinition.
    /// índice 0 = mais perto da beira (SAND), índice 1 = interior (GRASS), etc.
    /// </summary>
    public LayerDefinition GetLandLayerByDepth(int depthIndex)
    {
        int currentDepth = 0;
        foreach (var layer in _layers)
        {
            if (!layer.IsLandLayer) continue;
            if (currentDepth == depthIndex) return layer;
            currentDepth++;
        }
        // Fallback: camada de terra mais alta
        return HighestLandLayer;
    }

    /// <summary>Número de camadas de terra definidas na pilha.</summary>
    public int LandLayerCount
    {
        get
        {
            int count = 0;
            foreach (var layer in _layers) if (layer.IsLandLayer) count++;
            return count;
        }
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        // Verifica ordenação crescente de SolidValue
        for (int i = 1; i < _layers.Count; i++)
        {
            if (_layers[i] == null || _layers[i - 1] == null) continue;
            if (_layers[i].SolidValue <= _layers[i - 1].SolidValue)
                Debug.LogWarning($"[LayerStack] Camada '{_layers[i].LayerName}' (SolidValue={_layers[i].SolidValue}) " +
                                 $"não está em ordem crescente após '{_layers[i-1].LayerName}' ({_layers[i-1].SolidValue}).");
        }
    }
#endif
}