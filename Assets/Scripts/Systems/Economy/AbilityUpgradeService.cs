using DefenseDot.Domain.Models;
using DefenseDot.Systems.Abilities;

namespace DefenseDot.Systems.Economy
{
    /// <summary> 골드로 능력을 강화(레벨업)하거나 삭제(환불)하는 인-런 조작 서비스입니다. (조율 + UI Facade) </summary>
    public sealed class AbilityUpgradeService
    {
        private readonly IAbilityCommandTarget core;      // 능력 레벨업/삭제 명령 대상
        private readonly EconomyModel economy;         // 골드 차감/가산
        private readonly AbilityUpgradeConfig config;  // 비용 곡선 파라미터

        /// <summary> 명령 대상·경제 모델·비용 설정을 주입받습니다. </summary>
        public AbilityUpgradeService(IAbilityCommandTarget core, EconomyModel economy, AbilityUpgradeConfig config)
        {
            this.core = core;
            this.economy = economy;
            this.config = config;
        }

        /// <summary> 다음 레벨 강화 비용입니다. </summary>
        public int GetUpgradeCost(AbilityInstance ability) => ability.UpgradeCost(config);

        /// <summary> 최대 레벨 도달 여부입니다. </summary>
        public bool IsMaxLevel(AbilityInstance ability) => ability.level >= ability.data.maxLevel;

        /// <summary> 강화 가능 여부(비최대 + 골드 충분)입니다. </summary>
        public bool CanUpgrade(AbilityInstance ability)
        {
            return !IsMaxLevel(ability) && economy.CanAfford(GetUpgradeCost(ability));
        }

        /// <summary> 삭제 시 환급액입니다. </summary>
        public int GetRefund(AbilityInstance ability) => ability.RefundValue(config);

        /// <summary> 강화를 시도합니다. MAX·골드부족이면 아무 변화 없이 false. </summary>
        public bool TryUpgrade(AbilityInstance ability)
        {
            if (IsMaxLevel(ability)) return false;
            if (!economy.TrySpend(GetUpgradeCost(ability))) return false;
            core.LevelUpAbility(ability);
            return true;
        }

        /// <summary> 능력을 삭제하고 강화비 일부를 환급합니다. </summary>
        public void Dismiss(AbilityInstance ability)
        {
            economy.AddGold(GetRefund(ability));
            core.RemoveAbility(ability);
        }
    }
}
