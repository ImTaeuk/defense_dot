using UnityEngine;

namespace DefenseDot.Systems.Cards
{
    /// <summary> 카드 선택 허브 설정(토글·곡선·비율·향후 플래그). </summary>
    [CreateAssetMenu(menuName = "DefenseDot/Cards/Arena Card Config", fileName = "ArenaCardConfig")]
    public sealed class ArenaCardConfig : ScriptableObject
    {
        [Header("정지")]
        public bool pauseOnCardSelect = true;

        [Header("카드 생성")]
        public int choiceCount = 3;

        [Header("레벨 곡선  kills = max(3, curveBase + level*curvePerLevel)")]
        public int curveBase = 8;
        public int curvePerLevel = 4;

        [Header("신규 vs 레벨업 비율")]
        [Range(0f, 1f)] public float newCardChanceEarly = 0.75f;
        [Range(0f, 1f)] public float newCardChanceLate = 0.45f;
        public int earlyLevelThreshold = 4;

        [Header("향후 겹 (기본 off)")]
        public bool enableLucky = false;
        public bool enableCombo = false;
        public bool enableBonus = false;
        [Range(0f, 1f)] public float luckyChance = 0.12f;
        [Range(0f, 1f)] public float superLuckyChance = 0.03f;

        [Header("연출")]
        public CardTierSet tierSet;

        /// <summary> 해당 레벨에서 다음 레벨까지 필요한 처치 수. </summary>
        public int KillsToNextLevel(int level)
            => Mathf.Max(3, curveBase + level * curvePerLevel);
    }
}
