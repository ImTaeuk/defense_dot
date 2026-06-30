using NUnit.Framework;
using DefenseDot.Domain.Models;

namespace DefenseDot.Tests.EditMode
{
    public class WaveModelTests
    {
        [Test]
        public void SetWave_UpdatesProgress()
        {
            var m = new WaveModel();
            m.SetWave(2, 5);
            Assert.AreEqual(2, m.Current);
            Assert.AreEqual(5, m.Total);
            Assert.AreEqual(2, m.Progress.Value.Current);
            Assert.AreEqual(5, m.Progress.Value.Total);
        }

        [Test]
        public void SetWave_NotifiesProgress()
        {
            var m = new WaveModel();
            WaveProgress got = default;
            m.Progress.Subscribe(p => got = p);   // 즉시 (0,0)
            m.SetWave(3, 7);
            Assert.AreEqual(3, got.Current);
            Assert.AreEqual(7, got.Total);
        }

        [Test]
        public void SetRemaining_NotifiesAndDedupes()
        {
            var m = new WaveModel();
            int count = 0, last = -1;
            m.RemainingEnemies.Subscribe(v => { count++; last = v; });   // 즉시 0
            m.SetRemaining(4);
            m.SetRemaining(4);   // 동일값 — 통지 생략
            Assert.AreEqual(2, count);
            Assert.AreEqual(4, last);
        }

        [Test]
        public void IsLastWave_TrueWhenCurrentReachesTotal()
        {
            var m = new WaveModel();
            m.SetWave(5, 5);
            Assert.IsTrue(m.IsLastWave);
        }

        [Test]
        public void MarkWaveCleared_RaisesEvent()
        {
            var m = new WaveModel();
            bool fired = false;
            m.OnWaveCleared += () => fired = true;
            m.MarkWaveCleared();
            Assert.IsTrue(fired);
        }
    }
}
