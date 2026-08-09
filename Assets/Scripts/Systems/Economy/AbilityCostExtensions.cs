// 골드 강화 비용 계산 — AbilityUpgradeSystem 과 함께 현재 쓰이지 않는다 (그 파일 상단 주석 참고)
using UnityEngine;
using DefenseDot.Systems.Abilities;

namespace DefenseDot.Systems.Economy
{
    /// <summary> 능력 인스턴스의 강화 비용·환불액을 계산하는 확장 메서드입니다. 현재 사용처 없음. </summary>
    public static class AbilityCostExtensions
    {
        /// <summary> 다음 레벨로의 강화 비용입니다. </summary>
        public static int UpgradeCost(this AbilityInstance ability, AbilityUpgradeConfig config)
            => ability.UpgradeCostAtLevel(ability.level, config);

        /// <summary> 지정 레벨 기준 강화 비용입니다(환불 합산용). </summary>
        public static int UpgradeCostAtLevel(this AbilityInstance ability, int level, AbilityUpgradeConfig config)
        {
            float lvScale = (level + 1) + level * config.levelSlope;
            float roundMul = 1f + (ability.acquiredRound - 1) * config.roundInflation;
            float discountStack = 1f;   // 할인원(A7) 없음
            float costMul = Mathf.Max(1f - config.maxDiscountRate, discountStack);
            return Mathf.CeilToInt(ability.data.baseCost * lvScale * roundMul * costMul);
        }

        /// <summary> 삭제 시 환급액(레벨1~직전 강화비 합 × 환급률, 레벨별 올림)입니다. </summary>
        public static int RefundValue(this AbilityInstance ability, AbilityUpgradeConfig config)
        {
            int sum = 0;
            for (int lv = 1; lv < ability.level; lv++)
                sum += Mathf.CeilToInt(ability.UpgradeCostAtLevel(lv, config) * config.refundRatio);
            return sum;
        }
    }
}
