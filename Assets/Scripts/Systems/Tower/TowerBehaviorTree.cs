using UnityEngine;
using DefenseDot.Systems.Actor;

namespace DefenseDot.Systems.Tower
{
    /// <summary> 타워의 주 행동: 사거리 내 타겟 공격(없으면 탐색·Idle). </summary>
    public sealed class TowerBehaviorTree : ActorBehaviorTree
    {
        /// <summary> 타겟이 있으면 공격, 없으면 탐색으로 떨어지는 트리를 조립합니다. </summary>
        protected override BTNode BuildPrimary()
        {
            TowerActor tower = actor as TowerActor;
            if (tower == null)
                Debug.LogError("[TowerBehaviorTree] 같은 GameObject 에 TowerActor 가 없습니다. 트리가 아무 일도 하지 않습니다.", this);

            return BT.Selector(
                BT.Sequence(
                    new HasTargetCondition(tower),
                    new AttackAction(tower)),
                new AcquireTargetAction(tower));
        }
    }
}
