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
    }
}
