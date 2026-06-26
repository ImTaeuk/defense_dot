namespace DefenseDot.Core
{
    /// <summary> 조건부 데미지 판정에 필요한 대상 정보(보스 여부·체력 비율). </summary>
    public interface ICombatTargetInfo
    {
        bool IsBoss { get; }
        float HealthRatio { get; }
    }
}
