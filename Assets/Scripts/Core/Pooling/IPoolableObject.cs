namespace DefenseDot.Core.Pooling
{
    /// <summary>
    /// 풀이 재사용하는 객체의 계약입니다. 재사용 시 자기 상태를 초기화하고,
    /// Dispose() 는 파괴가 아니라 "풀로 반납"을 뜻합니다(반납 실제 동작은 PoolManager 가 주입).
    /// </summary>
    public interface IPoolableObject : System.IDisposable
    {
        /// <summary> 풀에서 꺼내 재사용할 때 — 이전 사용 흔적을 초기화합니다. </summary>
        void OnSpawn();
        /// <summary> 풀로 반납되기 직전 — 참조·상태를 정리합니다. </summary>
        void OnDespawn();
    }

    /// <summary>
    /// 켜기/끄기 상태와 활성 전이 알림입니다.
    /// 외부(연출·사운드 등)가 객체 내부를 몰라도 켜진/꺼진 순간을 구독할 수 있습니다.
    /// </summary>
    public interface IActivatable
    {
        bool IsActive { get; }
        void Activate();
        void Deactivate();
        event System.Action OnActivated;
        event System.Action OnDeactivated;
    }

    /// <summary>
    /// PoolManager 가 객체에 "반납하는 법"(Dispose 시 어느 풀로 돌아갈지)을 주입하기 위한 내부 계약입니다.
    /// 덕분에 객체는 자기가 어느 풀 소속인지 몰라도 됩니다.
    /// </summary>
    internal interface IReturnBindable
    {
        void BindReturn(System.Action returnAction);
    }
}
