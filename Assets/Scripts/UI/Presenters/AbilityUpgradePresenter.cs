using System.Collections.Generic;
using DefenseDot.Domain;
using DefenseDot.Systems.Abilities;
using DefenseDot.UI.Base;
using DefenseDot.UI.Views;

namespace DefenseDot.UI.Presenters
{
    /// <summary> 능력 목록 패널 프레젠터. 로드아웃 변화를 구독해 행을 갱신합니다. </summary>
    public sealed class AbilityUpgradePresenter : UIPresenter<AbilityUpgradeView>
    {
        private readonly IAbilityCommandTarget core;
        private readonly List<AbilityInstance> buffer = new List<AbilityInstance>();

        /// <summary> GameContext에서 필요한 모델을 추출해 주입받습니다. </summary>
        public AbilityUpgradePresenter(AbilityUpgradeView view, GameContext ctx) : base(view)
        {
            core = ctx.CoreTarget;
        }

        /// <summary> 로드아웃 구독을 등록합니다. </summary>
        protected override void OnInitialize()
        {
            // 비-아레나(코어 없음)면 패널을 비활성 상태로 둠
            if (core == null) return;

            core.Loadout.OnChanged += RebuildRows;
            RebuildRows();
        }

        /// <summary> 등록한 구독을 해제합니다. </summary>
        protected override void OnDispose()
        {
            if (core == null) return;

            core.Loadout.OnChanged -= RebuildRows;
        }

        /// <summary> 현재 로드아웃(액티브+패시브)을 뷰에 반영합니다. </summary>
        private void RebuildRows()
        {
            buffer.Clear();
            buffer.AddRange(core.Loadout.Actives);
            buffer.AddRange(core.Loadout.Passives);
            view.Render(buffer);
        }
    }
}
