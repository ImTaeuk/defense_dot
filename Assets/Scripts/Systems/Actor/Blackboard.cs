using DefenseDot.Core;

namespace DefenseDot.Systems.Actor
{
    /// <summary>
    /// BT 노드 간 공유 데이터입니다. (한 행동의 내부 상태는 POCO가 보유 — 여기엔 노드 간 공유만)
    /// </summary>
    public sealed class Blackboard
    {
        /// <summary> 현재 타겟(노드 간 공유 캐시). </summary>
        public ITargetable target;

        /// <summary> 남은 기절 시간(초). 외부 CC 시스템이 기록하고 BT가 소비합니다. </summary>
        public float stunTimer;
    }
}
