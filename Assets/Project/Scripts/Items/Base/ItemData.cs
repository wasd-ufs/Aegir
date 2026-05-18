using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Classe base abstrata para todos os itens do jogo.
/// Define os dados fundamentais que qualquer item (arma, armadura, consumível, etc.)
/// deve ter para interagir com o inventário e a interface gráfica.
/// </summary>
public abstract class ItemData : ScriptableObject
{
    #region Propriedades Básicas
    [Header("Informações do Item")]
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

    [Tooltip("Quantidade máxima deste item que pode ser acumulada num único slot do inventário.")]
    [SerializeField]
    private int _maximumQuantityPerSlot;

    [Tooltip("Valor comercial base do item.")]
    [SerializeField]
    private int _unitaryPrice;
    #endregion

    #region Propriedades Públicas
    public string ItemName => _itemName;
    public string Description => _description;
    public Sprite Icon => _icon;

    public List<NPCsData.Type> PossibleTypes => _possibleTypes;

    public int MaximumQuantityPerSlot => _maximumQuantityPerSlot;
    public int UnitaryPrice => _unitaryPrice;
    #endregion
}