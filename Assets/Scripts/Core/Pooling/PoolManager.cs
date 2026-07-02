using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.AddressableAssets;
using DefenseDot.Systems.Assets;

namespace DefenseDot.Core.Pooling
{
    /// <summary>
    /// 프리팹 풀들을 총괄하는 조율자입니다. 레벨 진입 시 예열하고,
    /// 대여 장부·소유 연쇄·뿌리 절단으로 누수 없이 정리합니다. GameContext 가 보유합니다.
    /// 타이밍은 도메인이(구체 타입이 끝나는 지점에서 Dispose), 메커니즘·안전망은 이 조율자가 맡습니다.
    /// </summary>
    public sealed class PoolManager : System.IDisposable
    {
        private readonly AssetLoader assetLoader;
        private readonly Dictionary<object, Pool> pools = new Dictionary<object, Pool>();      // RuntimeKey → 풀
        private readonly Dictionary<IPoolableObject, Pool> origin = new Dictionary<IPoolableObject, Pool>();  // 대여 객체 → 소속 풀
        private readonly HashSet<IPoolableObject> live = new HashSet<IPoolableObject>();       // 대여 중(이중반납 가드)
        private readonly Dictionary<IPoolableObject, List<IPoolableObject>> owned              // 부모 → 자식들
            = new Dictionary<IPoolableObject, List<IPoolableObject>>();
        private readonly Dictionary<IPoolableObject, IPoolableObject> parentOf                 // 자식 → 현재 부모
            = new Dictionary<IPoolableObject, IPoolableObject>();

        public PoolManager(AssetLoader assetLoader)
        {
            this.assetLoader = assetLoader;
        }

        /// <summary>
        /// 이펙트 프리팹들을 로드하고 각 풀에 count 개를 미리 채웁니다(레벨 시작 시 1회).
        /// 예열 개수는 데이터가 아니라 호출자가 결정합니다(기본 3).
        /// </summary>
        public async UniTask WarmupAsync(IEnumerable<EffectEntry> entries, int count = 3)
        {
            foreach (EffectEntry entry in entries)
            {
                object key = entry.asset.RuntimeKey;
                if (pools.ContainsKey(key)) continue;
                GameObject prefab = await assetLoader.LoadAsync<GameObject>(entry.asset);
                var pool = new Pool(prefab);
                pools[key] = pool;
                Prewarm(pool, count);
            }
        }

        /// <summary> 미리 count 개 만들어 큐를 채웁니다. </summary>
        private void Prewarm(Pool pool, int count)
        {
            using (UnityEngine.Pool.ListPool<GameObject>.Get(out List<GameObject> temp))
            {
                for (int i = 0; i < count; i++) temp.Add(pool.Get());
                foreach (GameObject instance in temp) pool.Return(instance);
            }
        }

        /// <summary> 예열된 풀에서 동기로 꺼냅니다. owner 를 주면 소유 자식으로 등록합니다(부모 반납 시 함께 회수). </summary>
        public T Get<T>(AssetReference reference, object owner = null) where T : Component, IPoolableObject
        {
            Pool pool = pools[reference.RuntimeKey];
            GameObject instance = pool.Get();
            T item = instance.GetComponent<T>();
            Retain(item, pool, owner);
            return item;
        }

        /// <summary> 방금 꺼낸 객체를 대여 장부에 등록합니다(반납 배선 주입 + 소유 등록). 테스트 seam 겸용. </summary>
        internal void Retain(IPoolableObject item, Pool pool, object owner)
        {
            live.Add(item);
            origin[item] = pool;
            if (item is IReturnBindable bindable)
                bindable.BindReturn(() => Return(item));
            if (owner is IPoolableObject parent)
            {
                parentOf[item] = parent;
                if (!owned.TryGetValue(parent, out List<IPoolableObject> children))
                {
                    children = new List<IPoolableObject>();
                    owned[parent] = children;
                }
                children.Add(item);
            }
        }

        /// <summary> 풀로 반납합니다. 소유 자식부터 연쇄 회수하고 이중 반납을 가드합니다. </summary>
        public void Return(object obj)
        {
            if (obj is not IPoolableObject pooled) return;
            if (!live.Contains(pooled)) return;       // 이미 반납됨

            live.Remove(pooled);

            // 내가 자식이면 부모 목록에서 제거
            if (parentOf.TryGetValue(pooled, out IPoolableObject parent))
            {
                parentOf.Remove(pooled);
                if (owned.TryGetValue(parent, out List<IPoolableObject> siblings))
                    siblings.Remove(pooled);
            }

            // 자식부터 연쇄 회수(목록 분리)
            if (owned.TryGetValue(pooled, out List<IPoolableObject> children))
            {
                owned.Remove(pooled);
                for (int i = children.Count - 1; i >= 0; i--) Return(children[i]);
            }

            // 실제 풀로 되돌림
            if (origin.TryGetValue(pooled, out Pool pool))
            {
                origin.Remove(pooled);
                pool.Return(((Component)pooled).gameObject);
            }
        }

        /// <summary> 남은 대여 객체를 전량 회수하고 풀·에셋을 정리합니다(뿌리 절단 = 누수 안전망). </summary>
        public void Dispose()
        {
            using (UnityEngine.Pool.ListPool<IPoolableObject>.Get(out List<IPoolableObject> snapshot))
            {
                snapshot.AddRange(live);
                foreach (IPoolableObject item in snapshot)
                    if (live.Contains(item)) Return(item);
            }

            foreach (Pool pool in pools.Values) pool.Clear();
            pools.Clear();
            origin.Clear();
            live.Clear();
            owned.Clear();
            parentOf.Clear();
            assetLoader?.ReleaseAll();
        }
    }
}
