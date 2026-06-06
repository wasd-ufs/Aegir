using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "New Weapon", menuName = "Scriptable Objects/WeaponData")]
public class WeaponData : BaseItemData
{

    // A categoria deste item é sempre Weapon, então sobrescreve a propriedade para retornar isso.
    public override ItemCategory Category => ItemCategory.Weapon;


    #region Estruturas
    [System.Serializable]
    public struct AttackBonus
    {
        public NPCsData.DamageType damageType;
        public float intensity;
    }
    #endregion

    #region Atributos de Combate
    [Header("Restricoes")]
    [Tooltip("Lista de classes da tripulacao permitidas a equipar esta arma.")]
    [SerializeField]
    private List<NPCsData.Class> _allowedClassList = new();

    [Header("Status de Ataque")]
    [Tooltip("Poder de ataque base adicionado a forca da entidade utilizadora.")]
    [SerializeField]
    private float _attackBaseValue;

    [Tooltip("Bonus adicionais aplicados a diferentes tipos de dano.")]
    [SerializeField]
    private List<AttackBonus> _perTypeAttackBonusList = new();
    #endregion

    #region Propriedades Publicas
    public List<NPCsData.Class> AllowedClassList => _allowedClassList;
    public float AttackBaseValue => _attackBaseValue;
    public List<AttackBonus> PerTypeAttackBonusList => _perTypeAttackBonusList;

    public override string GetItemType() => "Arma";

    public override string GetPerTypeDescriptionText()
    {
        System.Text.StringBuilder sb = new System.Text.StringBuilder();

        sb.AppendLine($"Raridade: {Rarity}");
        sb.AppendLine($"ATQ: {_attackBaseValue:F1}");

        if (_allowedClassList != null && _allowedClassList.Count > 0)
            sb.AppendLine($"Pode ser usada por: {FormatClassList(_allowedClassList)}");

        if (_perTypeAttackBonusList != null && _perTypeAttackBonusList.Count > 0)
            foreach (AttackBonus bonus in _perTypeAttackBonusList)
                sb.AppendLine($"ATQ {FormatDamageType(bonus.damageType)}: +{bonus.intensity:F1}");

        return sb.ToString().TrimEnd();
    }

    public override void UseItem()
    {
        // Deve ser feito   
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
    #endregion
}