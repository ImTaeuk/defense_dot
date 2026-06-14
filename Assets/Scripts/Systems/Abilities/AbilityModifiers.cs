namespace DefenseDot.Systems.Abilities
{
    /// <summary> 패시브들이 누적하는 합산 보정값입니다. (필드는 패시브 추가에 따라 확장) </summary>
    public sealed class AbilityModifiers
    {
        /// <summary> 가산 공격력 보너스. </summary>
        public float damageBonus;
        /// <summary> 쿨다운 감소(초). </summary>
        public float cooldownReduction;
    }
}
