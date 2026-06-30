using UnityEngine;
using DefenseDot.Data;
using DefenseDot.Domain.Models;
using DefenseDot.Systems.Tower;
using DefenseDot.UI.Views;

namespace DefenseDot.UI.Presenters
{
    /// <summary>
    /// 슬롯 선택과 빌드 모달, 구매·설치를 중재하는 Presenter 입니다.
    /// 모달 표시 상태(선택 셀)만 다루므로 BaseModel 없이 IPresenter 를 직접 구현합니다.
    /// </summary>
    public class TowerBuildPresenter : IPresenter
    {
        private readonly TowerBuildModalView view;
        private readonly TowerRoster roster;
        private readonly EconomyModel economy;
        private readonly TowerPlacementController placement;
        private Vector2Int currentCell;

        /// <summary> 모달 뷰·로스터·경제 모델·배치 컨트롤러를 주입받습니다. </summary>
        public TowerBuildPresenter(TowerBuildModalView view, TowerRoster roster, EconomyModel economy, TowerPlacementController placement)
        {
            this.view = view;
            this.roster = roster;
            this.economy = economy;
            this.placement = placement;
        }

        /// <summary> 선택·구매 사건을 구독하고 모달을 초기 숨김 처리합니다. </summary>
        public void Initialize()
        {
            placement.OnSlotSelected += HandleSlotSelected;
            placement.OnSlotDeselected += HandleDeselected;
            view.OnTowerChosen += HandleTowerChosen;
            view.Hide();
        }

        /// <summary> 구독을 해제합니다. </summary>
        public void Dispose()
        {
            placement.OnSlotSelected -= HandleSlotSelected;
            placement.OnSlotDeselected -= HandleDeselected;
            view.OnTowerChosen -= HandleTowerChosen;
        }

        private void HandleSlotSelected(Vector2Int cell, Vector3 worldPos)
        {
            currentCell = cell;
            view.Show(roster, economy.Gold.Value);
        }

        private void HandleTowerChosen(TowerData data)
        {
            if (!economy.TrySpend(data.cost)) return;
            if (!placement.PlaceAt(currentCell, data)) economy.AddGold(data.cost);
            else view.Hide();
        }

        private void HandleDeselected()
        {
            view.Hide();
        }
    }
}
