using NUnit.Framework;
using UnityEngine;
using DefenseDot.Systems.Visual.Camera;

namespace DefenseDot.Tests.EditMode
{
    public class CameraRigMathTests
    {
        [Test]
        public void Solve_HorizontalDefault_PlacesBehindCenterAlongZ()
        {
            CameraPose pose = CameraRigMath.Solve(Vector3.zero, 0f, 0f, 10f, 0f);
            Assert.AreEqual(0f, pose.Position.x, 0.001f);
            Assert.AreEqual(0f, pose.Position.y, 0.001f);
            Assert.AreEqual(-10f, pose.Position.z, 0.001f);
        }

        [Test]
        public void Solve_TopDown_PlacesAboveCenter()
        {
            CameraPose pose = CameraRigMath.Solve(Vector3.zero, 90f, 0f, 10f, 0f);
            Assert.AreEqual(0f, pose.Position.x, 0.01f);
            Assert.AreEqual(10f, pose.Position.y, 0.01f);
            Assert.AreEqual(0f, pose.Position.z, 0.01f);
        }

        [Test]
        public void Solve_HeightOffset_RaisesPositionByOffset()
        {
            CameraPose pose = CameraRigMath.Solve(Vector3.zero, 0f, 0f, 10f, 2f);
            Assert.AreEqual(2f, pose.Position.y, 0.001f);
        }

        [Test]
        public void Solve_AnyAngle_CameraForwardPointsAtCenter()
        {
            Vector3 center = new Vector3(3f, 1f, -2f);
            CameraPose pose = CameraRigMath.Solve(center, 35f, 45f, 12f, 0f);
            Vector3 toCenter = (center - pose.Position).normalized;
            Vector3 forward = pose.Rotation * Vector3.forward;
            Assert.AreEqual(1f, Vector3.Dot(toCenter, forward), 0.001f);
        }

        [Test]
        public void CameraRigConfig_HasExpectedDefaults()
        {
            var config = ScriptableObject.CreateInstance<CameraRigConfig>();
            Assert.AreEqual(25f, config.pitch, 0.001f);
            Assert.AreEqual(30f, config.distance, 0.001f);
            Assert.IsTrue(config.perspective);
            Object.DestroyImmediate(config);
        }
    }
}
