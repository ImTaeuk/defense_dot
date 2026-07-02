using NUnit.Framework;
using UnityEngine;
using DefenseDot.Core.Pooling;

namespace DefenseDot.Tests.EditMode
{
    /// <summary>
    /// 편의 베이스(PooledObject·PooledBehaviour) 테스트.
    /// 활성 플래그·이벤트·Dispose 반환 위임을 방어한다.
    /// </summary>
    public class PooledBaseTests
    {
        private sealed class Node : PooledObject { }

        private sealed class Behaviour : PooledBehaviour { }

        [Test]
        public void PooledObject_Activate_SetsFlagAndRaisesEvent()
        {
            var node = new Node();
            int activated = 0;
            int deactivated = 0;
            node.OnActivated += () => activated++;
            node.OnDeactivated += () => deactivated++;

            node.Activate();
            Assert.IsTrue(node.IsActive);
            node.Deactivate();
            Assert.IsFalse(node.IsActive);

            Assert.AreEqual(1, activated);
            Assert.AreEqual(1, deactivated);
        }

        [Test]
        public void PooledObject_Dispose_InvokesBoundReturn()
        {
            var node = new Node();
            int returned = 0;
            ((IReturnBindable)node).BindReturn(() => returned++);

            node.Dispose();

            Assert.AreEqual(1, returned, "Dispose 는 주입된 반환 동작을 호출");
        }

        [Test]
        public void PooledObject_Dispose_WithoutBinding_DoesNotThrow()
        {
            var node = new Node();
            Assert.DoesNotThrow(() => node.Dispose());
        }

        [Test]
        public void PooledBehaviour_ActivateDeactivate_TogglesGameObject()
        {
            var go = new GameObject("pooled");
            var behaviour = go.AddComponent<Behaviour>();

            behaviour.Deactivate();
            Assert.IsFalse(behaviour.IsActive, "Deactivate 는 gameObject 비활성");
            behaviour.Activate();
            Assert.IsTrue(behaviour.IsActive, "Activate 는 gameObject 활성");

            Object.DestroyImmediate(go);
        }

        [Test]
        public void PooledBehaviour_Dispose_InvokesBoundReturn()
        {
            var go = new GameObject("pooled");
            var behaviour = go.AddComponent<Behaviour>();
            int returned = 0;
            ((IReturnBindable)behaviour).BindReturn(() => returned++);

            behaviour.Dispose();

            Assert.AreEqual(1, returned);
            Object.DestroyImmediate(go);
        }
    }
}
