using NUnit.Framework;
using DefenseDot.Domain.Models;

namespace DefenseDot.Tests.EditMode
{
    public class LevelModelTests
    {
        // 곡선: level*2. level1→2칸, level2→4칸 ...
        private static LevelModel New() => new LevelModel(lv => lv * 2);

        [Test]
        public void RegisterKill_LevelsUpAtThreshold()
        {
            var m = New();                 // KillsToNextLevel=2 (level1)
            int fired = 0; m.OnLevelUp += () => fired++;
            m.RegisterKill();              // kills 1
            Assert.AreEqual(1, m.Level);
            m.RegisterKill();              // kills 2 → 레벨업
            Assert.AreEqual(2, m.Level);
            Assert.AreEqual(0, m.Kills);
            Assert.AreEqual(1, fired);
            Assert.AreEqual(1, m.PendingLevelUps);
        }

        [Test]
        public void RegisterKill_HandlesMultiLevelInOneKill()
        {
            var m = new LevelModel(lv => 1); // 매 처치마다 레벨업
            m.RegisterKill();
            Assert.AreEqual(2, m.Level);
            Assert.AreEqual(1, m.PendingLevelUps);
        }

        [Test]
        public void TryConsumePending_DecrementsAndReportsEmpty()
        {
            var m = new LevelModel(lv => 1);
            m.RegisterKill();              // pending 1
            Assert.IsTrue(m.TryConsumePending());
            Assert.IsFalse(m.TryConsumePending());
        }
    }
}
