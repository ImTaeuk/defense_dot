using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using DefenseDot.Core.Pooling;

namespace DefenseDot.Tests.EditMode
{
    /// <summary> Pool(비제네릭·프리팹 기반) 테스트 — 재사용·즉석생성·순서·폐기 큐비움을 방어한다. </summary>
    public class PoolTests
    {
        private sealed class TestPooled : PooledBehaviour
        {
            public int SpawnCount;
            public int DespawnCount;
            public override void OnSpawn() => SpawnCount++;
            public override void OnDespawn() => DespawnCount++;
        }

        private readonly List<GameObject> spawned = new List<GameObject>();

        [TearDown]
        public void Cleanup()
        {
            foreach (GameObject go in spawned) if (go != null) Object.DestroyImmediate(go);
            spawned.Clear();
        }

        private GameObject NewPrefab()
        {
            var prefab = new GameObject("prefab");
            prefab.AddComponent<TestPooled>();
            spawned.Add(prefab);
            return prefab;
        }

        [Test]
        public void Get_ThenReturn_ThenGet_ReusesSameInstance()
        {
            var pool = new Pool(NewPrefab());
            GameObject a = pool.Get(); spawned.Add(a);
            pool.Return(a);
            GameObject b = pool.Get();
            Assert.AreSame(a, b, "반환 후 재요청은 같은 인스턴스");
        }

        [Test]
        public void Get_EmptyPool_InstantiatesEachTime()
        {
            var pool = new Pool(NewPrefab());
            GameObject a = pool.Get(); spawned.Add(a);
            GameObject b = pool.Get(); spawned.Add(b);
            Assert.AreNotSame(a, b, "빈 풀은 매번 새 인스턴스");
        }

        [Test]
        public void Get_ActivatesAndSpawns_ReturnDeactivatesAndDespawns()
        {
            var pool = new Pool(NewPrefab());
            GameObject go = pool.Get(); spawned.Add(go);
            TestPooled fx = go.GetComponent<TestPooled>();

            Assert.AreEqual(1, fx.SpawnCount, "Get 시 OnSpawn");
            Assert.IsTrue(fx.IsActive, "Get 시 활성");

            pool.Return(go);

            Assert.AreEqual(1, fx.DespawnCount, "Return 시 OnDespawn");
            Assert.IsFalse(fx.IsActive, "Return 시 비활성");
        }

        [Test]
        public void Clear_EmptiesQueue_NextGetInstantiatesNew()
        {
            var pool = new Pool(NewPrefab());
            GameObject a = pool.Get(); spawned.Add(a);
            pool.Return(a);
            pool.Clear();
            GameObject b = pool.Get(); spawned.Add(b);
            Assert.AreNotSame(a, b, "Clear 후엔 재사용 없이 새 인스턴스");
        }
    }
}
