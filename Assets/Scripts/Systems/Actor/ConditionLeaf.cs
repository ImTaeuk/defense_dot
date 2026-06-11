namespace DefenseDot.Systems.Actor
{
    /// <summary> 술어가 참이면 Success, 거짓이면 Failure를 반환하는 리프입니다. </summary>
    public sealed class ConditionLeaf : BTNode
    {
        private readonly System.Func<Blackboard, bool> predicate;

        /// <summary> 평가할 술어를 받습니다. </summary>
        public ConditionLeaf(System.Func<Blackboard, bool> predicate) { this.predicate = predicate; }

        /// <summary> 술어 결과를 Success/Failure로 변환합니다. </summary>
        public override NodeStatus Evaluate(Blackboard blackboard)
        {
            return predicate(blackboard) ? NodeStatus.Success : NodeStatus.Failure;
        }
    }
}
