using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Representa materiais, itens de venda ou espolios comuns,
/// registando quais criaturas podem largar este material.
/// </summary>
[CreateAssetMenu(fileName = "New Material", menuName = "Scriptable Objects/MaterialData")]
public class MaterialData : BaseItemData
{

    // A categoria deste item é sempre ShipMaterial, então sobrescreve a propriedade para retornar isso.
    public override ItemCategory Category => ItemCategory.ShipMaterial;

    #region Estruturas
    [System.Serializable]
    public struct NpcDropSource
    {
        public GameObject npc;
        public int maxQuantity;
    }
    #endregion

    #region Atributos do Material
    [Header("Fontes do Material")]
    [Tooltip("Criaturas que podem largar este material apos a sua morte.")]
    [SerializeField]
    private List<NpcDropSource> _dropSourceList = new();
    #endregion

    #region Propriedades Publicas
    public List<NpcDropSource> DropSourceList => _dropSourceList;

    public override string GetItemType() => "Material";

    /// <summary>
    /// Lista de NPCs de drop e quantidade maxima por saque.
    /// </summary>
    public override string GetPerTypeDescriptionText()
    {
        if (_dropSourceList == null || _dropSourceList.Count == 0)
            return "Drop: None";

        System.Text.StringBuilder sb = new System.Text.StringBuilder();
        
        sb.AppendLine($"Raridade: {Rarity}");
        sb.AppendLine("Dropped by:");
        foreach (NpcDropSource source in _dropSourceList)
            sb.AppendLine($"  {FormatNpcName(source.npc)} (max {source.maxQuantity})");

        return sb.ToString().TrimEnd();
    }

    public override void UseItem()
    {
        // Deve ser feito   
    }

    private string FormatNpcName(GameObject npc)
    {
        return npc != null ? npc.name : "Unknown";
    }
    #endregion
}