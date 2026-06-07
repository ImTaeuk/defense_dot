namespace DefenseDot.UI.Presenters
{
    /// <summary>
    /// 프레젠터의 공통 수명 계약입니다. UI 합성 루트가 일괄로 초기화·해제합니다.
    /// </summary>
    public interface IPresenter
    {
        /// <summary>
        /// 구독·초기값 반영 등 시작 처리를 수행합니다.
        /// </summary>
        void Initialize();

        /// <summary>
        /// 구독 해제 등 정리 처리를 수행합니다.
        /// </summary>
        void Dispose();
    }
}
