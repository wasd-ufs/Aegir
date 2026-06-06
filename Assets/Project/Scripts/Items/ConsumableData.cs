using UnityEngine;

[CreateAssetMenu(fileName = "New Consumable", menuName = "Scriptable Objects/ConsumableData")]
public class ConsumableData : ItemData
{
    #region Tipos e Efeitos
    public enum Effect { Heal, Strength }

    [Header("Atributos do Consumivel")]
    [Tooltip("A magnitude do efeito aplicado.")]
    [SerializeField]
    private float _intensity;

    [Tooltip("Qual o tipo de efeito que este item causa ao ser consumido.")]
    [SerializeField]
    private Effect _effectType;

    [Tooltip("Duracao do efeito em turnos. Use 1 para efeito imediato.")]
    [SerializeField]
    private int _durationInTurns = 1;
    #endregion

    #region Propriedades Publicas
    public float Intensity => _intensity;
    public Effect EffectType => _effectType;
    public int DurationInTurns => _durationInTurns;

    public override string GetItemType() => "Consumivel";

    public override string GetPerTypeDescriptionText()
    {
        System.Text.StringBuilder sb = new System.Text.StringBuilder();

        sb.AppendLine($"{FormatEffect(_effectType)}: {_intensity:F1}");
        sb.Append(_durationInTurns <= 1 ? "Imediato" : $"Duracao: {_durationInTurns} turnos");

        return sb.ToString().TrimEnd();
    }

    public override void UseItem()
    {
        // Deve ser feito   
    }

    private string FormatEffect(Effect effect)
    {
        return effect switch
        {
            Effect.Heal     => "Cura",
            Effect.Strength => "Forca",
            _               => effect.ToString()
        };
    }
    #endregion
}