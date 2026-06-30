// 라운드 제한시간(남은/총)을 보유·통지하는 도메인 모델
using UnityEngine;
using DefenseDot.Domain;

namespace DefenseDot.Domain.Models
{
    /// <summary>
    /// 라운드 제한시간을 보유하고 통지하는 도메인 모델입니다.
    /// 외부(스포너)가 매 프레임 Tick하며, 만료 여부를 제공합니다.
    /// </summary>
    public class RoundTimerModel : BaseModel
    {
        private readonly ReactiveProperty<TimerState> time = new ReactiveProperty<TimerState>(new TimerState(0f, 0f));

        /// <summary> 남은/총 시간 상태입니다. (읽기 전용 RP) </summary>
        public IReadOnlyReactiveProperty<TimerState> Time => time;

        /// <summary> 남은 시간(초)입니다. </summary>
        public float Remaining => time.Value.Remaining;

        /// <summary> 이번 라운드의 총 제한시간(초)입니다. </summary>
        public float Duration => time.Value.Duration;

        /// <summary> 시간바 비율(남은/총)입니다. </summary>
        public float Ratio => time.Value.Ratio;

        /// <summary> 시간이 만료되었는지 여부입니다. </summary>
        public bool IsExpired => time.Value.Remaining <= 0f;

        /// <summary> 새 라운드의 제한시간을 설정하고 강제 통지합니다. </summary>
        public void StartWave(float waveDuration)
        {
            float duration = Mathf.Max(0f, waveDuration);
            time.SetValueAndForceNotify(new TimerState(duration, duration));
        }

        /// <summary> 경과 시간만큼 남은 시간을 줄이고 통지합니다. </summary>
        public void Tick(float deltaTime)
        {
            TimerState current = time.Value;
            if (current.Remaining <= 0f) return;
            float remaining = Mathf.Max(0f, current.Remaining - deltaTime);
            time.Value = new TimerState(remaining, current.Duration);
        }

        /// <summary> 남은·총 시간을 0으로 초기화하고 강제 통지합니다. (재시작용) </summary>
        public void Reset()
        {
            time.SetValueAndForceNotify(new TimerState(0f, 0f));
        }
    }
}
