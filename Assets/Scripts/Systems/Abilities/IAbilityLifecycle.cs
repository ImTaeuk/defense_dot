namespace DefenseDot.Systems.Abilities
{
    /// <summary> 상시 수명 능력(오비탈 등)이 구현하는 장착/해제 훅입니다. </summary>
    public interface IAbilityLifecycle
    {
        /// <summary> 로드아웃 장착 시 1회 호출(상시 효과 스폰 등). </summary>
        void OnEquip(in AbilityContext ctx, AbilityInstance self);

        /// <summary> 로드아웃 해제 시 1회 호출(상시 효과 반납 등). </summary>
        void OnUnequip(in AbilityContext ctx, AbilityInstance self);
    }
}
