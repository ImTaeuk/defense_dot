using System.Collections.Generic;
using UnityEngine;
using DefenseDot.Domain;
using DefenseDot.UI.Base;
using DefenseDot.UI.Presenters;

namespace DefenseDot.UI.InGame
{
    /// <summary>
    /// UI 합성 루트입니다. 주입된 GameContext 로 팩토리를 만들어 등록된 View 들의 Presenter 를 자동 배선하고,
    /// 각 View 를 Depth 레이어에 배치합니다. 새 UI 는 View+Presenter 1쌍을 만들고 리스트에 등록하면 됩니다.
    /// </summary>
    public sealed class UIRoot : MonoBehaviour
    {
        [SerializeField] private List<UIView> views = new List<UIView>();

        [Header("Depth Layers (Canvas 우선순위 순)")]
        [SerializeField] private RectTransform hudLayer;
        [SerializeField] private RectTransform fixedLayer;
        [SerializeField] private RectTransform popupLayer;
        [SerializeField] private RectTransform systemLayer;

        private readonly List<IPresenter> presenters = new List<IPresenter>();

        /// <summary> 컨텍스트를 받아 각 View 를 Depth 레이어에 배치하고 Presenter 를 생성·초기화합니다. </summary>
        public void Inject(GameContext ctx)
        {
            var factory = new DefenseDot.UI.UIPresenterFactory(ctx);
            foreach (UIView view in views)
            {
                if (view == null) continue;
                PlaceByDepth(view);
                IPresenter presenter = factory.Create(view);
                if (presenter == null) continue;
                presenters.Add(presenter);
                presenter.Initialize();
            }
        }

        /// <summary> UIObject 를 Depth 에 맞는 레이어의 자식으로 배치합니다. (풀링 런타임 생성 UI 도 동일 사용) </summary>
        public void PlaceByDepth(UIObject target)
        {
            if (target == null) return;
            RectTransform layer = LayerFor(target.Depth);
            if (layer != null) target.transform.SetParent(layer, false);
        }

        private RectTransform LayerFor(UIDepth depth) => depth switch
        {
            UIDepth.HUD => hudLayer,
            UIDepth.Fixed => fixedLayer,
            UIDepth.Popup => popupLayer,
            UIDepth.System => systemLayer,
            _ => hudLayer,
        };

        private void OnDestroy()
        {
            foreach (IPresenter presenter in presenters) presenter.Dispose();
            presenters.Clear();
        }
    }
}
