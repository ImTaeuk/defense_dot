namespace DefenseDot.Core
{
    /// <summary>
    /// 타워의 공격 대상이 될 수 있는 객체를 위한 인터페이스입니다.
    /// </summary>
    public interface ITargetable : IActor
    {
        bool IsActive { get; }
    }
}
