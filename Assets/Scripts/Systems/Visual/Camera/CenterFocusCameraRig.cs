// 중앙 주시 카메라 리그 — 에디터/런타임에서 중심을 바라보게 배치
using UnityEngine;

namespace DefenseDot.Systems.Visual.Camera
{
    /// <summary>
    /// 지정한 중심을 항상 바라보도록 카메라를 배치하는 리그입니다.
    /// 에디터에서는 config 값을 실시간 반영하고, 런타임에는 Bind로 주입된 값/중심을 사용합니다.
    /// </summary>
    [ExecuteAlways]
    public class CenterFocusCameraRig : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private UnityEngine.Camera targetCamera;
        [SerializeField] private Transform target;
        [SerializeField] private CameraRigConfig config;

        // 런타임 상태(에셋 오염 방지를 위해 config에서 복사)
        private float currentPitch;
        private float currentYaw;
        private float currentDistance;
        private float currentHeightOffset;
        private Vector3 runtimeCenter;
        private bool hasRuntimeCenter;

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

        /// <summary> 런타임 거리. </summary>
        public float Distance
        {
            get => currentDistance;
            set => currentDistance = value;
        }

        /// <summary>
        /// 리그가 보유한 config로 중심만 주입해 바인딩합니다. (모드=씬 1:1 기본 경로)
        /// </summary>
        public void Bind(Vector3 center)
        {
            Bind(center, config);
        }

        /// <summary>
        /// 중심과 설정을 함께 주입합니다. config 값을 런타임 상태로 복사합니다. (런타임 config 교체용)
        /// </summary>
        public void Bind(Vector3 center, CameraRigConfig rigConfig)
        {
            if (rigConfig != null) config = rigConfig;
            runtimeCenter = center;
            hasRuntimeCenter = true;
            CopyFromConfig();
            ApplyCameraProjection();
            ApplyPose(GetCenter(), instant: true);
        }

        private void OnEnable()
        {
            CopyFromConfig();
        }

        private void CopyFromConfig()
        {
            if (config == null) return;
            currentPitch = config.pitch;
            currentYaw = config.yaw;
            currentDistance = config.distance;
            currentHeightOffset = config.heightOffset;
        }

        private UnityEngine.Camera ResolveCamera()
        {
            if (targetCamera != null) return targetCamera;
            targetCamera = GetComponent<UnityEngine.Camera>();
            if (targetCamera == null) targetCamera = UnityEngine.Camera.main;
            return targetCamera;
        }

        private Vector3 GetCenter()
        {
            // 런타임 Bind 주입을 최우선(권위). target은 에디터 프리뷰 폴백.
            if (hasRuntimeCenter) return runtimeCenter;
            if (target != null) return target.position;
            return transform.position;
        }

        private void Update()
        {
            if (!Application.isPlaying)
            {
                CopyFromConfig();
                ApplyCameraProjection();
                ApplyPose(GetCenter(), instant: true);
            }
        }

        private void LateUpdate()
        {
            if (Application.isPlaying)
            {
                float lerp = config != null ? config.followLerp : 0f;
                ApplyPose(GetCenter(), instant: lerp <= 0f);
            }
        }

        private void ApplyCameraProjection()
        {
            UnityEngine.Camera cam = ResolveCamera();
            if (cam == null || config == null) return;
            cam.orthographic = !config.perspective;
            if (config.perspective) cam.fieldOfView = config.fieldOfView;
            else cam.orthographicSize = config.orthoSize;
        }

        private void ApplyPose(Vector3 center, bool instant)
        {
            UnityEngine.Camera cam = ResolveCamera();
            if (cam == null) return;

            CameraPose pose = CameraRigMath.Solve(
                center, currentPitch, currentYaw, currentDistance, currentHeightOffset);
            Transform t = cam.transform;

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
