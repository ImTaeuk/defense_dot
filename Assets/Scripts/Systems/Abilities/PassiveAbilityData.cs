namespace DefenseDot.Systems.Abilities
{
    /// <summary> 보정형 능력(추상). 보유/레벨에 따라 합산 보정에 기여합니다. </summary>
    public abstract class PassiveAbilityData : AbilityData
    {
        /// <summary> 자신의 보정을 mods에 누적합니다. </summary>
        public abstract void ApplyModifiers(AbilityModifiers mods, int level);
    }
}
