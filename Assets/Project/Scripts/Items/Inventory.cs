using System;
using System.Collections.Generic;
using Unity.VisualScripting;
using UnityEditor.PackageManager;
using UnityEngine;

/// <summary>
/// Sistema gestor do inventário de um grupo (como a tripulação via CrewData).
/// Controla a adição e a remoção segura de itens, limitando o empilhamento 
/// de acordo com a configuração de cada BaseItemData (maximumQttPerSlot).
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
        public BaseItemData item;
        public int quantity;
    }
    #endregion

    #region Campos e Estado do Inventário
    [Header("Estado Interno")]
    [Tooltip("Lista com a disposição atual dos itens no inventário.")]
    [SerializeField]
    private List<Slot> _inventorySlotsList = new();

    [Tooltip("O número limite de slots totais (células) que este inventário pode comportar.")]
    [SerializeField]
    private int _maxItemsPerInventory;
    private float _maxInventoryWeight = 50f;
    #endregion

    #region Propriedades Públicas
    public List<Slot> InventorySlots => _inventorySlotsList;
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
    public void AddItem(BaseItemData item, int quantity)
    {
        int remainingQuantity = quantity;

        // Passo 1: Inserir nos slots que já contêm este tipo de item, se não estiverem cheios
        for (int i = 0; i < _inventorySlotsList.Count; i++)
        {
            if (_inventorySlotsList[i].item == item &&
                _inventorySlotsList[i].item.MaximumQuantityPerSlot > _inventorySlotsList[i].quantity)
            {
                int maxItemsToAddHere =
                    _inventorySlotsList[i].item.MaximumQuantityPerSlot - _inventorySlotsList[i].quantity;

                int addedItems = Mathf.Min(remainingQuantity, maxItemsToAddHere);

                Slot slot = _inventorySlotsList[i];

                slot.quantity += addedItems;
                remainingQuantity -= addedItems;

                _inventorySlotsList[i] = slot;
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
            if (_maxItemsPerInventory == _inventorySlotsList.Count)
            {
                return;
            }

            Slot slot = new()
            {
                item = item,
                quantity = Mathf.Min(remainingQuantity, item.MaximumQuantityPerSlot)
            };

            _inventorySlotsList.Add(slot);

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
    public Slot RemoveItem(BaseItemData item, int quantity = 1)
    {
        int quantityToRemove = quantity;

        for (int i = _inventorySlotsList.Count - 1; i >= 0; i--)
        {
            if (_inventorySlotsList[i].item == item)
            {
                Slot tempSlot = _inventorySlotsList[i];

                int removedQuantity = Mathf.Min(quantityToRemove, tempSlot.quantity);

                tempSlot.quantity -= removedQuantity;
                quantityToRemove -= removedQuantity;

                // Se o slot ficou sem nenhum item, o próprio index é obliterado da List
                if (tempSlot.quantity <= 0)
                {
                    _inventorySlotsList.RemoveAt(i);
                    return new();
                }
                else
                {
                    _inventorySlotsList[i] = tempSlot;
                    return _inventorySlotsList[i];
                }
            }

            if (quantityToRemove <= 0)
            {
                return _inventorySlotsList[i];
            }
        }
        return _inventorySlotsList[0];
    }

    public Slot RemoveItemAt(int index, int quantity = 1)
    {
        if (_inventorySlotsList.Count > index)
        {
            int quantityToRemove = quantity;
            Slot tempSlot = _inventorySlotsList[index];

            int removedQuantity = Mathf.Min(quantityToRemove, tempSlot.quantity);

            tempSlot.quantity -= removedQuantity;
            quantityToRemove -= removedQuantity;

            // Se o slot ficou sem nenhum item, o próprio index é obliterado da List
            if (tempSlot.quantity <= 0)
            {
                _inventorySlotsList.RemoveAt(index);
                return new();
            }
            else
            {
                _inventorySlotsList[index] = tempSlot;
                return _inventorySlotsList[index];
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

        foreach (Slot slot in _inventorySlotsList)
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
    
    
    public List<Slot> FilterByItemType(BaseItemData.ItemCategory category) 
    { 
        return _inventorySlotsList.FindAll(slot => slot.item.Category == category); 
    }

    public void ShowAllItems()
    {

    }
    
    public void SortByItemType() 
    { 
        _inventorySlotsList.Sort((a, b) => a.item.Category.CompareTo(b.item.Category)); 
    }
    public void SortAlphabetically()  
    { 
        _inventorySlotsList.Sort((a, b) => string.Compare(a.item.ItemName, b.item.ItemName, StringComparison.Ordinal));
    }
    public void SortByRarityOrLevel()
    {
        _inventorySlotsList.Sort((a, b) =>
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
        _inventorySlotsList.Sort((a, b) => b.item.UnitaryPrice.CompareTo(a.item.UnitaryPrice));
    }
    #endregion
}