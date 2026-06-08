using NUnit.Framework;
using DefenseDot.Systems.Visual.Billboard;

namespace DefenseDot.Tests.EditMode
{
    /// <summary> 경과시간→프레임 인덱스 순수 계산을 검증합니다. </summary>
    public sealed class SpriteFrameMathTests
    {
        [Test]
        public void FrameIndex_Start_ReturnsZero()
        {
            Assert.AreEqual(0, SpriteFrameMath.FrameIndex(0f, 8f, 4, true));
        }

        [Test]
        public void FrameIndex_Loops()
        {
            Assert.AreEqual(0, SpriteFrameMath.FrameIndex(0.5f, 8f, 4, true));
            Assert.AreEqual(3, SpriteFrameMath.FrameIndex(0.375f, 8f, 4, true));
        }

        [Test]
        public void FrameIndex_NoLoop_ClampsToLast()
        {
            Assert.AreEqual(3, SpriteFrameMath.FrameIndex(10f, 8f, 4, false));
        }

        [Test]
        public void FrameIndex_EmptyOrZeroFps_ReturnsZero()
        {
            Assert.AreEqual(0, SpriteFrameMath.FrameIndex(1f, 8f, 0, true));
            Assert.AreEqual(0, SpriteFrameMath.FrameIndex(1f, 0f, 4, true));
        }
    }
}
