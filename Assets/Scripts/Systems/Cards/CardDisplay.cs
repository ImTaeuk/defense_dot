using UnityEngine;
using DefenseDot.Systems.Abilities;

namespace DefenseDot.Systems.Cards
{
    /// <summary> 카드 표시용 데이터. </summary>
    public struct CardDisplay
    {
        public string title;
        public string kindTag;
        public string desc;
        public CardTier tier;
        public Sprite icon;
    }

    /// <summary> Card → 표시용 데이터 변환(순수). </summary>
    public static class CardDisplayBuilder
    {
        public static CardDisplay Build(in Card c)
        {
            AbilityData d = c.data;
            bool passive = d is PassiveAbilityData;
            string kind = passive ? "[ 패시브 ]" : "[ 액티브 ]";
            string luckMark = c.tier == CardTier.SuperLucky ? "★★ "
                : c.tier == CardTier.Lucky ? "★ " : "";
            string body = c.applyType == CardApplyType.Level
                ? $"Lv{c.fromLevel} > Lv{c.toLevel}"
                : (string.IsNullOrEmpty(d.description) ? d.displayName : d.description);
            string desc = luckMark + body;
            return new CardDisplay
            {
                title = d.displayName,
                kindTag = kind,
                desc = desc,
                tier = c.tier,
                icon = d.icon,
            };
        }
    }
}
