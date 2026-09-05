using System.Reflection;
using NUnit.Framework;
using UnityEngine;

namespace Aegir.Tests.Entities
{
    [TestFixture]
    public class NPCsDataTests
    {
        private GameObject _npcGameObject;
        private NPCsData _npc;

        [SetUp]
        public void SetUp()
        {
            _npcGameObject = new GameObject("TestNPC");
            _npc = _npcGameObject.AddComponent<NPCsData>();
            _npc.MaxHealth = 100f;
            _npc.Heal(100f);
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(_npcGameObject);
        }

        private void SetCreatureType(NPCsData.Type type)
        {
            typeof(NPCsData).GetField("_creatureType", BindingFlags.NonPublic | BindingFlags.Instance)
                ?.SetValue(_npc, type);
        }

        [Test]
        public void Fantasma_PhysicalDamage_IsImmune()
        {
            SetCreatureType(NPCsData.Type.Fantasma);

            _npc.TakeDamage(50f, NPCsData.DamageType.Physical);

            Assert.AreEqual(100f, _npc.CurrentHealth, "Fantasmas devem ser totalmente imunes a dano físico (0x).");
        }

        [Test]
        public void Fantasma_HolyDamage_TakesDoubleDamage()
        {
            SetCreatureType(NPCsData.Type.Fantasma);

            _npc.TakeDamage(30f, NPCsData.DamageType.Holy);

            // 30 * 2.0x = 60 de dano -> 100 - 60 = 40
            Assert.AreEqual(40f, _npc.CurrentHealth, "Fantasmas devem receber 2.0x de dano Sagrado.");
        }

        [Test]
        public void Esqueleto_IceDamage_IsImmune()
        {
            SetCreatureType(NPCsData.Type.Esqueleto);

            _npc.TakeDamage(40f, NPCsData.DamageType.Ice);

            Assert.AreEqual(100f, _npc.CurrentHealth, "Esqueletos devem ser imunes a dano de Gelo (0x).");
        }

        [Test]
        public void TakeDamage_WithArmorEquipped_MitigatesFlatValue()
        {
            SetCreatureType(NPCsData.Type.Humano);

            ArmorData testArmor = ScriptableObject.CreateInstance<ArmorData>();
            typeof(ArmorData).GetField("_resistanceBaseValue", BindingFlags.NonPublic | BindingFlags.Instance)
                ?.SetValue(testArmor, 10f);

            _npc.EquippedArmor = testArmor;

            // Dano 30 - 10 de armadura = 20 de dano real
            _npc.TakeDamage(30f, NPCsData.DamageType.Physical);

            Assert.AreEqual(80f, _npc.CurrentHealth);

            Object.DestroyImmediate(testArmor);
        }

        [Test]
        public void Heal_CannotExceedMaxHealth()
        {
            SetCreatureType(NPCsData.Type.Humano);
            _npc.TakeDamage(20f, NPCsData.DamageType.Physical); // Vida = 80

            _npc.Heal(50f); // Tenta curar 50

            Assert.AreEqual(100f, _npc.CurrentHealth, "Cura não deve ultrapassar a vida máxima.");
        }

        [Test]
        public void Events_OnHealthChangedAndOnDeath_FireCorrectly()
        {
            SetCreatureType(NPCsData.Type.Humano);

            int healthChangeCount = 0;
            float lastCurrent = -1f;
            float lastMax = -1f;

            bool deathFired = false;

            _npc.OnHealthChanged += (npc, cur, max) =>
            {
                healthChangeCount++;
                lastCurrent = cur;
                lastMax = max;
            };

            _npc.OnDeath += (npc) =>
            {
                deathFired = true;
            };

            _npc.TakeDamage(40f, NPCsData.DamageType.Physical);

            Assert.AreEqual(1, healthChangeCount);
            Assert.AreEqual(60f, lastCurrent);
            Assert.AreEqual(100f, lastMax);
            Assert.IsFalse(deathFired);

            // Dano letal
            _npc.TakeDamage(100f, NPCsData.DamageType.Physical);

            Assert.AreEqual(2, healthChangeCount);
            Assert.AreEqual(0f, lastCurrent);
            Assert.IsTrue(deathFired);
            Assert.IsFalse(_npc.isAlive);
        }

        [Test]
        public void DeadEntity_DoesNotTakeFurtherDamage()
        {
            SetCreatureType(NPCsData.Type.Humano);
            _npc.TakeDamage(200f, NPCsData.DamageType.Physical); // Morte

            Assert.IsFalse(_npc.isAlive);
            Assert.AreEqual(0f, _npc.CurrentHealth);

            _npc.TakeDamage(50f, NPCsData.DamageType.Physical);
            Assert.AreEqual(0f, _npc.CurrentHealth);
        }

        [Test]
        public void EquipWeapon_SwapsAndReturnsPreviousWeapon()
        {
            WeaponData sword = ScriptableObject.CreateInstance<WeaponData>();
            WeaponData axe = ScriptableObject.CreateInstance<WeaponData>();

            WeaponData old1 = _npc.EquipWeapon(sword);
            Assert.IsNull(old1);
            Assert.AreSame(sword, _npc.EquippedWeapon);

            WeaponData old2 = _npc.EquipWeapon(axe);
            Assert.AreSame(sword, old2);
            Assert.AreSame(axe, _npc.EquippedWeapon);

            Object.DestroyImmediate(sword);
            Object.DestroyImmediate(axe);
        }

        [Test]
        public void EquipArmor_SwapsAndReturnsPreviousArmor()
        {
            ArmorData light = ScriptableObject.CreateInstance<ArmorData>();
            ArmorData heavy = ScriptableObject.CreateInstance<ArmorData>();

            ArmorData old1 = _npc.EquipArmor(light);
            Assert.IsNull(old1);
            Assert.AreSame(light, _npc.EquippedArmor);

            ArmorData old2 = _npc.EquipArmor(heavy);
            Assert.AreSame(light, old2);
            Assert.AreSame(heavy, _npc.EquippedArmor);

            Object.DestroyImmediate(light);
            Object.DestroyImmediate(heavy);
        }

        [Test]
        public void GetAttackPower_CombinesStrengthAndWeaponDamage()
        {
            _npc.Strength = 15f;
            Assert.AreEqual(15f, _npc.GetAttackPower());

            WeaponData bow = ScriptableObject.CreateInstance<WeaponData>();
            typeof(WeaponData).GetField("_attackBaseValue", BindingFlags.NonPublic | BindingFlags.Instance)
                ?.SetValue(bow, 25f);

            _npc.EquipWeapon(bow);
            Assert.AreEqual(40f, _npc.GetAttackPower(), "O poder de ataque deve somar a força base com o dano da arma.");

            Object.DestroyImmediate(bow);
        }

        [Test]
        public void GainXp_And_LevelUp_UpgradesStatsAndHealsToFull()
        {
            _npc.Strength = 10f;
            _npc.MaxHealth = 100f;
            _npc.TakeDamage(50f, NPCsData.DamageType.Physical); // HP = 50

            _npc.GainXp(120f); // xpToNextLevel é 100
            _npc.LevelUp();

            // Level subiu para 2
            // maxHealth *= 1.2f -> 120
            // strength *= 1.2f -> 12
            // Heal(maxHealth) restaura a vida para o novo teto de 120
            Assert.AreEqual(120f, _npc.MaxHealth, 0.01f);
            Assert.AreEqual(120f, _npc.CurrentHealth, 0.01f);
            Assert.AreEqual(12f, _npc.Strength, 0.01f);
        }

        [Test]
        public void ActiveEffects_StrengthBuffAndExpiry_RevertsCorrectly()
        {
            _npc.Strength = 20f;

            NPCsData.ActiveEffect strengthBuff = new NPCsData.ActiveEffect
            {
                effectType = CombatBase.EffectType.Strength,
                intensity = 10f,
                remainingTurns = 1
            };

            _npc.AddEffect(strengthBuff);
            Assert.AreEqual(30f, _npc.Strength, "Buff deve aumentar a força de imediato.");

            _npc.TickEffects(); // Turno passa, efeito expira
            Assert.AreEqual(20f, _npc.Strength, "Ao expirar o efeito, a força original deve ser restaurada.");
        }

        [Test]
        public void ActiveEffects_TickDamage_DecreasesHealthOverTurns()
        {
            SetCreatureType(NPCsData.Type.Humano);

            NPCsData.ActiveEffect poison = new NPCsData.ActiveEffect
            {
                effectType = CombatBase.EffectType.Damage,
                intensity = 15f,
                remainingTurns = 2,
                damageType = NPCsData.DamageType.Physical
            };

            _npc.AddEffect(poison);
            Assert.AreEqual(100f, _npc.CurrentHealth);

            _npc.TickEffects(); // 1º tick: sofre 15 de dano -> 85
            Assert.AreEqual(85f, _npc.CurrentHealth);

            _npc.TickEffects(); // 2º tick: sofre 15 de dano -> 70, e expira
            Assert.AreEqual(70f, _npc.CurrentHealth);

            _npc.TickEffects(); // Sem efeitos ativos, vida permanece 70
            Assert.AreEqual(70f, _npc.CurrentHealth);
        }
    }
}
