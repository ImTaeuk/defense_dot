// 활성 적 레지스트리 — 타겟 탐색용 단일 목록(SSOT)
using System.Collections.Generic;

namespace DefenseDot.Systems.Enemy
{
    /// <summary>
    /// 현재 살아있는 적들을 모아두는 레지스트리입니다. (POCO)
    /// 타워의 타겟 탐색과 패배 판정이 이 목록을 참조합니다.
    /// </summary>
    public class EnemyRegistry
    {
        private readonly List<MonsterActor> actors = new List<MonsterActor>();

        /// <summary>
        /// 현재 등록된 활성 적 목록입니다.
        /// </summary>
        public IReadOnlyList<MonsterActor> Actors => actors;

        /// <summary>
        /// 현재 활성 적 수입니다.
        /// </summary>
        public int Count => actors.Count;

        /// <summary>
        /// 적을 레지스트리에 등록합니다. (중복 방지)
        /// </summary>
        public void Register(MonsterActor actor)
        {
            if (actor != null && !actors.Contains(actor)) actors.Add(actor);
        }

        /// <summary>
        /// 적을 레지스트리에서 제거합니다.
        /// </summary>
        public void Unregister(MonsterActor actor)
        {
            actors.Remove(actor);
        }

        /// <summary>
        /// 모든 등록을 비웁니다.
        /// </summary>
        public void Clear() => actors.Clear();
    }
}
