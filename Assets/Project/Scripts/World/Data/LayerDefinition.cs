using UnityEngine;

/// <summary>
/// Define uma única camada da pilha de terreno.
/// Criada como ScriptableObject para ser configurada no Inspector sem tocar em código.
///
/// Convenção de valores (mantida do sistema original):
///   Camadas sólidas  → valores pares  (ex: -4, -2, 0, 2, 4, 6, 8 ...)
///   Camadas de trans → valor sólido-1 (ex: -3, -1,  1, 3, 5, 7 ...)
///
/// Não quebre esta convenção: os CornerSockets dos Tiles no Inspector já
/// dependem deste esquema de numeração.
/// </summary>
[CreateAssetMenu(fileName = "LayerDefinition", menuName = "WFC/Layer Definition")]
public class LayerDefinition : ScriptableObject
{
    [Header("Identidade")]
    [Tooltip("Nome legível, usado apenas para debug e Inspector.")]
    [SerializeField] private string _layerName;

    [Header("Valores de Camada")]
    [Tooltip("Valor inteiro desta camada quando está longe de qualquer transição. DEVE ser par.")]
    [SerializeField] private int _solidValue;

    [Tooltip("Valor inteiro usado nas células de borda (transição para a camada inferior). DEVE ser solidValue - 1.")]
    [SerializeField] private int _transitionValue;

    [Header("Tipo")]
    [Tooltip("True = camada de terra (areia, grama, floresta...). False = camada de água (água, mar, oceano...).")]
    [SerializeField] private bool _isLandLayer;

    [Header("Threshold de Altura (apenas para camadas de água)")]
    [Tooltip("O sampler deve retornar um valor ACIMA deste para esta camada ser escolhida. " +
             "Ignorado em camadas de terra — a camada de terra é determinada por distância à beira.")]
    [SerializeField] private float _heightThreshold;

    public string LayerName       => _layerName;
    public int    SolidValue      => _solidValue;
    public int    TransitionValue => _transitionValue;
    public bool   IsLandLayer     => _isLandLayer;
    public float  HeightThreshold => _heightThreshold;

#if UNITY_EDITOR
    private void OnValidate()
    {
        // Garante que SolidValue é par
        if (_solidValue % 2 != 0)
            Debug.LogWarning($"[LayerDefinition] '{_layerName}': SolidValue ({_solidValue}) deveria ser par.");

        // Garante que TransitionValue == SolidValue - 1
        if (_transitionValue != _solidValue - 1)
            Debug.LogWarning($"[LayerDefinition] '{_layerName}': TransitionValue ({_transitionValue}) deveria ser SolidValue-1 ({_solidValue - 1}).");
    }
#endif
}