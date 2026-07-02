using System.Collections.Generic;
using NUnit.Framework;
using DefenseDot.Core.Pooling;
using DefenseDot.Systems.Assets;

namespace DefenseDot.Tests.EditMode
{
    /// <summary>
    /// PoolManager 단위 테스트(POCO 경로).
    /// 재사용·자기 Dispose 반환·소유 연쇄·뿌리 절단(누수 0)·이중반환 가드를 방어한다.
    /// </summary>
    public class PoolManagerTests
    {
        private sealed class Node : PooledObject
        {
            public int DespawnCount;
            public override void OnDespawn() => DespawnCount++;
        }

        private static PoolManager NewManager() => new PoolManager(new AssetLoader());

        [Test]
        public void Get_ThenReturn_ThenGet_ReusesInstance()
        {
            PoolManager m = NewManager();

            Node a = m.Get<Node>();
            m.Return(a);
            Node b = m.Get<Node>();

            Assert.AreSame(a, b, "반환 후 재요청은 같은 인스턴스");
        }

        [Test]
        public void Dispose_OnPooledObject_ReturnsItToPool()
        {
            PoolManager m = NewManager();

            Node a = m.Get<Node>();
            a.Dispose();               // 자기 반환 API
            Node b = m.Get<Node>();

            Assert.AreSame(a, b, "Dispose 가 곧 풀 반환");
            Assert.AreEqual(1, a.DespawnCount, "반환 시 OnDespawn 1회");
        }

        [Test]
        public void Return_Parent_CascadesToChildrenFirst()
        {
            PoolManager m = NewManager();

            Node parent = m.Get<Node>();
            Node child = m.Get<Node>(owner: parent);

            m.Return(parent);

            Assert.AreEqual(1, child.DespawnCount, "부모 반환 시 자식이 먼저 회수");
            Assert.AreSame(child, m.Get<Node>(), "회수된 자식이 재사용됨");
        }

        [Test]
        public void Dispose_Manager_ReclaimsAllLiveObjects()
        {
            PoolManager m = NewManager();

            Node a = m.Get<Node>();
            Node b = m.Get<Node>();
            Node c = m.Get<Node>(owner: a);

            m.Dispose();               // 뿌리 절단

            Assert.AreEqual(1, a.DespawnCount, "a 회수");
            Assert.AreEqual(1, b.DespawnCount, "b 회수");
            Assert.AreEqual(1, c.DespawnCount, "소유 자식 c 회수 — 누수 0");
        }

        [Test]
        public void Return_Twice_DoesNotDoubleDespawn()
        {
            PoolManager m = NewManager();

            Node a = m.Get<Node>();
            m.Return(a);
            m.Return(a);               // 이중 반환

            Assert.AreEqual(1, a.DespawnCount, "이중 반환은 무시");
        }
    }
}
