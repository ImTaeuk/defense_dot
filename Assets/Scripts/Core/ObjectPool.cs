using System.Collections.Generic;
using UnityEngine;

namespace DefenseDot.Core
{
    /// <summary>
    /// 제네릭 오브젝트 풀링 시스템입니다. 빈번하게 생성/파괴되는 객체의 성능 최적화를 위해 사용합니다.
    /// </summary>
    /// <typeparam name="T">MonoBehaviour와 IPoolable을 구현하는 타입</typeparam>
    public class ObjectPool<T> : MonoBehaviour where T : MonoBehaviour, IPoolable
    {
        [SerializeField, Tooltip("풀링할 대상 프리팹")] 
        private T prefab;
        [SerializeField, Tooltip("초기에 미리 생성해둘 객체 수")] 
        private int initialSize = 10;

        private Queue<T> pool = new Queue<T>();

        /// <summary>
        /// 풀을 초기화하고 지정된 수만큼 인스턴스를 미리 생성합니다.
        /// </summary>
        public void Initialize()
        {
            for (int i = 0; i < initialSize; i++)
            {
                CreateNewInstance();
            }
        }

        /// <summary>
        /// 새로운 인스턴스를 생성하여 풀에 추가합니다.
        /// </summary>
        private T CreateNewInstance()
        {
            T instance = Instantiate(prefab, transform);
            instance.gameObject.SetActive(false);
            pool.Enqueue(instance);
            return instance;
        }

        /// <summary>
        /// 풀에서 사용 가능한 객체를 가져옵니다. 부족할 경우 새로 생성합니다.
        /// </summary>
        /// <returns>활성화된 인스턴스</returns>
        public T Get()
        {
            T instance = pool.Count > 0 ? pool.Dequeue() : CreateNewInstance();
            instance.gameObject.SetActive(true);
            instance.OnSpawn();
            return instance;
        }

        /// <summary>
        /// 사용이 끝난 객체를 풀로 반환합니다.
        /// </summary>
        /// <param name="instance">반환할 인스턴스</param>
        public void ReturnToPool(T instance)
        {
            instance.OnDespawn();
            instance.gameObject.SetActive(false);
            pool.Enqueue(instance);
        }
    }
}
