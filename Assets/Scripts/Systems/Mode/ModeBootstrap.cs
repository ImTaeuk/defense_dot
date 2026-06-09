// 모드별 합성 루트 베이스 — 모드(IGameMode)를 생성한다
using UnityEngine;
using DefenseDot.Systems.Visual.Camera;

namespace DefenseDot.Systems.Mode
{
    /// <summary>
    /// 모드별 부트스트랩의 베이스입니다. 모드 고유 자원(뷰·맵 데이터)을 보유하고
    /// 해당 모드의 IGameMode를 생성합니다. (인터페이스 대신 추상 MonoBehaviour — 인스펙터 직렬화)
    /// </summary>
    public abstract class ModeBootstrap : MonoBehaviour
    {
        [Header("Presentation")]
        [SerializeField] protected CenterFocusCameraRig cameraRig;
        [SerializeField] protected UnityEngine.Rendering.Volume globalVolume;
        [SerializeField] protected UnityEngine.Rendering.VolumeProfile postFxProfile;
        [SerializeField] protected DefenseDot.Systems.Visual.PostFx.PostFxBinder postFxBinder;

        /// <summary> 공통 입력을 받아 이 부트스트랩의 모드를 생성합니다. </summary>
        public abstract IGameMode CreateMode(ModeContext ctx);

        /// <summary> 이 모드의 적 수 표시 한계(HUD capacity)입니다. </summary>
        public abstract int EnemyDisplayCapacity { get; }

        /// <summary>
        /// 모드 연출을 바인딩합니다. 카메라 중심 주입 → 모드별 포스트FX 프리셋 활성화
        /// → DoF 연동 시작. (자원 미설정 모드는 해당 단계 무시)
        /// </summary>
        protected void BindPresentation(in ModeContext ctx)
        {
            // 1) 카메라 바인딩 (config는 리그가 단독 소유, 중심은 모드별 산출)
            if (cameraRig != null) cameraRig.Bind(GetCameraCenter(ctx));

            // 2) 모드별 프리셋 참조 교체 (읽기전용 — sharedProfile 비파괴)
            if (globalVolume != null && postFxProfile != null)
            {
                globalVolume.sharedProfile = postFxProfile;
            }

            // 3) DoF 연동 위임 — 볼륨·프리셋·바인더가 모두 배선됐을 때만.
            //    (프리셋 누락 시 stale 프로파일에 바인딩되는 것을 방지)
            if (globalVolume != null && postFxProfile != null && postFxBinder != null)
            {
                postFxBinder.Bind(cameraRig, globalVolume);
            }
        }

        /// <summary> 카메라가 바라볼 중심을 반환합니다. 기본은 코어 중심(모드별 재정의 가능). </summary>
        protected virtual Vector3 GetCameraCenter(in ModeContext ctx)
        {
            return ctx.CoreCenter;
        }
    }
}
