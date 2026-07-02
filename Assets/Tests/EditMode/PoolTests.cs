using NUnit.Framework;
using DefenseDot.Core.Pooling;

namespace DefenseDot.Tests.EditMode
{
    /// <summary>
    /// Pool&lt;T&gt; 단위 테스트. 재사용·즉석생성·호출순서·이벤트를 방어한다.
    /// </summary>
    public class PoolTests
    {
        private sealed class Dummy : IPoolable, IActivatable
        {
            public int Spawns;
            public int Despawns;
            public int Activations;
            public int Deactivations;
            public System.Collections.Generic.List<string> Trace = new System.Collections.Generic.List<string>();

            public bool IsActive { get; private set; }
            public event System.Action OnActivated;
            public event System.Action OnDeactivated;

            public void OnSpawn() { Spawns++; Trace.Add("spawn"); }
            public void OnDespawn() { Despawns++; Trace.Add("despawn"); }
            public void Activate() { IsActive = true; Activations++; Trace.Add("activate"); OnActivated?.Invoke(); }
            public void Deactivate() { IsActive = false; Deactivations++; Trace.Add("deactivate"); OnDeactivated?.Invoke(); }
        }

        private sealed class DummyFactory : IPoolFactory<Dummy>
        {
            public int Created;
            public Dummy Create() { Created++; return new Dummy(); }
        }

        [Test]
        public void Get_ThenReturn_ThenGet_ReusesSameInstance()
        {
            var factory = new DummyFactory();
            var pool = new Pool<Dummy>(factory);

            Dummy first = pool.Get();
            pool.Return(first);
            Dummy second = pool.Get();

            Assert.AreSame(first, second, "반환 후 다시 꺼내면 같은 인스턴스");
            Assert.AreEqual(1, factory.Created, "재사용 시 신규 생성 0");
        }

        [Test]
        public void Get_EmptyPool_CreatesViaFactory()
        {
            var factory = new DummyFactory();
            var pool = new Pool<Dummy>(factory);

            pool.Get();
            pool.Get();

            Assert.AreEqual(2, factory.Created, "빈 풀에서 꺼낼 때마다 즉석 생성");
        }

        [Test]
        public void Get_RunsSpawnBeforeActivate_AndReturnRunsDeactivateBeforeDespawn()
        {
            var pool = new Pool<Dummy>(new DummyFactory());

            Dummy item = pool.Get();
            pool.Return(item);

            Assert.AreEqual(
                new[] { "spawn", "activate", "deactivate", "despawn" },
                item.Trace.ToArray(),
                "Get=리셋→켜기, Return=끄기→정리 순서");
            Assert.AreEqual(1, item.Activations);
            Assert.AreEqual(1, item.Deactivations);
        }
    }
}
