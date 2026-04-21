using System;
using System.Collections.Generic;
using UnityEngine;

public class Inventory : MonoBehaviour
{
    [Serializable]
    public struct Slot
    {
        public ItemData item;
        public int quantity;
    }

    public List<Slot> InventorySlots;
    public int MaxItemsPerInventory;

    public void AdicionarItem(ItemData item, int quantidade)
    {
        int qttRestante = quantidade;
        for (int i = 0; i < InventorySlots.Count; i++)
        {
            if (InventorySlots[i].item == item && InventorySlots[i].item.maximumQttPerSlot > InventorySlots[i].quantity)
            {
                int maxItemsToAddHere = InventorySlots[i].item.maximumQttPerSlot - InventorySlots[i].quantity;
                int addedItems = Mathf.Min(qttRestante, maxItemsToAddHere);

                Slot slot = InventorySlots[i];
                
                slot.quantity += addedItems;
                qttRestante -= addedItems;

                InventorySlots[i] = slot;
            }

            if (qttRestante <= 0)
                return;
        }
        
        while (qttRestante > 0)
        {
            if (MaxItemsPerInventory == InventorySlots.Count) return;

            Slot slot = new()
            {
                item = item,
                quantity = Mathf.Min(qttRestante, item.maximumQttPerSlot)
            };

            InventorySlots.Add(slot);
            qttRestante -= Mathf.Min(qttRestante, item.maximumQttPerSlot);
        }
    }

    public void RemoverItem(ItemData item, int quantidade = 1)
    {
        int qttParaRemover = quantidade;

        for (int i = InventorySlots.Count - 1; i >= 0; i--)
        {
            if (InventorySlots[i].item == item)
            {
                Slot temp = InventorySlots[i];
                
                temp.quantity -= Mathf.Min(qttParaRemover, temp.quantity);
                qttParaRemover -= Mathf.Min(qttParaRemover, InventorySlots[i].quantity);

                if (temp.quantity <= 0)
                {
                    InventorySlots.RemoveAt(i);
                }
                else
                {
                    InventorySlots[i] = temp;
                }
            }
            if (qttParaRemover <= 0) return;
        }
    }
}
