using System.Collections.Generic;
using UnityEngine;

namespace DefenseDot.Core.Pooling
{
    /// <summary>
    /// 프리팹 하나를 재사용하는 풀입니다. 놀고 있으면 큐에서 꺼내고, 없으면 Instantiate 합니다.
    /// 폐기(Clear) 시 큐에 남은 인스턴스를 파괴합니다.
    /// </summary>
    public sealed class Pool
    {
        private readonly GameObject prefab;
        private readonly Queue<GameObject> idle = new Queue<GameObject>();

        public Pool(GameObject prefab)
        {
            this.prefab = prefab;
        }

        /// <summary> 재사용 인스턴스를 켜서 내줍니다. 없으면 새로 Instantiate 합니다. </summary>
        public GameObject Get()
        {
            GameObject instance = idle.Count > 0 ? idle.Dequeue() : Object.Instantiate(prefab);
            IPoolableObject poolable = instance.GetComponent<IPoolableObject>();
            poolable.OnSpawn();                       // 상태 리셋
            ((IActivatable)poolable).Activate();      // 켜기 + 알림
            return instance;
        }

        /// <summary> 인스턴스를 끄고 큐로 되돌립니다. </summary>
        public void Return(GameObject instance)
        {
            IPoolableObject poolable = instance.GetComponent<IPoolableObject>();
            ((IActivatable)poolable).Deactivate();    // 끄기 + 알림
            poolable.OnDespawn();                     // 정리
            idle.Enqueue(instance);
        }

        /// <summary> 큐에 남은 인스턴스를 파괴합니다(풀 폐기 시 GameObject 누수 방지). </summary>
        public void Clear()
        {
            while (idle.Count > 0)
            {
                GameObject instance = idle.Dequeue();
                if (Application.isPlaying) Object.Destroy(instance);
                else Object.DestroyImmediate(instance);   // 에디트 모드(테스트)
            }
        }
    }
}
