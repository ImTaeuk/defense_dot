# HD-2D Phase 2 구현 계획 — 빌트인 포스트 프로세싱 스택 (Volume 기반)

> **For agentic workers:** REQUIRED SUB-SKILL — 실행 시 `superpowers:subagent-driven-development`(권장) 또는 `superpowers:executing-plans` 를 사용하라. 코드 작성 전 `superpowers:test-driven-development`, 완료/커밋 전 `superpowers:verification-before-completion`. URP API 사실 확인은 `judge` 에이전트(웹 검색)로.
>
> **프로젝트 규칙:** 사용자 명시 승인 없이 git 커밋 금지. 커밋 전 반드시 `lint` 스킬로 컨벤션 검증. 모든 코드는 프로젝트 컨벤션 준수([SerializeField] private camelCase, 모든 멤버 접근제한자 명시, 한국어 `<summary>`, System.* 풀패스, 비동기는 UniTask만).

> **검증 출처:** 본 계획은 ultracode 워크플로(조사 3 → 초안 → 적대적 검증 3)로 작성됐다. 모든 URP 타입·어셈블리명은 로컬 `Library/PackageCache/com.unity.render-pipelines.*@17.4.0` 소스 + Unity 6000.0 매뉴얼과 대조 검증됨. 검증에서 발견된 critical(Task 2 삭제) / important(profile 재클론) 이슈가 본 최종본에 반영됨.

**Goal:** HD-2D 룩의 색감·광량 레이어를 **셰이더 코드 0줄**로 구현한다. URP 빌트인 Volume 스택으로 ②블룸 · ③색감(Color Adjustments + Tonemapping ACES) · ③비네팅 · ①틸트시프트 근사(Depth of Field Bokeh, focusDistance≈카메라-중심 거리)를 적용하고, 모드별 VolumeProfile 프리셋을 씬 글로벌 Volume 1개에 런타임 교체한다.

**Architecture:**
- **단일 글로벌 Volume + 프로파일 swap**: 씬마다 isGlobal Volume 1개. `ModeBootstrap.BindPresentation` 이 모드별 프리셋(`HD2D_Arena.asset`/`HD2D_Grid.asset`)을 활성화.
- **에셋 오염 방지(핵심 불변식)**: 프리셋은 읽기전용. 참조 지정은 `volume.sharedProfile = preset`. 런타임 가변 파라미터(DoF focusDistance)는 `volume.profile`(인스턴스 사본)에만 기록. 원본 `.asset` 은 코드/에디터에서 `.value` 수정 금지.
- **clone-on-access 방어**: `volume.profile` 게터는 **첫 접근 시에만** sharedProfile 에서 복제한다. 재바인드·사전 접근으로 stale 인스턴스가 남을 수 있어, `PostFxBinder.Bind` 진입부에서 `volume.profile = null` 로 무효화 후 재클론을 강제한다.
- **DoF↔카메라 연동**: `PostFxBinder` 가 `rig.Distance`(이미 결정된 config 복사본)를 LateUpdate 폴링해 DoF focusDistance 갱신. 리그가 이벤트 미제공 → 폴링. 순수 매핑은 static 함수로 분리(EditMode TDD).
- **소유권 모델 유지(Phase1 Option A)**: 부트스트랩에 cameraConfig 미추가. 카메라 값 단일 진실 원천은 리그. Phase2 신규 직렬화 필드는 PostFx 자원(globalVolume/postFxProfile/postFxBinder)만.

**Tech Stack:** Unity 6000.2.x / URP 17.4.0. `Volume`/`VolumeProfile`/`VolumeComponent`/`TryGet<T>` → `UnityEngine.Rendering`(Unity.RenderPipelines.**Core**.Runtime). `Bloom`/`ColorAdjustments`/`Tonemapping`/`Vignette`/`DepthOfField` → `UnityEngine.Rendering.Universal`(Unity.RenderPipelines.**Universal**.Runtime). 테스트: NUnit EditMode.

**관련 스펙:** `docs/superpowers/specs/2026-06-07-hd2d-visual-design.md` §4.2-A(빌트인 포스트FX), §5(모드별 룩), §8-R3(모바일 비용).

---

## File Structure

| 파일 | 책임 | 상태 |
|---|---|---|
| `Assets/Scripts/DefenseDot.asmdef` | URP 런타임 참조 2개 추가 (Core + Universal) | 수정 |
| `Assets/Scripts/Systems/Visual/PostFx/PostFxBinder.cs` | 리그·Volume 주입, profile 재클론, LateUpdate 폴링으로 DoF focusDistance 갱신, 순수 매핑 함수 | 신규 |
| `Assets/Tests/EditMode/PostFxBinderTests.cs` | 거리→focusDistance 순수 함수 EditMode 단위 테스트 | 신규 |
| `Assets/Scripts/Systems/Mode/ModeBootstrap.cs` | PostFx 직렬화 필드 추가, `BindCamera`→`BindPresentation` 확장 | 수정 |
| `Assets/Scripts/Systems/Mode/ArenaModeBootstrap.cs` | `BindCamera(ctx)`→`BindPresentation(ctx)` | 수정 |
| `Assets/Scripts/Systems/Mode/GridDefenseModeBootstrap.cs` | `BindCamera(ctx)`→`BindPresentation(ctx)` | 수정 |
| `Assets/Settings/HD2D/HD2D_Arena.asset` | Arena VolumeProfile 프리셋(5컴포넌트) | 신규(에디터) |
| `Assets/Settings/HD2D/HD2D_Grid.asset` | Grid VolumeProfile 프리셋(값 차등) | 신규(에디터) |
| `Assets/Settings/PC_RPAsset.asset` | Depth Texture ON 점검(DoF 필수) | 점검 |
| `Assets/Scenes/AreanaScene.unity` | Main Camera 리그 부착(Phase1 잔여) + 부트스트랩 PostFx 배선 | 수정(에디터) |
| `Assets/Scenes/GridScene.unity` | 부트스트랩 PostFx 배선(리그 부착됨) | 수정(에디터) |

> **테스트 asmdef 변경 없음**: `PostFxBinderTests` 는 `ResolveFocusDistance(float)→float` 순수 함수와 `const float` 만 호출하므로 URP 참조가 불필요하다(CS0012 미발생 — 검증 확인). 향후 "sharedProfile 불변 회귀 테스트"(DepthOfField 사용)를 추가하는 시점에 `DefenseDot.Tests.EditMode.asmdef` 에 `Unity.RenderPipelines.Core.Runtime` **및** `Unity.RenderPipelines.Universal.Runtime` 두 개를 함께 추가한다.

---

## Task 1 — DefenseDot.asmdef 에 URP 런타임 참조 추가

URP Volume/DepthOfField 타입을 코드에서 다루는 모든 후속 Task의 선결 조건. 어셈블리명 오타 시 메인 어셈블리 전체 컴파일 실패.

**Files:** `Assets/Scripts/DefenseDot.asmdef`

- [ ] **Step 1: references 에 두 어셈블리 추가** — 파일 전문을 다음으로 교체:

```json
{
    "name": "DefenseDot",
    "rootNamespace": "",
    "references": [
        "UniTask",
        "Unity.Collections",
        "Unity.Burst",
        "Unity.TextMeshPro",
        "Unity.InputSystem",
        "UnityEngine.UI",
        "Unity.RenderPipelines.Core.Runtime",
        "Unity.RenderPipelines.Universal.Runtime"
    ],
    "includePlatforms": [],
    "excludePlatforms": [],
    "allowUnsafeCode": false,
    "overrideReferences": false,
    "precompiledReferences": [],
    "autoReferenced": true,
    "defineConstraints": [],
    "versionDefines": [],
    "noEngineReferences": false
}
```
- `Unity.RenderPipelines.Core.Runtime` — Volume / VolumeProfile / VolumeComponent / TryGet.
- `Unity.RenderPipelines.Universal.Runtime` — Bloom / ColorAdjustments / Tonemapping / Vignette / **DepthOfField**(Universal 네임스페이스).

- [ ] **Step 2: 컴파일 확인** — Unity 포커스 전환 → 자동 컴파일. 기대: 콘솔 컴파일 에러 0, asmdef 인스펙터 References 에 두 URP 어셈블리가 `(Missing)` 아님.

---

## Task 2 — PostFxBinder 순수 로직 EditMode 테스트 (TDD: Red)

구현 전 테스트 작성. 검증 대상: "리그 Distance → DoF focusDistance" 매핑. 규칙: `focusDistance = max(distance, 0.1)` (focusDistance 는 MinFloatParameter 라 양수 하한).

**Files:** `Assets/Tests/EditMode/PostFxBinderTests.cs`

- [ ] **Step 1: 테스트 파일 작성**

```csharp
// PostFxBinder 순수 로직 단위 테스트 — 거리→DoF focus 매핑
using NUnit.Framework;
using DefenseDot.Systems.Visual.PostFx;

namespace DefenseDot.Tests.EditMode
{
    /// <summary> PostFxBinder의 거리→focusDistance 순수 매핑을 검증합니다. </summary>
    public sealed class PostFxBinderTests
    {
        [Test]
        public void ResolveFocusDistance_PositiveDistance_ReturnsSameValue()
        {
            float focus = PostFxBinder.ResolveFocusDistance(30f);
            Assert.AreEqual(30f, focus, 0.0001f);
        }

        [Test]
        public void ResolveFocusDistance_Zero_ClampsToMinimum()
        {
            float focus = PostFxBinder.ResolveFocusDistance(0f);
            Assert.AreEqual(PostFxBinder.MinFocusDistance, focus, 0.0001f);
        }

        [Test]
        public void ResolveFocusDistance_Negative_ClampsToMinimum()
        {
            float focus = PostFxBinder.ResolveFocusDistance(-5f);
            Assert.AreEqual(PostFxBinder.MinFocusDistance, focus, 0.0001f);
        }

        [Test]
        public void ResolveFocusDistance_BelowMinimum_ClampsToMinimum()
        {
            float focus = PostFxBinder.ResolveFocusDistance(0.05f);
            Assert.AreEqual(PostFxBinder.MinFocusDistance, focus, 0.0001f);
        }
    }
}
```

- [ ] **Step 2: 실패 확인(Red)** — EditMode Test Runner. 기대: 컴파일 실패(`PostFxBinder`/`ResolveFocusDistance`/`MinFocusDistance` 미존재). 정상.

---

## Task 3 — PostFxBinder 구현 (TDD: Green)

`CenterFocusCameraRig.Distance` 폴링 → 글로벌 Volume DoF focusDistance 갱신. 에셋 오염 방지를 위해 **stale 인스턴스 무효화 후 재클론**한 `volume.profile`(인스턴스 사본)에만 기록.

**Files:** `Assets/Scripts/Systems/Visual/PostFx/PostFxBinder.cs`

- [ ] **Step 1: 구현 작성**

```csharp
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
```

- [ ] **Step 2: 컴파일 + 테스트(Green)** — Unity 컴파일 → EditMode `PostFxBinderTests` 4건 실행. 기대: 4 PASS, 콘솔 에러 0. `sync_cs_filename.py` 훅이 파일명 유지 확인.

---

## Task 4 — ModeBootstrap 을 BindPresentation 으로 확장

`BindCamera`→`BindPresentation`: (1) 카메라 바인딩(유지) (2) 모드별 프리셋을 `sharedProfile` 로 참조 교체 (3) **3종 모두 배선됐을 때만** `PostFxBinder.Bind` 위임.

**Files:** `Assets/Scripts/Systems/Mode/ModeBootstrap.cs`

- [ ] **Step 1: 파일 전문 교체**

```csharp
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
            // 1) 카메라 바인딩 (config는 리그가 단독 소유)
            if (cameraRig != null) cameraRig.Bind(ctx.CoreCenter);

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
    }
}
```

- [ ] **Step 2:** 컴파일 에러 발생 예상(하위가 아직 `BindCamera` 호출) → Task 5 에서 해소.

---

## Task 5 — 하위 부트스트랩 호출부 교체

**Files:** `Assets/Scripts/Systems/Mode/ArenaModeBootstrap.cs`, `Assets/Scripts/Systems/Mode/GridDefenseModeBootstrap.cs`

- [ ] **Step 1:** 두 파일 Read 로 `BindCamera(ctx);` 컨텍스트 확인 (조사: ArenaModeBootstrap.cs:32, GridDefenseModeBootstrap.cs:24, return 직전).
- [ ] **Step 2:** `ArenaModeBootstrap.cs` 에서 `BindCamera(ctx);` → `BindPresentation(ctx);` (Edit).
- [ ] **Step 3:** `GridDefenseModeBootstrap.cs` 에서 `BindCamera(ctx);` → `BindPresentation(ctx);` (Edit).
- [ ] **Step 4:** Unity 컴파일. 기대: 콘솔 에러 0, `BindCamera` 잔존 참조 0 (Grep `BindCamera` → 결과 없음).

---

## Task 6 — 모드별 VolumeProfile 프리셋 에셋 생성 (에디터 작업)

`HD2D_Arena.asset` / `HD2D_Grid.asset` 생성 후 각각 5컴포넌트(Bloom, Color Adjustments, Tonemapping, Vignette, Depth of Field) 추가. 셰이더 코드 0, 모드별 차이는 값으로만.

> **읽기전용 프리셋**: 디자이너 기본값. 런타임에 코드가 `.value` 를 쓰는 유일 파라미터는 DoF focusDistance 이며, `volume.profile`(인스턴스 사본)에만 기록되므로 이 디스크 에셋은 오염되지 않는다.
> **enum 은 심볼만**: `TonemappingMode.ACES`, `DepthOfFieldMode.Bokeh` (정수 캐스팅 금지).
> 기존 `DefaultVolumeProfile.asset` 은 테스트 컴포넌트 혼재 → 복제 베이스 부적합. **신규 빈 프로파일에 수동 추가**(값은 `SampleSceneProfile.asset` 참고 가능).

**Files:** `Assets/Settings/HD2D/HD2D_Arena.asset`(신규), `Assets/Settings/HD2D/HD2D_Grid.asset`(신규)

- [ ] **Step 1:** `Assets > Create > Rendering > URP Volume Profile` 로 `Assets/Settings/HD2D/` 에 `HD2D_Arena` 생성. (HD2D 폴더는 Phase1 의 CameraRig_*.asset 으로 이미 존재)
- [ ] **Step 2:** 동일 방식으로 `HD2D_Grid` 생성.
- [ ] **Step 3 (HD2D_Arena):** Add Override 로 5컴포넌트 추가, 각 파라미터 override 체크 ON 후 값 입력:
  - **Bloom**: Threshold 0.9 / Intensity 0.9 / Scatter 0.7 / Tint FFF4E0(따뜻) / High Quality Filtering ON(PC 우선)
  - **Color Adjustments**: Post Exposure 0 / Contrast 10 / Color Filter FFEAD0 / Hue Shift 0 / Saturation 12
  - **Tonemapping**: Mode = ACES
  - **Vignette**: Intensity 0.3 / Smoothness 0.4 / Rounded OFF / Color 검정
  - **Depth of Field**: Mode = Bokeh / Focus Distance 30(CameraRig_Arena.distance=30 정합) / Aperture 5.6 / Focal Length 50
- [ ] **Step 4 (HD2D_Grid):** 동일 5컴포넌트, Grid 톤으로 차등:
  - Bloom Intensity 0.6, Tint 흰색 / Color Adjustments Saturation 5, Color Filter 중립 / Tonemapping ACES / Vignette Intensity 0.25
  - **Depth of Field**: Mode Bokeh / Aperture 8 / Focus Distance = **`Assets/Settings/HD2D/CameraRig_Grid.asset` 의 `distance` 필드를 Read 로 확인해 그 값 입력**(런타임에 PostFxBinder 가 덮어쓰므로 에디터 프리뷰 정합용)
- [ ] **Step 5 (수동 검증):** 각 프로파일에 정확히 5컴포넌트 / DoF Mode 둘 다 Bokeh / Tonemapping 둘 다 ACES / `Glob Assets/Settings/HD2D/HD2D_*.asset` 로 .asset 2개 생성 확인.

---

## Task 7 — 씬 배선 및 깊이 텍스처 점검 (에디터 작업)

글로벌 Volume(기존)·리그·부트스트랩 PostFx 참조를 두 씬에 배선. DoF 는 깊이 텍스처 필수.

**Files:** `Assets/Scenes/AreanaScene.unity`, `Assets/Scenes/GridScene.unity` (+ RP 에셋 점검)

### 7-A. 깊이 텍스처 점검
- [ ] `Assets/Settings/PC_RPAsset.asset` **Depth Texture = ON** 강제(검증: 현재 ON). DoF·Bokeh 필수.
- [ ] `Assets/Settings/Mobile_RPAsset.asset` 은 **점검만, 변경하지 않음** (모바일 Bokeh DoF 는 Phase2 비범위 — 위험 R5). 현재 OFF.
- [ ] 두 씬 Main Camera `Post Processing = ON`, `Depth Texture = Use Pipeline Settings` 확인.

### 7-B. AreanaScene 배선
- [ ] **Phase1 잔여(선결)**: AreanaScene Main Camera 가 orthographic + `CenterFocusCameraRig` 미부착 상태(검증 확인). 리그 부착 후 `config` ← `CameraRig_Arena.asset`. (리그 Bind 가 런타임에 perspective 로 전환 → Bokeh DoF 전제 충족)
- [ ] Arena 부트스트랩 인스펙터:
  - `Camera Rig` ← Main Camera 의 CenterFocusCameraRig
  - `Global Volume` ← 씬 'Global Volume'(m_IsGlobal:1) 의 Volume 컴포넌트
  - `Post Fx Profile` ← `HD2D_Arena.asset`
  - `Post Fx Binder` ← `PostFxBinder` 컴포넌트(Global Volume 또는 부트스트랩 GameObject 에 1개 추가 후 배선)

### 7-C. GridScene 배선
- [ ] Grid 부트스트랩 인스펙터:
  - `Camera Rig` ← Main Camera 의 CenterFocusCameraRig(이미 부착, 참조만 확인)
  - `Global Volume` ← 씬 'Global Volume' Volume
  - `Post Fx Profile` ← `HD2D_Grid.asset`
  - `Post Fx Binder` ← `PostFxBinder` 컴포넌트(추가 후 배선)

- [ ] **수동 검증(all-or-nothing)**: 두 부트스트랩의 4개 PostFx/카메라 슬롯이 **모두 None 아님**(부분 배선 금지 — BindPresentation 의 3단계 가드가 부분 배선을 무시하므로 룩이 안 먹는 혼선 방지). 콘솔 Missing Reference 경고 0.

---

## Task 8 — 통합 검증 (PC 타깃 · Play + 시각/콘솔)

> **전제: PC 타깃(PC_RPAsset 활성, Depth Texture ON) 기준.** 모바일 룩은 R5 후속(모바일 전용 프로파일/깊이)에서 별도 검증.

**Files:** (없음 — 런타임 검증)

- [ ] **8-A EditMode 테스트:** 전체 실행 → `PostFxBinderTests` 4건 + 기존 전부 PASS.
- [ ] **8-B Arena Play:** AreanaScene Play → `Unity.GetConsoleLogs` 에러/예외 0 → Game View 또는 `Unity.SceneView` 스크린샷으로 확인:
  - 블룸(따뜻한 발광) / 가장자리 비네팅 / ACES+따뜻한 색감 / **DoF 로 전경·배경 블러, 중심부 선명(육안 합격 기준)**.
- [ ] **8-C DoF↔리그:** 런타임 Volume 의 profile 인스펙터에서 focusDistance 가 리그 distance(예: 30)에 정합되는지 확인. (거리 변동 시 추종)
- [ ] **8-D 에셋 오염 불변식(최우선):** Play 종료 후 `git status` 에 `HD2D_Arena.asset`/`HD2D_Grid.asset`/`SampleSceneProfile.asset` **변경 없음** 확인(런타임 동적 값이 디스크 프리셋에 새지 않음).
- [ ] **8-E Grid Play:** GridScene Play → 콘솔 에러 0 → 스크린샷으로 Grid 톤(약한 블룸, Grid distance 정합 DoF) 확인.
- [ ] **8-F (선택) Screen Damage 공존:** 피해 비네팅 활성 시 HD2D Vignette 와 누적 과다 여부 시각 확인. 과하면 프리셋 Vignette Intensity 하향(에셋 값만).

---

## Task 9 — 커밋 (사용자 승인 + lint 후)

> 사용자 명시 승인 없이 커밋 금지. 커밋 직전 lint 게이트 필수.

- [ ] `superpowers:verification-before-completion` 으로 Task 8 결과(테스트 PASS, 콘솔 에러 0, 에셋 오염 0)를 증거와 함께 재확인.
- [ ] `lint` 스킬로 변경 `.cs`(PostFxBinder.cs, ModeBootstrap.cs, ArenaModeBootstrap.cs, GridDefenseModeBootstrap.cs, PostFxBinderTests.cs) 검증.
- [ ] **사용자 커밋 승인 요청.** 승인 시 선별 스테이징(병행 작업 혼입 주의) 후 커밋.
- [ ] 커밋 메시지 예: `feat: HD-2D Phase 2 빌트인 포스트FX 스택(Volume 기반) 도입`

---

## 위험 및 주의 (실행 중 참조)

- **R1 에셋 오염(최우선)**: `SampleSceneProfile.asset`(guid 10fc4df2)이 두 씬 Global Volume + PC_RPAsset default 로 3중 공유. DoF 동적 기록은 반드시 `volume.profile`(인스턴스 사본). 프리셋 교체는 `sharedProfile` 참조 할당만(값 미수정).
- **R2 clone-on-access**: `volume.profile` 은 첫 접근 시에만 sharedProfile 복제. `PostFxBinder.Bind` 가 `profile=null` 후 재클론으로 stale 방지(코드 반영됨). 모드=씬 1:1 이라 재바인드는 드물며, 재바인드 시 직전 인스턴스 사본은 GC 대상(미세 누수, 무시 가능).
- **R3 깊이 텍스처**: DoF 는 깊이 필요. PC_RPAsset Depth Texture ON 필수(Task 7-A).
- **R4 Arena 리그 미부착(Phase1 잔여)**: AreanaScene Main Camera orthographic + 리그 미부착 → Bokeh DoF 비정상. Task 7-B 리그 부착 선행.
- **R5 모바일 비용(§8-R3)**: Bokeh DoF + Bloom HighQuality 는 모바일 프레임 저하. Phase2 는 PC 우선, Mobile_RPAsset 깊이 OFF 유지. 모바일 분기(DoF Gaussian/비활성)는 후속.
- **R6 asmdef 어셈블리명**: `Unity.RenderPipelines.Core.Runtime`/`Unity.RenderPipelines.Universal.Runtime` 정확히(오타 시 전체 컴파일 실패). DepthOfField/Bloom/Tonemapping/Vignette/ColorAdjustments 5종은 Universal, Volume/VolumeProfile/TryGet 은 Core.
- **R7 enum 정수 캐스팅 금지**: `TonemappingMode`/`DepthOfFieldMode` 는 심볼로만.
- **R8 API 최종 대조**: 검증은 17.4.0 로컬 소스 기반. 컴파일 에러 시 `Library/PackageCache/com.unity.render-pipelines.universal@17.4.0` 소스로 프로퍼티명 대조.

---

## Self-Review

**스펙 커버리지(§4.2-A):** ②블룸·③색감(ColorAdjustments+Tonemapping ACES)·③비네팅 → Task 6. ①틸트시프트 근사(DoF Bokeh, focus≈distance) → Task 3·6·8. 모드별 프로파일 2개 → Task 6. 글로벌 Volume swap → Task 4·7. BindCamera→BindPresentation → Task 4. 하위 호출 교체 → Task 5. asmdef URP 참조 → Task 1. DoF↔리그(인스턴스에만 기록) → Task 3. ✓

**플레이스홀더:** 코드 Task(1·3·4·5) 전문 포함, "TODO/적절히 처리" 0. 에디터 Task(6·7) 메뉴 경로·수치·검증 기준 구체화. Task 6 Grid DoF 값은 "CameraRig_Grid.distance Read 후 입력"으로 자기완결화. ✓

**타입 일관성:** Volume/VolumeProfile(Core), DepthOfField/Bloom/ColorAdjustments/Tonemapping/Vignette(Universal). 파라미터 `.value`, 획득 `TryGet<T>(out T)`, 교체 `sharedProfile`(참조)·동적 `profile`(인스턴스). enum 심볼만. `ResolveFocusDistance`/`MinFocusDistance`/`Bind`/`BindPresentation` 명칭 Task 간 일치. ✓

**컨벤션·소유권:** [SerializeField] protected camelCase, 접근제한자 명시, 한국어 `<summary>`, 부트스트랩 cameraConfig 미추가(Option A 유지). ✓

**검증 반영:** verify-completeness 의 critical(테스트 asmdef URP 참조 Task 삭제)·important(profile 재클론) 반영. verify-integration minor(부분 배선 가드, 모바일 점검만, PC 전제) 반영.

## 후속 Phase
- **Phase 3**: 픽셀 빌보드 스프라이트(논리/비주얼 분리, 알파클립 정렬, SpriteActorView 상태 연동).
- **Phase 4**: 커스텀 틸트시프트 Renderer Feature(빌트인 DoF 근사 대체, Render Graph). Phase2 DoF 와 공존/대체 전략, Screen Damage 렌더 순서 해소.
- **Phase 2 후속**: 모바일 전용 경량 프로파일 분기, Screen Damage 활성 시 Vignette 동적 하향.
