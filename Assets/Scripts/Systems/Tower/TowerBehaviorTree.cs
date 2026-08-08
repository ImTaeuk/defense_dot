using UnityEngine;
using DefenseDot.Systems.Actor;

namespace DefenseDot.Systems.Tower
{
    /// <summary> 타워의 주 행동: 사거리 내 타겟 공격(없으면 탐색·Idle). </summary>
    public sealed class TowerBehaviorTree : ActorBehaviorTree
    {
        /// <summary> 공격 주기를 먼저 진행시킨 뒤 트리를 평가합니다. </summary>
        public override void Tick()
        {
            // 타겟이 없어도 주기는 돈다. 0 아래로는 쌓지 않는다
            Blackboard.attackCooldown = Mathf.Max(0f, Blackboard.attackCooldown - Time.deltaTime);
            base.Tick();
        }

        /// <summary> 타겟이 있으면 공격, 없으면 탐색으로 떨어지는 트리를 조립합니다. </summary>
        protected override BTNode BuildPrimary()
        {
            TowerActor tower = actor as TowerActor;
            if (tower == null)
                Debug.LogError("[TowerBehaviorTree] 같은 GameObject 에 TowerActor 가 없습니다. 트리가 아무 일도 하지 않습니다.", this);

            return BT.Selector(
                BT.Sequence(
                    new HasTargetCondition(tower),
                    new IsAttackReadyCondition(),
                    new AttackAction(tower)),
                new AcquireTargetAction(tower));
        }
    }
}
