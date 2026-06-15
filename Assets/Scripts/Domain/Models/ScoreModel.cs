// 인-런 점수(처치·시간보너스)를 보유·통지하는 도메인 모델
using UnityEngine;

namespace DefenseDot.Domain.Models
{
    /// <summary>
    /// 인-런 점수를 보유하고 통지하는 도메인 모델입니다.
    /// 처치 점수와 라운드 조기 클리어 시간보너스를 가산합니다.
    /// </summary>
    [System.Serializable]
    public class ScoreModel : BaseModel
    {
        [SerializeField] private int score;

        /// <summary> 점수가 변경되면 발생합니다. (현재 점수) </summary>
        [field: System.NonSerialized]
        public event System.Action<int> OnScoreChanged;

        /// <summary> 현재 누적 점수입니다. </summary>
        public int Score => score;

        /// <summary> 처치 점수를 가산합니다. (floor(10 × 라운드 × 배율)) </summary>
        public void AddKillScore(int round, float multiplier = 1f)
        {
            int gained = Mathf.FloorToInt(10f * Mathf.Max(1, round) * Mathf.Max(0f, multiplier));
            if (gained <= 0) return;
            score += gained;
            OnScoreChanged?.Invoke(score);
        }

        /// <summary> 조기 클리어 시간보너스를 가산합니다. (floor(절약초 × 10 × 라운드 × 배율)) </summary>
        public void AddTimeBonus(float savedSeconds, int round, float multiplier = 1f)
        {
            int bonus = Mathf.FloorToInt(Mathf.Max(0f, savedSeconds) * 10f * Mathf.Max(1, round) * Mathf.Max(0f, multiplier));
            if (bonus <= 0) return;
            score += bonus;
            OnScoreChanged?.Invoke(score);
        }

        /// <summary> 점수를 0으로 초기화하고 통지합니다. (재시작용) </summary>
        public void Reset()
        {
            score = 0;
            OnScoreChanged?.Invoke(score);
        }
    }
}
