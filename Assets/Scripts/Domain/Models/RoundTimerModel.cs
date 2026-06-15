// 라운드 제한시간(남은/총)을 보유·통지하는 도메인 모델
using UnityEngine;

namespace DefenseDot.Domain.Models
{
    /// <summary>
    /// 라운드 제한시간을 보유하고 통지하는 도메인 모델입니다.
    /// 외부(스포너)가 매 프레임 Tick하며, 만료 여부를 제공합니다.
    /// </summary>
    [System.Serializable]
    public class RoundTimerModel : BaseModel
    {
        [SerializeField] private float remaining;
        [SerializeField] private float duration;

        /// <summary> 남은/총 시간이 변경되면 발생합니다. (남은초, 총초) </summary>
        [field: System.NonSerialized]
        public event System.Action<float, float> OnTimeChanged;

        /// <summary> 남은 시간(초)입니다. </summary>
        public float Remaining => remaining;

        /// <summary> 이번 라운드의 총 제한시간(초)입니다. </summary>
        public float Duration => duration;

        /// <summary> 시간바 비율(남은/총)입니다. </summary>
        public float Ratio => duration > 0f ? remaining / duration : 0f;

        /// <summary> 시간이 만료되었는지 여부입니다. </summary>
        public bool IsExpired => remaining <= 0f;

        /// <summary> 새 라운드의 제한시간을 설정하고 통지합니다. </summary>
        public void StartWave(float waveDuration)
        {
            duration = Mathf.Max(0f, waveDuration);
            remaining = duration;
            OnTimeChanged?.Invoke(remaining, duration);
        }

        /// <summary> 경과 시간만큼 남은 시간을 줄이고 통지합니다. </summary>
        public void Tick(float deltaTime)
        {
            if (remaining <= 0f) return;
            remaining = Mathf.Max(0f, remaining - deltaTime);
            OnTimeChanged?.Invoke(remaining, duration);
        }

        /// <summary> 남은·총 시간을 0으로 초기화하고 통지합니다. (재시작용) </summary>
        public void Reset()
        {
            remaining = 0f;
            duration = 0f;
            OnTimeChanged?.Invoke(remaining, duration);
        }
    }
}
