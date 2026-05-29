using System.Collections.Generic;
using UnityEngine;
using DefenseDot.Systems.Enemy;
using DefenseDot.Core;

namespace DefenseDot.Systems.Actor
{
    /// <summary>
    /// BT 노드의 실행 결과를 정의합니다.
    /// </summary>
    public enum NodeStatus { Running, Success, Failure }

    /// <summary>
    /// Behavior Tree의 모든 노드가 상속받는 베이스 클래스입니다.
    /// </summary>
    public abstract class BTNode
    {
        public abstract NodeStatus Evaluate();
    }

    /// <summary>
    /// 특정 셀로 이동하는 액션을 수행하는 BT 노드입니다.
    /// </summary>
    public class MoveToTargetNode : BTNode
    {
        private readonly MonsterActor actor;
        private readonly Vector2Int targetCell;
        private bool started;

        public MoveToTargetNode(MonsterActor actor, Vector2Int target)
        {
            this.actor = actor;
            targetCell = target;
        }

        public override NodeStatus Evaluate()
        {
            if (!started)
            {
                // PathfindingService 연동 로직이 MonsterActor 내부에 있거나 외부에서 주입되어야 함
                // 여기서는 예시로 MonsterActor의 이동 로직을 호출
                started = true;
            }

            return actor.CurrentState == ActorState.Moving ? NodeStatus.Running : NodeStatus.Success;
        }
    }
}
