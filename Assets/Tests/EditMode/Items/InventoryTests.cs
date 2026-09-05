using System.Reflection;
using NUnit.Framework;
using UnityEngine;

namespace Aegir.Tests.Items
{
    [TestFixture]
    public class InventoryTests
    {
        private GameObject _inventoryHolder;
        private Inventory _inventory;
        private ConsumableData _potionItem;

        [SetUp]
        public void SetUp()
        {
            _inventoryHolder = new GameObject("InventoryTestObject");
            _inventory = _inventoryHolder.AddComponent<Inventory>();

            // Configura o limite de itens do inventário via reflexão
            typeof(Inventory).GetField("_maxItemsPerInventory", BindingFlags.NonPublic | BindingFlags.Instance)
                ?.SetValue(_inventory, 3);

            // Cria um item consumível de teste com pilha máxima de 5
            _potionItem = ScriptableObject.CreateInstance<ConsumableData>();
            typeof(BaseItemData).GetField("_itemName", BindingFlags.NonPublic | BindingFlags.Instance)
                ?.SetValue(_potionItem, "Health Potion");
            typeof(BaseItemData).GetField("_maximumQuantityPerSlot", BindingFlags.NonPublic | BindingFlags.Instance)
                ?.SetValue(_potionItem, 5);
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(_potionItem);
            Object.DestroyImmediate(_inventoryHolder);
        }

        [Test]
        public void AddItem_EmptyInventory_CreatesNewSlotWithExactQuantity()
        {
            _inventory.AddItem(_potionItem, 3);

            Assert.AreEqual(1, _inventory.InventorySlots.Count);
            Assert.AreEqual(_potionItem, _inventory.InventorySlots[0].item);
            Assert.AreEqual(3, _inventory.InventorySlots[0].quantity);
        }

        [Test]
        public void AddItem_ExistingSlotWithSpace_StacksIntoSameSlot()
        {
            _inventory.AddItem(_potionItem, 2);
            _inventory.AddItem(_potionItem, 2);

            Assert.AreEqual(1, _inventory.InventorySlots.Count);
            Assert.AreEqual(4, _inventory.InventorySlots[0].quantity);
        }

        [Test]
        public void AddItem_ExceedsMaxSlotCapacity_SplitsAcrossMultipleSlots()
        {
            // Pilha máxima é 5. Adicionando 8, deve resultar em 1 slot de 5 e 1 slot de 3.
            _inventory.AddItem(_potionItem, 8);

            Assert.AreEqual(2, _inventory.InventorySlots.Count);
            Assert.AreEqual(5, _inventory.InventorySlots[0].quantity);
            Assert.AreEqual(3, _inventory.InventorySlots[1].quantity);
        }

        [Test]
        public void AddItem_WhenInventoryFull_DoesNotExceedMaxSlots()
        {
            // Capacidade máxima é 3 slots (cada um cabe 5 = máx 15 itens)
            _inventory.AddItem(_potionItem, 20);

            Assert.AreEqual(3, _inventory.InventorySlots.Count);
            Assert.AreEqual(5, _inventory.InventorySlots[0].quantity);
            Assert.AreEqual(5, _inventory.InventorySlots[1].quantity);
            Assert.AreEqual(5, _inventory.InventorySlots[2].quantity);
        }

        [Test]
        public void RemoveItem_PartialQuantity_DecrementsSlotQuantity()
        {
            _inventory.AddItem(_potionItem, 5);

            _inventory.RemoveItem(_potionItem, 2);

            Assert.AreEqual(1, _inventory.InventorySlots.Count);
            Assert.AreEqual(3, _inventory.InventorySlots[0].quantity);
        }

        [Test]
        public void RemoveItem_EntireQuantity_RemovesSlotFromInventory()
        {
            _inventory.AddItem(_potionItem, 4);

            _inventory.RemoveItem(_potionItem, 4);

            Assert.AreEqual(0, _inventory.InventorySlots.Count);
        }
    }
}
