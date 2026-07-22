namespace DefenseDot.Systems.Combat
{
    /// <summary> 전투 능력치. 능력·패시브·장비·코어가 보정을 얹는 단일 원천입니다. </summary>
    public sealed class CombatStats
    {
        /// <summary> 초당 기본 공격 횟수(기본값 × 보정). 기본 공격만 사용합니다. </summary>
        public float attackSpeed = 1f;
        /// <summary> 능력 쿨다운 배율(1 = 원본, 0.7 = 30% 감소). 기본 공격 외 능력이 사용합니다. </summary>
        public float cooldownRate = 1f;
    }
}
