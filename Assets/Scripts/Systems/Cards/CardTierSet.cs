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
            public Color borderColor;
            public Color bgTop;
            public Color bgBottom;
            public Color glowColor;
            public float glowIntensity;
            public bool useParticle;
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
