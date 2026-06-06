using System.Collections.Generic;
using UnityEditor.Overlays;
using UnityEngine;

[CreateAssetMenu(fileName = "New Armor", menuName = "Scriptable Objects/ArmorData")]
public class ArmorData : ItemData
{

    // A categoria deste item é sempre Armadura, então sobrescreve a propriedade para retornar isso.
    public override ItemCategory Category => ItemCategory.Armor;

    #region Estruturas
    [System.Serializable]
    public struct ResistanceBonus
    {
        public NPCsData.DamageType damageType;
        public float intensity;
    }
    #endregion

    #region Atributos de Defesa
    [Header("Restricoes")]
    [Tooltip("Classes permitidas a equipar e utilizar esta armadura.")]
    [SerializeField]
    private List<NPCsData.Class> _allowedClassList;

    [Header("Status Defensivos")]
    [Tooltip("Valor de armadura generica subtraida de forma passiva ao dano recebido.")]
    [SerializeField]
    private float _resistanceBaseValue;

    [Tooltip("Resistencias extra calculadas contra tipos especificos de ataque.")]
    [SerializeField]
    private List<ResistanceBonus> _perTypeResistanceBonusList;
    #endregion

    #region Propriedades Publicas
    public List<NPCsData.Class> AllowedClassList => _allowedClassList;
    public float ResistanceBaseValue => _resistanceBaseValue;
    public List<ResistanceBonus> PerTypeResistanceBonusList => _perTypeResistanceBonusList;

    public override string GetItemType() => "Armadura";

    public override string GetPerTypeDescriptionText()
    {
        System.Text.StringBuilder sb = new System.Text.StringBuilder();

        
        sb.AppendLine($"Raridade: {Rarity}");
        sb.AppendLine($"DEF: {_resistanceBaseValue:F1}");

        if (_allowedClassList != null && _allowedClassList.Count > 0)
            sb.AppendLine($"Pode ser usada por: {FormatClassList(_allowedClassList)}");

        if (_perTypeResistanceBonusList != null && _perTypeResistanceBonusList.Count > 0)
            foreach (ResistanceBonus bonus in _perTypeResistanceBonusList)
                sb.AppendLine($"RES {FormatDamageType(bonus.damageType)}: +{bonus.intensity:F1}");

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
            NPCsData.DamageType.Physical => "Fisica",
            NPCsData.DamageType.Magical  => "Magica",
            NPCsData.DamageType.Fire     => "a Fogo",
            NPCsData.DamageType.Ice      => "a Gelo",
            NPCsData.DamageType.Poison   => "a Veneno",
            NPCsData.DamageType.Holy     => "ao Sagrado",
            NPCsData.DamageType.Cursed   => "ao Amaldicoado",
            _                            => damageType.ToString()
        };
    }

    private string FormatClassList(List<NPCsData.Class> classList)
    {
        return string.Join(", ", classList);
    }
    #endregion
}