// 골드 재화 상태를 소유·통지하는 도메인 모델
using UnityEngine;

namespace DefenseDot.Domain.Models
{
    /// <summary>
    /// 골드 재화 상태를 소유하고 변경을 통지하는 도메인 모델입니다.
    /// </summary>
    [System.Serializable]
    public class EconomyModel : BaseModel
    {
        [SerializeField] private int gold;

        /// <summary>
        /// 골드가 변경되면 발생합니다. (현재 골드 총량)
        /// </summary>
        [field: System.NonSerialized]
        public event System.Action<int> OnGoldChanged;

        /// <summary>
        /// 현재 소지 골드입니다.
        /// </summary>
        public int Gold
        {
            get => gold;
            private set { if (SetField(ref gold, value)) OnGoldChanged?.Invoke(gold); }
        }

        /// <summary>
        /// 초기 골드를 설정하고 통지합니다.
        /// </summary>
        public void Initialize(int startGold)
        {
            gold = -1;        // 강제 통지 위한 더미
            Gold = startGold;
        }

        /// <summary>
        /// 골드를 가산합니다. (적 처치 보상 등)
        /// </summary>
        public void AddGold(int amount)
        {
            if (amount == 0) return;
            Gold = gold + amount;
        }

        /// <summary>
        /// 비용을 감당할 수 있는지 확인합니다.
        /// </summary>
        public bool CanAfford(int cost) => gold >= cost;

        /// <summary>
        /// 비용을 차감합니다. 잔액이 부족하면 false를 반환합니다.
        /// </summary>
        public bool TrySpend(int cost)
        {
            if (!CanAfford(cost)) return false;
            Gold = gold - cost;
            return true;
        }
    }
}
