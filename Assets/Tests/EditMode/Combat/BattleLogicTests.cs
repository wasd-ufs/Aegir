using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

namespace Aegir.Tests.Combat
{
    [TestFixture]
    public class BattleLogicTests
    {
        private GameObject _npcObject;
        private NPCsData _npc;

        [SetUp]
        public void SetUp()
        {
            _npcObject = new GameObject("BattleTestActor");
            _npc = _npcObject.AddComponent<NPCsData>();
            _npc.MaxHealth = 100f;
            _npc.Heal(100f);
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(_npcObject);
        }

        [Test]
        public void NPCsData_ActionEconomy_ResetAndConsumeWorkCorrectly()
        {
            _npc.ResetActions();
            Assert.IsTrue(_npc.CanAct(), "NPC vivo e com ações resetadas deve poder agir.");

            _npc.ConsumeAction();
            Assert.IsFalse(_npc.CanAct(), "Após consumir ação, NPC não deve poder agir no mesmo turno.");

            _npc.ResetActions();
            Assert.IsTrue(_npc.CanAct(), "Após ResetActions, NPC deve poder agir novamente.");
        }

        [Test]
        public void NPCsData_DeadNPC_CannotActEvenWithReset()
        {
            _npc.TakeDamage(200f, NPCsData.DamageType.Physical); // Morte

            Assert.IsFalse(_npc.isAlive);

            _npc.ResetActions();
            Assert.IsFalse(_npc.CanAct(), "NPC morto jamais deve poder agir.");
        }

        [Test]
        public void CombatBase_TargetTeam_AllyAndEnemyEnumsAreDistinct()
        {
            Assert.AreNotEqual(CombatBase.TargetTeam.Ally, CombatBase.TargetTeam.Enemy);
        }

        [Test]
        public void BattleManager_ChooseAction_WeightedSelection_PicksOnlyValidWeightAction()
        {
            GameObject battleManagerHost = new GameObject("BattleManagerHost");
            BattleManager battleManager = battleManagerHost.AddComponent<BattleManager>();

            GameObject enemyAttacksHost = new GameObject("EnemyAttacksHost");
            CrewAttacks enemyAttacks = enemyAttacksHost.AddComponent<CrewAttacks>();

            CombatBase.ActionData zeroWeightAction = new CombatBase.ActionData
            {
                actionName = "ZeroChance",
                weight = 0f
            };

            CombatBase.ActionData guaranteedAction = new CombatBase.ActionData
            {
                actionName = "GuaranteedStrike",
                weight = 100f
            };

            List<CombatBase.ActionData> actionsList = new List<CombatBase.ActionData>
            {
                zeroWeightAction,
                guaranteedAction
            };

            typeof(CombatBase).GetField("_actions", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)
                ?.SetValue(enemyAttacks, actionsList);

            typeof(BattleManager).GetField("_enemyAttacks", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)
                ?.SetValue(battleManager, enemyAttacks);

            // Executar múltiplas vezes para garantir determinismo estatístico da roleta
            for (int i = 0; i < 5; i++)
            {
                CombatBase.ActionData chosen = battleManager.ChooseAction();
                Assert.AreEqual("GuaranteedStrike", chosen.actionName, "A ação de peso zero nunca deve ser sorteada.");
            }

            Object.DestroyImmediate(enemyAttacksHost);
            Object.DestroyImmediate(battleManagerHost);
        }
    }
}
