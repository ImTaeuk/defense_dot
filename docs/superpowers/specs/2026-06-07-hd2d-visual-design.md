# HD-2D 비주얼 연출 설계 (옥토패스 트래블러 스타일)

**작성일**: 2026-06-07
**상태**: 설계 승인 대기 (검토 요청)
**대상 프로젝트**: defense_dot (Unity 6000.2.x / URP 17.4)
**관련 스킬 흐름**: superpowers:brainstorming → (승인 시) superpowers:writing-plans

---

## 0. 이 문서의 역할 — 비주얼 작업 전 먼저 확인

> 이 문서는 **앞으로 모든 비주얼 연출 작업의 기준 레퍼런스**입니다.
> 카메라·포스트프로세싱·스프라이트/빌보드·셰이더 관련 작업을 시작하기 전에 **반드시 이 문서를 먼저 확인**하고, 이 문서의 원칙·구조·Phase 순서를 따른다.
> 새로운 비주얼 결정(아트 방향 변경, 효과 추가/제거)이 생기면 이 문서를 갱신한 뒤 진행한다.

---

## 1. 목표 & 확정 요구사항

옥토패스 트래블러의 화면 효과("HD-2D")를 defense_dot 에 도입한다. HD-2D 룩은 단일 효과가 아니라 **4개 독립 레이어의 합**이다.

| 항목 | 결정 | 비고 |
|---|---|---|
| **범위** | 풀 HD-2D (①②③④ 전부) | 아래 4개 레이어 모두 적용 |
| ① 틸트시프트 피사계 심도 | 적용 | 미니어처/디오라마 룩 (시그니처) |
| ② 블룸·빛 번짐 | 적용 | 따뜻·몽환적 분위기 |
| ③ 색감 그레이딩 + 비네팅 | 적용 | 회화적 톤, 가장자리 어둠 |
| ④ 픽셀 스프라이트 + 3D 배경 | 적용 | HD-2D 본질 (빌보드) |
| **스프라이트 소스** | 손으로 그린 픽셀 아트 | 정석 HD-2D, idle/이동/공격 애니메이션 프레임 필요 |
| **카메라** | 중앙 주시 오빗 리그 | 항상 맵 중앙을 바라봄 |
| 카메라 조절 | pitch/거리 등 **디자이너 조절** | 기본 약한 기울기(15~30°), 모드별 상이 |
| 카메라 조절 시점 | **에디터 + 런타임** 양쪽 | |
| **모드 연동** | 모드별 설정 분리 | 기존 모드별 합성 루트(`ModeBootstrap`) 활용 |

---

## 2. 핵심 개념 & 원칙 (반드시 숙지)

1. **HD-2D 는 "픽셀 퍼펙트"가 아니다.** 스프라이트를 고해상도로 그려 서브픽셀로 부드럽게 움직이고 포스트FX를 두껍게 얹는다(그래서 "HD"-2D). 도트를 저해상도 격자에 스냅시키는 레트로 방식과 정반대다. → 픽셀 스냅·저해상도 렌더타깃을 도입하지 않는다.
2. **틸트시프트 ≠ 일반 DoF.** 일반 DoF는 "거리(깊이)" 기준 블러, 틸트시프트는 "화면 세로 위치" 기준 블러다. 다만 카메라를 기울이면 위=원경/아래=근경이 되어 표준 DoF가 틸트시프트를 *근사*한다(Phase 2). 정밀 제어는 커스텀 Renderer Feature 로 한다(Phase 4).
3. **HD-2D = 3개 독립 하위시스템의 합**: ⒶPostFX(카메라가 본 화면에 후처리) Ⓑ카메라 리그 Ⓒ빌보드(2D 스프라이트를 3D 위에 세움). 각각 독립 테스트·교체 가능하도록 경계를 나눈다.
4. **알파 클립 정렬**: 스프라이트는 알파 블렌드가 아닌 알파 클립(cutout)+ZWrite 로 처리해 깊이 버퍼 기반으로 3D 오브젝트처럼 자동 정렬한다(반투명 정렬 문제 회피).
5. **설정(영구)과 런타임 상태(임시)의 분리**: 디자이너가 런타임에 조절해도 ScriptableObject 에셋이 오염되지 않도록, config 값은 인스턴스 필드로 복사해 사용한다.

---

## 3. 전체 아키텍처

### 3.1 신규 폴더/네임스페이스
```
Assets/Scripts/Systems/Visual/          (DefenseDot.Systems.Visual)
├── Camera/
│   ├── CenterFocusCameraRig.cs         [ExecuteAlways] 중앙 주시 리그
│   └── CameraRigConfig.cs              ScriptableObject (모드별 카메라 값)
├── Billboard/
│   ├── BillboardSprite.cs              스프라이트가 카메라를 보게 함
│   ├── SpriteActorView.cs              액터 상태 ↔ 스프라이트 애니메이션
│   └── SpriteAnimationSet.cs           상태별 프레임 배열 SO
└── PostFx/
    ├── TiltShiftRendererFeature.cs     커스텀 풀스크린 패스 (Phase 4)
    ├── TiltShiftPass.cs
    └── TiltShiftVolumeComponent.cs     디자이너 조절(VolumeComponent)

Assets/Settings/HD2D/                    URP Volume Profile(모드별), Renderer Data 참조
Assets/Shaders/HD2D/                     SpriteBillboard.shadergraph, TiltShift.shader
Assets/Art/Sprites/                      손그림 픽셀 아트 (추후 채움)
```

### 3.2 하위시스템 ↔ 기존 코드 연결
```
ModeBootstrap (기존, 모드별 합성 루트)
  + CenterFocusCameraRig 참조  (SerializeField)   ← 카메라 config는 리그가 소유
  + VolumeProfile 참조         (SerializeField, Phase 2)
  → CreateMode(ctx) 시:
        cameraRig.Bind(ctx.CoreCenter)            ← 런타임 중심만 주입 (Phase 1)
        globalVolume.profile = postFxProfile       ← Volume 교체 (Phase 2, BindPresentation)
        │
   ┌────┼─────────────────┬────────────────────────┐
   ▼    ▼                 ▼                         ▼
[카메라 리그]        [포스트FX]                [빌보드]              (기존 액터)
중앙 주시            Volume(모드별)             SpriteActorView ←──── MonsterActor / TowerActor
pitch/dist          + 틸트시프트 RF            BillboardSprite       (프리팹에 부착)
```

### 3.3 기존 코드 통합 지점 (변경 대상)
| 파일 | 변경 |
|---|---|
| `Systems/Mode/ModeBootstrap.cs` | `CenterFocusCameraRig` 참조 + `BindCamera`(P1, 카메라 config는 리그 소유). Phase 2에서 `VolumeProfile` + `BindPresentation`으로 확장 |
| `Systems/Mode/ArenaModeBootstrap.cs`, `GridDefenseModeBootstrap.cs` | `CreateMode` 말미에 `BindCamera(ctx)` 호출 (P1). Phase 2에서 `BindPresentation(ctx)`로 확장 |
| `Systems/Grid/MapVisualizer.cs` | `cameraPitch/Yaw/Padding` 직접 조작 로직(46~90행) 제거, 카메라 책임을 리그로 이관. fit-to-bounds 는 리그 옵션 또는 에디터 유틸로 흡수 |
| `Core/ActorBase.cs` | `public event System.Action<ActorState> StateChanged;` 추가, `SetState` 에서 invoke |
| `Settings/PC_Renderer.asset`, `Mobile_Renderer.asset` | `TiltShiftRendererFeature` 등록(PC 필수, Mobile 선택) |

---

## 4. 하위시스템 상세 설계

### 4.1 카메라 리그 (Phase 1)

**CameraRigConfig (ScriptableObject)** — `ArenaConfig` 와 동일 패턴(`[CreateAssetMenu(menuName="DefenseDot/CameraRigConfig")]`, 공개 필드 + `<summary>`).

| 필드 | 기본값 | 의미 |
|---|---|---|
| `pitch` | 25 | 상하 각 (0=수평, 90=탑다운) — **핵심 조절값** |
| `yaw` | 0 | 수평 회전 각 |
| `distance` | 30 | 중심→카메라 거리 |
| `heightOffset` | 0 | 타깃 높이 보정 |
| `perspective` | true | 원근(HD-2D 권장) / 직교 |
| `fieldOfView` / `orthoSize` | 40 / 15 | 투영별 크기 |
| `followLerp` | 0 | 타깃 추적 부드러움(0=즉시) |
| `pitchRange` | (10,60) | 런타임 조절 클램프 |

**CenterFocusCameraRig (MonoBehaviour, `[ExecuteAlways]`)** — 중심을 향해 카메라를 배치하는 단일 책임.
```csharp
Quaternion rot = Quaternion.Euler(currentPitch, currentYaw, 0f);
Vector3 focus = center + Vector3.up * heightOffset;
cam.transform.SetPositionAndRotation(focus - rot * Vector3.forward * distance, rot);
```
- 에디터 즉시 반영: `OnValidate()` → 슬라이더 ↔ 씬 카메라 실시간 연동.
- 런타임: `LateUpdate()` 타깃 추적(`followLerp`) + 공개 프로퍼티 `Pitch/Yaw/Distance { get; set; }`.
- 에셋 오염 방지: `Bind(center, config)` 시 config 값을 인스턴스 필드(`currentPitch` 등)로 복사. 런타임 조절은 인스턴스만 변경.
- **config 소유권(구현 시 보강 — Option A)**: config는 **리그(`CenterFocusCameraRig`)가 단독 소유**(단일 진실 원천 → 에디터 프리뷰=런타임 일치). 부트스트랩은 config를 들지 않고 리그만 참조하며, `BindCamera` 는 `cameraRig.Bind(ctx.CoreCenter)` 로 런타임 중심만 주입. (한 씬에서 여러 모드를 공용 리그로 돌릴 때만 `Bind(center, config)` 오버로드로 config 주입)

### 4.2 포스트FX 스택 (Phase 2 + Phase 4)

**A. Volume 기반 ②③ + ① 근사 (Phase 2, 셰이더 코드 0)**

모드별 프로파일: `Assets/Settings/HD2D/HD2D_Arena.asset`, `HD2D_Grid.asset`.

| 효과 | URP 컴포넌트 |
|---|---|
| ② 블룸 | Bloom |
| ③ 색감 | Color Adjustments + Tonemapping |
| ③ 비네팅 | Vignette |
| ① 근사 | Depth of Field (Bokeh), focusDistance ≈ 카메라-중심 거리 |

- 모드별 전환: 씬 글로벌 `Volume` 1개. `ModeBootstrap.BindPresentation` 이 활성 프로파일 교체. (프로파일은 읽기전용 프리셋 취급)
- DoF↔카메라 연동: `PostFxBinder` 가 리그 `Distance` 를 읽어 DoF `focusDistance` 갱신.

**B. 커스텀 틸트시프트 Renderer Feature (Phase 4, 정석 미니어처 룩)**
```
TiltShiftRendererFeature : ScriptableRendererFeature   (렌더러에 등록)
 └ TiltShiftPass : ScriptableRenderPass                (URP 17 Render Graph API)
TiltShiftVolumeComponent : VolumeComponent             (디자이너 조절, 프로파일 포함)
TiltShift.shader                                       (세로 블러 + 마스크 합성)
```
- `VolumeComponent` 로 노출: `focusCenter`(0~1), `focusWidth`, `blurStrength`. → 블룸/비네팅과 같은 모드별 프로파일에서 함께 조절, 블렌딩·런타임 애니메이션 무료.
- 동작: 임시 RT 에 2-pass 가우시안 → `lerp(원본, 블러, mask(uv.y))`.
- **URP 17(Unity 6)은 Render Graph 가 기본** — 구식 `cmd.Blit` 예제 금지, `RenderGraph.AddRasterRenderPass` 패턴 사용. (구현 시 Unity API 사실 검증 필요)
- PC 렌더러 필수 등록, Mobile 은 비용상 DoF 근사만 사용 가능.
- `ExternalResources/Screen Damage`(풀스크린 피해 비네팅)와 공존 — 렌더 순서만 주의(틸트시프트 → 피해 오버레이).

### 4.3 빌보드 시스템 (Phase 3 + Phase 5)

**프리팹 구조 (논리 ↔ 비주얼 분리)**
```
Enemy.prefab (루트: MonsterActor + SpriteActorView)   ← 루트=논리 위치(XZ 이동·콜라이더)
└── Visual (자식: BillboardSprite + SpriteRenderer)    ← 빌보드 회전·시각 오프셋 독립
    └── ShadowBlob (옵션, 접지 그림자)
```

**BillboardSprite (MonoBehaviour)** — LateUpdate 에서 카메라를 보게 회전.
- `BillboardMode { CameraPlane, YAxisUpright }`. 옥토패스의 "서 있는" 느낌엔 **YAxisUpright 권장**.
  - CameraPlane: `rotation = cam.rotation` (화면과 평행, 왜곡 0)
  - YAxisUpright: 수직 유지 + Y회전 정면
- 성능: 적 최대 80 → LateUpdate 무시 가능. Phase 5 에서 버텍스 셰이더 빌보드로 전환 옵션.

**정렬·깊이**: 알파 클립(cutout) + ZWrite On → 깊이 기반 자동 정렬. `SortingGroup` 보조 가능.

**SpriteActorView (MonoBehaviour)** — 액터 상태 ↔ 애니메이션.
- `ActorBase.StateChanged` 이벤트 구독 → idle/move/attack/death 전환.
- **경량 프레임 플레이어**: `SpriteAnimationSet`(상태별 프레임 배열 SO)을 fps로 재생. `Animator` 대신(80개 적 오버헤드·풀링 친화).
- 이동 방향 ↔ 카메라 right 비교로 좌우 플립(선택).
- 풀링: `OnSpawn` → Idle → 상태 이벤트로 idle 자동 재생.

---

## 5. 데이터/설정 모드별 연동 요약

| 모드 | 카메라 | 포스트FX | 비고 |
|---|---|---|---|
| Arena | `CameraRigConfig`(Arena) | `HD2D_Arena.asset` | `ArenaView.Config.coreRadius` 등과 함께 튜닝 |
| Grid | `CameraRigConfig`(Grid) | `HD2D_Grid.asset` | `MapVisualizer` fit-to-bounds 연동 |

각 `ModeBootstrap` 에 리그 참조(+ Phase 2의 Volume profile)를 직렬화 필드로 꽂는다. **카메라 config는 씬별 리그(`CenterFocusCameraRig`)가 소유**한다(부트스트랩은 들지 않음).

---

## 6. Phase 로드맵

| Phase | 내용 | 산출물 | 위험 | 검증 |
|---|---|---|---|---|
| **P1 카메라 리그** | 중앙 주시 리그 + 모드별 config, MapVisualizer 카메라 이관 | `CenterFocusCameraRig`, `CameraRigConfig` | 낮음 | 에디터 슬라이더↔씬 즉시 반영, 플레이 중 중앙 유지 |
| **P2 포스트FX(빌트인)** | Volume 로 ②③ + 표준 DoF 로 ① 근사 (모드별) | `HD2D_*.asset`, `PostFxBinder` | 낮음 | 모드 전환 시 룩 변화, DoF 초점=중심 |
| **P3 빌보드 MVP** | 플레이스홀더 스프라이트로 빌보드+정렬+상태연동 | `BillboardSprite`, `SpriteActorView`, `ActorBase` 이벤트 | 중간 | 스프라이트가 서고 카메라 회전에도 정면 유지, 깊이 정렬 정상 |
| **P4 정석 틸트시프트** | 커스텀 Renderer Feature(세로 블러, 디자이너 조절) | `TiltShiftRendererFeature` 외 | 중간 | 초점 띠만 선명, 프로파일 값 실시간 반영 |
| **P5 스프라이트 셰이더·아트** | 알파클립+깊이+림 셰이더, 접지 그림자, 손그림 아트 교체, 셰이더 빌보드 | `SpriteBillboard.shadergraph` | 중간 | 손그림 아트 적용 후 최종 룩 |

> P1→P2 만 완료해도 "기운 카메라 + 블룸/색감"으로 옥토패스 분위기의 절반이 나온다. 각 Phase 는 독립 빌드·검증 가능.

---

## 7. 테스트 / 검증 시나리오

| # | 시나리오 | Expected |
|---|---|---|
| T1 | 에디터에서 `pitch` 슬라이더 조절 | 씬 카메라가 중앙 주시 유지하며 즉시 각도 변경 |
| T2 | 런타임 중 `rig.Pitch` 변경 | 카메라 각도 변경, config 에셋은 불변 |
| T3 | 모드 전환(Arena↔Grid) | 카메라 config·Volume 프로파일이 모드별로 교체 |
| T4 | DoF 초점 | 카메라 거리 변경 시 초점이 중심에 유지 |
| T5 | 적 80마리 스폰 | 모든 스프라이트 카메라 정면, 서로/배경과 깊이 정렬 정상, 프레임 저하 없음 |
| T6 | 적 상태 전환(이동→사망) | 스프라이트 애니메이션 전환, 풀 회수 후 재스폰 시 idle 정상 |
| T7 | 틸트시프트 `focusWidth` 조절 | 선명 띠 폭 실시간 변화 |

---

## 8. 위험 & 미결정 사항

- **R1 (P4)** URP 17 Render Graph API 는 자료가 변동적 — 구현 전 Unity 공식 문서로 API 검증 필요(judge 활용).
- **R2 (P5)** 손그림 픽셀 아트 제작/조달 일정은 별도 트랙. 시스템은 어떤 스프라이트든 동작하도록 설계(플레이스홀더로 선검증).
- **R3** Mobile 렌더러에서 커스텀 틸트시프트 비용 — 필요 시 DoF 근사로 대체.
- **미결정 D1** MapVisualizer fit-to-bounds 를 리그 옵션으로 흡수할지 / 에디터 전용 유틸로 분리할지 → P1 구현 시 결정.
- **미결정 D2** 8방향 스프라이트 vs 좌우 플립만 → 아트 분량에 따라 P5 에서 결정.
- **미결정 D3** 접지 그림자: 블롭 스프라이트 vs 프로젝터/데칼 → P5 에서 결정.

---

## 9. 컨벤션 준수 메모

- 네임스페이스 `DefenseDot.Systems.Visual.*`, 기존 `Systems.*` 규칙 일치.
- MonoBehaviour 필드: `[SerializeField] private camelCase` (m_/_ 금지). ScriptableObject(`CameraRigConfig`)는 기존 `ArenaConfig` 컨벤션(공개 필드 + `<summary>`) 일치.
- 모든 멤버 명시적 접근 제한자(IDE0040).
- System 라이브러리 풀패스(`System.Action`), 컬렉션 임시 사용 시 `UnityEngine.Pool.CollectionPool`.
- 비동기 필요 시 UniTask 만(Coroutine/Task 금지) — 프레임 애니메이션은 LateUpdate 기반 경량 플레이어 우선.
- 커밋 전 `lint` 스킬로 컨벤션 검증.

---

## 10. 향후 비주얼 작업 가이드 (이 문서 사용법)

1. 비주얼 관련 작업 시작 전 **§0·§2(원칙)** 을 먼저 확인한다.
2. 어떤 효과/시스템인지 §4 에서 해당 하위시스템 설계를 확인한다.
3. Phase 순서(§6)를 지킨다 — 카메라 → 빌트인 PostFX → 빌보드 → 커스텀 틸트시프트 → 셰이더/아트.
4. 새 비주얼 결정이 생기면 이 문서의 해당 절을 갱신한 뒤 진행한다(특히 §8 미결정 사항 해소 시).
5. 셰이더/Renderer Feature 작업은 URP 17 Render Graph 기준임을 잊지 않는다(§4.2-B, §8-R1).
