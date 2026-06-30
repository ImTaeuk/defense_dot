namespace DefenseDot.Systems.Abilities
{
    /// <summary> 카드 선택이 능력을 추가/레벨업하는 명령 대상. </summary>
    public interface ICardCommandTarget
    {
        AbilityLoadout Loadout { get; }
        /// <summary> 능력 추가. 추가된 인스턴스 반환(실패 시 null). </summary>
        AbilityInstance AddAbility(AbilityData data);
        void LevelUpAbility(AbilityInstance instance);
    }
}
