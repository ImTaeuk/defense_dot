using System.Collections.Generic;

namespace DefenseDot.Systems.Actor
{
    /// <summary> 자식을 순서대로 평가하다 Failure가 아닌 결과(Success/Running)를 만나면 그 결과로 중단하는 Composite입니다. </summary>
    public sealed class Selector : BTNode
    {
        private readonly IReadOnlyList<BTNode> children;

        /// <summary> 평가할 자식 노드 목록을 받습니다. </summary>
        public Selector(IReadOnlyList<BTNode> children) { this.children = children; }

        /// <summary> 전부 Failure면 Failure, 아니면 첫 비-Failure 결과를 반환합니다. </summary>
        public override NodeStatus Evaluate(Blackboard blackboard)
        {
            // 반응형: 매 tick 처음부터
            for (int i = 0; i < children.Count; i++)
            {
                NodeStatus status = children[i].Evaluate(blackboard);
                if (status != NodeStatus.Failure) return status; // Failure 외 즉시 중단
            }
            return NodeStatus.Failure;
        }
    }
}
