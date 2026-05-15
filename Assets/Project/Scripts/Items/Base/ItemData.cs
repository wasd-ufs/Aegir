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
    public string itemName;
    [TextArea] public string description;
    public Sprite Icon;
    #endregion

    #region Regras de Uso e Valor
    [Header("Regras do Item")]
    [Tooltip("Tipos de criatura que podem interagir ou utilizar este item.")]
    public List<NPCsData.Type> possibleTypes;
    
    [Tooltip("Quantidade máxima deste item que pode ser acumulada num único slot do inventário.")]
    public int maximumQttPerSlot;
    
    [Tooltip("Valor comercial base do item.")]
    public int unitaryPrice;
    #endregion
}