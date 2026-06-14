namespace DefenseDot.Systems.Abilities
{
    /// <summary> 발동형 능력(추상). 쿨다운마다 Execute로 1회 발동합니다. </summary>
    public abstract class ActiveAbilityData : AbilityData
    {
        /// <summary> 기본 쿨다운(초). </summary>
        public float baseCooldown = 1f;

        /// <summary> 레벨별 효과값(데미지 등). 서브클래스가 재정의. </summary>
        public virtual float ValueAtLevel(int level) { return level; }

        /// <summary> 레벨별 쿨다운(보정 적용은 호출부). </summary>
        public virtual float CooldownAtLevel(int level) { return baseCooldown; }

        /// <summary> 1회 발동(투사체·데미지 등). 가동 루프는 A2. </summary>
        public abstract void Execute(in AbilityContext ctx, AbilityInstance self);
    }
}
