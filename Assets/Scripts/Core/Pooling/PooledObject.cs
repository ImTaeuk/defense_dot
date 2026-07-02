namespace DefenseDot.Core.Pooling
{
    /// <summary> POCO 풀 대상 베이스입니다. 상속만으로 활성·반환 계약을 획득합니다. </summary>
    public abstract class PooledObject : IPoolable, IActivatable, IPooledObject, IReturnBindable
    {
        private System.Action returnToPool;

        public bool IsActive { get; private set; }
        public event System.Action OnActivated;
        public event System.Action OnDeactivated;

        public void Activate()
        {
            IsActive = true;
            OnActivated?.Invoke();
        }

        public void Deactivate()
        {
            IsActive = false;
            OnDeactivated?.Invoke();
        }

        public virtual void OnSpawn() { }
        public virtual void OnDespawn() { }

        void IReturnBindable.BindReturn(System.Action action) => returnToPool = action;
        public void Dispose() => returnToPool?.Invoke();
    }

    /// <summary> PoolManager 가 Get 시 반환 동작을 심기 위한 내부 계약입니다. </summary>
    internal interface IReturnBindable
    {
        void BindReturn(System.Action action);
    }
}
