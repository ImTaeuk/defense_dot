using DefenseDot.Systems.Abilities;

namespace DefenseDot.Systems.Cards
{
    /// <summary> 레벨업 시 제시되는 카드 1장의 데이터. </summary>
    public readonly struct Card
    {
        public readonly CardApplyType applyType;
        public readonly AbilityData data;
        public readonly AbilityInstance instance; // Level 카드일 때 대상
        public readonly int fromLevel;
        public readonly int toLevel;
        public readonly CardTier tier;
        /// <summary> 합성 카드일 때 소진할 재료 A(능력 식별자 — 적용 시 인스턴스 재해석). </summary>
        public readonly AbilityData materialA;
        /// <summary> 합성 카드일 때 소진할 재료 B(능력 식별자 — 적용 시 인스턴스 재해석). </summary>
        public readonly AbilityData materialB;

        public Card(CardApplyType applyType, AbilityData data, AbilityInstance instance,
            int fromLevel, int toLevel, CardTier tier,
            AbilityData materialA = null, AbilityData materialB = null)
        {
            this.applyType = applyType;
            this.data = data;
            this.instance = instance;
            this.fromLevel = fromLevel;
            this.toLevel = toLevel;
            this.tier = tier;
            this.materialA = materialA;
            this.materialB = materialB;
        }

        /// <summary> 신규 능력 카드(시작 레벨 = toLevel). </summary>
        public static Card NewCard(AbilityData data, CardTier tier, int toLevel)
            => new Card(CardApplyType.New, data, null, 0, toLevel, tier);

        /// <summary> 기존 능력 레벨업 카드(목표 레벨 = toLevel). </summary>
        public static Card LevelCard(AbilityInstance inst, CardTier tier, int toLevel)
            => new Card(CardApplyType.Level, inst.data, inst, inst.level, toLevel, tier);

        /// <summary> 합성 카드(재료 2개 소진 → 결과 Lv1). 재료는 능력 식별자로 보관. </summary>
        public static Card FusionCard(AbilityData result, AbilityData materialA, AbilityData materialB, CardTier tier)
            => new Card(CardApplyType.Fuse, result, null, 0, 1, tier, materialA, materialB);
    }
}
