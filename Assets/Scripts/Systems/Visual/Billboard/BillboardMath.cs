// 카메라를 수평으로 바라보는 Y축 각도 순수 계산
using UnityEngine;

namespace DefenseDot.Systems.Visual.Billboard
{
    /// <summary> 빌보드 회전 각도 순수 계산 모음입니다. </summary>
    public static class BillboardMath
    {
        /// <summary>
        /// 스프라이트가 카메라를 수평(Y축)으로 바라보는 각도(도)를 계산합니다.
        /// 높이 차이는 무시하여 스프라이트가 직립(서 있는)을 유지합니다.
        /// </summary>
        public static float YawTowardCamera(Vector3 spritePosition, Vector3 cameraPosition)
        {
            Vector3 dir = cameraPosition - spritePosition;
            dir.y = 0f;
            if (dir.sqrMagnitude < 1e-6f) return 0f;
            return Mathf.Atan2(dir.x, dir.z) * Mathf.Rad2Deg;
        }
    }
}
