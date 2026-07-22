namespace DefenseDot.Systems.Abilities
{
    /// <summary> 능력의 형태(무엇을 스폰하는가). 3D 모션 분류이자 실행기 분기의 기준입니다. </summary>
    public enum AbilityKind
    {
        Projectile = 0,
        Beam = 1,
        Field = 2,
        Orbital = 3,
        Summon = 4,
        Buff = 5,
    }
}
