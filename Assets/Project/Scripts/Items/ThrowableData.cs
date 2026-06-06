using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "New Throwable", menuName = "Scriptable Objects/ThrowableData")]
public class ThrowableData : BaseItemData
{

    // A categoria deste item é sempre Throwable, então sobrescreve a propriedade para retornar isso.
    public override ItemCategory Category => ItemCategory.Throwable;

    #region Atributos de Arremesso
    [Header("Restricoes e Alvos")]
    [Tooltip("Lista de classes que tem permissao para arremessar este item.")]
    [SerializeField]
    private List<NPCsData.Class> _throwableByList = new();

    [Tooltip("Numero maximo de alvos que a area de efeito pode atingir.")]
    [SerializeField]
    private int _maxTargetQuantity;

    [Header("Dano")]
    [Tooltip("Tipo de dano infligido no arremesso.")]
    [SerializeField]
    private NPCsData.DamageType _damageType;

    [Tooltip("Dano base infligido pelo item arremessavel.")]
    [SerializeField]
    private float _intensity;
    #endregion

    #region Propriedades Publicas
    public List<NPCsData.Class> ThrowableByList => _throwableByList;
    public int MaxTargetQuantity => _maxTargetQuantity;
    public NPCsData.DamageType DamageType => _damageType;
    public float Intensity => _intensity;

    public override string GetItemType() => "Arremessavel";

    public override string GetPerTypeDescriptionText()
    {
        System.Text.StringBuilder sb = new System.Text.StringBuilder();

    
        sb.AppendLine($"Raridade: {Rarity}");
        sb.AppendLine($"Dano {FormatDamageType(_damageType)}: {_intensity:F1}");
        sb.AppendLine($"Alvos Max: {_maxTargetQuantity}");

        if (_throwableByList != null && _throwableByList.Count > 0)
            sb.Append($"Pode ser arremessado por: {FormatClassList(_throwableByList)}");

        return sb.ToString().TrimEnd();
    }

    private string FormatDamageType(NPCsData.DamageType damageType)
    {
        return damageType switch
        {
            NPCsData.DamageType.Physical => "Fisico",
            NPCsData.DamageType.Magical  => "Magico",
            NPCsData.DamageType.Fire     => "de Fogo",
            NPCsData.DamageType.Ice      => "de Gelo",
            NPCsData.DamageType.Poison   => "de Veneno",
            NPCsData.DamageType.Holy     => "Sagrado",
            NPCsData.DamageType.Cursed   => "Amaldicoado",
            _                            => damageType.ToString()
        };
    }

    private string FormatClassList(List<NPCsData.Class> classList)
    {
        return string.Join(", ", classList);
    }

    public override void UseItem()
    {
        // Deve ser feito   
    }
    #endregion
}