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

    /// <summary> CardChoice → 표시용 데이터 변환(순수). </summary>
    public static class CardPresentation
    {
        public static CardDisplay Build(in CardChoice c)
        {
            AbilityData d = c.data;
            bool passive = d is PassiveAbilityData;
            string kind = passive ? "[ 패시브 ]" : "[ 액티브 ]";
            string desc = c.action == CardAction.Level
                ? $"Lv{c.fromLevel} > Lv{c.toLevel}"
                : (string.IsNullOrEmpty(d.description) ? d.displayName : d.description);
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
