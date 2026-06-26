namespace DefenseDot.Core
{
    /// <summary> 조건부 데미지 계산에 쓰이는 실시간 전투 상태(라운드·생존 적 수). </summary>
    public interface ICombatState
    {
        int Round { get; }
        int AliveEnemyCount { get; }
    }
}
