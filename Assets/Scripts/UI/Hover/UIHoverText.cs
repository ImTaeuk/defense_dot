// 고정 문구를 호버로 띄우는 범용 컴포넌트
using UnityEngine;
using UnityEngine.EventSystems;

namespace DefenseDot.UI.Hover
{
    /// <summary>
    /// 오브젝트에 고정 문구를 달아 호버 시 패널로 띄우는 범용 컴포넌트입니다.
    /// 문구가 상태에 따라 바뀌는 요소는 자기 위젯이 직접 IUIHoverable 을 구현합니다.
    /// </summary>
    public sealed class UIHoverText : MonoBehaviour, IUIHoverable, IPointerEnterHandler, IPointerExitHandler
    {
        /// <summary> 호버 시 패널에 띄울 문구. </summary>
        [SerializeField] [TextArea] private string text;

        /// <summary> 패널을 띄울 기준 위치. </summary>
        [SerializeField] private RectTransform hoverAnchor;

        /// <summary> 호버 시 표시할 내용을 만듭니다. </summary>
        public HoverContent BuildHoverContent()
        {
            return new HoverContent(text, hoverAnchor.position);
        }

        /// <summary> 포인터 진입을 중재자에 알립니다. </summary>
        /// <param name="eventData">포인터 이벤트 데이터</param>
        public void OnPointerEnter(PointerEventData eventData)
        {
            HoverMediator.NotifyEntered(this);
        }

        /// <summary> 포인터 이탈을 중재자에 알립니다. </summary>
        /// <param name="eventData">포인터 이벤트 데이터</param>
        public void OnPointerExit(PointerEventData eventData)
        {
            HoverMediator.NotifyExited(this);
        }

        /// <summary> 호버 중 꺼질 때 패널이 고아로 남지 않도록 이탈을 알립니다. </summary>
        private void OnDisable()
        {
            HoverMediator.NotifyExited(this);
        }
    }
}