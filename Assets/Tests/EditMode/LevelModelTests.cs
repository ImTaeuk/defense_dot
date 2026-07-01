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

        [Test]
        public void Progress_AfterKillsBelowThreshold_ReflectsKillsAndRatio()
        {
            var m = new LevelModel(lv => 4);   // 필요 4
            m.RegisterKill();
            m.RegisterKill();
            LevelProgress p = m.Progress.Value;
            Assert.AreEqual(1, p.Level);
            Assert.AreEqual(2, p.Kills);
            Assert.AreEqual(4, p.KillsToNext);
            Assert.AreEqual(2, p.Remaining);
            Assert.AreEqual(0.5f, p.Ratio, 1e-4f);
        }

        [Test]
        public void Progress_OnLevelUp_ResetsKillsAndAdvancesLevel()
        {
            var m = new LevelModel(lv => 3);   // 필요 3
            m.RegisterKill();
            m.RegisterKill();
            m.RegisterKill();                  // 레벨업
            LevelProgress p = m.Progress.Value;
            Assert.AreEqual(2, p.Level);
            Assert.AreEqual(0, p.Kills);
            Assert.AreEqual(3, p.Remaining);
            Assert.AreEqual(0f, p.Ratio, 1e-4f);
        }

        [Test]
        public void Progress_NotifiesSubscriberOnEachKill()
        {
            var m = new LevelModel(lv => 10);  // 레벨업 없이
            int notified = 0;
            m.Progress.Subscribe(_ => notified++);   // 즉시 1회
            m.RegisterKill();
            m.RegisterKill();
            m.RegisterKill();
            Assert.AreEqual(4, notified);      // 초기 1 + 3회
        }
    }
}
