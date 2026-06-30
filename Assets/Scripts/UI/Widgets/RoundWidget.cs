using UnityEngine;
using TMPro;
using DefenseDot.UI.Base;
using DefenseDot.Domain.Models;

namespace DefenseDot.UI.Widgets
{
    /// <summary> 라운드(현재/전체)를 표시하는 위젯입니다. </summary>
    public sealed class RoundWidget : UIWidget<WaveProgress>
    {
        [SerializeField] private TextMeshProUGUI valueText;

        /// <summary> 라운드 표시를 갱신합니다. </summary>
        public override void SetData(WaveProgress data)
        {
            if (valueText != null) valueText.text = $"{data.Current} / {data.Total}";
        }
    }
}
