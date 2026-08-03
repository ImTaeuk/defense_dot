using System.Collections.Generic;
using UnityEngine;

namespace DefenseDot.Core.Pooling
{
    /// <summary>
    /// 프리팹 하나를 재사용하는 풀입니다. 놀고 있으면 큐에서 꺼내고, 없으면 Instantiate 합니다.
    /// 쉬는 인스턴스는 보관 자리(container) 아래 두고, 꺼낼 때 요청한 부모로 옮깁니다.
    /// 폐기(Clear) 시 큐에 남은 인스턴스를 파괴합니다.
    /// </summary>
    public sealed class Pool
    {
        private readonly GameObject prefab;

        /// <summary> 쉬는 인스턴스를 담아두는 자리. 이 자리의 수명이 곧 풀의 수명이다. </summary>
        private readonly Transform container;

        private readonly Queue<GameObject> idle = new Queue<GameObject>();

        public Pool(GameObject prefab, Transform container)
        {
            this.prefab = prefab;
            this.container = container;
        }

        /// <summary> 재사용 인스턴스를 켜서 내줍니다. 없으면 새로 Instantiate 합니다. </summary>
        /// <param name="parent">붙일 부모. null 이면 보관 자리에 그대로 둔다(월드 오브젝트는 부모가 필요 없다)</param>
        public GameObject Get(Transform parent = null)
        {
            GameObject instance = idle.Count > 0 ? idle.Dequeue() : Object.Instantiate(prefab, container);
            IPoolableObject poolable = instance.GetComponent<IPoolableObject>();
            if (poolable == null)
                throw new System.InvalidOperationException($"프리팹 루트에 IPoolableObject가 없습니다: {prefab.name}");

            // UI 는 캔버스 아래에 있어야 그려진다 — 요청한 자리로 옮긴다
            if (parent != null)
                instance.transform.SetParent(parent, false);

            poolable.OnSpawn();     // 상태 리셋
            poolable.Activate();    // 켜기 + 알림
            return instance;
        }

        /// <summary> 인스턴스를 끄고 보관 자리로 되돌립니다. </summary>
        public void Return(GameObject instance)
        {
            IPoolableObject poolable = instance.GetComponent<IPoolableObject>();
            poolable.Deactivate();  // 끄기 + 알림
            poolable.OnDespawn();   // 정리

            // 빌려간 자리에 남겨두면 그 자리가 사라질 때 함께 죽는다
            if (instance.transform.parent != container)
                instance.transform.SetParent(container, false);

            idle.Enqueue(instance);
        }

        /// <summary> 큐에 남은 인스턴스를 파괴합니다(풀 폐기 시 GameObject 누수 방지). </summary>
        public void Clear()
        {
            while (idle.Count > 0)
            {
                GameObject instance = idle.Dequeue();

                // 보관 자리가 먼저 파괴됐으면 인스턴스도 이미 없다
                if (instance == null)
                    continue;

                if (Application.isPlaying)
                    Object.Destroy(instance);
                else
                    Object.DestroyImmediate(instance);   // 에디트 모드(테스트)
            }
        }
    }
}
