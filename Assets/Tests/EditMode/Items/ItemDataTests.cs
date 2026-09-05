using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;

namespace Aegir.Tests.Items
{
    [TestFixture]
    public class ItemDataTests
    {
        [Test]
        public void ArmorData_CategoryAndType_AreCorrect()
        {
            ArmorData armor = ScriptableObject.CreateInstance<ArmorData>();

            Assert.AreEqual(BaseItemData.ItemCategory.Armor, armor.Category);
            Assert.AreEqual("Armadura", armor.GetItemType());

            Object.DestroyImmediate(armor);
        }

        [Test]
        public void ArmorData_DescriptionFormatting_IncludesDefenseAndRarity()
        {
            ArmorData armor = ScriptableObject.CreateInstance<ArmorData>();
            typeof(ArmorData).GetField("_resistanceBaseValue", BindingFlags.NonPublic | BindingFlags.Instance)
                ?.SetValue(armor, 15f);
            typeof(BaseItemData).GetField("_rarity", BindingFlags.NonPublic | BindingFlags.Instance)
                ?.SetValue(armor, 2);

            string desc = armor.GetPerTypeDescriptionText();

            StringAssert.Contains("Raridade: 2", desc);
            StringAssert.Contains("DEF: 15,0", desc.Replace('.', ',')); // Suporta diferentes locales

            Object.DestroyImmediate(armor);
        }

        [Test]
        public void WeaponData_CategoryAndType_AreCorrect()
        {
            WeaponData weapon = ScriptableObject.CreateInstance<WeaponData>();

            Assert.AreEqual(BaseItemData.ItemCategory.Weapon, weapon.Category);
            Assert.AreEqual("Arma", weapon.GetItemType());

            Object.DestroyImmediate(weapon);
        }

        [Test]
        public void WeaponData_DescriptionFormatting_IncludesAttackValue()
        {
            WeaponData weapon = ScriptableObject.CreateInstance<WeaponData>();
            typeof(WeaponData).GetField("_attackBaseValue", BindingFlags.NonPublic | BindingFlags.Instance)
                ?.SetValue(weapon, 25f);
            typeof(BaseItemData).GetField("_rarity", BindingFlags.NonPublic | BindingFlags.Instance)
                ?.SetValue(weapon, 1);

            string desc = weapon.GetPerTypeDescriptionText();

            StringAssert.Contains("Raridade: 1", desc);
            StringAssert.Contains("ATQ: 25,0", desc.Replace('.', ','));

            Object.DestroyImmediate(weapon);
        }

        [Test]
        public void ConsumableData_CategoryAndType_AreCorrect()
        {
            ConsumableData consumable = ScriptableObject.CreateInstance<ConsumableData>();

            Assert.AreEqual(BaseItemData.ItemCategory.Consumable, consumable.Category);
            Assert.AreEqual("Consumivel", consumable.GetItemType());

            Object.DestroyImmediate(consumable);
        }
    }
}
