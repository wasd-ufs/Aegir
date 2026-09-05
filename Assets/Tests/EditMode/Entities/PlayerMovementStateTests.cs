using NUnit.Framework;
using UnityEngine;

namespace Aegir.Tests.Entities
{
    [TestFixture]
    public class PlayerMovementStateTests
    {
        private GameObject _playerHost;
        private PlayerMovement _playerMovement;
        private GameObject _captainObject;

        private class MockState : PlayerMovement.IPlayerState
        {
            public bool EnterCalled { get; private set; }
            public bool ExitCalled { get; private set; }
            public bool UpdateCalled { get; private set; }
            public bool FixedUpdateCalled { get; private set; }

            public void Enter() => EnterCalled = true;
            public void Exit() => ExitCalled = true;
            public void Update() => UpdateCalled = true;
            public void FixedUpdate() => FixedUpdateCalled = true;
        }

        [SetUp]
        public void SetUp()
        {
            _playerHost = new GameObject("PlayerShip");
            _captainObject = new GameObject("Captain");

            // Configura componentes requeridos pelo PlayerMovement
            _playerHost.AddComponent<Rigidbody2D>();
            _playerHost.AddComponent<BoxCollider2D>();
            _playerHost.AddComponent<Animator>();

            _captainObject.AddComponent<Rigidbody2D>();
            _captainObject.AddComponent<Animator>();

            _playerMovement = _playerHost.AddComponent<PlayerMovement>();
            _playerMovement.captain = _captainObject;
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(_captainObject);
            Object.DestroyImmediate(_playerHost);
        }

        [Test]
        public void ChangeState_CallsExitOnOldAndEnterOnNew()
        {
            MockState state1 = new MockState();
            MockState state2 = new MockState();

            _playerMovement.ChangeState(state1);
            Assert.IsTrue(state1.EnterCalled);
            Assert.IsFalse(state1.ExitCalled);

            _playerMovement.ChangeState(state2);
            Assert.IsTrue(state1.ExitCalled);
            Assert.IsTrue(state2.EnterCalled);
        }

        [Test]
        public void CaptainProperty_AndCapitaoLegacyAlias_PointToSameObject()
        {
            Assert.AreSame(_captainObject, _playerMovement.captain);
            Assert.AreSame(_captainObject, _playerMovement.capitão);
        }
    }
}
