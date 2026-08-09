// 골드 강화 비용 곡선 — AbilityUpgradeSystem 과 함께 현재 쓰이지 않는다 (그 파일 상단 주석 참고)
using UnityEngine;

namespace DefenseDot.Systems.Economy
{
    /// <summary> 능력 강화 비용 곡선 파라미터입니다(능력 무관). 현재 사용처 없음. </summary>
    [CreateAssetMenu(fileName = "AbilityUpgradeConfig", menuName = "DefenseDot/Ability Upgrade Config")]
    public sealed class AbilityUpgradeConfig : ScriptableObject
    {
        /// <summary> 레벨당 가격 배율 가산. </summary>
        public float levelSlope = 0.10f;
        /// <summary> 획득 라운드당 가격 배율 가산. </summary>
        public float roundInflation = 0.05f;
        /// <summary> 누적 최대 할인 상한(0.55 = 최대 55%). 할인원 도입 전 비활성. </summary>
        public float maxDiscountRate = 0.55f;
        /// <summary> 삭제 시 강화비 환급률. </summary>
        public float refundRatio = 0.40f;
    }
}
