using NUnit.Framework;
using DefenseDot.Domain.Models;

namespace DefenseDot.Tests.EditMode
{
    public class CoreModelTests
    {
        [Test]
        public void Configure_FillsToMax()
        {
            var m = new CoreModel();
            m.Configure(50f);
            Assert.AreEqual(50f, m.CurrentHp, 0.0001f);
            Assert.AreEqual(50f, m.MaxHp, 0.0001f);
            Assert.AreEqual(1f, m.HealthRatio, 0.0001f);
        }

        [Test]
        public void ApplyDamage_ReducesHpAndRatio()
        {
            var m = new CoreModel();
            m.Configure(40f);
            m.ApplyDamage(10f);
            Assert.AreEqual(30f, m.CurrentHp, 0.0001f);
            Assert.AreEqual(0.75f, m.HealthRatio, 0.0001f);
        }

        [Test]
        public void Health_NotifiesState()
        {
            var m = new CoreModel();
            m.Configure(40f);
            HealthState got = default;
            m.Health.Subscribe(s => got = s);   // 즉시 (40,40)
            m.ApplyDamage(20f);
            Assert.AreEqual(20f, got.Hp, 0.0001f);
            Assert.AreEqual(0.5f, got.Ratio, 0.0001f);
        }

        [Test]
        public void ApplyDamage_ToZero_RaisesDestroyed()
        {
            var m = new CoreModel();
            m.Configure(10f);
            bool destroyed = false;
            m.OnCoreDestroyed += () => destroyed = true;
            m.ApplyDamage(999f);
            Assert.AreEqual(0f, m.CurrentHp, 0.0001f);
            Assert.IsTrue(destroyed);
        }
    }
}
