using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;

namespace Aegir.Tests.Combat
{
    [TestFixture]
    public class CombatBaseTests
    {
        private GameObject _combatHost;
        private CrewAttacks _combat;
        private GameObject _allyHost, _enemyHost;
        private CrewData _allyCrew, _enemyCrew;
        private GameObject _actor, _enemyTarget;

        [SetUp]
        public void SetUp()
        {
            _combatHost = new GameObject("CombatHost");
            _combat = _combatHost.AddComponent<CrewAttacks>();

            _allyHost = new GameObject("Allies");
            _allyCrew = _allyHost.AddComponent<CrewData>();
            typeof(CrewData).GetField("_maxCrewLength", BindingFlags.NonPublic | BindingFlags.Instance)
                ?.SetValue(_allyCrew, 5);

            _enemyHost = new GameObject("Enemies");
            _enemyCrew = _enemyHost.AddComponent<CrewData>();
            typeof(CrewData).GetField("_maxCrewLength", BindingFlags.NonPublic | BindingFlags.Instance)
                ?.SetValue(_enemyCrew, 5);

            _actor = new GameObject("Actor");
            NPCsData actorData = _actor.AddComponent<NPCsData>();
            actorData.MaxHealth = 100f;
            actorData.Heal(100f);
            actorData.Strength = 2.0f; // Força 2x
            _allyCrew.AddToCrew(_actor);

            _enemyTarget = new GameObject("EnemyTarget");
            NPCsData enemyData = _enemyTarget.AddComponent<NPCsData>();
            enemyData.MaxHealth = 100f;
            enemyData.Heal(100f);
            _enemyCrew.AddToCrew(_enemyTarget);
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(_actor);
            Object.DestroyImmediate(_enemyTarget);
            Object.DestroyImmediate(_allyHost);
            Object.DestroyImmediate(_enemyHost);
            Object.DestroyImmediate(_combatHost);
        }

        [Test]
        public void DoAction_DamageEffect_AppliesScaledByStrengthToEnemies()
        {
            CombatBase.EffectData damageEffect = new CombatBase.EffectData
            {
                effectType = CombatBase.EffectType.Damage,
                intensity = 15f,
                damageType = NPCsData.DamageType.Physical,
                maxTargets = 1,
                targetTeams = new List<CombatBase.TargetTeam> { CombatBase.TargetTeam.Enemy }
            };

            CombatBase.ActionData attackAction = new CombatBase.ActionData
            {
                actionName = "Heavy Slash",
                targetTeams = new List<CombatBase.TargetTeam> { CombatBase.TargetTeam.Enemy },
                effects = new List<CombatBase.EffectData> { damageEffect }
            };

            List<GameObject> targets = new List<GameObject> { _enemyTarget };

            // Intensidade 15 * Força 2.0 = 30 de dano
            _combat.DoAction(attackAction, targets, _allyCrew, _enemyCrew, _actor);

            NPCsData enemyNpc = _enemyTarget.GetComponent<NPCsData>();
            Assert.AreEqual(70f, enemyNpc.CurrentHealth);
        }

        [Test]
        public void DoAction_HealEffect_AppliesToAllies()
        {
            NPCsData actorData = _actor.GetComponent<NPCsData>();
            actorData.MaxHealth = 100f;
            actorData.Heal(100f);
            actorData.TakeDamage(50f, NPCsData.DamageType.Physical); // Vida = 50

            CombatBase.EffectData healEffect = new CombatBase.EffectData
            {
                effectType = CombatBase.EffectType.Heal,
                intensity = 10f,
                maxTargets = 1,
                targetTeams = new List<CombatBase.TargetTeam> { CombatBase.TargetTeam.Ally }
            };

            CombatBase.ActionData healAction = new CombatBase.ActionData
            {
                actionName = "Bandage",
                targetTeams = new List<CombatBase.TargetTeam> { CombatBase.TargetTeam.Ally },
                effects = new List<CombatBase.EffectData> { healEffect }
            };

            List<GameObject> targets = new List<GameObject> { _actor };

            // Intensidade 10 * Força 2.0 = 20 de cura -> 50 + 20 = 70
            _combat.DoAction(healAction, targets, _allyCrew, _enemyCrew, _actor);

            Assert.AreEqual(70f, actorData.CurrentHealth);
        }

        [Test]
        public void DoAction_StrengthBuffEffect_AppliesActiveEffectToAlly()
        {
            NPCsData actorData = _actor.GetComponent<NPCsData>();
            float initialStrength = actorData.Strength;

            CombatBase.EffectData buffEffect = new CombatBase.EffectData
            {
                effectType = CombatBase.EffectType.Strength,
                intensity = 5f,
                durationTurns = 2,
                maxTargets = 1,
                targetTeams = new List<CombatBase.TargetTeam> { CombatBase.TargetTeam.Ally }
            };

            CombatBase.ActionData buffAction = new CombatBase.ActionData
            {
                actionName = "War Cry",
                targetTeams = new List<CombatBase.TargetTeam> { CombatBase.TargetTeam.Ally },
                effects = new List<CombatBase.EffectData> { buffEffect }
            };

            _combat.DoAction(buffAction, new List<GameObject> { _actor }, _allyCrew, _enemyCrew, _actor);

            // A força deve ter recebido o buff imediato (+5)
            Assert.AreEqual(initialStrength + 5f, actorData.Strength);
            Assert.AreEqual(1, actorData.activeEffects.Count);
            Assert.AreEqual(2, actorData.activeEffects[0].remainingTurns);
        }
    }
}
