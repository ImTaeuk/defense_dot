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

        /// <summary>
        /// 이동 방향(XZ)을 카메라 yaw 기준 4방향 인덱스로 변환합니다.
        /// (AC Player Direction 파라미터: 0=S, 1=N, 2=E, 3=W. 정지 시 S)
        /// </summary>
        public static int DirectionIndex(Vector3 worldMoveDir, float cameraYaw)
        {
            Vector3 flat = worldMoveDir;
            flat.y = 0f;
            if (flat.sqrMagnitude < 1e-6f) return 0;
            Vector3 rel = Quaternion.Euler(0f, -cameraYaw, 0f) * flat;
            if (Mathf.Abs(rel.x) >= Mathf.Abs(rel.z))
                return rel.x > 0f ? 2 : 3;
            return rel.z > 0f ? 1 : 0;
        }
    }
}
