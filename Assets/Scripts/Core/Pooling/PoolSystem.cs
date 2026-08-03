using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.AddressableAssets;
using DefenseDot.Systems.Assets;

namespace DefenseDot.Core.Pooling
{
    /// <summary>
    /// 프리팹 풀들을 총괄하는 조율자입니다. 레벨 진입 시 예열하고,
    /// 대여 장부·소유 연쇄·뿌리 절단으로 누수 없이 정리합니다. PoolManager 가 보유합니다.
    /// 타이밍은 도메인이(구체 타입이 끝나는 지점에서 Dispose), 메커니즘·안전망은 이 조율자가 맡습니다.
    /// </summary>
    public sealed class PoolSystem : System.IDisposable
    {
        /// <summary> 씬을 넘어 사는 풀들이 모이는 칸 이름. 씬 이름과 겹치지 않는다. </summary>
        public const string GLOBAL_BUCKET = "Global";

        /// <summary> 네임스페이스가 없는 타입이 들어갈 칸. </summary>
        private const string UNCATEGORIZED = "Uncategorized";

        /// <summary> 칸 이름에서 떼어내는 공통 접두사. </summary>
        private const string NAMESPACE_PREFIX = "DefenseDot.";

        private readonly AssetLoader assetLoader;

        /// <summary> 모든 보관 칸의 뿌리. PoolManager 오브젝트의 Transform. </summary>
        private readonly Transform root;

        private readonly Dictionary<object, Pool> pools = new Dictionary<object, Pool>();      // RuntimeKey → 풀
        private readonly Dictionary<IPoolableObject, Pool> origin = new Dictionary<IPoolableObject, Pool>();  // 대여 객체 → 소속 풀
        private readonly HashSet<IPoolableObject> live = new HashSet<IPoolableObject>();       // 대여 중(이중반납 가드)
        private readonly Dictionary<IPoolableObject, List<IPoolableObject>> owned              // 부모 → 자식들
            = new Dictionary<IPoolableObject, List<IPoolableObject>>();
        private readonly Dictionary<IPoolableObject, IPoolableObject> parentOf                 // 자식 → 현재 부모
            = new Dictionary<IPoolableObject, IPoolableObject>();

        // ─── 배치·수명 관리 ───
        private readonly Dictionary<object, string> poolBucket = new Dictionary<object, string>();                // RuntimeKey → 칸 이름
        private readonly Dictionary<object, AssetReference> poolAsset = new Dictionary<object, AssetReference>();  // RuntimeKey → 참조(개별 해제용)
        private readonly Dictionary<string, Transform> buckets = new Dictionary<string, Transform>();              // 칸 이름 → Transform

        public PoolSystem(AssetLoader assetLoader, Transform root)
        {
            this.assetLoader = assetLoader;
            this.root = root;
        }

        /// <summary>
        /// AssetReference 목록을 로드해 각 풀에 count 개를 미리 채웁니다(레벨 시작 시 1회).
        /// 예열 개수는 데이터가 아니라 호출자가 결정합니다(기본 3).
        /// </summary>
        /// <param name="assets">예열할 프리팹 참조들</param>
        /// <param name="scope">씬과 함께 정리할지, 씬을 넘어 유지할지</param>
        /// <param name="count">풀마다 미리 만들어 둘 개수</param>
        public async UniTask WarmupAsync(IEnumerable<AssetReferenceGameObject> assets,
            PoolScope scope = PoolScope.Scene, int count = 3)
        {
            using (UnityEngine.Pool.HashSetPool<object>.Get(out HashSet<object> seen))
            using (UnityEngine.Pool.ListPool<UniTask>.Get(out List<UniTask> tasks))
            {
                foreach (AssetReferenceGameObject asset in assets)
                {
                    object key = asset.RuntimeKey;

                    // 이미 예열됐거나 이번 배치에 중복으로 들어온 것
                    if (pools.ContainsKey(key) || !seen.Add(key))
                        continue;

                    tasks.Add(WarmupOneAsync(asset, scope, count));
                }
                await UniTask.WhenAll(tasks);   // 병렬 대기
            }
        }

        /// <summary> 참조 1개를 로드해 풀을 만들고 예열합니다. 로드 실패·프리팹 미구성이면 값으로 스킵. </summary>
        /// <param name="asset">예열할 프리팹 참조</param>
        /// <param name="scope">수명 범위</param>
        /// <param name="count">미리 만들 개수</param>
        private async UniTask WarmupOneAsync(AssetReferenceGameObject asset, PoolScope scope, int count)
        {
            object key = asset.RuntimeKey;
            if (pools.ContainsKey(key))
                return;

            GameObject prefab = await assetLoader.LoadAsync<GameObject>(asset);

            // 로드 실패는 경계에서 값으로 번역된다
            if (prefab == null)
                return;

            IPoolableObject sample = prefab.GetComponent<IPoolableObject>();
            if (sample == null)
            {
                Debug.LogWarning($"프리팹 루트에 IPoolableObject 없음: {prefab.name}");
                return;
            }

            // await 사이에 다른 예열이 같은 키를 끝냈을 수 있다
            if (pools.ContainsKey(key))
                return;

            string bucket = BucketOf(scope);
            Transform container = ResolveContainer(bucket, CategoryOf(sample));

            var pool = new Pool(prefab, container);
            pools[key] = pool;
            poolBucket[key] = bucket;
            poolAsset[key] = asset;
            Prewarm(pool, count);
        }

        /// <summary> 수명 범위를 칸 이름으로 바꿉니다. </summary>
        /// <param name="scope">수명 범위</param>
        private static string BucketOf(PoolScope scope)
        {
            switch (scope)
            {
                case PoolScope.Global:
                    return GLOBAL_BUCKET;
                case PoolScope.Scene:
                    return UnityEngine.SceneManagement.SceneManager.GetActiveScene().name;
                default:
                    throw new System.ArgumentOutOfRangeException(nameof(scope), scope, "처리되지 않은 값입니다.");
            }
        }

        /// <summary> 타입의 네임스페이스를 하이어라키 칸 이름으로 씁니다(DefenseDot. 접두 제거). </summary>
        /// <param name="sample">프리팹 루트의 풀링 구현체</param>
        private static string CategoryOf(IPoolableObject sample)
        {
            string ns = sample.GetType().Namespace;
            if (string.IsNullOrEmpty(ns))
                return UNCATEGORIZED;

            if (ns.StartsWith(NAMESPACE_PREFIX))
                return ns.Substring(NAMESPACE_PREFIX.Length);

            return ns;
        }

        /// <summary> 칸(수명) 아래 분류 자리를 찾거나 만듭니다. </summary>
        /// <param name="bucket">Global 또는 씬 이름</param>
        /// <param name="category">네임스페이스에서 뽑은 분류 이름</param>
        private Transform ResolveContainer(string bucket, string category)
        {
            if (!buckets.TryGetValue(bucket, out Transform bucketRoot) || bucketRoot == null)
            {
                bucketRoot = new GameObject(bucket).transform;
                bucketRoot.SetParent(root, false);
                buckets[bucket] = bucketRoot;
            }

            Transform found = bucketRoot.Find(category);
            if (found != null)
                return found;

            Transform created = new GameObject(category).transform;
            created.SetParent(bucketRoot, false);
            return created;
        }

        /// <summary> 미리 count 개 만들어 큐를 채웁니다. </summary>
        private void Prewarm(Pool pool, int count)
        {
            using (UnityEngine.Pool.ListPool<GameObject>.Get(out List<GameObject> temp))
            {
                for (int i = 0; i < count; i++)
                {
                    temp.Add(pool.Get());
                }
                foreach (GameObject instance in temp)
                {
                    pool.Return(instance);
                }
            }
        }

        /// <summary> 예열된 풀에서 동기로 꺼냅니다. 실패 시 false + 로그(예외 없음). owner 주면 소유 자식 등록. </summary>
        /// <param name="reference">꺼낼 프리팹 참조</param>
        /// <param name="item">꺼낸 인스턴스</param>
        /// <param name="owner">소유자(있으면 반납 시 연쇄 회수 대상이 된다)</param>
        /// <param name="parent">붙일 부모. UI 처럼 특정 계층 아래 있어야 하는 경우 지정한다</param>
        public bool TryGet<T>(AssetReference reference, out T item, object owner = null, Transform parent = null)
            where T : Component, IPoolableObject
        {
            item = null;
            if (!pools.TryGetValue(reference.RuntimeKey, out Pool pool))
            {
                Debug.LogWarning($"예열되지 않은 풀: {reference.RuntimeKey}");
                return false;
            }

            GameObject instance = pool.Get(parent);
            item = instance.GetComponent<T>();
            if (item == null)
            {
                pool.Return(instance);   // 누수 방지: 방금 꺼낸 인스턴스 되돌림
                Debug.LogWarning($"프리팹에 {typeof(T).Name} 컴포넌트가 없습니다.");
                return false;
            }

            Retain(item, pool, owner);
            return true;
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
            if (obj is not IPoolableObject pooled)
                return;

            if (!live.Contains(pooled))
                return;   // 이미 반납됨

            live.Remove(pooled);

            // 1. 내가 자식이면 부모 목록에서 제거
            if (parentOf.TryGetValue(pooled, out IPoolableObject parent))
            {
                parentOf.Remove(pooled);
                if (owned.TryGetValue(parent, out List<IPoolableObject> siblings))
                    siblings.Remove(pooled);
            }

            // 2. 자식부터 연쇄 회수(목록 분리)
            if (owned.TryGetValue(pooled, out List<IPoolableObject> children))
            {
                owned.Remove(pooled);
                for (int i = children.Count - 1; i >= 0; i--)
                {
                    Return(children[i]);
                }
            }

            // 3. 실제 풀로 되돌림 (에디터 종료 시 이미 파괴된 오브젝트는 스킵)
            if (origin.TryGetValue(pooled, out Pool pool))
            {
                origin.Remove(pooled);
                if (pooled is Component comp && comp != null)
                    pool.Return(comp.gameObject);
            }
        }

        /// <summary> 한 씬 소속 풀만 정리합니다(대여분 회수 → 풀 폐기 → 에셋 해제 → 칸 파괴). </summary>
        /// <param name="sceneName">떠나는 씬 이름. Global 칸은 건드리지 않는다</param>
        public void ReleaseScene(string sceneName)
        {
            if (string.IsNullOrEmpty(sceneName) || sceneName == GLOBAL_BUCKET)
                return;

            using (UnityEngine.Pool.ListPool<object>.Get(out List<object> targets))
            {
                foreach (KeyValuePair<object, string> pair in poolBucket)
                {
                    if (pair.Value == sceneName)
                        targets.Add(pair.Key);
                }

                if (targets.Count == 0)
                    return;

                ReturnBorrowedFrom(targets);
                DiscardPools(targets);
            }

            if (buckets.TryGetValue(sceneName, out Transform bucketRoot))
            {
                buckets.Remove(sceneName);
                if (bucketRoot != null)
                    Object.Destroy(bucketRoot.gameObject);
            }
        }

        /// <summary> 지정 풀들에서 빌려간 객체를 먼저 회수합니다(장부에 죽은 참조를 남기지 않기 위해). </summary>
        /// <param name="targets">정리 대상 풀의 RuntimeKey 목록</param>
        private void ReturnBorrowedFrom(List<object> targets)
        {
            using (UnityEngine.Pool.ListPool<IPoolableObject>.Get(out List<IPoolableObject> borrowed))
            {
                foreach (IPoolableObject item in live)
                {
                    if (!origin.TryGetValue(item, out Pool from))
                        continue;

                    foreach (object key in targets)
                    {
                        if (!pools.TryGetValue(key, out Pool target) || target != from)
                            continue;

                        borrowed.Add(item);
                        break;
                    }
                }

                foreach (IPoolableObject item in borrowed)
                {
                    if (live.Contains(item))
                        Return(item);
                }
            }
        }

        /// <summary> 지정 풀들을 폐기하고 붙잡고 있던 에셋 핸들을 놓습니다. </summary>
        /// <param name="targets">정리 대상 풀의 RuntimeKey 목록</param>
        private void DiscardPools(List<object> targets)
        {
            foreach (object key in targets)
            {
                if (pools.TryGetValue(key, out Pool pool))
                    pool.Clear();

                pools.Remove(key);
                poolBucket.Remove(key);

                if (poolAsset.TryGetValue(key, out AssetReference asset))
                {
                    assetLoader.Release(asset);
                    poolAsset.Remove(key);
                }
            }
        }

        /// <summary> 남은 대여 객체를 전량 회수하고 풀·에셋을 정리합니다(앱 종료 = 뿌리 절단). </summary>
        public void Dispose()
        {
            using (UnityEngine.Pool.ListPool<IPoolableObject>.Get(out List<IPoolableObject> snapshot))
            {
                snapshot.AddRange(live);
                foreach (IPoolableObject item in snapshot)
                {
                    if (live.Contains(item))
                        Return(item);
                }
            }

            foreach (Pool pool in pools.Values)
            {
                pool.Clear();
            }

            pools.Clear();
            origin.Clear();
            live.Clear();
            owned.Clear();
            parentOf.Clear();
            poolBucket.Clear();
            poolAsset.Clear();

            foreach (Transform bucketRoot in buckets.Values)
            {
                if (bucketRoot == null)
                    continue;

                if (Application.isPlaying)
                    Object.Destroy(bucketRoot.gameObject);
                else
                    Object.DestroyImmediate(bucketRoot.gameObject);
            }
            buckets.Clear();

            assetLoader?.ReleaseAll();
        }
    }
}
