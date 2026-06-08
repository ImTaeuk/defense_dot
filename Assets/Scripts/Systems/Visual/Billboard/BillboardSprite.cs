// 스프라이트를 카메라 향으로 회전 (Y축 직립 빌보드)
using UnityEngine;

namespace DefenseDot.Systems.Visual.Billboard
{
    /// <summary>
    /// 매 프레임 스프라이트를 카메라 향으로 회전시킵니다.
    /// YAxisUpright: 수직 유지 + Y회전(서 있는 느낌). CameraPlane: 화면과 평행.
    /// </summary>
    [ExecuteAlways]
    public sealed class BillboardSprite : MonoBehaviour
    {
        /// <summary> 빌보드 회전 방식. </summary>
        public enum BillboardMode { YAxisUpright, CameraPlane }

        [SerializeField] private BillboardMode mode = BillboardMode.YAxisUpright;
        [SerializeField] private UnityEngine.Camera targetCamera;

        private void LateUpdate()
        {
            UnityEngine.Camera cam = ResolveCamera();
            if (cam == null) return;

            if (mode == BillboardMode.CameraPlane)
            {
                transform.rotation = cam.transform.rotation;
                return;
            }

            float yaw = BillboardMath.YawTowardCamera(transform.position, cam.transform.position);
            transform.rotation = Quaternion.Euler(0f, yaw, 0f);
        }

        private UnityEngine.Camera ResolveCamera()
        {
            if (targetCamera != null) return targetCamera;
            targetCamera = UnityEngine.Camera.main;
            return targetCamera;
        }
    }
}
