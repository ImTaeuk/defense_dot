using System.Collections.Generic;
using UnityEngine;

namespace DefenseDot.Systems.Cards
{
    /// <summary> 티어별 색/연출 스타일(원작 CARD_TIERS 이관). </summary>
    [CreateAssetMenu(menuName = "DefenseDot/Cards/Card Tier Set", fileName = "CardTierSet")]
    public sealed class CardTierSet : ScriptableObject
    {
        [System.Serializable]
        public struct TierStyle
        {
            public CardTier tier;
            public Sprite cardSprite;      // 카드 모양 배경 스프라이트(등급색)
            public Material foilMaterial;  // 홀로그램 포일 머티리얼(등급색)
        }

        public List<TierStyle> styles = new List<TierStyle>();

        /// <summary> 티어 스타일 조회(없으면 첫 항목/기본값). </summary>
        public TierStyle Get(CardTier tier)
        {
            for (int i = 0; i < styles.Count; i++)
                if (styles[i].tier == tier) return styles[i];
            return styles.Count > 0 ? styles[0] : default;
        }
    }
}
