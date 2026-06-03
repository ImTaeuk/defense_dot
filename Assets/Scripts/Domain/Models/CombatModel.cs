// 전투 집계(처치 수)와 처치 사건을 소유·통지하는 도메인 모델
using UnityEngine;

namespace DefenseDot.Domain.Models
{
    /// <summary>
    /// 전투 집계(처치 수)를 소유하고 적 처치 사건을 통지하는 도메인 모델입니다.
    /// </summary>
    [System.Serializable]
    public class CombatModel : BaseModel
    {
        [SerializeField] private int totalKills;

        /// <summary>
        /// 적이 처치되면 발생합니다. (획득할 골드 보상)
        /// </summary>
        [field: System.NonSerialized]
        public event System.Action<int> OnEnemyKilled;

        /// <summary>
        /// 누적 처치 수입니다.
        /// </summary>
        public int TotalKills => totalKills;

        /// <summary>
        /// 적 처치를 집계하고 보상 통지를 발행합니다.
        /// </summary>
        public void RegisterKill(int reward)
        {
            totalKills++;
            OnEnemyKilled?.Invoke(reward);
        }
    }
}
