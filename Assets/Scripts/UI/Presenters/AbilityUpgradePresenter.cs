using System.Collections.Generic;
using DefenseDot.Domain;
using DefenseDot.Domain.Models;
using DefenseDot.Systems.Abilities;
using DefenseDot.Systems.Economy;
using DefenseDot.UI.Base;
using DefenseDot.UI.Views;

namespace DefenseDot.UI.Presenters
{
    /// <summary> 능력 강화 패널 프레젠터. 로드아웃·골드 변화를 구독해 행을 갱신하고 강화/삭제를 위임합니다. </summary>
    public sealed class AbilityUpgradePresenter : UIPresenter<AbilityUpgradeView>
    {
        private readonly EconomyModel economy;
        private readonly IAbilityCommandTarget core;
        private readonly AbilityUpgradeService upgrades;
        private readonly List<AbilityInstance> buffer = new List<AbilityInstance>();

        /// <summary> GameContext에서 필요한 모델·서비스를 추출해 주입받습니다. </summary>
        public AbilityUpgradePresenter(AbilityUpgradeView view, GameContext ctx) : base(view)
        {
            economy = ctx.Economy;
            core = ctx.CoreTarget;
            upgrades = ctx.AbilityUpgrades;
        }

        /// <summary> 뷰·로드아웃·골드 구독을 등록합니다. </summary>
        protected override void OnInitialize()
        {
            // 비-아레나(코어/서비스 없음)면 패널을 비활성 상태로 둠
            if (core == null || upgrades == null) return;

            view.OnUpgrade += HandleUpgrade;
            view.OnDismiss += HandleDismiss;
            core.Loadout.OnChanged += RebuildRows;
            Bind(economy.Gold, _ => RebuildRows());   // 즉시 1회 + 이후 골드 변화 시 재갱신(자동 해제)
        }

        /// <summary> 등록한 구독을 해제합니다. </summary>
        protected override void OnDispose()
        {
            if (core == null || upgrades == null) return;

            view.OnUpgrade -= HandleUpgrade;
            view.OnDismiss -= HandleDismiss;
            core.Loadout.OnChanged -= RebuildRows;
        }

        /// <summary> 강화 요청을 서비스에 위임합니다. </summary>
        private void HandleUpgrade(AbilityInstance ability) => upgrades.TryUpgrade(ability);

        /// <summary> 삭제 요청을 서비스에 위임합니다. </summary>
        private void HandleDismiss(AbilityInstance ability) => upgrades.Dismiss(ability);

        /// <summary> 현재 로드아웃(액티브+패시브)을 뷰에 반영합니다. </summary>
        private void RebuildRows()
        {
            buffer.Clear();
            buffer.AddRange(core.Loadout.Actives);
            buffer.AddRange(core.Loadout.Passives);
            view.Render(buffer, Query);
        }

        /// <summary> 한 능력의 강화 상태(MAX/비용/구매가능)를 질의합니다. </summary>
        private (bool isMax, int cost, bool canAfford) Query(AbilityInstance ability)
        {
            bool isMax = upgrades.IsMaxLevel(ability);
            int cost = isMax ? 0 : upgrades.GetUpgradeCost(ability);
            bool canAfford = !isMax && economy.CanAfford(cost);
            return (isMax, cost, canAfford);
        }
    }
}
