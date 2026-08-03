using NUnit.Framework;
using UnityEngine;
using DefenseDot.Systems.Visual.Camera;

namespace DefenseDot.Tests.EditMode
{
    public class CameraManagerTests
    {
        [Test]
        public void Bind_PositionsCameraBehindCenter()
        {
            var go = new GameObject("CameraManager");
            var cam = go.AddComponent<Camera>();
            var system = go.AddComponent<CameraManager>();
            SetTargetCamera(system, cam);

            var config = ScriptableObject.CreateInstance<CameraRigConfig>();
            config.pitch = 0f;
            config.yaw = 0f;
            config.distance = 10f;
            config.heightOffset = 0f;

            system.Bind(config, Vector3.zero);

            Assert.AreEqual(0f, cam.transform.position.x, 0.01f);
            Assert.AreEqual(0f, cam.transform.position.y, 0.01f);
            Assert.AreEqual(-10f, cam.transform.position.z, 0.01f);

            Object.DestroyImmediate(go);
            Object.DestroyImmediate(config);
        }

        [Test]
        public void PitchSetter_ClampsToConfigRange()
        {
            var go = new GameObject("CameraManager");
            var cam = go.AddComponent<Camera>();
            var system = go.AddComponent<CameraManager>();
            SetTargetCamera(system, cam);

            var config = ScriptableObject.CreateInstance<CameraRigConfig>();
            config.pitchRange = new Vector2(10f, 60f);
            system.Bind(config, Vector3.zero);

            system.Pitch = 200f;
            Assert.AreEqual(60f, system.Pitch, 0.001f);
            system.Pitch = -50f;
            Assert.AreEqual(10f, system.Pitch, 0.001f);

            Object.DestroyImmediate(go);
            Object.DestroyImmediate(config);
        }

        [Test]
        public void Bind_AppliesRenderSettings()
        {
            var go = new GameObject("CameraManager");
            var cam = go.AddComponent<Camera>();
            var system = go.AddComponent<CameraManager>();
            SetTargetCamera(system, cam);

            var config = ScriptableObject.CreateInstance<CameraRigConfig>();
            config.cullingMask = 0;
            config.clearFlags = CameraClearFlags.SolidColor;
            config.backgroundColor = new Color(0.1f, 0.2f, 0.3f, 1f);

            system.Bind(config, Vector3.zero);

            Assert.AreEqual(0, cam.cullingMask);
            Assert.AreEqual(CameraClearFlags.SolidColor, cam.clearFlags);
            Assert.AreEqual(0.2f, cam.backgroundColor.g, 0.01f);

            Object.DestroyImmediate(go);
            Object.DestroyImmediate(config);
        }

        [Test]
        public void Bind_SkipsPoseWhenUsePoseIsFalse()
        {
            var go = new GameObject("CameraManager");
            var cam = go.AddComponent<Camera>();
            var system = go.AddComponent<CameraManager>();
            SetTargetCamera(system, cam);
            cam.transform.position = new Vector3(7f, 8f, 9f);

            var config = ScriptableObject.CreateInstance<CameraRigConfig>();
            config.usePose = false;
            config.distance = 10f;

            system.Bind(config, Vector3.zero);

            Assert.AreEqual(7f, cam.transform.position.x, 0.01f);
            Assert.AreEqual(8f, cam.transform.position.y, 0.01f);
            Assert.AreEqual(9f, cam.transform.position.z, 0.01f);

            Object.DestroyImmediate(go);
            Object.DestroyImmediate(config);
        }

        /// <summary> private [SerializeField] targetCamera를 배선합니다. </summary>
        /// <param name="system">배선할 대상</param>
        /// <param name="camera">연결할 카메라</param>
        private static void SetTargetCamera(CameraManager system, Camera camera)
        {
            var so = new UnityEditor.SerializedObject(system);
            so.FindProperty("targetCamera").objectReferenceValue = camera;
            so.ApplyModifiedPropertiesWithoutUndo();
        }
    }
}