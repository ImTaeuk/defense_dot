// 경과시간 → 프레임 인덱스 순수 계산
using UnityEngine;

namespace DefenseDot.Systems.Visual.Billboard
{
    /// <summary> 프레임 애니메이션 인덱스 순수 계산 모음입니다. </summary>
    public static class SpriteFrameMath
    {
        /// <summary>
        /// 경과시간(초)과 fps, 프레임 수로 현재 프레임 인덱스를 계산합니다.
        /// loop=true 면 순환, false 면 마지막 프레임에서 고정.
        /// </summary>
        public static int FrameIndex(float elapsed, float fps, int frameCount, bool loop)
        {
            if (frameCount <= 0 || fps <= 0f) return 0;
            int raw = Mathf.FloorToInt(elapsed * fps);
            if (raw < 0) raw = 0;
            if (loop) return raw % frameCount;
            return raw >= frameCount ? frameCount - 1 : raw;
        }
    }
}
