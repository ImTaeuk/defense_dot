using UnityEngine;

namespace DefenseDot.Systems.Abilities
{
    /// <summary> 능력의 정적 설계도(추상). 능력 1종 = 이 파생형의 에셋 1개. </summary>
    public abstract class AbilityData : ScriptableObject
    {
        /// <summary> 고유 식별자. </summary>
        public string id;
        /// <summary> 표시 이름. </summary>
        public string displayName;
        /// <summary> 카드/슬롯 아이콘. </summary>
        public Sprite icon;
        /// <summary> 등급/티어. </summary>
        public int rarity;
        /// <summary> 최대 레벨. </summary>
        public int maxLevel = 5;
        /// <summary> 카드 표시용 설명(선택). </summary>
        [TextArea] public string description;
    }
}
