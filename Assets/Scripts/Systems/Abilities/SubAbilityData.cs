namespace DefenseDot.Systems.Abilities
{
    /// <summary>
    /// 동반 공격 능력(추상). 스스로 발사하지 않고 주축 발사에 함께 나갑니다.
    /// 보유한 만큼 발사 주기에 시간을 더하거나 뺍니다.
    /// </summary>
    public abstract class SubAbilityData : ActiveAbilityData
    {
        /// <summary> 타워 기본 주기에 더할 시간(초). 음수면 빨라집니다. </summary>
        public float cycleDelta;
    }
}
