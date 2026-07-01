using NUnit.Framework;
using DefenseDot.UI;
using DefenseDot.UI.Base;
using DefenseDot.UI.Presenters;

namespace DefenseDot.Tests.EditMode
{
    public class UIPresenterFactoryTests
    {
        // 테스트 전용 View/Presenter (자동배선 스캔 대상)
        private sealed class FactoryProbeView : UIView { }
        private sealed class FactoryProbePresenter : UIPresenter<FactoryProbeView>
        {
            public FactoryProbePresenter(FactoryProbeView view, DefenseDot.Domain.GameContext ctx) : base(view) { }
            protected override void OnInitialize() { }
        }

        [Test]
        public void Create_NullView_ReturnsNull()
        {
            var factory = new UIPresenterFactory(null);
            Assert.IsNull(factory.Create(null));
        }

        [Test]
        public void Create_MapsViewTypeToPresenter()
        {
            var go = new UnityEngine.GameObject("probe");
            var view = go.AddComponent<FactoryProbeView>();
            var factory = new UIPresenterFactory(null);
            IPresenter p = factory.Create(view);
            Assert.IsInstanceOf<FactoryProbePresenter>(p);
            UnityEngine.Object.DestroyImmediate(go);
        }
    }
}
