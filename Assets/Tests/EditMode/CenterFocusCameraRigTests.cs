using NUnit.Framework;
using UnityEngine;
using DefenseDot.Systems.Visual.Camera;

namespace DefenseDot.Tests.EditMode
{
    public class CenterFocusCameraRigTests
    {
        [Test]
        public void Bind_PositionsCameraBehindCenter()
        {
            var go = new GameObject("RigCam");
            var cam = go.AddComponent<Camera>();
            var rig = go.AddComponent<CenterFocusCameraRig>();

            var config = ScriptableObject.CreateInstance<CameraRigConfig>();
            config.pitch = 0f;
            config.yaw = 0f;
            config.distance = 10f;
            config.heightOffset = 0f;

            rig.Bind(Vector3.zero, config);

            Assert.AreEqual(0f, cam.transform.position.x, 0.01f);
            Assert.AreEqual(0f, cam.transform.position.y, 0.01f);
            Assert.AreEqual(-10f, cam.transform.position.z, 0.01f);

            Object.DestroyImmediate(go);
            Object.DestroyImmediate(config);
        }

        [Test]
        public void PitchSetter_ClampsToConfigRange()
        {
            var go = new GameObject("RigCam");
            go.AddComponent<Camera>();
            var rig = go.AddComponent<CenterFocusCameraRig>();

            var config = ScriptableObject.CreateInstance<CameraRigConfig>();
            config.pitchRange = new Vector2(10f, 60f);
            rig.Bind(Vector3.zero, config);

            rig.Pitch = 200f;
            Assert.AreEqual(60f, rig.Pitch, 0.001f);
            rig.Pitch = -50f;
            Assert.AreEqual(10f, rig.Pitch, 0.001f);

            Object.DestroyImmediate(go);
            Object.DestroyImmediate(config);
        }

        [Test]
        public void BindCenterOnly_ReusesRigConfig()
        {
            var go = new GameObject("RigCam");
            var cam = go.AddComponent<Camera>();
            var rig = go.AddComponent<CenterFocusCameraRig>();

            var config = ScriptableObject.CreateInstance<CameraRigConfig>();
            config.pitch = 0f;
            config.yaw = 0f;
            config.distance = 10f;
            config.heightOffset = 0f;

            rig.Bind(Vector3.zero, config);      // config 주입
            rig.Bind(new Vector3(5f, 0f, 0f));   // 중심만 갱신, config 재사용

            Assert.AreEqual(5f, cam.transform.position.x, 0.01f);
            Assert.AreEqual(-10f, cam.transform.position.z, 0.01f);

            Object.DestroyImmediate(go);
            Object.DestroyImmediate(config);
        }
    }
}
