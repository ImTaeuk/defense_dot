// 호버 내용을 받아 표시만 하는 공용 패널
using UnityEngine;
using TMPro;
using DefenseDot.UI.Base;

namespace DefenseDot.UI.Hover
{
    /// <summary> 호버 내용을 전달받아 표시만 하는 공용 텍스트 패널입니다. </summary>
    public sealed class UIHoverTextPanel : UIPanel
    {
        /// <summary> 호버 내용을 그려낼 텍스트 컴포넌트. </summary>
        [SerializeField] private TextMeshProUGUI label;

        /// <summary> 중재자 구독을 시작합니다. </summary>
        protected override void Awake()
        {
            base.Awake();
            HoverMediator.OnHoverEntered += HandleHoverEntered;
            HoverMediator.OnHoverExited += HandleHoverExited;
        }

        /// <summary> 중재자 구독을 해제합니다. </summary>
        private void OnDestroy()
        {
            HoverMediator.OnHoverEntered -= HandleHoverEntered;
            HoverMediator.OnHoverExited -= HandleHoverExited;
        }

        /// <summary> 전달받은 내용으로 텍스트와 위치를 세팅하고 표시합니다. </summary>
        /// <param name="content">표시할 호버 내용</param>
        private void HandleHoverEntered(HoverContent content)
        {
            label.text = content.Text;
            RectTransform.position = content.Position;
            Show();
        }

        /// <summary> 패널을 숨깁니다. </summary>
        private void HandleHoverExited()
        {
            Hide();
        }
    }
}