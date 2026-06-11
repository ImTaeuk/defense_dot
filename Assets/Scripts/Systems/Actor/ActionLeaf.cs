namespace DefenseDot.Systems.Actor
{
    /// <summary> 주어진 동작을 실행하고 그 결과 상태를 그대로 반환하는 리프입니다. </summary>
    public sealed class ActionLeaf : BTNode
    {
        private readonly System.Func<Blackboard, NodeStatus> action;

        /// <summary> 실행할 동작을 받습니다. </summary>
        public ActionLeaf(System.Func<Blackboard, NodeStatus> action) { this.action = action; }

        /// <summary> 동작을 실행하고 반환된 상태를 그대로 돌려줍니다. </summary>
        public override NodeStatus Evaluate(Blackboard blackboard)
        {
            return action(blackboard);
        }
    }
}
