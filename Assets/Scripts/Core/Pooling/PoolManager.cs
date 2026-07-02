using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using DefenseDot.Systems.Assets;
using UnityEngine.AddressableAssets;

namespace DefenseDot.Core.Pooling
{
    /// <summary>
    /// 풀들을 감싸 반환 동작 주입·OUT 장부·소유 연쇄·뿌리 절단을 담당합니다.
    /// 타이밍은 도메인이(구체 타입 이벤트→Dispose), 메커니즘·안전망은 이 매니저가 맡습니다.
    /// </summary>
    public sealed class PoolManager : System.IDisposable
    {
        private readonly AssetLoader assetLoader;
        // 풀 키(타입/RuntimeKey)
        private readonly Dictionary<object, IPool> pools = new Dictionary<object, IPool>();
        // 빌려나간 객체(OUT) → 소속 풀
        private readonly Dictionary<IPooledObject, IPool> origin = new Dictionary<IPooledObject, IPool>();
        // OUT 집합(이중 반환 가드)
        private readonly HashSet<IPooledObject> live = new HashSet<IPooledObject>();
        // 소유 부모 → 자식들
        private readonly Dictionary<IPooledObject, List<IPooledObject>> owned
            = new Dictionary<IPooledObject, List<IPooledObject>>();
        // 자식 → 현재 부모(독립 반환 정리)
        private readonly Dictionary<IPooledObject, IPooledObject> parentOf
            = new Dictionary<IPooledObject, IPooledObject>();

        public PoolManager(AssetLoader assetLoader)
        {
            this.assetLoader = assetLoader;
        }

        /// <summary> POCO 풀에서 꺼냅니다. 타입을 키로 쓰고 없으면 즉석 생성합니다. </summary>
        public T Get<T>(object owner = null) where T : class, IPoolable, IActivatable, IPooledObject, new()
        {
            Pool<T> pool = ResolvePocoPool<T>();
            T item = pool.Get();
            Track(item, pool, owner);
            return item;
        }

        /// <summary> 스포너 데이터의 이펙트 목록을 에셋별로 로드·예열합니다(레벨 진입 시 1회). </summary>
        public async UniTask PoolAsync(IEnumerable<EffectEntry> entries)
        {
            foreach (EffectEntry entry in entries)
            {
                object key = entry.asset.RuntimeKey;
                if (pools.ContainsKey(key)) continue;
                UnityEngine.GameObject prefab = await assetLoader.LoadAsync<UnityEngine.GameObject>(entry.asset);
                pools[key] = new Pool<PooledBehaviour>(new PrefabFactory(prefab));
            }
        }

        /// <summary> 예열된 프리팹 풀에서 꺼냅니다. 동기(이미 로드됨). </summary>
        public T Get<T>(AssetReference reference, object owner = null) where T : PooledBehaviour
        {
            object key = reference.RuntimeKey;
            var pool = (Pool<PooledBehaviour>)pools[key];
            PooledBehaviour item = pool.Get();
            Track(item, pool, owner);
            return (T)item;
        }

        private Pool<T> ResolvePocoPool<T>() where T : class, IPoolable, IActivatable, new()
        {
            object key = typeof(T);
            if (pools.TryGetValue(key, out IPool existing)) return (Pool<T>)existing;
            var pool = new Pool<T>(new PocoFactory<T>());
            pools[key] = pool;
            return pool;
        }

        // 반환 주입·장부·소유 등록
        internal void Track(IPooledObject item, IPool pool, object owner)
        {
            live.Add(item);
            origin[item] = pool;
            if (item is IReturnBindable bindable)
                bindable.BindReturn(() => Return(item));
            if (owner is IPooledObject parent)
            {
                parentOf[item] = parent;
                if (!owned.TryGetValue(parent, out List<IPooledObject> list))
                {
                    list = new List<IPooledObject>();
                    owned[parent] = list;
                }
                list.Add(item);
            }
        }

        /// <summary> 객체를 풀로 되돌립니다. 소유 자식이 있으면 먼저 연쇄 회수합니다. </summary>
        public void Return(object obj)
        {
            if (obj is not IPooledObject pooled) return;
            if (!live.Contains(pooled)) return;   // 이미 반환됨 — 이중 반환 가드

            live.Remove(pooled);

            // 내가 자식이면 부모 목록에서 제거
            if (parentOf.TryGetValue(pooled, out IPooledObject parent))
            {
                parentOf.Remove(pooled);
                if (owned.TryGetValue(parent, out List<IPooledObject> siblings))
                    siblings.Remove(pooled);
            }

            // 자식부터 연쇄 회수(목록 분리)
            if (owned.TryGetValue(pooled, out List<IPooledObject> children))
            {
                owned.Remove(pooled);
                for (int i = children.Count - 1; i >= 0; i--) Return(children[i]);
            }

            if (origin.TryGetValue(pooled, out IPool pool))
                pool.ReturnObject(pooled);
        }

        /// <summary> 남은 OUT 을 전량 연쇄 회수하고 풀·에셋을 정리합니다(뿌리 절단). </summary>
        public void Dispose()
        {
            using (UnityEngine.Pool.CollectionPool<List<IPooledObject>, IPooledObject>.Get(out List<IPooledObject> snapshot))
            {
                snapshot.AddRange(live);
                foreach (IPooledObject o in snapshot)
                    if (live.Contains(o)) Return(o);
            }

            foreach (IPool p in pools.Values) p.Clear();
            pools.Clear();
            origin.Clear();
            live.Clear();
            owned.Clear();
            parentOf.Clear();
            assetLoader?.ReleaseAll();
        }
    }
}
