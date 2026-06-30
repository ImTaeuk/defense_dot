// View(제네릭)와 도메인 RP를 잇는 Presenter 베이스 — Model은 필드로 직접 보유
using System.Collections.Generic;
using DefenseDot.Domain;
using DefenseDot.UI.Presenters;

namespace DefenseDot.UI.Base
{
    /// <summary>
    /// View만 제네릭으로 받는 Presenter 베이스입니다. Model은 파생 클래스가 필드로 보유합니다.
    /// RP를 Bind하면 구독 즉시 초기값이 반영되고, View 표시 시 현재값이 재반영됩니다.
    /// </summary>
    /// <typeparam name="TView">제어할 View 타입</typeparam>
    public abstract class UIPresenter<TView> : IPresenter where TView : UIView
    {
        /// <summary> 제어 대상 View입니다. </summary>
        protected readonly TView view;

        private readonly List<System.IDisposable> bindings = new List<System.IDisposable>();
        private readonly List<System.Action> refreshers = new List<System.Action>();
        private bool initialized;

        /// <summary> View를 주입받습니다. </summary>
        protected UIPresenter(TView view)
        {
            this.view = view;
        }

        /// <summary> 구독을 등록하고 표시 재반영 훅을 연결합니다. (재진입 무시) </summary>
        public void Initialize()
        {
            if (initialized) return;
            initialized = true;
            if (view != null) view.OnShown += Refresh;
            OnInitialize();
        }

        /// <summary> 모든 구독을 해제합니다. </summary>
        public void Dispose()
        {
            if (!initialized) return;
            initialized = false;
            if (view != null) view.OnShown -= Refresh;
            foreach (System.IDisposable binding in bindings) binding.Dispose();
            bindings.Clear();
            refreshers.Clear();
            OnDispose();
        }

        /// <summary> 구독·바인딩을 등록하는 파생 훅입니다. </summary>
        protected abstract void OnInitialize();

        /// <summary> 추가 정리 훅입니다. </summary>
        protected virtual void OnDispose() { }

        /// <summary> RP를 구독해 핸들러에 연결하고, 해제 토큰과 재반영을 집계합니다. </summary>
        protected void Bind<V>(IReadOnlyReactiveProperty<V> source, System.Action<V> onValue)
        {
            if (source == null || onValue == null) return;
            bindings.Add(source.Subscribe(onValue));
            refreshers.Add(() => onValue(source.Value));
        }

        private void Refresh()
        {
            foreach (System.Action refresher in refreshers) refresher();
        }
    }
}
