using UnityEngine;

namespace DefenseDot.Core.Pooling
{
    /// <summary> MonoBehaviour 풀 대상 베이스입니다. 상속만으로 활성·반환 계약을 획득합니다. </summary>
    public abstract class PooledBehaviour : MonoBehaviour, IPoolable, IActivatable, IPooledObject, IReturnBindable
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
