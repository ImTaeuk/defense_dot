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

        /// <summary> 신규 능력 카드(시작 레벨 = toLevel). </summary>
        public static CardChoice NewCard(AbilityData data, CardTier tier, int toLevel)
            => new CardChoice(CardAction.New, data, null, 0, toLevel, tier);

        /// <summary> 기존 능력 레벨업 카드(목표 레벨 = toLevel). </summary>
        public static CardChoice LevelCard(AbilityInstance inst, CardTier tier, int toLevel)
            => new CardChoice(CardAction.Level, inst.data, inst, inst.level, toLevel, tier);
    }
}
