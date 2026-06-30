using NUnit.Framework;
using DefenseDot.Domain.Models;

namespace DefenseDot.Tests.EditMode
{
    public class RoundTimerModelTests
    {
        [Test]
        public void StartWave_SetsRemainingToDuration()
        {
            var t = new RoundTimerModel();
            t.StartWave(30f);
            Assert.AreEqual(30f, t.Remaining, 0.0001f);
            Assert.AreEqual(30f, t.Duration, 0.0001f);
        }

        [Test]
        public void Tick_DecrementsRemaining()
        {
            var t = new RoundTimerModel();
            t.StartWave(10f);
            t.Tick(3f);
            Assert.AreEqual(7f, t.Remaining, 0.0001f);
        }

        [Test]
        public void Tick_ClampsAtZero_AndIsExpired()
        {
            var t = new RoundTimerModel();
            t.StartWave(2f);
            t.Tick(5f);
            Assert.AreEqual(0f, t.Remaining, 0.0001f);
            Assert.IsTrue(t.IsExpired);
        }

        [Test]
        public void Ratio_IsRemainingOverDuration()
        {
            var t = new RoundTimerModel();
            t.StartWave(8f);
            t.Tick(2f);
            Assert.AreEqual(0.75f, t.Ratio, 0.0001f);
        }

        [Test]
        public void Time_NotifiesOnTick()
        {
            var t = new RoundTimerModel();
            t.StartWave(10f);
            float gotRemaining = -1f, gotDuration = -1f;
            t.Time.Subscribe(s => { gotRemaining = s.Remaining; gotDuration = s.Duration; });
            t.Tick(4f);
            Assert.AreEqual(6f, gotRemaining, 0.0001f);
            Assert.AreEqual(10f, gotDuration, 0.0001f);
        }

        [Test]
        public void Reset_ZeroesTimer()
        {
            var t = new RoundTimerModel();
            t.StartWave(10f);
            t.Reset();
            Assert.AreEqual(0f, t.Remaining, 0.0001f);
            Assert.AreEqual(0f, t.Duration, 0.0001f);
        }
    }
}
