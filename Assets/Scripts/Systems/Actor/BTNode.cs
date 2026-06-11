namespace DefenseDot.Systems.Actor
{
    /// <summary> 모든 BT 노드의 베이스입니다. 평가 시 공유 Blackboard를 전달받습니다. </summary>
    public abstract class BTNode
    {
        /// <summary> 노드를 1회 평가하고 결과 상태를 반환합니다. </summary>
        public abstract NodeStatus Evaluate(Blackboard blackboard);
    }
}
