using NUnit.Framework;
using DefenseDot.Domain.Models;

namespace DefenseDot.Tests.EditMode
{
    public class ArenaModelTests
    {
        private ArenaModel MakeModel()
        {
            var m = new ArenaModel();
            m.Initialize(29f, 2.2f, 4f, 2f, 80);
            return m;
        }

        [Test]
        public void Initialize_ComputesSpawnRange()
        {
            var m = MakeModel();
            Assert.AreEqual(6.2f, m.SpawnMinRadius, 0.001f);  // 2.2 + 4
            Assert.AreEqual(27f, m.SpawnMaxRadius, 0.001f);   // 29 - 2
            Assert.AreEqual(80, m.MaxAlive);
        }

        [Test]
        public void Shrink_ReducesArenaAndSpawnMax()
        {
            var m = MakeModel();
            m.Shrink(9f);
            Assert.AreEqual(20f, m.ArenaRadius, 0.001f);
            Assert.AreEqual(18f, m.SpawnMaxRadius, 0.001f);   // 20 - 2
        }

        [Test]
        public void Expand_IncreasesArena()
        {
            var m = MakeModel();
            m.Expand(1f);
            Assert.AreEqual(30f, m.ArenaRadius, 0.001f);
        }

        [Test]
        public void Shrink_RaisesOnRadiusChanged()
        {
            var m = MakeModel();
            bool raised = false;
            m.OnRadiusChanged += () => raised = true;
            m.Shrink(1f);
            Assert.IsTrue(raised);
        }

        [Test]
        public void Shrink_ClampsAtMinimum()
        {
            var m = MakeModel();
            m.Shrink(1000f);
            float min = 2.2f + 4f + 2f; // coreRadius + inner + outer = 8.2
            Assert.AreEqual(min, m.ArenaRadius, 0.001f);
        }
    }
}
