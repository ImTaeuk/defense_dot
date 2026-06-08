// 리그 거리 폴링 → DoF focusDistance 연동 (틸트시프트 근사)
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using DefenseDot.Systems.Visual.Camera;

namespace DefenseDot.Systems.Visual.PostFx
{
    /// <summary>
    /// 카메라 리그의 거리(Distance)를 폴링하여 글로벌 Volume의 피사계 심도
    /// focusDistance를 갱신합니다. 초점면을 카메라-중심 거리에 정합(틸트시프트 근사).
    /// 런타임 인스턴스 프로파일(volume.profile)에만 기록해 원본 에셋 오염을 막습니다.
    /// </summary>
    public sealed class PostFxBinder : MonoBehaviour
    {
        /// <summary> focusDistance 하한(MinFloatParameter 양수 보장). </summary>
        public const float MinFocusDistance = 0.1f;

        private CenterFocusCameraRig boundRig;
        private Volume boundVolume;
        private DepthOfField cachedDof;

        /// <summary>
        /// 리그와 글로벌 Volume을 주입합니다. 직전 인스턴스 사본을 무효화하여
        /// 현재 sharedProfile(모드별 프리셋)로부터 재클론한 뒤, 피사계 심도 컴포넌트를
        /// 캐시합니다. (DoF 없으면 갱신 비활성)
        /// </summary>
        public void Bind(CenterFocusCameraRig rig, Volume volume)
        {
            boundRig = rig;
            boundVolume = volume;
            cachedDof = null;

            if (boundVolume == null) return;

            // stale 인스턴스 무효화 → 현재 sharedProfile 로부터 재클론 보장.
            // (profile 은 첫 접근 시에만 복제되므로, 재바인드/사전접근 시
            //  직전 프리셋이 남는 문제를 차단)
            boundVolume.profile = null;
            VolumeProfile profile = boundVolume.profile;
            if (profile != null) profile.TryGet(out cachedDof);

            ApplyFocus();
        }

        private void LateUpdate()
        {
            ApplyFocus();
        }

        /// <summary> 현재 리그 거리로 DoF focusDistance를 즉시 갱신합니다. </summary>
        private void ApplyFocus()
        {
            if (boundRig == null || cachedDof == null) return;
            cachedDof.focusDistance.value = ResolveFocusDistance(boundRig.Distance);
        }

        /// <summary>
        /// 카메라-중심 거리를 focusDistance로 매핑합니다. 양수 하한으로 클램프.
        /// (순수 함수 — EditMode 테스트 대상)
        /// </summary>
        public static float ResolveFocusDistance(float distance)
        {
            return distance < MinFocusDistance ? MinFocusDistance : distance;
        }
    }
}
