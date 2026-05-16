using UnityEngine;

/// <summary>
/// Define itens consumíveis (como poções ou comida) que aplicam efeitos temporários 
/// ou instantâneos (como cura ou bónus de força) em unidades durante o combate ou no mapa.
/// </summary>
[CreateAssetMenu(fileName = "New Consumable", menuName = "Scriptable Objects/ConsumableData")]
public class ConsumableData : ItemData
{
    #region Tipos e Efeitos
    public enum Effect {Heal, Strength}

    [Header("Atributos do Consumível")]
    [Tooltip("A magnitude do efeito aplicado (quantidade de HP curado ou força adicionada).")]
    [SerializeField]
    private float _intensity;

    [Tooltip("Qual o tipo de efeito que este item causa ao ser consumido.")]
    [SerializeField]
    private Effect _effectType;

    [Tooltip("Duração do efeito em turnos de combate. (Use 1 para efeito imediato/único).")]
    [SerializeField]
    private int _durationInTurns = 1;
    #endregion

    #region Propriedades Públicas
    public float Intensity => _intensity;

    public Effect EffectType => _effectType;

    public int DurationInTurns => _durationInTurns;
    #endregion
}