using NUnit.Framework;
using DefenseDot.Domain.Models;

namespace DefenseDot.Tests.EditMode
{
    public class EconomyModelTests
    {
        [Test]
        public void Initialize_SetsGold()
        {
            var m = new EconomyModel();
            m.Initialize(100);
            Assert.AreEqual(100, m.Gold.Value);
        }

        [Test]
        public void Initialize_ForceNotifies_EvenSameValue()
        {
            var m = new EconomyModel();
            m.Initialize(50);
            int notified = -1;
            m.Gold.Subscribe(v => notified = v);   // 즉시 50
            m.Initialize(50);                       // 동일값이어도 통지
            Assert.AreEqual(50, notified);
        }

        [Test]
        public void AddGold_Increases()
        {
            var m = new EconomyModel();
            m.Initialize(10);
            m.AddGold(15);
            Assert.AreEqual(25, m.Gold.Value);
        }

        [Test]
        public void TrySpend_InsufficientReturnsFalse()
        {
            var m = new EconomyModel();
            m.Initialize(10);
            Assert.IsFalse(m.TrySpend(20));
            Assert.AreEqual(10, m.Gold.Value);
        }

        [Test]
        public void TrySpend_SufficientDeducts()
        {
            var m = new EconomyModel();
            m.Initialize(30);
            Assert.IsTrue(m.TrySpend(20));
            Assert.AreEqual(10, m.Gold.Value);
        }
    }
}
