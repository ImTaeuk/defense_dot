using UnityEngine;

namespace DefenseDot.Core.Pooling
{
    /// <summary>
    /// 풀링되는 MonoBehaviour 를 위한 선택적 편의 베이스입니다.
    /// 상속만으로 활성·반납 계약을 얻고, 하위는 필요한 리셋(OnSpawn/OnDespawn)만 오버라이드합니다.
    /// 강제는 아닙니다 — 자기 베이스가 있는 타입은 계약(IPoolableObject/IActivatable)을 직접 구현해도 됩니다.
    /// </summary>
    public abstract class PooledBehaviour : MonoBehaviour, IPoolableObject, IActivatable, IReturnBindable
    {
        private System.Action returnToPool;

        public bool IsActive => gameObject.activeSelf;
        public event System.Action OnActivated;
        public event System.Action OnDeactivated;

        public void Activate()
        {
            gameObject.SetActive(true);
            OnActivated?.Invoke();
        }

        public void Deactivate()
        {
            gameObject.SetActive(false);
            OnDeactivated?.Invoke();
        }

        public virtual void OnSpawn() { }
        public virtual void OnDespawn() { }

        void IReturnBindable.BindReturn(System.Action action) => returnToPool = action;
        public void Dispose() => returnToPool?.Invoke();
    }
}
