using NUnit.Framework;
using UnityEngine;

namespace Aegir.Tests.Camera
{
    [TestFixture]
    public class CameraFollowTests
    {
        private GameObject _cameraObj;
        private UnityEngine.Camera _camera;
        private CameraFollow _cameraFollow;
        private GameObject _playerObj;
        private PlayerMovement _playerMovement;
        private GameObject _captainObj;

        [SetUp]
        public void SetUp()
        {
            GameState.IsGameStarted = true;
            GameState.IsInBattle = false;
            GameState.IsOnWater = true;

            _cameraObj = new GameObject("MainCamera");
            _camera = _cameraObj.AddComponent<UnityEngine.Camera>();
            _camera.orthographic = true;
            _camera.orthographicSize = 5f;
            _cameraFollow = _cameraObj.AddComponent<CameraFollow>();

            _playerObj = new GameObject("Player");
            _playerObj.tag = "Player";
            _playerObj.AddComponent<Rigidbody2D>();
            _playerMovement = _playerObj.AddComponent<PlayerMovement>();

            _captainObj = new GameObject("Captain");
            _captainObj.AddComponent<Rigidbody2D>();
            _captainObj.transform.SetParent(_playerObj.transform);
            _captainObj.SetActive(false);

            _playerMovement.captain = _captainObj;
            _playerMovement.isOnWater = true;

            _cameraFollow.PlayerMovement = _playerMovement;
        }

        [TearDown]
        public void TearDown()
        {
            GameState.IsGameStarted = false;
            GameState.IsInBattle = false;
            GameState.IsOnWater = false;

            if (_cameraObj != null) Object.DestroyImmediate(_cameraObj);
            if (_playerObj != null) Object.DestroyImmediate(_playerObj);
        }

        [Test]
        public void CameraFollow_WhenOnWater_FocusesBoatAndBoatZoom()
        {
            _playerMovement.isOnWater = true;
            GameState.IsOnWater = true;
            _captainObj.SetActive(false);

            Transform target = _cameraFollow.GetActivePlayerTransform();

            Assert.AreEqual(_playerObj.transform, target);
            Assert.IsFalse(_cameraFollow.IsFocusingCaptain);
            Assert.AreEqual(5f, _cameraFollow.BoatOrthographicSize);
        }

        [Test]
        public void CameraFollow_WhenOnLand_FocusesCaptainAndCaptainZoom()
        {
            _playerMovement.isOnWater = false;
            GameState.IsOnWater = false;
            _captainObj.SetActive(true);

            Transform target = _cameraFollow.GetActivePlayerTransform();

            Assert.AreEqual(_captainObj.transform, target);
            Assert.IsTrue(_cameraFollow.IsFocusingCaptain);
            Assert.AreEqual(3.5f, _cameraFollow.CaptainOrthographicSize);
        }

        [Test]
        public void CameraFollow_CustomTarget_OverridesAutomaticSwitching()
        {
            var custom = new GameObject("CustomTarget");
            _cameraFollow.CustomTarget = custom.transform;

            Transform target = _cameraFollow.GetActivePlayerTransform();
            Assert.AreEqual(custom.transform, target);

            Object.DestroyImmediate(custom);
        }
    }
}
