// 전역 카메라 하나를 보유하고 씬별 Config를 적용한다
using UnityEngine;
using DefenseDot.Core;

namespace DefenseDot.Systems.Visual.Camera
{
    /// <summary>
    /// 게임 전체가 쓰는 카메라 하나를 보유하고, 씬별 Config로 배치와 렌더 설정을 적용합니다.
    /// 무엇을 바라볼지는 결정하지 않으며, 씬 전환도 맡지 않습니다.
    /// </summary>
    public sealed class CameraSystem : Singleton<CameraSystem>
    {
        /// <summary> 이 시스템이 제어하는 카메라. 같은 오브젝트에 두고 인스펙터로 잇는다. </summary>
        [SerializeField] private UnityEngine.Camera targetCamera;

        /// <summary> 씬이 아직 아무것도 넘기지 않은 상태에서 쓸 기본 설정. </summary>
        [SerializeField] private CameraRigConfig defaultConfig;

        /// <summary> 현재 적용된 설정. Bind로 교체된다. </summary>
        private CameraRigConfig config;

        // 런타임 상태(에셋 오염 방지를 위해 config에서 복사)
        private float currentPitch;
        private float currentYaw;
        private float currentDistance;
        private float currentHeightOffset;
        private Vector3 currentCenter;

        /// <summary> 런타임 상하 각. 설정 시 config 범위로 클램프됩니다. </summary>
        public float Pitch
        {
            get => currentPitch;
            set => currentPitch = config != null
                ? Mathf.Clamp(value, config.pitchRange.x, config.pitchRange.y)
                : value;
        }

        /// <summary> 런타임 수평 회전 각. </summary>
        public float Yaw
        {
            get => currentYaw;
            set => currentYaw = value;
        }

        /// <summary> 중심에서 카메라까지의 거리. </summary>
        public float Distance
        {
            get => currentDistance;
            set => currentDistance = value;
        }

        /// <summary> 부팅 직후 기본 설정을 스스로 적용해 화면 상태를 확정합니다. </summary>
        protected override void OnAwake()
        {
            Bind(defaultConfig, Vector3.zero);
        }

        /// <summary> 설정과 중심을 받아 카메라의 렌더 설정과 배치를 적용합니다. </summary>
        /// <param name="config">적용할 카메라 설정. null이면 아무것도 하지 않는다</param>
        /// <param name="center">바라볼 중심 좌표</param>
        public void Bind(CameraRigConfig config, Vector3 center)
        {
            if (config == null)
                return;

            // 1. 설정 교체·런타임 상태 복사
            this.config = config;
            currentCenter = center;
            currentPitch = config.pitch;
            currentYaw = config.yaw;
            currentDistance = config.distance;
            currentHeightOffset = config.heightOffset;

            // 2. 렌더 설정은 포즈 사용 여부와 무관하게 항상 적용
            ApplyRender();

            // 3. 포즈는 쓰기로 한 설정에서만
            if (!config.usePose)
                return;

            ApplyPose(instant: true);
        }

        /// <summary> 중심 추적을 진행합니다. </summary>
        private void LateUpdate()
        {
            if (config == null || !config.usePose)
                return;

            ApplyPose(instant: config.followLerp <= 0f);
        }

        /// <summary> 투영·컬링·클리어 설정을 카메라에 씁니다. </summary>
        private void ApplyRender()
        {
            if (targetCamera == null)
                return;

            targetCamera.cullingMask = config.cullingMask;
            targetCamera.clearFlags = config.clearFlags;
            targetCamera.backgroundColor = config.backgroundColor;
            targetCamera.orthographic = !config.perspective;
            if (config.perspective)
                targetCamera.fieldOfView = config.fieldOfView;
            else
                targetCamera.orthographicSize = config.orthoSize;
        }

        /// <summary> 계산된 포즈로 카메라를 옮깁니다. </summary>
        /// <param name="instant">true면 즉시, false면 followLerp로 보간</param>
        private void ApplyPose(bool instant)
        {
            if (targetCamera == null)
                return;

            CameraPose pose = CameraRigMath.Solve(
                currentCenter, currentPitch, currentYaw, currentDistance, currentHeightOffset);
            Transform t = targetCamera.transform;

            if (instant)
            {
                t.SetPositionAndRotation(pose.Position, pose.Rotation);
                return;
            }

            float k = 1f - Mathf.Exp(-config.followLerp * Time.deltaTime);
            t.SetPositionAndRotation(
                Vector3.Lerp(t.position, pose.Position, k),
                Quaternion.Slerp(t.rotation, pose.Rotation, k));
        }
    }
}