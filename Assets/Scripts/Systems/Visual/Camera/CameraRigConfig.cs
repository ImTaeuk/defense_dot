// 모드별 카메라 리그 설정 — 디자이너가 조절하는 영구 기본값
using UnityEngine;

namespace DefenseDot.Systems.Visual.Camera
{
    /// <summary>
    /// 중앙 주시 카메라 리그의 모드별 설정 값입니다.
    /// </summary>
    [CreateAssetMenu(fileName = "NewCameraRigConfig", menuName = "DefenseDot/CameraRigConfig")]
    public class CameraRigConfig : ScriptableObject
    {
        /// <summary> 상하 각(0=수평, 90=탑다운). 핵심 조절값 </summary>
        public float pitch = 25f;
        /// <summary> 수평 회전 각 </summary>
        public float yaw = 0f;
        /// <summary> 중심에서 카메라까지 거리 </summary>
        public float distance = 30f;
        /// <summary> 타깃 높이 보정 </summary>
        public float heightOffset = 0f;
        /// <summary> 원근(true) / 직교(false). HD-2D는 원근 권장 </summary>
        public bool perspective = true;
        /// <summary> 원근 시야각 </summary>
        public float fieldOfView = 40f;
        /// <summary> 직교 크기 </summary>
        public float orthoSize = 15f;
        /// <summary> 타깃 추적 부드러움(0=즉시) </summary>
        public float followLerp = 0f;
        /// <summary> 런타임 pitch 조절 클램프 범위 </summary>
        public Vector2 pitchRange = new Vector2(10f, 60f);
    }
}
