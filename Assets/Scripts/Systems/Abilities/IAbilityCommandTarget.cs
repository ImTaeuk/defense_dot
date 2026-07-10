namespace DefenseDot.Systems.Abilities
{
    /// <summary> 능력을 추가/레벨업/삭제하는 명령 대상입니다. (카드 선택·강화 서비스 공용) </summary>
    public interface IAbilityCommandTarget
    {
        AbilityLoadout Loadout { get; }
        /// <summary> 능력 추가. 추가된 인스턴스 반환(실패 시 null). </summary>
        AbilityInstance AddAbility(AbilityData data);
        void LevelUpAbility(AbilityInstance instance);
        /// <summary> 능력 삭제(액티브는 언장착 동반). </summary>
        void RemoveAbility(AbilityInstance instance);
    }
}
