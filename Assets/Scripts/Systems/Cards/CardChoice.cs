using DefenseDot.Systems.Abilities;

namespace DefenseDot.Systems.Cards
{
    /// <summary> 레벨업 시 제시되는 카드 1장의 데이터. </summary>
    public readonly struct CardChoice
    {
        public readonly CardAction action;
        public readonly AbilityData data;
        public readonly AbilityInstance instance; // Level 카드일 때 대상
        public readonly int fromLevel;
        public readonly int toLevel;
        public readonly CardTier tier;

        public CardChoice(CardAction action, AbilityData data, AbilityInstance instance,
            int fromLevel, int toLevel, CardTier tier)
        {
            this.action = action;
            this.data = data;
            this.instance = instance;
            this.fromLevel = fromLevel;
            this.toLevel = toLevel;
            this.tier = tier;
        }

        /// <summary> 신규 능력 카드. </summary>
        public static CardChoice NewCard(AbilityData data)
            => new CardChoice(CardAction.New, data, null, 0, 1, CardTier.New);

        /// <summary> 기존 능력 레벨업 카드. </summary>
        public static CardChoice LevelCard(AbilityInstance inst)
            => new CardChoice(CardAction.Level, inst.data, inst, inst.level, inst.level + 1, CardTier.Upgrade);
    }
}
