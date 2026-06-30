// HUD 위젯·모델이 원자적으로 주고받는 표시 상태 값 묶음
namespace DefenseDot.Domain.Models
{
    /// <summary> 웨이브 진행(현재/전체) 표시 상태입니다. </summary>
    public readonly struct WaveProgress
    {
        /// <summary> 현재 웨이브 번호입니다. </summary>
        public readonly int Current;

        /// <summary> 전체 웨이브 수입니다. </summary>
        public readonly int Total;

        /// <summary> 현재/전체로 진행 상태를 만듭니다. </summary>
        public WaveProgress(int current, int total)
        {
            Current = current;
            Total = total;
        }
    }

    /// <summary> 라운드 제한시간(남은/총/비율) 표시 상태입니다. </summary>
    public readonly struct TimerState
    {
        /// <summary> 남은 시간(초)입니다. </summary>
        public readonly float Remaining;

        /// <summary> 총 제한시간(초)입니다. </summary>
        public readonly float Duration;

        /// <summary> 남은/총 비율(0~1)입니다. </summary>
        public readonly float Ratio;

        /// <summary> 남은·총 시간으로 상태를 만들고 비율을 계산합니다. </summary>
        public TimerState(float remaining, float duration)
        {
            Remaining = remaining;
            Duration = duration;
            Ratio = duration > 0f ? remaining / duration : 0f;
        }
    }

    /// <summary> 코어 체력(현재/최대/비율) 표시 상태입니다. </summary>
    public readonly struct HealthState
    {
        /// <summary> 현재 체력입니다. </summary>
        public readonly float Hp;

        /// <summary> 최대 체력입니다. </summary>
        public readonly float MaxHp;

        /// <summary> 현재/최대 비율(0~1)입니다. </summary>
        public readonly float Ratio;

        /// <summary> 현재·최대 체력으로 상태를 만들고 비율을 계산합니다. </summary>
        public HealthState(float hp, float maxHp)
        {
            Hp = hp;
            MaxHp = maxHp;
            Ratio = maxHp > 0f ? hp / maxHp : 0f;
        }
    }

    /// <summary> 생존 적/수용 한계(비율) 표시 상태입니다. </summary>
    public readonly struct EnemyState
    {
        /// <summary> 생존 적 수입니다. </summary>
        public readonly int Alive;

        /// <summary> 적 수용 한계입니다. </summary>
        public readonly int Capacity;

        /// <summary> 위험 비율(0~1)입니다. </summary>
        public readonly float Ratio;

        /// <summary> 생존 적·수용 한계로 상태를 만들고 비율을 계산합니다. </summary>
        public EnemyState(int alive, int capacity)
        {
            Alive = alive;
            Capacity = capacity;
            Ratio = capacity > 0 ? alive / (float)capacity : 0f;
        }
    }
}
