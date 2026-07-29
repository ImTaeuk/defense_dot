// 호버 대상이 전달할 데이터를 정의하도록 강제하는 계약
namespace DefenseDot.UI.Hover
{
    /// <summary> 호버 시 표시할 내용을 제공하는 UI 요소의 계약입니다. </summary>
    public interface IUIHoverable
    {
        /// <summary> 호버 시 패널에 표시할 내용을 만듭니다. </summary>
        HoverContent BuildHoverContent();
    }
}