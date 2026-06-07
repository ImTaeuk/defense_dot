// 카메라 배치 순수 계산 — 중심·각도·거리로 카메라 포즈 산출
using UnityEngine;

namespace DefenseDot.Systems.Visual.Camera
{
    /// <summary> 카메라의 위치와 회전을 함께 담는 값입니다. </summary>
    public readonly struct CameraPose
    {
        /// <summary> 월드 위치 </summary>
        public readonly Vector3 Position;
        /// <summary> 월드 회전 </summary>
        public readonly Quaternion Rotation;

        public CameraPose(Vector3 position, Quaternion rotation)
        {
            Position = position;
            Rotation = rotation;
        }
    }

    /// <summary> 중심 주시 카메라의 포즈를 계산하는 순수 함수 모음입니다. </summary>
    public static class CameraRigMath
    {
        /// <summary>
        /// 중심점을 바라보는 카메라의 위치·회전을 계산합니다.
        /// </summary>
        /// <param name="center">바라볼 중심(맵/코어 중심)</param>
        /// <param name="pitch">상하 각(도). 0=수평, 90=바로 위</param>
        /// <param name="yaw">수평 회전 각(도)</param>
        /// <param name="distance">중심에서 카메라까지 거리</param>
        /// <param name="heightOffset">중심 높이 보정</param>
        public static CameraPose Solve(Vector3 center, float pitch, float yaw, float distance, float heightOffset)
        {
            Quaternion rotation = Quaternion.Euler(pitch, yaw, 0f);
            Vector3 focus = center + Vector3.up * heightOffset;
            Vector3 position = focus - (rotation * Vector3.forward) * distance;
            return new CameraPose(position, rotation);
        }
    }
}
