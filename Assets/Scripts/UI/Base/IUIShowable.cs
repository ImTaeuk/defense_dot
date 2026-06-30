namespace DefenseDot.UI.Base
{
    /// <summary> 표시/숨김이 가능한 UI 계약입니다. </summary>
    public interface IUIShowable
    {
        /// <summary> UI를 표시합니다. </summary>
        void Show();

        /// <summary> UI를 숨깁니다. </summary>
        void Hide();
    }
}
