using NUnit.Framework;
using UnityEngine;
using Aegir.Entities.V2.Player;
using Aegir.Entities.V2.NPCs;

namespace Aegir.Tests.Entities.V2
{
    [TestFixture]
    public class MovementV2Tests
    {
        [SetUp]
        public void Setup()
        {
            GameState.IsGameStarted = true;
            GameState.IsInBattle = false;
            GameState.ChasersCount = 0;
            GameState.IsOnWater = true;
        }

        [TearDown]
        public void Teardown()
        {
            GameState.IsGameStarted = false;
            GameState.IsInBattle = false;
            GameState.ChasersCount = 0;
            GameState.IsOnWater = false;
        }

        [Test]
        public void PlayerMovementV2_AnimatorHashes_AreConsistent()
        {
            Assert.AreEqual(Animator.StringToHash("Horizontal"), PlayerMovementV2.HashHorizontal);
            Assert.AreEqual(Animator.StringToHash("Vertical"), PlayerMovementV2.HashVertical);
            Assert.AreEqual(Animator.StringToHash("MoveSpeed"), PlayerMovementV2.HashMoveSpeed);
        }

        [Test]
        public void NpcMovementV2_AnimatorHashes_AreConsistent()
        {
            Assert.AreEqual(Animator.StringToHash("Horizontal"), NpcMovementV2.HashHorizontal);
            Assert.AreEqual(Animator.StringToHash("Vertical"), NpcMovementV2.HashVertical);
            Assert.AreEqual(Animator.StringToHash("MoveSpeed"), NpcMovementV2.HashMoveSpeed);
        }

        [Test]
        public void NpcBrainV2_LifecycleAndStateTransitions_WorkCorrectly()
        {
            var hostObj = new GameObject("TestNPC_Host");
            var npcMovement = hostObj.AddComponent<NpcMovementV2>();

            var brain = new NpcBrainV2(npcMovement, hostObj.transform, null);

            // 1. Estado inicial é Wandering
            Assert.AreEqual(NpcAiStateV2.Wandering, brain.CurrentState);
            Assert.IsNull(brain.CurrentPrey);
            Assert.AreNotEqual(Vector2.zero, brain.CurrentDirection);

            // 2. Alvo detectado -> Entra em Alert
            var playerObj = new GameObject("TestPlayer");
            playerObj.tag = "Player";

            brain.OnTargetDetected(playerObj);
            brain.UpdateBrain(0.1f);
            Assert.AreEqual(NpcAiStateV2.Alert, brain.CurrentState);

            // 3. Após transcorrer o tempo de alerta -> Trava presa e inicia perseguição
            brain.UpdateBrain(npcMovement.TimeUntilAggressive + 0.1f);
            Assert.AreEqual(NpcAiStateV2.Chasing, brain.CurrentState);
            Assert.AreEqual(playerObj, brain.CurrentPrey);

            // 4. Se atinge borda territorial -> Interrompe agro e assume direção de fuga
            Vector2 escapeDir = Vector2.right;
            brain.NotifyBoundaryHit(escapeDir);
            Assert.AreEqual(NpcAiStateV2.Wandering, brain.CurrentState);
            Assert.IsNull(brain.CurrentPrey);
            Assert.AreEqual(escapeDir, brain.CurrentDirection);

            // 5. Embarque em navio -> Transição para BoardingShip
            var shipObj = new GameObject("TestShip");
            brain.StartBoarding(shipObj.transform);
            brain.UpdateBrain(0.1f);
            Assert.AreEqual(NpcAiStateV2.BoardingShip, brain.CurrentState);

            // Limpeza
            Object.DestroyImmediate(playerObj);
            Object.DestroyImmediate(shipObj);
            Object.DestroyImmediate(hostObj);
        }
    }
}
