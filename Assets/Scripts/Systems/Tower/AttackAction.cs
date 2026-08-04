using UnityEngine;
using DefenseDot.Core;
using DefenseDot.Systems.Actor;

namespace DefenseDot.Systems.Tower
{
    /// <summary> 공격 상태로 바꾸고 전투를 한 프레임 진행시킵니다. </summary>
    public sealed class AttackAction : ActionNode
    {
        /// <summary> 공격을 수행할 타워. </summary>
        private readonly TowerActor tower;

        /// <summary> 공격 주체 타워를 받습니다. </summary>
        /// <param name="tower">전투를 진행시킬 타워</param>
        public AttackAction(TowerActor tower)
        {
            this.tower = tower;
        }

        /// <summary> 공격이 이어지는 동안 Running 을 반환합니다. </summary>
        /// <param name="blackboard">노드 간 공유 데이터(이 노드는 쓰지 않음)</param>
        public override NodeStatus Evaluate(Blackboard blackboard)
        {
            if (tower == null)
                return NodeStatus.Failure;

            tower.SetState(ActorState.Attacking);
            tower.UpdateCombat(Time.deltaTime);   // 쿨다운 시 PerformAttack
            return NodeStatus.Running;
        }
    }
}