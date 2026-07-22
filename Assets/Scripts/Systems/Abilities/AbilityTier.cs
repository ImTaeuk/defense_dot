namespace DefenseDot.Systems.Abilities
{
    /// <summary> 능력의 위계(어디서 얻는가). Basic만 기본 공격 판정에 쓰이며, Unset은 미저작을 Basic과 구분합니다. </summary>
    public enum AbilityTier
    {
        Unset = 0,
        Basic = 1,
        Signature = 2,
        Combo = 3,
        Triple = 4,
        Eighth = 5,
        Star = 6,
        Passive = 7,
    }
}
