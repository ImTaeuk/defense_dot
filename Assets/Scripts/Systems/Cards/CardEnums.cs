namespace DefenseDot.Systems.Cards
{
    /// <summary> 카드가 적용하는 동작. (향후 Combo·Bonus 확장) </summary>
    public enum CardAction { New, Level }

    /// <summary> 카드 희귀도/연출 티어. (향후 Lucky·Combo 확장) </summary>
    public enum CardTier { New, Upgrade }
}
