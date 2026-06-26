namespace DefenseDot.Systems.Cards
{
    /// <summary> 카드가 적용하는 동작. (향후 Combo·Bonus 확장) </summary>
    public enum CardAction { New, Level }

    /// <summary> 카드 희귀도/연출 티어. New·Upgrade 는 코어, 나머지는 향후 겹(조합·럭키)용. </summary>
    public enum CardTier { New, Upgrade, Combo, Lucky, SuperLucky }
}
