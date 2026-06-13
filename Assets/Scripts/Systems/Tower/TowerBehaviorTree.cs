using UnityEngine;
using DefenseDot.Core;
using DefenseDot.Systems.Actor;

namespace DefenseDot.Systems.Tower
{
    /// <summary> 타워의 주 행동: 사거리 내 타겟 공격(없으면 탐색·Idle). </summary>
    public sealed class TowerBehaviorTree : ActorBehaviorTree
    {
        protected override BTNode BuildPrimary()
        {
            return BT.Selector(
                BT.Sequence(
                    BT.Condition(bb => { TowerActor t = actor as TowerActor; return t != null && t.HasValidTarget(); }),
                    BT.Action(bb =>
                    {
                        actor.SetState(ActorState.Attacking);
                        (actor as TowerActor)?.UpdateCombat(Time.deltaTime);   // 쿨다운 시 PerformAttack
                        return NodeStatus.Running;
                    })),
                BT.Action(bb =>
                {
                    (actor as TowerActor)?.AcquireTarget();
                    actor.SetState(ActorState.Idle);
                    return NodeStatus.Success;
                }));
        }
    }
}
