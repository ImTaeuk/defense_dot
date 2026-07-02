using UnityEngine;

namespace DefenseDot.Core.Pooling
{
    /// <summary>
    /// 풀링되는 MonoBehaviour 를 위한 선택적 편의 베이스입니다.
    /// 상속만으로 활성·반납 계약을 얻고, 하위는 필요한 리셋(OnSpawn/OnDespawn)만 오버라이드합니다.
    /// 강제는 아닙니다 — 자기 베이스가 있는 타입은 계약(IPoolableObject)을 직접 구현해도 됩니다.
    /// </summary>
    public abstract class PooledBehaviour : MonoBehaviour, IPoolableObject, IReturnBindable
    {
        private System.Action returnToPool;

        /// <summary> 현재 활성 여부입니다. </summary>
        public bool IsActive => gameObject.activeSelf;
        public event System.Action OnActivated;
        public event System.Action OnDeactivated;

        /// <summary> 켜고 활성 알림을 발화합니다. </summary>
        public void Activate()
        {
            gameObject.SetActive(true);
            OnActivated?.Invoke();
        }

        /// <summary> 끄고 비활성 알림을 발화합니다. </summary>
        public void Deactivate()
        {
            gameObject.SetActive(false);
            OnDeactivated?.Invoke();
        }

        /// <summary> 재사용 시 상태 리셋(하위 오버라이드). </summary>
        public virtual void OnSpawn() { }
        /// <summary> 반납 직전 정리(하위 오버라이드). </summary>
        public virtual void OnDespawn() { }

        void IReturnBindable.BindReturn(System.Action action) => returnToPool = action;

        /// <summary> 풀로 반납합니다(주입된 반납 동작 실행). </summary>
        public void Dispose() => returnToPool?.Invoke();
    }
}
