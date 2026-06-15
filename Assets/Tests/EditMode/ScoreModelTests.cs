using NUnit.Framework;
using DefenseDot.Domain.Models;

namespace DefenseDot.Tests.EditMode
{
    public class ScoreModelTests
    {
        [Test]
        public void AddKillScore_AddsTenTimesRound()
        {
            var model = new ScoreModel();
            model.AddKillScore(3);
            Assert.AreEqual(30, model.Score);
        }

        [Test]
        public void AddKillScore_Accumulates()
        {
            var model = new ScoreModel();
            model.AddKillScore(1);
            model.AddKillScore(2);
            Assert.AreEqual(30, model.Score);
        }

        [Test]
        public void AddTimeBonus_FloorsSavedTimesTenTimesRound()
        {
            var model = new ScoreModel();
            model.AddTimeBonus(2.5f, 4);   // floor(2.5 * 10 * 4) = 100
            Assert.AreEqual(100, model.Score);
        }

        [Test]
        public void AddTimeBonus_NonPositiveSaved_NoChange()
        {
            var model = new ScoreModel();
            model.AddTimeBonus(0f, 5);
            Assert.AreEqual(0, model.Score);
        }

        [Test]
        public void AddKillScore_AppliesMultiplier()
        {
            var model = new ScoreModel();
            model.AddKillScore(3, 2f);   // floor(10 * 3 * 2) = 60
            Assert.AreEqual(60, model.Score);
        }

        [Test]
        public void AddTimeBonus_AppliesMultiplier()
        {
            var model = new ScoreModel();
            model.AddTimeBonus(4f, 3, 0.5f);   // floor(4 * 10 * 3 * 0.5) = 60
            Assert.AreEqual(60, model.Score);
        }

        [Test]
        public void OnScoreChanged_FiresWithNewScore()
        {
            var model = new ScoreModel();
            int notified = -1;
            model.OnScoreChanged += s => notified = s;
            model.AddKillScore(2);
            Assert.AreEqual(20, notified);
        }

        [Test]
        public void Reset_ZeroesScore()
        {
            var model = new ScoreModel();
            model.AddKillScore(5);
            model.Reset();
            Assert.AreEqual(0, model.Score);
        }
    }
}
