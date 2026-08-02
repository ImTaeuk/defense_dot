using DefenseDot.Systems.Abilities;

namespace DefenseDot.UI.Views
{
    /// <summary> 능력 목록 행의 표시 데이터입니다. </summary>
    public readonly struct AbilityUpgradeRowData
    {
        /// <summary> 대상 능력 인스턴스. </summary>
        public readonly AbilityInstance ability;

        /// <summary> 표시 데이터를 구성합니다. </summary>
        public AbilityUpgradeRowData(AbilityInstance ability)
        {
            this.ability = ability;
        }
    }
}
