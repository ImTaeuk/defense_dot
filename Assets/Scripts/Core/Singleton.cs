// 씬에 배치된 단일 인스턴스를 전역에서 찾게 하는 베이스 — 자동 생성하지 않는다
using UnityEngine;

namespace DefenseDot.Core
{
    /// <summary>
    /// 씬에 배치된 컴포넌트 하나를 전역에서 찾을 수 있게 하는 베이스입니다.
    /// 없으면 자동으로 만들지 않습니다 — 배치는 씬이 책임집니다.
    /// </summary>
    /// <typeparam name="T">단일 인스턴스로 다룰 컴포넌트 타입</typeparam>
    public abstract class Singleton<T> : MonoBehaviour where T : Singleton<T>
    {
        private static T instance;

        /// <summary> 배치된 인스턴스입니다. 씬에 없으면 null 입니다. </summary>
        public static T Instance => instance;

        /// <summary> 첫 인스턴스를 등록하고 중복은 스스로 파괴합니다. </summary>
        private void Awake()
        {
            if (instance != null && instance != this)
            {
                Debug.LogWarning($"{typeof(T).Name} 이 이미 있어 중복 인스턴스를 파괴합니다.", this);
                Destroy(gameObject);
                return;
            }

            instance = (T)this;
            OnAwake();
        }

        /// <summary> 자기가 등록된 인스턴스면 참조를 비웁니다. </summary>
        private void OnDestroy()
        {
            if (instance == this)
                instance = null;

            OnDestroyed();
        }

        /// <summary> 인스턴스 등록 직후의 초기화 훅입니다. </summary>
        protected virtual void OnAwake()
        {
        }

        /// <summary> 파괴 직전의 정리 훅입니다. </summary>
        protected virtual void OnDestroyed()
        {
        }
    }
}