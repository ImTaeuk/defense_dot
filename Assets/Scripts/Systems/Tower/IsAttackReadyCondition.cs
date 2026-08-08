using DefenseDot.Systems.Actor;

namespace DefenseDot.Systems.Tower
{
    /// <summary> 기본 공격 주기가 찼는지 판정합니다. 주기는 트리 진입부가 깎습니다. </summary>
    public sealed class IsAttackReadyCondition : ConditionNode
    {
        /// <summary> 남은 주기가 0 이하면 발사할 때입니다. </summary>
        /// <param name="blackboard">공격 주기를 담고 있는 공유 데이터</param>
        protected override bool Check(Blackboard blackboard)
        {
            return blackboard.attackCooldown <= 0f;
        }
    }
}
