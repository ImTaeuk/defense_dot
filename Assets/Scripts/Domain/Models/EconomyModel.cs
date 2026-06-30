// 골드 재화 상태를 소유·통지하는 도메인 모델
using DefenseDot.Domain;

namespace DefenseDot.Domain.Models
{
    /// <summary>
    /// 골드 재화 상태를 소유하고 변경을 통지하는 도메인 모델입니다.
    /// </summary>
    public class EconomyModel : BaseModel
    {
        private readonly ReactiveProperty<int> gold = new ReactiveProperty<int>(0);

        /// <summary> 현재 소지 골드입니다. (읽기 전용 RP) </summary>
        public IReadOnlyReactiveProperty<int> Gold => gold;

        /// <summary> 초기 골드를 설정하고 강제 통지합니다. </summary>
        public void Initialize(int startGold)
        {
            gold.SetValueAndForceNotify(startGold);
        }

        /// <summary> 골드를 가산합니다. (적 처치 보상 등) </summary>
        public void AddGold(int amount)
        {
            if (amount == 0) return;
            gold.Value += amount;
        }

        /// <summary> 비용을 감당할 수 있는지 확인합니다. </summary>
        public bool CanAfford(int cost) => gold.Value >= cost;

        /// <summary> 비용을 차감합니다. 잔액이 부족하면 false를 반환합니다. </summary>
        public bool TrySpend(int cost)
        {
            if (!CanAfford(cost)) return false;
            gold.Value -= cost;
            return true;
        }
    }
}
