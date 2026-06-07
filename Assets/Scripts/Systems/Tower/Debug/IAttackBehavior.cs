// DEBUG: 공격 타입 테스트용 — 실제 능력 시스템 구현 시 삭제
namespace DefenseDot.Systems.Tower.Debugging
{
    /// <summary>
    /// 공격 타입 전략입니다. 1회 공격을 수행하고 디버그 비주얼을 그립니다. (DEBUG)
    /// </summary>
    public interface IAttackBehavior
    {
        /// <summary> 주어진 컨텍스트로 공격 1회를 수행합니다. </summary>
        void Execute(in AttackContext ctx);
    }
}
