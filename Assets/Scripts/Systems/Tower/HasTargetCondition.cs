using DefenseDot.Systems.Actor;

namespace DefenseDot.Systems.Tower
{
    /// <summary> 타워가 사거리 안의 유효한 타겟을 들고 있는지 판정합니다. </summary>
    public sealed class HasTargetCondition : ConditionNode
    {
        /// <summary> 타겟 보유를 물어볼 타워. </summary>
        private readonly TowerActor tower;

        /// <summary> 판정 대상 타워를 받습니다. </summary>
        /// <param name="tower">타겟 보유 여부를 아는 타워</param>
        public HasTargetCondition(TowerActor tower)
        {
            this.tower = tower;
        }

        /// <summary> 타워가 없으면 거짓입니다. </summary>
        /// <param name="blackboard">노드 간 공유 데이터(이 노드는 쓰지 않음)</param>
        protected override bool Check(Blackboard blackboard)
        {
            if (tower == null)
                return false;

            return tower.HasValidTarget();
        }
    }
}