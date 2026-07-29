// Presenter 가 자동 배선되는 패널이라는 타입 표식
namespace DefenseDot.UI.Base
{
    /// <summary>
    /// Presenter 가 자동 배선되는 UI 패널 베이스입니다.
    /// 표시/숨김은 UIPanel 이 담당하며, 이 타입은 UIRoot.views 등록 대상을 가르는 경계로만 존재합니다.
    /// </summary>
    public abstract class UIView : UIPanel
    {
    }
}