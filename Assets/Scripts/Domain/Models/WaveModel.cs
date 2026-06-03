// 웨이브 진행 상태(현재/전체/남은 적)를 소유·통지하는 도메인 모델
using UnityEngine;

namespace DefenseDot.Domain.Models
{
    /// <summary>
    /// 웨이브 진행 상태(현재/전체/남은 적)를 소유하고 통지하는 도메인 모델입니다.
    /// </summary>
    [System.Serializable]
    public class WaveModel : BaseModel
    {
        [SerializeField] private int current;
        [SerializeField] private int total;
        [SerializeField] private int remaining;

        /// <summary>
        /// 웨이브 단계가 변경되면 발생합니다. (현재 웨이브, 전체 웨이브)
        /// </summary>
        [field: System.NonSerialized]
        public event System.Action<int, int> OnWaveChanged;

        /// <summary>
        /// 남은 적 수가 변경되면 발생합니다. (남은 적 수)
        /// </summary>
        [field: System.NonSerialized]
        public event System.Action<int> OnRemainingChanged;

        /// <summary>
        /// 한 웨이브의 적을 모두 소탕하면 발생합니다.
        /// </summary>
        [field: System.NonSerialized]
        public event System.Action OnWaveCleared;

        /// <summary>
        /// 현재 웨이브 번호입니다.
        /// </summary>
        public int Current => current;

        /// <summary>
        /// 전체 웨이브 수입니다.
        /// </summary>
        public int Total => total;

        /// <summary>
        /// 현재 남아있는 적 수입니다.
        /// </summary>
        public int Remaining => remaining;

        /// <summary>
        /// 마지막 웨이브 여부입니다.
        /// </summary>
        public bool IsLastWave => current >= total;

        /// <summary>
        /// 웨이브 단계를 설정하고 통지합니다.
        /// </summary>
        public void SetWave(int currentWave, int totalWaves)
        {
            current = currentWave;
            total = totalWaves;
            OnWaveChanged?.Invoke(current, total);
        }

        /// <summary>
        /// 남은 적 수를 설정하고 통지합니다.
        /// </summary>
        public void SetRemaining(int value)
        {
            if (SetField(ref remaining, value)) OnRemainingChanged?.Invoke(remaining);
        }

        /// <summary>
        /// 한 웨이브 소탕을 통지합니다. (소탕 판정은 호출자가 결정)
        /// </summary>
        public void MarkWaveCleared()
        {
            OnWaveCleared?.Invoke();
        }
    }
}
