using UnityEngine;
using UnityEngine.UI;
using TMPro;
using DefenseDot.UI.Base;

namespace DefenseDot.UI.Widgets
{
    /// <summary> 타워 버튼 1개의 표시 데이터입니다. </summary>
    public readonly struct TowerButtonData
    {
        /// <summary> 타워 이름입니다. </summary>
        public readonly string Name;
        /// <summary> 비용입니다. </summary>
        public readonly int Cost;
        /// <summary> 구매 가능 여부입니다. </summary>
        public readonly bool Affordable;

        /// <summary> 이름·비용·구매가능으로 만듭니다. </summary>
        public TowerButtonData(string name, int cost, bool affordable)
        {
            Name = name; Cost = cost; Affordable = affordable;
        }
    }

    /// <summary> 구매 가능 타워 버튼 1개를 표시하는 위젯입니다. </summary>
    public sealed class TowerButtonWidget : UIWidget<TowerButtonData>
    {
        [SerializeField] private Button button;
        [SerializeField] private TextMeshProUGUI label;

        /// <summary> 클릭 중계를 위해 버튼을 노출합니다. </summary>
        public Button Button => button;

        /// <summary> 타워 이름·비용·구매가능을 반영합니다. </summary>
        public override void SetData(TowerButtonData data)
        {
            if (label != null) label.text = $"{data.Name}\n{data.Cost}G";
            if (button != null) button.interactable = data.Affordable;
        }
    }
}
