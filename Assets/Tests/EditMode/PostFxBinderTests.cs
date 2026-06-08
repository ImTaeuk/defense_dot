// PostFxBinder 순수 로직 단위 테스트 — 거리→DoF focus 매핑
using NUnit.Framework;
using DefenseDot.Systems.Visual.PostFx;

namespace DefenseDot.Tests.EditMode
{
    /// <summary> PostFxBinder의 거리→focusDistance 순수 매핑을 검증합니다. </summary>
    public sealed class PostFxBinderTests
    {
        [Test]
        public void ResolveFocusDistance_PositiveDistance_ReturnsSameValue()
        {
            float focus = PostFxBinder.ResolveFocusDistance(30f);
            Assert.AreEqual(30f, focus, 0.0001f);
        }

        [Test]
        public void ResolveFocusDistance_Zero_ClampsToMinimum()
        {
            float focus = PostFxBinder.ResolveFocusDistance(0f);
            Assert.AreEqual(PostFxBinder.MinFocusDistance, focus, 0.0001f);
        }

        [Test]
        public void ResolveFocusDistance_Negative_ClampsToMinimum()
        {
            float focus = PostFxBinder.ResolveFocusDistance(-5f);
            Assert.AreEqual(PostFxBinder.MinFocusDistance, focus, 0.0001f);
        }

        [Test]
        public void ResolveFocusDistance_BelowMinimum_ClampsToMinimum()
        {
            float focus = PostFxBinder.ResolveFocusDistance(0.05f);
            Assert.AreEqual(PostFxBinder.MinFocusDistance, focus, 0.0001f);
        }
    }
}
