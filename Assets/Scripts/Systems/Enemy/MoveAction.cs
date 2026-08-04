using UnityEngine;
using DefenseDot.Core;
using DefenseDot.Systems.Actor;

namespace DefenseDot.Systems.Enemy
{
    /// <summary> 적을 주입된 이동 전략대로 한 프레임 움직이고, 목표에 닿으면 도달 처리를 맡깁니다. </summary>
    public sealed class MoveAction : ActionNode
    {
        /// <summary> 이동시킬 적. 이동 전략은 이쪽이 들고 있다. </summary>
        private readonly MonsterActor monster;

        /// <summary> 이동 주체 적을 받습니다. </summary>
        /// <param name="monster">이동 전략을 보유한 적</param>
        public MoveAction(MonsterActor monster)
        {
            this.monster = monster;
        }

        /// <summary> 이동 중이면 Running, 목표 도달이면 Success, 전략이 없으면 Failure 입니다. </summary>
        /// <param name="blackboard">노드 간 공유 데이터(이 노드는 쓰지 않음)</param>
        public override NodeStatus Evaluate(Blackboard blackboard)
        {
            if (monster == null)
                return NodeStatus.Failure;

            IMovementStrategy movement = monster.CurrentMovement;   // live-read
            if (movement == null)
                return NodeStatus.Failure;

            movement.Tick(Time.deltaTime);
            monster.SetState(ActorState.Moving);

            if (movement.HasReachedGoal)
            {
                monster.HandleReachedGoal();
                return NodeStatus.Success;
            }

            return NodeStatus.Running;
        }
    }
}