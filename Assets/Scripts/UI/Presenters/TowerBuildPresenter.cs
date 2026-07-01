using UnityEngine;
using DefenseDot.Data;
using DefenseDot.Domain;
using DefenseDot.Domain.Models;
using DefenseDot.Systems.Tower;
using DefenseDot.UI.Base;
using DefenseDot.UI.Views;

namespace DefenseDot.UI.Presenters
{
    /// <summary> 슬롯 선택·빌드 모달·구매 설치를 중재하는 Presenter 입니다. </summary>
    public sealed class TowerBuildPresenter : UIPresenter<TowerBuildModalView>
    {
        private readonly TowerRoster roster;
        private readonly EconomyModel economy;
        private readonly TowerPlacementController placement;
        private Vector2Int currentCell;

        /// <summary> View 와 GameContext 를 주입받습니다. </summary>
        public TowerBuildPresenter(TowerBuildModalView view, GameContext ctx) : base(view)
        {
            roster = ctx.Roster;
            economy = ctx.Economy;
            placement = ctx.Placement;
        }

        protected override void OnInitialize()
        {
            if (placement != null)
            {
                placement.OnSlotSelected += HandleSlotSelected;
                placement.OnSlotDeselected += HandleDeselected;
            }
            view.OnTowerChosen += HandleTowerChosen;
            view.Hide();
        }

        protected override void OnDispose()
        {
            if (placement != null)
            {
                placement.OnSlotSelected -= HandleSlotSelected;
                placement.OnSlotDeselected -= HandleDeselected;
            }
            view.OnTowerChosen -= HandleTowerChosen;
        }

        private void HandleSlotSelected(Vector2Int cell, Vector3 worldPos)
        {
            currentCell = cell;
            view.ShowTowers(roster, economy.Gold.Value);
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
