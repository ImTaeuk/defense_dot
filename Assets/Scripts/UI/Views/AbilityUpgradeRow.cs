using UnityEngine;
using UnityEngine.UI;
using TMPro;
using DefenseDot.UI.Base;

namespace DefenseDot.UI.Views
{
    /// <summary> 능력 목록의 한 행. 아이콘·이름·레벨을 표시합니다. </summary>
    public sealed class AbilityUpgradeRow : UIWidget<AbilityUpgradeRowData>
    {
        [SerializeField] private Image icon;
        [SerializeField] private TextMeshProUGUI nameText;
        [SerializeField] private TextMeshProUGUI levelText;

        /// <summary> 능력 정보를 행에 반영합니다. </summary>
        public override void SetData(AbilityUpgradeRowData data)
        {
            if (icon != null) icon.sprite = data.ability.data.icon;
            if (nameText != null) nameText.text = data.ability.data.displayName;
            if (levelText != null) levelText.text = $"Lv{data.ability.level}";
        }
    }
}
