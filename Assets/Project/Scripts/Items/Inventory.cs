using System;
using System.Collections.Generic;
using Unity.VisualScripting;
using UnityEditor.PackageManager;
using UnityEngine;

/// <summary>
/// Sistema gestor do inventário de um grupo (como a tripulação via CrewData).
/// Controla a adição e a remoção segura de itens, limitando o empilhamento 
/// de acordo com a configuração de cada ItemData (maximumQttPerSlot).
/// </summary>
public class Inventory : MonoBehaviour
{

    #region Estruturas de Dados
    /// <summary>
    /// Representa um espaço no inventário contendo a referência do item e a sua quantidade em pilha.
    /// </summary>
    [Serializable]
    public struct Slot
    {
        public ItemData item;
        public int quantity;
    }
    #endregion

    #region Campos e Estado do Inventário
    [Header("Estado Interno")]
    [Tooltip("Lista com a disposição atual dos itens no inventário.")]
    [SerializeField]
    private List<Slot> _inventorySlots = new();

    [Tooltip("O número limite de slots totais (células) que este inventário pode comportar.")]
    [SerializeField]
    private int _maxItemsPerInventory;
    private float _maxInventoryWeight = 50f;
    #endregion

    #region Propriedades Públicas
    public List<Slot> InventorySlots => _inventorySlots;
    public int MaxItemsPerInventory => _maxItemsPerInventory;
    #endregion

    #region Lógica de Adição e Remoção
    /// <summary>
    /// Adiciona uma quantidade específica de um item ao inventário.
    /// Prioriza sempre o preenchimento de slots já existentes que não atingiram a lotação máxima,
    /// e só depois cria novos slots caso o limite global de itens do inventário o permita.
    /// </summary>
    /// <param name="item">A base de dados (ScriptableObject) do item a adicionar.</param>
    /// <param name="quantity">O volume de itens a inserir no total.</param>
    public void AddItem(ItemData item, int quantity)
    {
        int remainingQuantity = quantity;

        // Passo 1: Inserir nos slots que já contêm este tipo de item, se não estiverem cheios
        for (int i = 0; i < _inventorySlots.Count; i++)
        {
            if (_inventorySlots[i].item == item &&
                _inventorySlots[i].item.MaximumQuantityPerSlot > _inventorySlots[i].quantity)
            {
                int maxItemsToAddHere =
                    _inventorySlots[i].item.MaximumQuantityPerSlot - _inventorySlots[i].quantity;

                int addedItems = Mathf.Min(remainingQuantity, maxItemsToAddHere);

                Slot slot = _inventorySlots[i];

                slot.quantity += addedItems;
                remainingQuantity -= addedItems;

                _inventorySlots[i] = slot;
            }

            if (remainingQuantity <= 0)
            {
                return;
            }
        }

        // Passo 2: Se ainda sobrar item, cria novos slots independentes respeitando a quantidade de pilha
        while (remainingQuantity > 0)
        {
            // Bloqueio se o inventário não comportar a abertura de mais espaços
            if (_maxItemsPerInventory == _inventorySlots.Count)
            {
                return;
            }

            Slot slot = new()
            {
                item = item,
                quantity = Mathf.Min(remainingQuantity, item.MaximumQuantityPerSlot)
            };

            _inventorySlots.Add(slot);

            remainingQuantity -= Mathf.Min(
                remainingQuantity,
                item.MaximumQuantityPerSlot
            );
        }
    }

    /// <summary>
    /// Deduz uma determinada quantidade de um item do inventário.
    /// O loop é executado do último slot (de trás) para o primeiro (frente), garantindo que
    /// os restos se consomem de forma estável, removendo completamente da lista os slots cujo valor atinja 0.
    /// </summary>
    /// <param name="item">A base de dados (ScriptableObject) do item a remover.</param>
    /// <param name="quantity">Total de itens a ser debitado.</param>
    public Slot RemoveItem(ItemData item, int quantity = 1)
    {
        int quantityToRemove = quantity;

        for (int i = _inventorySlots.Count - 1; i >= 0; i--)
        {
            if (_inventorySlots[i].item == item)
            {
                Slot tempSlot = _inventorySlots[i];

                int removedQuantity = Mathf.Min(quantityToRemove, tempSlot.quantity);

                tempSlot.quantity -= removedQuantity;
                quantityToRemove -= removedQuantity;

                // Se o slot ficou sem nenhum item, o próprio index é obliterado da List
                if (tempSlot.quantity <= 0)
                {
                    _inventorySlots.RemoveAt(i);
                    return new();
                }
                else
                {
                    _inventorySlots[i] = tempSlot;
                    return _inventorySlots[i];
                }
            }

            if (quantityToRemove <= 0)
            {
                return _inventorySlots[i];
            }
        }
        return _inventorySlots[0];
    }

    public Slot RemoveItemAt(int index, int quantity = 1)
    {
        if (_inventorySlots.Count > index)
        {
            int quantityToRemove = quantity;
            Slot tempSlot = _inventorySlots[index];

            int removedQuantity = Mathf.Min(quantityToRemove, tempSlot.quantity);

            tempSlot.quantity -= removedQuantity;
            quantityToRemove -= removedQuantity;

            // Se o slot ficou sem nenhum item, o próprio index é obliterado da List
            if (tempSlot.quantity <= 0)
            {
                _inventorySlots.RemoveAt(index);
                return new();
            }
            else
            {
                _inventorySlots[index] = tempSlot;
                return _inventorySlots[index];
            }
        }
        else
        {
            throw new IndexOutOfRangeException();
        }
    }

    /// <summary>
    /// Soma o peso total de todos os itens no inventario (peso unitario * quantidade por slot).
    /// </summary>
    public float CalculateTotalWeight()
    {
        float totalWeight = 0f;

        foreach (Slot slot in _inventorySlots)
            totalWeight += slot.item.UnitaryWeight * slot.quantity;

        return totalWeight;
    }

    public float GetMaxInventoryWeight()
    {
        return _maxInventoryWeight;
    }

    public void SetNewMaxInventoryWeight(float newMaxWeight)
    {
        _maxInventoryWeight = newMaxWeight;
    }
    
    
    public List<Slot> FilterByItemType(ItemData.ItemCategory category) 
    { 
        return _inventorySlots.FindAll(slot => slot.item.Category == category); 
    }

    public void ShowAllItems()
    {

    }
    
    public void SortByItemType() 
    { 
        _inventorySlots.Sort((a, b) => a.item.Category.CompareTo(b.item.Category)); 
    }
    public void SortAlphabetically()  
    { 
        _inventorySlots.Sort((a, b) => string.Compare(a.item.ItemName, b.item.ItemName, StringComparison.Ordinal));
    }
    public void SortByRarityOrLevel()
    {
        _inventorySlots.Sort((a, b) =>
        {
            int rarityComparison = b.item.Rarity.CompareTo(a.item.Rarity);
            if (rarityComparison != 0)
                return rarityComparison;

            // Se a raridade for igual, ordena por nome
            return string.Compare(a.item.ItemName, b.item.ItemName, StringComparison.Ordinal);
        });
    }
    public void SortByPrice()
    {
        _inventorySlots.Sort((a, b) => b.item.UnitaryPrice.CompareTo(a.item.UnitaryPrice));
    }
    #endregion
}