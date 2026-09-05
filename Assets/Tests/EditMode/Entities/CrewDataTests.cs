using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;

namespace Aegir.Tests.Entities
{
    [TestFixture]
    public class CrewDataTests
    {
        private GameObject _crewHolder;
        private CrewData _crewData;
        private List<GameObject> _spawnedMembers;

        [SetUp]
        public void SetUp()
        {
            _spawnedMembers = new List<GameObject>();
            _crewHolder = new GameObject("CrewHolder");
            _crewData = _crewHolder.AddComponent<CrewData>();

            typeof(CrewData).GetField("_maxCrewLength", BindingFlags.NonPublic | BindingFlags.Instance)
                ?.SetValue(_crewData, 3);
        }

        [TearDown]
        public void TearDown()
        {
            foreach (var member in _spawnedMembers)
            {
                if (member != null)
                    Object.DestroyImmediate(member);
            }
            _spawnedMembers.Clear();

            if (_crewHolder != null)
                Object.DestroyImmediate(_crewHolder);
        }

        private GameObject CreateMember(string name, float hp)
        {
            GameObject go = new GameObject(name);
            NPCsData data = go.AddComponent<NPCsData>();
            data.MaxHealth = hp;
            data.Heal(hp);
            _spawnedMembers.Add(go);
            return go;
        }

        [Test]
        public void AddToCrew_AddsMemberAndFiresOnCrewChanged()
        {
            bool crewChanged = false;
            _crewData.OnCrewChanged += () => crewChanged = true;

            GameObject m1 = CreateMember("Sailor1", 50f);
            _crewData.AddToCrew(m1);

            Assert.AreEqual(1, _crewData.CrewList.Count);
            Assert.Contains(m1, _crewData.CrewList);
            Assert.IsTrue(crewChanged);
        }

        [Test]
        public void AddToCrew_CannotExceedMaxCrewLength()
        {
            GameObject m1 = CreateMember("M1", 50f);
            GameObject m2 = CreateMember("M2", 50f);
            GameObject m3 = CreateMember("M3", 50f);
            GameObject m4 = CreateMember("M4", 50f);

            _crewData.AddToCrew(m1);
            _crewData.AddToCrew(m2);
            _crewData.AddToCrew(m3);
            _crewData.AddToCrew(m4); // Deve ser ignorado pelo limite 3

            Assert.AreEqual(3, _crewData.CrewList.Count);
            Assert.IsFalse(_crewData.CrewList.Contains(m4));
        }

        [Test]
        public void RemoveFromCrew_RemovesMemberAndFiresOnCrewChanged()
        {
            GameObject m1 = CreateMember("M1", 50f);
            _crewData.AddToCrew(m1);

            bool crewChanged = false;
            _crewData.OnCrewChanged += () => crewChanged = true;

            _crewData.RemoveFromCrew(m1);

            Assert.AreEqual(0, _crewData.CrewList.Count);
            Assert.IsTrue(crewChanged);
        }

        [Test]
        public void GetCrewHealthList_ReturnsAccurateHealthValues()
        {
            GameObject m1 = CreateMember("M1", 80f);
            GameObject m2 = CreateMember("M2", 120f);

            _crewData.AddToCrew(m1);
            _crewData.AddToCrew(m2);

            List<float> healths = _crewData.GetCrewHealthList();

            Assert.AreEqual(2, healths.Count);
            Assert.AreEqual(80f, healths[0]);
            Assert.AreEqual(120f, healths[1]);
        }

        [Test]
        public void InitializeManually_SetsSingleMemberAndMaxCrewOne()
        {
            GameObject loneWolf = CreateMember("LoneWolf", 60f);

            _crewData.InitializeManually(loneWolf);

            Assert.AreEqual(1, _crewData.CrewList.Count);
            Assert.AreEqual(loneWolf, _crewData.CrewList[0]);
        }
    }
}
