using DefenseDot.Systems.Abilities;

namespace DefenseDot.UI.Views
{
    /// <summary> 강화 행의 표시 데이터(능력 + 강화 상태)입니다. </summary>
    public readonly struct AbilityUpgradeRowData
    {
        /// <summary> 대상 능력 인스턴스. </summary>
        public readonly AbilityInstance ability;
        /// <summary> 최대 레벨 도달 여부. </summary>
        public readonly bool isMax;
        /// <summary> 다음 레벨 강화 비용. </summary>
        public readonly int cost;
        /// <summary> 골드로 강화 가능 여부. </summary>
        public readonly bool canAfford;

        /// <summary> 표시 데이터를 구성합니다. </summary>
        public AbilityUpgradeRowData(AbilityInstance ability, bool isMax, int cost, bool canAfford)
        {
            this.ability = ability;
            this.isMax = isMax;
            this.cost = cost;
            this.canAfford = canAfford;
        }
    }
}
