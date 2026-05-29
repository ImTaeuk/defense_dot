using DefenseDot.Core;

namespace DefenseDot.Systems.Tower
{
    /// <summary>
    /// 전투 행위(공격)가 가능한 액터가 구현해야 하는 인터페이스입니다.
    /// </summary>
    public interface ICombatActor : IActor
    {
        /// <summary> 현재 공격이 가능한 상태인지 반환 </summary>
        bool IsAttackableState();

        /// <summary> 공격을 수행 </summary>
        void PerformAttack();

        /// <summary> 공격 쿨타임 등을 위한 업데이트 </summary>
        void UpdateCombat(float deltaTime);
    }
}
