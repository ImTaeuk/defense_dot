using UnityEngine;
using DefenseDot.Core;
using DefenseDot.Systems.Actor;

namespace DefenseDot.Systems.Enemy
{
    /// <summary> 적의 주 행동: 주입된 이동 전략으로 이동(비공격). </summary>
    public sealed class EnemyBehaviorTree : ActorBehaviorTree
    {
        protected override BTNode BuildPrimary()
        {
            return BT.Action(bb =>
            {
                MonsterActor monster = actor as MonsterActor;
                if (monster == null) return NodeStatus.Failure;
                IMovementStrategy mv = monster.CurrentMovement;   // live-read
                if (mv == null) return NodeStatus.Failure;
                mv.Tick(Time.deltaTime);
                actor.SetState(ActorState.Moving);
                if (mv.HasReachedGoal)
                {
                    monster.HandleReachedGoal();
                    return NodeStatus.Success;
                }
                return NodeStatus.Running;
            });
        }
    }
}
