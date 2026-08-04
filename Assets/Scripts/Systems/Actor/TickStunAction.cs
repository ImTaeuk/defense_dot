using UnityEngine;
using DefenseDot.Core;

namespace DefenseDot.Systems.Actor
{
    /// <summary> 기절 시간을 소비하고 기절 상태를 유지시켜 주 행동을 차단합니다. </summary>
    public sealed class TickStunAction : ActionNode
    {
        /// <summary> 상태를 기록할 대상 액터. </summary>
        private readonly IActor actor;

        /// <summary> 기절 상태를 쓸 액터를 받습니다. </summary>
        /// <param name="actor">상태 기록 대상</param>
        public TickStunAction(IActor actor)
        {
            this.actor = actor;
        }

        /// <summary> 기절 시간을 깎고 Running 을 반환해 뒤 노드를 막습니다. </summary>
        /// <param name="blackboard">기절 타이머를 담은 공유 데이터</param>
        public override NodeStatus Evaluate(Blackboard blackboard)
        {
            blackboard.stunTimer -= Time.deltaTime;   // 외부가 기록한 기절 소비
            actor.SetState(ActorState.Stunned);
            return NodeStatus.Running;                // primary 차단
        }
    }
}