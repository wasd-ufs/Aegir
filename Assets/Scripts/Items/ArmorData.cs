using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Define os dados de uma armadura ou equipamento defensivo.
/// Fornece mitigação de dano global e resistências elementais específicas.
/// </summary>
[CreateAssetMenu(fileName = "New Armor", menuName = "Scriptable Objects/ArmorData")]
public class ArmorData : ItemData
{
    #region Estruturas
    /// <summary>
    /// Bónus de resistência passiva contra um tipo de dano elemental específico.
    /// </summary>
    [System.Serializable]
    public struct ResistanceBonus
    {
        public NPCsData.DamageType damageType;
        public float intensity;
    }
    #endregion

    #region Atributos de Defesa
    [Header("Restrições")]
    [Tooltip("Classes permitidas a equipar e utilizar esta armadura.")]
    public List<NPCsData.Class> classe;

    [Header("Status Defensivos")]
    [Tooltip("Valor de armadura genérica subtraída de forma passiva ao dano recebido.")]
    public float resistanceBaseValue;

    [Tooltip("Resistências extra calculadas contra tipos específicos de ataque.")]
    public List<ResistanceBonus> perTypeResistanceBonus;
    #endregion
}