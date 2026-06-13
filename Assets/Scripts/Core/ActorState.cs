namespace DefenseDot.Core
{
    /// <summary>
    /// 액터의 현재 상태를 정의합니다.
    /// 순서 = Animator State int 매핑이므로 재정렬 금지.
    /// </summary>
    public enum ActorState
    {
        Idle,
        Moving,
        Attacking,
        Stunned,
        Dead
    }
}
