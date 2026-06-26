namespace DefenseDot.Systems.Abilities
{
    /// <summary> 카드 선택이 능력을 추가/레벨업하는 명령 대상. </summary>
    public interface ICardCommandTarget
    {
        AbilityLoadout Loadout { get; }
        bool AddAbility(AbilityData data);
        void LevelUpAbility(AbilityInstance instance);
    }
}
