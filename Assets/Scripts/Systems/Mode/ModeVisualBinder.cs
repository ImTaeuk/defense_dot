// 씬의 연출 자원과 전역 카메라·포스트FX 를 잇는다 — 무엇을 비출지는 정하지 않는다
using UnityEngine;
using UnityEngine.Rendering;
using DefenseDot.Systems.Visual.Camera;

namespace DefenseDot.Systems.Mode
{
    /// <summary>
    /// 씬이 가진 연출 자원(카메라 설정·포스트FX 프리셋)을 전역 카메라와 볼륨에 잇습니다.
    /// 무엇을 비출지(중심)는 모드가 정해 넘기며, 이 타입은 판단하지 않습니다.
    /// </summary>
    public sealed class ModeVisualBinder : MonoBehaviour
    {
        /// <summary> 이 씬이 쓸 카메라 설정. 전역 CameraManager 에 넘긴다. </summary>
        [Header("Camera")]
        [SerializeField] private CameraRigConfig cameraConfig;

        /// <summary> 에디터 카메라 프리뷰가 바라볼 중심. 런타임에는 쓰이지 않는다. </summary>
        [SerializeField] private Transform previewCenter;

        [Header("Post FX")]
        [SerializeField] private Volume globalVolume;
        [SerializeField] private VolumeProfile postFxProfile;
        [SerializeField] private DefenseDot.Systems.Visual.PostFx.PostFxBinder postFxBinder;

        /// <summary> 이 씬의 카메라 설정입니다. 에디터 프리뷰가 읽습니다. </summary>
        public CameraRigConfig CameraConfig => cameraConfig;

        /// <summary>
        /// 카메라와 포스트FX 를 잇습니다. 자원이 없는 단계는 건너뜁니다.
        /// </summary>
        /// <param name="center">카메라가 바라볼 중심. 모드가 계산해 넘긴다</param>
        public void Bind(Vector3 center)
        {
            // 1. 카메라 — 설정은 씬이 소유하고 중심은 모드가 정한다
            if (CameraManager.Instance != null)
                CameraManager.Instance.Bind(cameraConfig, center);

            // 2. 포스트FX 프리셋 교체 (읽기전용 — sharedProfile 비파괴)
            if (globalVolume != null && postFxProfile != null)
                globalVolume.sharedProfile = postFxProfile;

            // 3. DoF 연동 위임 — 셋이 모두 배선됐을 때만(프리셋 누락 시 stale 프로파일 바인딩 방지)
            if (globalVolume != null && postFxProfile != null && postFxBinder != null)
                postFxBinder.Bind(globalVolume);
        }
    }
}
