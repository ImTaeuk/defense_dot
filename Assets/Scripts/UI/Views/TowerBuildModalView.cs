using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using DefenseDot.Data;
using DefenseDot.UI.Base;
using DefenseDot.UI.Widgets;

namespace DefenseDot.UI.Views
{
    /// <summary> 빈 슬롯 선택 시 구매 가능 타워를 TowerButtonWidget 으로 나열하는 모달입니다. </summary>
    public sealed class TowerBuildModalView : UIView
    {
        [SerializeField] private GameObject panel;
        [SerializeField] private Transform buttonContainer;
        [SerializeField] private TowerButtonWidget buttonPrefab;

        /// <summary> 타워가 선택됨. </summary>
        public event System.Action<TowerData> OnTowerChosen;
        private readonly List<TowerButtonWidget> spawned = new List<TowerButtonWidget>();

        /// <summary> 로스터로 위젯을 구성하고 모달을 표시합니다. </summary>
        public void ShowTowers(TowerRoster roster, int gold)
        {
            Show();
            Clear();
            if (panel != null) panel.SetActive(true);
            if (roster != null && roster.towers != null)
            {
                foreach (TowerData tower in roster.towers)
                {
                    if (tower == null) continue;
                    TowerButtonWidget widget = Instantiate(buttonPrefab, buttonContainer);
                    widget.SetData(new TowerButtonData(tower.towerName, tower.cost, gold >= tower.cost));
                    TowerData captured = tower;
                    if (widget.Button != null)
                        widget.Button.onClick.AddListener(() => OnTowerChosen?.Invoke(captured));
                    spawned.Add(widget);
                    LayoutRebuilder.ForceRebuildLayoutImmediate((RectTransform)widget.transform);
                }
            }
            if (buttonContainer is RectTransform containerRect)
                LayoutRebuilder.ForceRebuildLayoutImmediate(containerRect);
        }

        /// <summary> 모달을 숨깁니다. </summary>
        protected override void OnHide()
        {
            if (panel != null) panel.SetActive(false);
        }

        private void Clear()
        {
            foreach (TowerButtonWidget widget in spawned)
            {
                if (widget == null) continue;
                widget.gameObject.SetActive(false);
                Destroy(widget.gameObject);
            }
            spawned.Clear();
        }
    }
}
