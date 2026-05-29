namespace DefenseDot.Core
{
    /// <summary>
    /// 오브젝트 풀링 시스템에서 관리되는 객체를 위한 인터페이스입니다.
    /// </summary>
    public interface IPoolable
    {
        void OnSpawn();
        void OnDespawn();
    }
}
