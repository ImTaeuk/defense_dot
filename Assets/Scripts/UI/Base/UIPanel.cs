// Presenter 유무와 무관하게 스스로 켜고 끄는 패널 베이스
using UnityEngine;

namespace DefenseDot.UI.Base
{
    /// <summary>
    /// 표시/숨김과 표시 시점 훅을 제공하는 UI 패널 베이스입니다.
    /// Presenter 를 전제하지 않으므로 스스로 켜고 끄는 패널이 여기서 파생합니다.
    /// </summary>
    public abstract class UIPanel : UIObject, IUIShowable
    {
        /// <summary> 시작 시 활성/비활성 중 어느 쪽으로 둘지 정합니다. </summary>
        [SerializeField] private UIInitType initType = UIInitType.ActiveOnStart;

        /// <summary> 표시(Show)될 때 발생합니다. (Presenter 재반영용) </summary>
        public event System.Action OnShown;

        /// <summary> 시작 활성 상태를 적용합니다. </summary>
        protected virtual void Awake()
        {
            if (initType == UIInitType.InactiveOnStart)
                gameObject.SetActive(false);
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
        protected virtual void OnShow()
        {
        }

        /// <summary> 숨김 직전 훅입니다. </summary>
        protected virtual void OnHide()
        {
        }
    }
}