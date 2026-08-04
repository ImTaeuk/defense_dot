using UnityEngine;
using DefenseDot.Systems.Actor;

namespace DefenseDot.Systems.Enemy
{
    /// <summary> 적의 주 행동: 주입된 이동 전략으로 이동(비공격). </summary>
    public sealed class EnemyBehaviorTree : ActorBehaviorTree
    {
        /// <summary> 이동 노드 하나로 이루어진 트리를 조립합니다. </summary>
        protected override BTNode BuildPrimary()
        {
            MonsterActor monster = actor as MonsterActor;
            if (monster == null)
                Debug.LogError("[EnemyBehaviorTree] 같은 GameObject 에 MonsterActor 가 없습니다. 트리가 아무 일도 하지 않습니다.", this);

            return new MoveAction(monster);
        }
    }
}
