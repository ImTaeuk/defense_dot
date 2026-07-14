namespace DefenseDot.Systems.Cards
{
    /// <summary> 카드가 적용하는 동작. </summary>
    public enum CardApplyType { New, Level, Fuse }

    /// <summary> 카드 희귀도/연출 티어. New·Upgrade 는 코어, Fusion 은 합성, 나머지는 럭키용. </summary>
    public enum CardTier { New, Upgrade, Fusion, Lucky, SuperLucky }
}
