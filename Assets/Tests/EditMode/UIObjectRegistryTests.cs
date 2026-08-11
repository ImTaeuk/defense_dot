using NUnit.Framework;
using UnityEngine;
using DefenseDot.UI.Base;

namespace DefenseDot.Tests.EditMode
{
    public class UIObjectRegistryTests
    {
        private sealed class ProbePanel : UIPanel
        {
        }

        private sealed class OtherProbePanel : UIPanel
        {
        }

        /// <summary> 각 테스트 후 장부를 비웁니다. </summary>
        [TearDown]
        public void TearDown()
        {
            UIObject.ClearRegistry();
        }

        /// <summary> 등록한 타입을 Create 로 그대로 돌려받는지 확인합니다. </summary>
        [Test]
        public void Create_RegisteredSingle_ReturnsSameInstance()
        {
            var go = new GameObject("probe");
            ProbePanel panel = go.AddComponent<ProbePanel>();
            UIObject.RegisterSingle(panel, "TestScene");

            ProbePanel found = UIObject.Create<ProbePanel>();

            Assert.AreSame(panel, found);
            Object.DestroyImmediate(go);
        }

        /// <summary> 등록되지 않은 타입은 null 을 돌려주는지 확인합니다. </summary>
        [Test]
        public void Create_UnregisteredType_ReturnsNull()
        {
            Assert.IsNull(UIObject.Create<ProbePanel>());
        }

        /// <summary> 씬을 정리하면 그 씬 소속만 사라지는지 확인합니다. </summary>
        [Test]
        public void ReleaseScene_RemovesOnlyThatScene()
        {
            var goA = new GameObject("a");
            var goB = new GameObject("b");
            ProbePanel a = goA.AddComponent<ProbePanel>();
            OtherProbePanel b = goB.AddComponent<OtherProbePanel>();
            UIObject.RegisterSingle(a, "SceneA");
            UIObject.RegisterSingle(b, "SceneB");

            UIObject.ReleaseScene("SceneA");

            Assert.IsNull(UIObject.Create<ProbePanel>());
            Assert.AreSame(b, UIObject.Create<OtherProbePanel>());
            Object.DestroyImmediate(goB);
        }

        /// <summary> 씬 정리가 그 씬 인스턴스를 실제로 파괴하는지 확인합니다. </summary>
        [Test]
        public void ReleaseScene_DestroysInstance()
        {
            var go = new GameObject("probe");
            ProbePanel panel = go.AddComponent<ProbePanel>();
            UIObject.RegisterSingle(panel, "SceneA");

            UIObject.ReleaseScene("SceneA");

            Assert.IsTrue(panel == null, "ReleaseScene 이 인스턴스를 파괴해야 합니다.");
        }

        /// <summary> ClearRegistry 가 장부를 비우는지 확인합니다. </summary>
        [Test]
        public void ClearRegistry_RemovesAllEntries()
        {
            var go = new GameObject("probe");
            ProbePanel panel = go.AddComponent<ProbePanel>();
            UIObject.RegisterSingle(panel, "TestScene");

            UIObject.ClearRegistry();

            Assert.IsNull(UIObject.Create<ProbePanel>());
            Object.DestroyImmediate(go);
        }

        private sealed class PresenterProbeView : UIView
        {
        }

        private sealed class PresenterProbePresenter : UIPresenter<PresenterProbeView>
        {
            /// <summary> 이 Presenter 가 초기화된 횟수입니다. </summary>
            public static int InitializedCount;

            /// <summary> 이 Presenter 가 정리된 횟수입니다. </summary>
            public static int DisposedCount;

            /// <summary> View 와 컨텍스트를 받아 베이스에 View 를 전달합니다. </summary>
            /// <param name="view">제어할 View</param>
            /// <param name="ctx">주입되는 게임 컨텍스트</param>
            public PresenterProbePresenter(PresenterProbeView view, DefenseDot.Domain.GameContext ctx) : base(view)
            {
            }

            /// <summary> 초기화 횟수를 센다. </summary>
            protected override void OnInitialize()
            {
                InitializedCount++;
            }

            /// <summary> 정리 횟수를 센다. </summary>
            protected override void OnDispose()
            {
                DisposedCount++;
            }
        }

        /// <summary> 등록된 View 의 Presenter 가 생성·초기화되는지 확인합니다. </summary>
        [Test]
        public void CreatePresenters_RegisteredView_InitializesPresenter()
        {
            PresenterProbePresenter.InitializedCount = 0;
            var go = new GameObject("probe");
            PresenterProbeView view = go.AddComponent<PresenterProbeView>();
            UIObject.RegisterSingle(view, "SceneA");

            UIObject.CreatePresenters(null, "SceneA");

            Assert.AreEqual(1, PresenterProbePresenter.InitializedCount);
            Object.DestroyImmediate(go);
        }

        /// <summary> 같은 씬을 재호출하면 이전 Presenter 가 해제되는지 확인합니다. </summary>
        [Test]
        public void CreatePresenters_CalledTwice_DisposesPreviousPresenters()
        {
            PresenterProbePresenter.DisposedCount = 0;
            var go = new GameObject("probe");
            PresenterProbeView view = go.AddComponent<PresenterProbeView>();
            UIObject.RegisterSingle(view, "SceneA");
            UIObject.CreatePresenters(null, "SceneA");

            UIObject.CreatePresenters(null, "SceneA");

            Assert.AreEqual(1, PresenterProbePresenter.DisposedCount);
            Object.DestroyImmediate(go);
        }

        /// <summary> 씬을 정리하면 그 씬 Presenter 도 함께 해제되는지 확인합니다. </summary>
        [Test]
        public void ReleaseScene_DisposesPresenters()
        {
            PresenterProbePresenter.DisposedCount = 0;
            var go = new GameObject("probe");
            PresenterProbeView view = go.AddComponent<PresenterProbeView>();
            UIObject.RegisterSingle(view, "SceneA");
            UIObject.CreatePresenters(null, "SceneA");

            UIObject.ReleaseScene("SceneA");

            Assert.AreEqual(1, PresenterProbePresenter.DisposedCount);
        }

        /// <summary> ClearRegistry 가 남은 Presenter 도 함께 해제하는지 확인합니다. </summary>
        [Test]
        public void ClearRegistry_DisposesPresenters()
        {
            PresenterProbePresenter.DisposedCount = 0;
            var go = new GameObject("probe");
            PresenterProbeView view = go.AddComponent<PresenterProbeView>();
            UIObject.RegisterSingle(view, "SceneA");
            UIObject.CreatePresenters(null, "SceneA");

            UIObject.ClearRegistry();

            Assert.AreEqual(1, PresenterProbePresenter.DisposedCount);
            Object.DestroyImmediate(go);
        }
    }
}
