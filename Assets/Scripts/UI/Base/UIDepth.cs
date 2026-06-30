namespace DefenseDot.UI.Base
{
    /// <summary> UI 렌더 깊이 계층입니다. (낮을수록 뒤, 높을수록 앞) </summary>
    public enum UIDepth
    {
        /// <summary> 상시 HUD 계층입니다. </summary>
        HUD = 0,

        /// <summary> 고정 오버레이 계층입니다. </summary>
        Fixed = 1,

        /// <summary> 팝업/모달 계층입니다. </summary>
        Popup = 2,

        /// <summary> 시스템 최상위 계층입니다. </summary>
        System = 3,
    }
}
