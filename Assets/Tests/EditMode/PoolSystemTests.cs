using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using DefenseDot.Core.Pooling;
using DefenseDot.Systems.Assets;

namespace DefenseDot.Tests.EditMode
{
    /// <summary>
    /// PoolSystem 테스트 — 자가/외부 반납·소유 연쇄·뿌리 절단·이중반납 가드·재부모를 방어한다.
    /// Addressables 없이 런타임 프리팹 Pool + 내부 Retain seam 으로 검증한다.
    /// </summary>
    public class PoolSystemTests
    {
        private sealed class TestPooled : PooledBehaviour
        {
            public int DespawnCount;
            public override void OnDespawn() => DespawnCount++;
        }

        private readonly List<GameObject> spawned = new List<GameObject>();

        [TearDown]
        public void Cleanup()
        {
            foreach (GameObject go in spawned) if (go != null) Object.DestroyImmediate(go);
            spawned.Clear();
        }

        private static PoolSystem NewManager() => new PoolSystem(new AssetLoader());

        private Pool NewPool()
        {
            var prefab = new GameObject("prefab");
            prefab.AddComponent<TestPooled>();
            spawned.Add(prefab);
            return new Pool(prefab);
        }

        // 꺼내 장부에 등록
        private TestPooled Lend(PoolSystem m, Pool pool, object owner = null)
        {
            GameObject go = pool.Get();
            spawned.Add(go);
            TestPooled fx = go.GetComponent<TestPooled>();
            m.Retain(fx, pool, owner);
            return fx;
        }

        [Test]
        public void Dispose_OnObject_ReturnsToPool()
        {
            PoolSystem m = NewManager();
            Pool pool = NewPool();
            TestPooled a = Lend(m, pool);

            a.Dispose();                       // 자가 반납

            Assert.AreEqual(1, a.DespawnCount, "Dispose = 반납 → OnDespawn 1회");
        }

        [Test]
        public void Return_Twice_DoesNotDoubleDespawn()
        {
            PoolSystem m = NewManager();
            Pool pool = NewPool();
            TestPooled a = Lend(m, pool);

            m.Return(a);
            m.Return(a);

            Assert.AreEqual(1, a.DespawnCount, "이중 반납은 무시");
        }

        [Test]
        public void Return_Parent_CascadesToChildren()
        {
            PoolSystem m = NewManager();
            Pool pool = NewPool();
            TestPooled parent = Lend(m, pool);
            TestPooled child = Lend(m, pool, owner: parent);

            m.Return(parent);

            Assert.AreEqual(1, child.DespawnCount, "부모 반납 시 자식도 회수");
        }

        [Test]
        public void Dispose_Manager_ReclaimsAll()
        {
            PoolSystem m = NewManager();
            Pool pool = NewPool();
            TestPooled a = Lend(m, pool);
            TestPooled b = Lend(m, pool);
            TestPooled c = Lend(m, pool, owner: a);

            m.Dispose();                       // 뿌리 절단

            Assert.AreEqual(1, a.DespawnCount, "a 회수");
            Assert.AreEqual(1, b.DespawnCount, "b 회수");
            Assert.AreEqual(1, c.DespawnCount, "소유 자식 c 회수 — 누수 0");
        }

        [Test]
        public void Reparent_OldParentReturn_Ignores_NewParentReturn_Reclaims()
        {
            PoolSystem m = NewManager();
            Pool pool = NewPool();
            TestPooled parent1 = Lend(m, pool);
            TestPooled parent2 = Lend(m, pool);
            TestPooled child = Lend(m, pool, owner: parent1);

            m.Return(child);                      // 자식 독립 반납
            TestPooled reused = Lend(m, pool, owner: parent2);  // 같은 인스턴스를 새 부모로 재취득

            int before = reused.DespawnCount;
            m.Return(parent1);                    // 옛 부모 — 재사용 객체 미영향
            Assert.AreEqual(before, reused.DespawnCount, "옛 부모 반납은 재사용 객체 미회수");

            m.Return(parent2);                    // 새 부모 — 회수
            Assert.AreEqual(before + 1, reused.DespawnCount, "새 부모 반납이 재사용 객체 회수");
        }
    }
}
