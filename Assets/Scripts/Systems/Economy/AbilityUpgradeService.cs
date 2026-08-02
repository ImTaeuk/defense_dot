// 골드를 내고 능력을 올리거나 버리던 조작기 — 현재 게임에서 쓰이지 않는다
//
// [무엇을 하나]
//   골드 지불과 능력 명령을 엮기만 한다. 능력을 실제로 올리고 지우는 주체는
//   IAbilityCommandTarget(CoreAbilitySystem 구현)이고, 골드 증감은 EconomyModel 이 한다.
//
// [왜 안 쓰이나]
//   2026-08-03 아레나에서 배선을 걷어냈다. 원작이 인게임 골드 강화를 스스로 폐지했는데
//   (Reference/dot-defense-main/index.html:26507 "Phase 3 — 인게임 골드 강화 폐지")
//   우리가 그 기능을 되살려 두고 있었기 때문이다. 삭제(환불)도 폐지된 강화비에 묶인
//   잔재라 함께 걷어냈다. 아레나의 능력 성장은 카드 획득과 합성 2단이 정본이다.
//
// [왜 남겨 두나]
//   그리드 모드를 도입하면 GameContext 배선만 되살려 그대로 쓸 수 있다.
//
// [되살릴 때 할 일]
//   이름을 함께 고친다. Service 접미는 CLAUDE.md 가 금지하는 엔터프라이즈 계층 용어이며,
//   무엇을 하는 타입인지 이름에서 읽히지 않는다.
using DefenseDot.Domain.Models;
using DefenseDot.Systems.Abilities;

namespace DefenseDot.Systems.Economy
{
    /// <summary> 골드를 내고 능력을 강화(레벨업)하거나 삭제(환불)합니다. 현재 사용처 없음 — 파일 상단 주석 참고. </summary>
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
