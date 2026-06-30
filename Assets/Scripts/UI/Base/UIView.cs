// 위젯들로 구성된 패널 — Presenter를 모른다
using UnityEngine;

namespace DefenseDot.UI.Base
{
    /// <summary>
    /// 위젯들로 구성된 UI 패널 베이스입니다. 표시/숨김과 표시 시점 훅을 제공합니다.
    /// View는 Presenter를 알지 못하며, 표시 시 OnShown으로만 통지합니다.
    /// </summary>
    public abstract class UIView : UIObject, IUIShowable
    {
        [SerializeField] private UIInitType initType = UIInitType.ActiveOnStart;

        /// <summary> 표시(Show)될 때 발생합니다. (Presenter 재반영용) </summary>
        public event System.Action OnShown;

        /// <summary> 시작 활성 상태를 적용합니다. </summary>
        protected virtual void Awake()
        {
            if (initType == UIInitType.InactiveOnStart) gameObject.SetActive(false);
        }

        /// <summary> 패널을 표시하고 OnShow/OnShown을 통지합니다. </summary>
        public void Show()
        {
            gameObject.SetActive(true);
            OnShow();
            OnShown?.Invoke();
        }

        /// <summary> 패널을 숨깁니다. </summary>
        public void Hide()
        {
            OnHide();
            gameObject.SetActive(false);
        }

        /// <summary> 표시 직후 훅입니다. </summary>
        protected virtual void OnShow() { }

        /// <summary> 숨김 직전 훅입니다. </summary>
        protected virtual void OnHide() { }
    }
}
