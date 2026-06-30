using UnityEngine;
using UnityEngine.UI;
using TMPro;
using DefenseDot.UI.Base;
using DefenseDot.Domain.Models;

namespace DefenseDot.UI.Widgets
{
    /// <summary> 남은 시간과 시간바를 표시하는 위젯입니다. </summary>
    public sealed class TimeWidget : UIWidget<TimerState>
    {
        [SerializeField] private TextMeshProUGUI valueText;
        [SerializeField] private Image barFill;

        /// <summary> 시간 표시와 바를 갱신합니다. </summary>
        public override void SetData(TimerState data)
        {
            if (valueText != null) valueText.text = $"{Mathf.CeilToInt(data.Remaining)}s";
            if (barFill != null) barFill.fillAmount = Mathf.Clamp01(data.Ratio);
        }
    }
}
