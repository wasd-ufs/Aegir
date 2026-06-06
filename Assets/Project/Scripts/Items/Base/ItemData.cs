using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Classe base abstrata para todos os itens do jogo.
/// Define os dados fundamentais que qualquer item deve ter
/// para interagir com o inventario e a interface grafica.
/// </summary>
public abstract class ItemData : ScriptableObject
{

    #region Enumerações
    public enum ItemCategory
    {
        Weapon,
        Armor,
        Consumable,
        ShipMaterial,
        KeyItem,
        Collectible,
        Misc
    }
    #endregion


    #region Propriedades Basicas
    [Header("Informacoes do Item")]
    [SerializeField] private string _itemName;

    [SerializeField]
    [TextArea]
    private string _description;

    [SerializeField] private Sprite _icon;


    #endregion

    #region Regras de Uso e Valor
    [Header("Regras do Item")]
    [Tooltip("Tipos de criatura que podem interagir ou utilizar este item.")]
    [SerializeField]
    private List<NPCsData.Type> _possibleTypes;

    [Tooltip("Quantidade maxima deste item acumulada num unico slot do inventario.")]
    [SerializeField]
    private int _maximumQuantityPerSlot;

    [Tooltip("Valor comercial base do item.")]
    [SerializeField]
    private int _unitaryPrice;
    
    [Tooltip("Peso unitário do item.")]
    [SerializeField]
    private float _unitaryWeight;





    [Tooltip("Raridade do item.")]
    [SerializeField]
    private int _rarity;

    [SerializeField]
    private ItemCategory _category;


    #endregion

    #region Propriedades Publicas
    public string ItemName => _itemName;
    public string Description => _description;
    public float UnitaryWeight => _unitaryWeight;
    public Sprite Icon => _icon;
    public List<NPCsData.Type> PossibleTypes => _possibleTypes;
    public int MaximumQuantityPerSlot => _maximumQuantityPerSlot;
    public int UnitaryPrice => _unitaryPrice;
    
    
    
    public ItemCategory Category => _category;
    public int Rarity => _rarity;



    public abstract string GetItemType();
    public abstract string GetPerTypeDescriptionText();
    public abstract void UseItem();

    /// <summary>
    /// Retorna o bloco de texto completo exibido na UI ao selecionar um item:
    /// tipo, peso unitario e atributos especificos do item.
    /// </summary>
    public string GetFullDescriptionText()
    {
        System.Text.StringBuilder sb = new System.Text.StringBuilder();

        sb.AppendLine($"Tipo: {GetItemType()}");
        sb.AppendLine($"Peso Unitario: {_unitaryWeight:F1}");
        sb.Append(GetPerTypeDescriptionText());

        return sb.ToString().TrimEnd();
    }
    #endregion
}