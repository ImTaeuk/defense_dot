using UnityEngine;
using TMPro;
using DefenseDot.UI.Base;

namespace DefenseDot.UI.Widgets
{
    /// <summary> 골드 수치를 표시하는 위젯입니다. </summary>
    public sealed class GoldWidget : UIWidget<int>
    {
        [SerializeField] private TextMeshProUGUI valueText;

        /// <summary> 골드 표시를 갱신합니다. </summary>
        public override void SetData(int gold)
        {
            if (valueText != null) valueText.text = gold.ToString();
        }
    }
}
