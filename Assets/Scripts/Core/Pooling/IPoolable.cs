namespace DefenseDot.Core.Pooling
{
    /// <summary> 풀 재사용 시 자기 상태를 리셋하는 훅입니다. </summary>
    public interface IPoolable
    {
        void OnSpawn();
        void OnDespawn();
    }

    /// <summary> 켜기/끄기 추상 + 활성 전이 외부 알림입니다. </summary>
    public interface IActivatable
    {
        bool IsActive { get; }
        void Activate();
        void Deactivate();
        event System.Action OnActivated;
        event System.Action OnDeactivated;
    }

    /// <summary> Dispose() = 풀 반환(파괴 아님). 반환 동작은 PoolManager 가 주입합니다. </summary>
    public interface IPooledObject : System.IDisposable
    {
    }
}
