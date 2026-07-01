using DefenseDot.Systems.Abilities;

namespace DefenseDot.Systems.Cards
{
    /// <summary> 선택된 카드를 능력 대상에 적용하는 뷰 비의존 순수 로직. </summary>
    public static class CardChoiceApplier
    {
        /// <summary> 카드 선택을 반영(신규 추가 또는 목표 레벨까지 레벨업). </summary>
        public static void Apply(ICardCommandTarget core, CardChoice choice)
        {
            if (choice.action == CardAction.New)
            {
                AbilityInstance added = core.AddAbility(choice.data);
                if (added != null)
                    for (int lv = added.level; lv < choice.toLevel; lv++) core.LevelUpAbility(added);
            }
            else
            {
                for (int lv = choice.fromLevel; lv < choice.toLevel; lv++) core.LevelUpAbility(choice.instance);
            }
        }
    }
}
