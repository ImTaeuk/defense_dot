namespace DefenseDot.Core
{
    /// <summary>
    /// 데미지를 입을 수 있는 객체를 위한 인터페이스입니다.
    /// </summary>
    public interface IDamageable : IActor
    {
        void TakeDamage(float amount);
    }
}
