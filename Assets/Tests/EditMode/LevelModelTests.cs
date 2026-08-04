using NUnit.Framework;
using UnityEngine;
using DefenseDot.Domain.Models;
using DefenseDot.Systems.Cards;

namespace DefenseDot.Tests.EditMode
{
    public class LevelModelTests
    {
        /// <summary> 곡선 계수를 지정한 카드 설정을 만듭니다. 실제 곡선은 max(3, base + level*perLevel). </summary>
        private static ArenaCardConfig MakeConfig(int curveBase, int curvePerLevel)
        {
            var config = ScriptableObject.CreateInstance<ArenaCardConfig>();
            config.curveBase = curveBase;
            config.curvePerLevel = curvePerLevel;
            return config;
        }

        // 곡선: max(3, level*3). level1→3칸, level2→6칸 ...
        private static LevelModel New()
        {
            return new LevelModel(MakeConfig(0, 3));
        }

        [Test]
        public void RegisterKill_LevelsUpAtThreshold()
        {
            var m = New();                 // KillsToNextLevel=3 (level1)
            int fired = 0; m.OnLevelUp += () => fired++;
            m.RegisterKill();              // kills 1
            m.RegisterKill();              // kills 2
            Assert.AreEqual(1, m.Level);
            m.RegisterKill();              // kills 3 → 레벨업
            Assert.AreEqual(2, m.Level);
            Assert.AreEqual(0, m.Kills);
            Assert.AreEqual(1, fired);
            Assert.AreEqual(1, m.PendingLevelUps);
        }

        [Test]
        public void RegisterKill_LevelsUpAsSoonAsThresholdMet()
        {
            var m = new LevelModel(MakeConfig(3, 0)); // 항상 3처치마다 레벨업
            m.RegisterKill();
            m.RegisterKill();
            m.RegisterKill();
            Assert.AreEqual(2, m.Level);
            Assert.AreEqual(1, m.PendingLevelUps);
        }

        [Test]
        public void TryConsumePending_DecrementsAndReportsEmpty()
        {
            var m = new LevelModel(MakeConfig(3, 0));
            m.RegisterKill();
            m.RegisterKill();
            m.RegisterKill();              // pending 1
            Assert.IsTrue(m.TryConsumePending());
            Assert.IsFalse(m.TryConsumePending());
        }

        [Test]
        public void Progress_AfterKillsBelowThreshold_ReflectsKillsAndRatio()
        {
            var m = new LevelModel(MakeConfig(4, 0));   // 필요 4
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
            var m = new LevelModel(MakeConfig(3, 0));   // 필요 3
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
            var m = new LevelModel(MakeConfig(10, 0));  // 레벨업 없이
            int notified = 0;
            m.Progress.Subscribe(_ => notified++);   // 즉시 1회
            m.RegisterKill();
            m.RegisterKill();
            m.RegisterKill();
            Assert.AreEqual(4, notified);      // 초기 1 + 3회
        }

        [Test]
        public void NoConfig_UsesDefaultCurve()
        {
            var m = new LevelModel(null);
            Assert.AreEqual(12, m.KillsToNextLevel, "기본 곡선 max(3, 8 + level*4) — level1 이면 12");
        }
    }
}