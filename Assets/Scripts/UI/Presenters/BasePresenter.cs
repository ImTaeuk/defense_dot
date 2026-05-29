using System;

namespace DefenseDot.UI
{
    /// <summary>
    /// UI 뷰의 기본 인터페이스입니다. 모든 UI View 클래스는 이를 상속받아야 합니다.
    /// </summary>
    public interface IView
    {
        /// <summary>
        /// UI 요소를 화면에 표시합니다.
        /// </summary>
        void Show();

        /// <summary>
        /// UI 요소를 화면에서 숨깁니다.
        /// </summary>
        void Hide();
    }

    /// <summary>
    /// UI 데이터 모델의 기본 클래스입니다.
    /// </summary>
    public abstract class BaseModel
    {
        // 데이터 중심의 모델 클래스를 위한 베이스
    }

    /// <summary>
    /// MVP 패턴의 Presenter 베이스 클래스입니다.
    /// View와 Model 사이의 중계 역할을 수행하며 로직을 담당합니다.
    /// </summary>
    /// <typeparam name="TView">제어할 View 타입 (IView 구현체)</typeparam>
    /// <typeparam name="TModel">사용할 데이터 Model 타입 (BaseModel 상속체)</typeparam>
    public abstract class BasePresenter<TView, TModel> where TView : IView where TModel : BaseModel
    {
        protected TView view;
        protected TModel model;

        /// <summary>
        /// BasePresenter의 생성자입니다.
        /// </summary>
        /// <param name="view">연결할 View 객체</param>
        /// <param name="model">연결할 Model 객체</param>
        public BasePresenter(TView view, TModel model)
        {
            this.view = view;
            this.model = model;
        }

        /// <summary>
        /// Presenter를 초기화하고 이벤트 구독 등을 수행합니다.
        /// </summary>
        public virtual void Initialize() { }

        /// <summary>
        /// Presenter가 파괴될 때 호출되며 이벤트 구독 해제 등을 수행합니다.
        /// </summary>
        public virtual void Dispose() { }
    }
}
