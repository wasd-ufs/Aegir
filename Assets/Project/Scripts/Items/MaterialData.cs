using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Representa itens base que funcionam como materiais, itens de venda ou espólios (loot) comuns,
/// registando adicionalmente quais criaturas podem largar este material.
/// </summary>
[CreateAssetMenu(fileName = "New Material", menuName = "Scriptable Objects/MaterialData")]
public class MaterialData : ItemData
{
    #region Estruturas
    /// <summary>
    /// Mapeia o NPC que deixa cair o item e a quantidade máxima possível no saque.
    /// </summary>
    [System.Serializable]
    public struct NpcDropSource
    {
        public GameObject npc;
        public int maxQuantity;
    }
    #endregion

    #region Atributos do Material
    [Header("Fontes do Material")]
    [Tooltip("Lista de entidades ou criaturas do jogo que podem largar este material após a sua morte.")]
    [SerializeField]
    private List<NpcDropSource> _dropSourceList = new();
    #endregion

    #region Propriedades Públicas
    public List<NpcDropSource> DropSourceList => _dropSourceList;
    #endregion
}