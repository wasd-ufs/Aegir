using System;
using System.Collections.Generic;
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
    public List<Slot> InventorySlots;
    
    [Tooltip("O número limite de slots totais (células) que este inventário pode comportar.")]
    public int MaxItemsPerInventory;
    #endregion

    #region Lógica de Adição e Remoção
    /// <summary>
    /// Adiciona uma quantidade específica de um item ao inventário.
    /// Prioriza sempre o preenchimento de slots já existentes que não atingiram a lotação máxima,
    /// e só depois cria novos slots caso o limite global de itens do inventário o permita.
    /// </summary>
    /// <param name="item">A base de dados (ScriptableObject) do item a adicionar.</param>
    /// <param name="quantidade">O volume de itens a inserir no total.</param>
    public void AdicionarItem(ItemData item, int quantidade)
    {
        int qttRestante = quantidade;
        
        // Passo 1: Inserir nos slots que já contêm este tipo de item, se não estiverem cheios
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
        
        // Passo 2: Se ainda sobrar item, cria novos slots independentes respeitando a quantidade de pilha
        while (qttRestante > 0)
        {
            // Bloqueio se o inventário não comportar a abertura de mais espaços
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

    /// <summary>
    /// Deduz uma determinada quantidade de um item do inventário.
    /// O loop é executado do último slot (de trás) para o primeiro (frente), garantindo que
    /// os restos se consomem de forma estável, removendo completamente da lista os slots cujo valor atinja 0.
    /// </summary>
    /// <param name="item">A base de dados (ScriptableObject) do item a remover.</param>
    /// <param name="quantidade">Total de itens a ser debitado.</param>
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

                // Se o slot ficou sem nenhum item, o próprio index é obliterado da List
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
    #endregion
}