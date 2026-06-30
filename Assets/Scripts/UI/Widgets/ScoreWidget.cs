using UnityEngine;
using TMPro;
using DefenseDot.UI.Base;

namespace DefenseDot.UI.Widgets
{
    /// <summary> 점수 수치를 표시하는 위젯입니다. </summary>
    public sealed class ScoreWidget : UIWidget<int>
    {
        [SerializeField] private TextMeshProUGUI valueText;

        /// <summary> 점수 표시를 갱신합니다. </summary>
        public override void SetData(int score)
        {
            if (valueText != null) valueText.text = score.ToString("N0");
        }
    }
}
