using UnityEngine;
using UnityEngine.UI;
using TMPro;
using DefenseDot.UI.Base;
using DefenseDot.Systems.Abilities;

namespace DefenseDot.UI.Views
{
    /// <summary> 능력 강화 패널의 한 행. 아이콘·이름·레벨과 강화/삭제 버튼을 표시·통지합니다. </summary>
    public sealed class AbilityUpgradeRow : UIWidget<AbilityUpgradeRowData>
    {
        [SerializeField] private Image icon;
        [SerializeField] private TextMeshProUGUI nameText;
        [SerializeField] private TextMeshProUGUI levelText;
        [SerializeField] private Button upgradeButton;
        [SerializeField] private TextMeshProUGUI upgradeLabel;
        [SerializeField] private Button dismissButton;

        /// <summary> 강화 버튼 클릭 시 대상 능력을 통지합니다. </summary>
        public event System.Action<AbilityInstance> OnUpgrade;
        /// <summary> 삭제 버튼 클릭 시 대상 능력을 통지합니다. </summary>
        public event System.Action<AbilityInstance> OnDismiss;

        private AbilityInstance bound;

        /// <summary> 버튼 클릭을 이벤트로 연결합니다. </summary>
        private void Awake()
        {
            if (upgradeButton != null) upgradeButton.onClick.AddListener(() => OnUpgrade?.Invoke(bound));
            if (dismissButton != null) dismissButton.onClick.AddListener(() => OnDismiss?.Invoke(bound));
        }

        /// <summary> 능력 정보와 강화 상태(비용/MAX/구매가능)를 DTO로 반영합니다. </summary>
        public override void SetData(AbilityUpgradeRowData data)
        {
            bound = data.ability;
            if (icon != null) icon.sprite = data.ability.data.icon;
            if (nameText != null) nameText.text = data.ability.data.displayName;
            if (levelText != null) levelText.text = $"Lv{data.ability.level}";

            if (data.isMax)
            {
                if (upgradeLabel != null) upgradeLabel.text = "MAX";
                if (upgradeButton != null) upgradeButton.interactable = false;
            }
            else
            {
                if (upgradeLabel != null) upgradeLabel.text = $"강화 Lv{data.ability.level + 1} ({data.cost}G)";
                if (upgradeButton != null) upgradeButton.interactable = data.canAfford;
            }
        }
    }
}
