using UnityEngine;
using UnityEngine.UI;
using TMPro;
using DefenseDot.UI.Base;
using DefenseDot.Domain.Models;

namespace DefenseDot.UI.Widgets
{
    /// <summary> 생존 적 수와 위험 바를 표시하는 위젯입니다. </summary>
    public sealed class EnemyWidget : UIWidget<EnemyState>
    {
        [SerializeField] private TextMeshProUGUI valueText;
        [SerializeField] private Image barFill;

        /// <summary> 적 수 표시와 위험 바를 갱신합니다. </summary>
        public override void SetData(EnemyState data)
        {
            if (valueText != null) valueText.text = $"{data.Alive} / {data.Capacity}";
            if (barFill != null) barFill.fillAmount = Mathf.Clamp01(data.Ratio);
        }
    }
}
