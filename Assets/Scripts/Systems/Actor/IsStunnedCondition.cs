namespace DefenseDot.Systems.Actor
{
    /// <summary> 남은 기절 시간이 있는지 판정합니다. </summary>
    public sealed class IsStunnedCondition : ConditionNode
    {
        /// <summary> 기절 타이머가 남아 있으면 참입니다. </summary>
        /// <param name="blackboard">기절 타이머를 담은 공유 데이터</param>
        protected override bool Check(Blackboard blackboard)
        {
            return blackboard.stunTimer > 0f;
        }
    }
}