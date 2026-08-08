using DefenseDot.Core;
using DefenseDot.Systems.Actor;

namespace DefenseDot.Systems.Tower
{
    /// <summary> 공격 상태로 바꾸고 기본 공격을 1회 쏜 뒤 다음 주기를 적어둡니다. </summary>
    public sealed class AttackAction : ActionNode
    {
        /// <summary> 공격을 수행할 타워. </summary>
        private readonly TowerActor tower;

        /// <summary> 공격 주체 타워를 받습니다. </summary>
        /// <param name="tower">기본 공격을 쏠 타워</param>
        public AttackAction(TowerActor tower)
        {
            this.tower = tower;
        }

        /// <summary> 공격이 이어지는 동안 Running 을 반환합니다. </summary>
        /// <param name="blackboard">다음 주기를 적어둘 공유 데이터</param>
        public override NodeStatus Evaluate(Blackboard blackboard)
        {
            if (tower == null)
                return NodeStatus.Failure;

            tower.SetState(ActorState.Attacking);

            // 못 쐈으면 주기를 유지한다
            float interval = tower.TryFireBasic();
            if (interval > 0f)
                blackboard.attackCooldown = interval;

            return NodeStatus.Running;
        }
    }
}
