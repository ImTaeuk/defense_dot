using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using DefenseDot.Core.Pooling;

namespace DefenseDot.Tests.EditMode
{
    /// <summary>
    /// PrefabFactory 단위 테스트.
    /// 프리팹(GameObject)에서 PooledBehaviour 컴포넌트를 인스턴스화해 반환하는지 검증한다.
    /// (Addressables 없이 런타임 GameObject 로 검증 — PoolAsync/Get(AssetReference) 는 컴파일 검증)
    /// </summary>
    public class PrefabFactoryTests
    {
        private readonly List<GameObject> spawned = new List<GameObject>();

        [TearDown]
        public void Cleanup()
        {
            foreach (GameObject go in spawned)
                if (go != null) Object.DestroyImmediate(go);
            spawned.Clear();
        }

        private sealed class Fx : PooledBehaviour { }

        [Test]
        public void Create_InstantiatesPrefab_AndReturnsPooledBehaviour()
        {
            var prefab = new GameObject("fxPrefab");
            spawned.Add(prefab);
            prefab.AddComponent<Fx>();

            var factory = new PrefabFactory(prefab);
            PooledBehaviour created = factory.Create();
            spawned.Add(created.gameObject);

            Assert.IsNotNull(created, "PooledBehaviour 컴포넌트를 반환");
            Assert.IsInstanceOf<Fx>(created, "프리팹의 컴포넌트 타입 유지");
            Assert.AreNotSame(prefab, created.gameObject, "원본이 아니라 인스턴스를 반환");
        }

        [Test]
        public void Create_TwoCalls_ProduceDistinctInstances()
        {
            var prefab = new GameObject("fxPrefab");
            spawned.Add(prefab);
            prefab.AddComponent<Fx>();
            var factory = new PrefabFactory(prefab);

            PooledBehaviour a = factory.Create();
            spawned.Add(a.gameObject);
            PooledBehaviour b = factory.Create();
            spawned.Add(b.gameObject);

            Assert.AreNotSame(a, b, "호출마다 새 인스턴스");
        }
    }
}
