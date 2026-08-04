using DefenseDot.Core;
using DefenseDot.Systems.Actor;

namespace DefenseDot.Systems.Tower
{
    /// <summary> 타겟을 새로 찾고 대기 상태로 돌립니다. (공격 가지가 실패했을 때의 기본 행동) </summary>
    public sealed class AcquireTargetAction : ActionNode
    {
        /// <summary> 타겟을 탐색할 타워. </summary>
        private readonly TowerActor tower;

        /// <summary> 탐색 주체 타워를 받습니다. </summary>
        /// <param name="tower">타겟을 찾을 타워</param>
        public AcquireTargetAction(TowerActor tower)
        {
            this.tower = tower;
        }

        /// <summary> 탐색은 매 프레임 끝나므로 항상 Success 입니다. </summary>
        /// <param name="blackboard">노드 간 공유 데이터(이 노드는 쓰지 않음)</param>
        public override NodeStatus Evaluate(Blackboard blackboard)
        {
            if (tower == null)
                return NodeStatus.Failure;

            tower.AcquireTarget();
            tower.SetState(ActorState.Idle);
            return NodeStatus.Success;
        }
    }
}