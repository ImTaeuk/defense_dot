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
        private sealed class Fx : PooledBehaviour { }

        [Test]
        public void Create_InstantiatesPrefab_AndReturnsPooledBehaviour()
        {
            var prefab = new GameObject("fxPrefab");
            prefab.AddComponent<Fx>();

            var factory = new PrefabFactory(prefab);
            PooledBehaviour created = factory.Create();

            Assert.IsNotNull(created, "PooledBehaviour 컴포넌트를 반환");
            Assert.IsInstanceOf<Fx>(created, "프리팹의 컴포넌트 타입 유지");
            Assert.AreNotSame(prefab, created.gameObject, "원본이 아니라 인스턴스를 반환");

            Object.DestroyImmediate(created.gameObject);
            Object.DestroyImmediate(prefab);
        }

        [Test]
        public void Create_TwoCalls_ProduceDistinctInstances()
        {
            var prefab = new GameObject("fxPrefab");
            prefab.AddComponent<Fx>();
            var factory = new PrefabFactory(prefab);

            PooledBehaviour a = factory.Create();
            PooledBehaviour b = factory.Create();

            Assert.AreNotSame(a, b, "호출마다 새 인스턴스");

            Object.DestroyImmediate(a.gameObject);
            Object.DestroyImmediate(b.gameObject);
            Object.DestroyImmediate(prefab);
        }
    }
}
