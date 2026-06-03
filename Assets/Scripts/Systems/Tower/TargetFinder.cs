// 타워 타겟 탐색 — 사거리 내 가장 가까운 적 선택(제곱거리 비교)
using UnityEngine;
using DefenseDot.Core;
using DefenseDot.Systems.Enemy;

namespace DefenseDot.Systems.Tower
{
    /// <summary>
    /// 타워의 사거리 내에서 타겟을 선택하는 POCO입니다.
    /// EnemyRegistry를 순회하며 제곱 거리로 최근접 적을 찾습니다. (sqrt 회피)
    /// </summary>
    public class TargetFinder
    {
        private readonly EnemyRegistry registry;

        public TargetFinder(EnemyRegistry registry)
        {
            this.registry = registry;
        }

        /// <summary>
        /// 원점에서 사거리 내 가장 가까운 활성 적을 반환합니다. 없으면 null입니다.
        /// </summary>
        public ITargetable FindNearest(Vector3 origin, float range)
        {
            if (registry == null) return null;

            float rangeSqr = range * range;
            ITargetable best = null;
            float bestSqr = float.MaxValue;

            var actors = registry.Actors;
            for (int i = 0; i < actors.Count; i++)
            {
                MonsterActor actor = actors[i];
                if (actor == null || !actor.IsActive) continue;

                float distSqr = (actor.Position - origin).sqrMagnitude;
                if (distSqr <= rangeSqr && distSqr < bestSqr)
                {
                    bestSqr = distSqr;
                    best = actor;
                }
            }
            return best;
        }
    }
}
