namespace DefenseDot.Systems.Actor
{
    /// <summary>
    /// 판정만 하는 조건 노드의 베이스입니다. 참·거짓을 Success/Failure 로 옮기며,
    /// Evaluate 를 봉인해 조건이 Running 을 반환하지 못하게 막습니다.
    /// </summary>
    public abstract class ConditionNode : BTNode
    {
        /// <summary> 조건을 판정합니다. </summary>
        /// <param name="blackboard">노드 간 공유 데이터</param>
        protected abstract bool Check(Blackboard blackboard);

        /// <summary> 판정 결과를 Success/Failure 로 옮깁니다. </summary>
        /// <param name="blackboard">Check 에 그대로 넘길 공유 데이터</param>
        public sealed override NodeStatus Evaluate(Blackboard blackboard)
        {
            return Check(blackboard) ? NodeStatus.Success : NodeStatus.Failure;
        }
    }
}