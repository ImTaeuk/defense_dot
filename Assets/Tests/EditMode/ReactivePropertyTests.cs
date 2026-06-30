using NUnit.Framework;
using DefenseDot.Domain;

namespace DefenseDot.Tests.EditMode
{
    public class ReactivePropertyTests
    {
        [Test]
        public void Subscribe_ImmediatelyNotifiesCurrentValue()
        {
            var rp = new ReactiveProperty<int>(7);
            int got = -1;
            rp.Subscribe(v => got = v);
            Assert.AreEqual(7, got);
        }

        [Test]
        public void Value_NotifiesOnChange()
        {
            var rp = new ReactiveProperty<int>(0);
            int count = 0, last = -1;
            rp.Subscribe(v => { count++; last = v; });   // 즉시 1회
            rp.Value = 5;
            Assert.AreEqual(2, count);
            Assert.AreEqual(5, last);
        }

        [Test]
        public void Value_SameValue_DoesNotNotify()
        {
            var rp = new ReactiveProperty<int>(3);
            int count = 0;
            rp.Subscribe(_ => count++);   // 즉시 1회
            rp.Value = 3;
            Assert.AreEqual(1, count);
        }

        [Test]
        public void SetValueAndForceNotify_NotifiesEvenIfEqual()
        {
            var rp = new ReactiveProperty<int>(3);
            int count = 0;
            rp.Subscribe(_ => count++);   // 즉시 1회
            rp.SetValueAndForceNotify(3);
            Assert.AreEqual(2, count);
        }

        [Test]
        public void Dispose_StopsNotifying()
        {
            var rp = new ReactiveProperty<int>(0);
            int count = 0;
            System.IDisposable token = rp.Subscribe(_ => count++);   // 즉시 1회
            token.Dispose();
            rp.Value = 9;
            Assert.AreEqual(1, count);
        }
    }
}
