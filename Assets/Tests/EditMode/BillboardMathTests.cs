using NUnit.Framework;
using UnityEngine;
using DefenseDot.Systems.Visual.Billboard;

namespace DefenseDot.Tests.EditMode
{
    /// <summary> 카메라 향 Y축 각도 순수 계산을 검증합니다. </summary>
    public sealed class BillboardMathTests
    {
        [Test]
        public void YawTowardCamera_CameraOnNegativeZ_Returns180()
        {
            float yaw = BillboardMath.YawTowardCamera(Vector3.zero, new Vector3(0f, 0f, -10f));
            Assert.AreEqual(180f, Mathf.Abs(yaw), 0.01f);
        }

        [Test]
        public void YawTowardCamera_CameraOnPositiveX_Returns90()
        {
            float yaw = BillboardMath.YawTowardCamera(Vector3.zero, new Vector3(10f, 0f, 0f));
            Assert.AreEqual(90f, yaw, 0.01f);
        }

        [Test]
        public void YawTowardCamera_IgnoresHeight()
        {
            float flat = BillboardMath.YawTowardCamera(Vector3.zero, new Vector3(10f, 0f, 0f));
            float high = BillboardMath.YawTowardCamera(Vector3.zero, new Vector3(10f, 50f, 0f));
            Assert.AreEqual(flat, high, 0.01f);
        }

        [Test]
        public void YawTowardCamera_DirectlyAbove_ReturnsZero()
        {
            float yaw = BillboardMath.YawTowardCamera(Vector3.zero, new Vector3(0f, 10f, 0f));
            Assert.AreEqual(0f, yaw, 0.01f);
        }
    }
}
