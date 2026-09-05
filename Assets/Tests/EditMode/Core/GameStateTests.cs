using NUnit.Framework;

namespace Aegir.Tests.Core
{
    [TestFixture]
    public class GameStateTests
    {
        [SetUp]
        [TearDown]
        public void ResetState()
        {
            GameState.IsGameStarted = false;
            GameState.IsInBattle = false;
            GameState.ChasersCount = 0;
            GameState.IsOnWater = false;
        }

        [Test]
        public void IsBeingChased_ReflectsChasersCount()
        {
            GameState.ChasersCount = 0;
            Assert.IsFalse(GameState.IsBeingChased);

            GameState.ChasersCount = 1;
            Assert.IsTrue(GameState.IsBeingChased);

            GameState.ChasersCount = 5;
            Assert.IsTrue(GameState.IsBeingChased);

            GameState.ChasersCount = 0;
            Assert.IsFalse(GameState.IsBeingChased);
        }

        [Test]
        public void Flags_CanBeSetAndRetrievedAccurately()
        {
            GameState.IsGameStarted = true;
            GameState.IsInBattle = true;
            GameState.IsOnWater = true;

            Assert.IsTrue(GameState.IsGameStarted);
            Assert.IsTrue(GameState.IsInBattle);
            Assert.IsTrue(GameState.IsOnWater);
        }
    }
}
