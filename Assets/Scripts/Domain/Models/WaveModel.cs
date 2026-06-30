// 웨이브 진행 상태(현재/전체/남은 적)를 소유·통지하는 도메인 모델
using DefenseDot.Domain;

namespace DefenseDot.Domain.Models
{
    /// <summary>
    /// 웨이브 진행 상태(현재/전체/남은 적)를 소유하고 통지하는 도메인 모델입니다.
    /// </summary>
    public class WaveModel : BaseModel
    {
        private readonly ReactiveProperty<WaveProgress> progress = new ReactiveProperty<WaveProgress>(new WaveProgress(0, 0));
        private readonly ReactiveProperty<int> remaining = new ReactiveProperty<int>(0);

        /// <summary> 웨이브 진행(현재/전체) 상태입니다. (읽기 전용 RP) </summary>
        public IReadOnlyReactiveProperty<WaveProgress> Progress => progress;

        /// <summary> 남은 적 수입니다. (읽기 전용 RP) </summary>
        public IReadOnlyReactiveProperty<int> RemainingEnemies => remaining;

        /// <summary> 현재 웨이브 번호입니다. </summary>
        public int Current => progress.Value.Current;

        /// <summary> 전체 웨이브 수입니다. </summary>
        public int Total => progress.Value.Total;

        /// <summary> 현재 남아있는 적 수입니다. </summary>
        public int Remaining => remaining.Value;

        /// <summary> 마지막 웨이브 여부입니다. </summary>
        public bool IsLastWave => progress.Value.Current >= progress.Value.Total;

        /// <summary> 한 웨이브의 적을 모두 소탕하면 발생합니다. </summary>
        public event System.Action OnWaveCleared;

        /// <summary> 웨이브 단계를 설정하고 강제 통지합니다. </summary>
        public void SetWave(int currentWave, int totalWaves)
        {
            progress.SetValueAndForceNotify(new WaveProgress(currentWave, totalWaves));
        }

        /// <summary> 남은 적 수를 설정하고 통지합니다. (동일값은 생략) </summary>
        public void SetRemaining(int value)
        {
            remaining.Value = value;
        }

        /// <summary> 한 웨이브 소탕을 통지합니다. (소탕 판정은 호출자가 결정) </summary>
        public void MarkWaveCleared()
        {
            OnWaveCleared?.Invoke();
        }
    }
}
