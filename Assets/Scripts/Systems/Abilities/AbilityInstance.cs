namespace DefenseDot.Systems.Abilities
{
    /// <summary> 한 능력의 런타임 상태입니다. (설계도=AbilityData, 상태=레벨·쿨다운) </summary>
    public sealed class AbilityInstance
    {
        /// <summary> 참조하는 정적 설계도. </summary>
        public readonly AbilityData data;
        /// <summary> 현재 레벨. </summary>
        public int level;
        /// <summary> 획득 라운드(강화비 스케일 기준). 스타터=1. </summary>
        public int acquiredRound = 1;
        /// <summary> 남은 쿨다운(초, 액티브용). </summary>
        public float cooldownRemaining;
        /// <summary> 효과 핸들·커스텀 런타임 상태(상시형 사용). </summary>
        public object runtimeState;

        public AbilityInstance(AbilityData data, int level = 1)
        {
            this.data = data;
            this.level = level;
        }
    }
}
