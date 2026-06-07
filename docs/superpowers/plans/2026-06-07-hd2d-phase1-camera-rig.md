# HD-2D Phase 1 — 중앙 주시 카메라 리그 Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

> **프로젝트 규칙 주의:** 이 저장소는 "사용자의 명시적 요청 없이 git 커밋 금지" 규칙이 있습니다. 각 태스크의 커밋 단계는 **실행 시 사용자 승인 후** 수행하며, 커밋 전 반드시 `lint` 스킬로 컨벤션을 검증합니다.

**Goal:** 맵 중앙을 항상 바라보며 pitch/거리를 디자이너가 에디터·런타임에서 조절할 수 있는 카메라 리그를, 모드별 설정으로 분리해 도입한다.

**Architecture:** 카메라 배치 수학을 순수 함수(`CameraRigMath`)로 분리해 단위 테스트하고, `[ExecuteAlways]` MonoBehaviour(`CenterFocusCameraRig`)가 이를 호출해 에디터/런타임에 적용한다. 모드별 값은 `CameraRigConfig`(ScriptableObject)로 분리하고, 기존 `ModeBootstrap` 합성 루트가 `ctx.CoreCenter` 와 함께 주입한다.

**Tech Stack:** Unity 6000.2 / URP 17.4 / C# / Unity Test Framework(NUnit, EditMode). Phase 1은 URP 타입 의존 없음(순수 UnityEngine).

**관련 스펙:** `docs/superpowers/specs/2026-06-07-hd2d-visual-design.md` §4.1, §6(P1)

---

## 구현 중 변경 (Option A — config 소유권 단순화)

원안의 Task 4 는 `ModeBootstrap` 에 `cameraConfig` 직렬화 필드를 두고 `cameraRig.Bind(center, cameraConfig)` 로 주입했으나, 구현·검증 후 **config를 리그가 단독 소유**하도록 단순화했다(단일 진실 원천 → 에디터 프리뷰=런타임 일치, 이중 배선 제거). 최종 적용본:
- `CenterFocusCameraRig` 에 `Bind(Vector3 center)` 오버로드 추가(자체 `config` 재사용). 기존 `Bind(center, config)` 는 런타임 config 교체용으로 유지.
- `ModeBootstrap` 에서 `cameraConfig` 필드 제거, `BindCamera` 는 `cameraRig.Bind(ctx.CoreCenter)`.
- 테스트 `BindCenterOnly_ReusesRigConfig` 추가(EditMode 총 8개).

> 아래 Task 4 등의 코드 블록은 **원안 기준**이며, 실제 적용본은 위 변경을 반영한다.

---

## File Structure

| 파일 | 책임 | 신규/수정 |
|---|---|---|
| `Assets/Scripts/Systems/Visual/Camera/CameraRigMath.cs` | 중심·각도·거리 → 카메라 포즈 계산(순수 함수) | 신규 |
| `Assets/Scripts/Systems/Visual/Camera/CameraRigConfig.cs` | 모드별 카메라 값(ScriptableObject) | 신규 |
| `Assets/Scripts/Systems/Visual/Camera/CenterFocusCameraRig.cs` | 리그 컴포넌트(에디터/런타임 적용) | 신규 |
| `Assets/Scripts/Systems/Mode/ModeBootstrap.cs` | 카메라 주입 필드 + `BindCamera` 헬퍼 | 수정 |
| `Assets/Scripts/Systems/Mode/ArenaModeBootstrap.cs` | `CreateMode` 에서 `BindCamera(ctx)` 호출 | 수정 |
| `Assets/Scripts/Systems/Mode/GridDefenseModeBootstrap.cs` | `CreateMode` 에서 `BindCamera(ctx)` 호출 | 수정 |
| `Assets/Scripts/Systems/Grid/MapVisualizer.cs` | 카메라 책임 제거(pitch/yaw/focus) | 수정 |
| `Assets/Scripts/Editor/MapVisualizerEditor.cs` | "Focus Camera" 버튼 제거 | 수정 |
| `Assets/Tests/EditMode/CameraRigMathTests.cs` | `CameraRigMath` 단위 테스트 | 신규 |
| `Assets/Tests/EditMode/CenterFocusCameraRigTests.cs` | 리그 통합(EditMode) 테스트 | 신규 |

> 네임스페이스: `DefenseDot.Systems.Visual.Camera`. 새 `.cs` 는 `Assets/Scripts/` 하위라 메인 `DefenseDot` 어셈블리에 자동 포함된다(별도 asmdef 불필요).

---

## Task 1: CameraRigMath — 순수 카메라 배치 계산

**Files:**
- Create: `Assets/Scripts/Systems/Visual/Camera/CameraRigMath.cs`
- Test: `Assets/Tests/EditMode/CameraRigMathTests.cs`

- [ ] **Step 1: 실패하는 테스트 작성**

`Assets/Tests/EditMode/CameraRigMathTests.cs`:
```csharp
using NUnit.Framework;
using UnityEngine;
using DefenseDot.Systems.Visual.Camera;

namespace DefenseDot.Tests.EditMode
{
    public class CameraRigMathTests
    {
        [Test]
        public void Solve_HorizontalDefault_PlacesBehindCenterAlongZ()
        {
            CameraPose pose = CameraRigMath.Solve(Vector3.zero, 0f, 0f, 10f, 0f);
            Assert.AreEqual(0f, pose.Position.x, 0.001f);
            Assert.AreEqual(0f, pose.Position.y, 0.001f);
            Assert.AreEqual(-10f, pose.Position.z, 0.001f);
        }

        [Test]
        public void Solve_TopDown_PlacesAboveCenter()
        {
            CameraPose pose = CameraRigMath.Solve(Vector3.zero, 90f, 0f, 10f, 0f);
            Assert.AreEqual(0f, pose.Position.x, 0.01f);
            Assert.AreEqual(10f, pose.Position.y, 0.01f);
            Assert.AreEqual(0f, pose.Position.z, 0.01f);
        }

        [Test]
        public void Solve_HeightOffset_RaisesPositionByOffset()
        {
            CameraPose pose = CameraRigMath.Solve(Vector3.zero, 0f, 0f, 10f, 2f);
            Assert.AreEqual(2f, pose.Position.y, 0.001f);
        }

        [Test]
        public void Solve_AnyAngle_CameraForwardPointsAtCenter()
        {
            Vector3 center = new Vector3(3f, 1f, -2f);
            CameraPose pose = CameraRigMath.Solve(center, 35f, 45f, 12f, 0f);
            Vector3 toCenter = (center - pose.Position).normalized;
            Vector3 forward = pose.Rotation * Vector3.forward;
            Assert.AreEqual(1f, Vector3.Dot(toCenter, forward), 0.001f);
        }
    }
}
```

- [ ] **Step 2: 테스트 실패 확인**

Unity Test Runner(Window > General > Test Runner > EditMode > Run All) 실행.
Expected: 컴파일 에러("CameraRigMath/CameraPose 형식을 찾을 수 없음") 로 실패.

- [ ] **Step 3: 최소 구현 작성**

`Assets/Scripts/Systems/Visual/Camera/CameraRigMath.cs`:
```csharp
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
```

- [ ] **Step 4: 테스트 통과 확인**

Test Runner 에서 EditMode 재실행.
Expected: `CameraRigMathTests` 4개 PASS.

- [ ] **Step 5: 커밋** (사용자 승인 + `lint` 검증 후)

```bash
git add Assets/Scripts/Systems/Visual/Camera/CameraRigMath.cs Assets/Tests/EditMode/CameraRigMathTests.cs
git commit -m "feat(visual): 카메라 배치 순수 계산 CameraRigMath 추가"
```

---

## Task 2: CameraRigConfig — 모드별 카메라 설정 SO

**Files:**
- Create: `Assets/Scripts/Systems/Visual/Camera/CameraRigConfig.cs`
- Test: `Assets/Tests/EditMode/CameraRigMathTests.cs` (기존 파일에 테스트 추가)

- [ ] **Step 1: 실패하는 테스트 추가**

`CameraRigMathTests.cs` 의 클래스 안에 다음 테스트를 추가:
```csharp
        [Test]
        public void CameraRigConfig_HasExpectedDefaults()
        {
            var config = ScriptableObject.CreateInstance<CameraRigConfig>();
            Assert.AreEqual(25f, config.pitch, 0.001f);
            Assert.AreEqual(30f, config.distance, 0.001f);
            Assert.IsTrue(config.perspective);
            Object.DestroyImmediate(config);
        }
```

- [ ] **Step 2: 테스트 실패 확인**

Test Runner EditMode 실행.
Expected: 컴파일 에러("CameraRigConfig 형식을 찾을 수 없음").

- [ ] **Step 3: 최소 구현 작성**

`Assets/Scripts/Systems/Visual/Camera/CameraRigConfig.cs`:
```csharp
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
```

- [ ] **Step 4: 테스트 통과 확인**

Test Runner EditMode 재실행.
Expected: `CameraRigConfig_HasExpectedDefaults` 포함 전체 PASS.

- [ ] **Step 5: 커밋** (사용자 승인 + `lint` 검증 후)

```bash
git add Assets/Scripts/Systems/Visual/Camera/CameraRigConfig.cs Assets/Tests/EditMode/CameraRigMathTests.cs
git commit -m "feat(visual): 모드별 카메라 설정 CameraRigConfig 추가"
```

---

## Task 3: CenterFocusCameraRig — 리그 컴포넌트

**Files:**
- Create: `Assets/Scripts/Systems/Visual/Camera/CenterFocusCameraRig.cs`
- Test: `Assets/Tests/EditMode/CenterFocusCameraRigTests.cs`

- [ ] **Step 1: 실패하는 테스트 작성**

`Assets/Tests/EditMode/CenterFocusCameraRigTests.cs`:
```csharp
using NUnit.Framework;
using UnityEngine;
using DefenseDot.Systems.Visual.Camera;

namespace DefenseDot.Tests.EditMode
{
    public class CenterFocusCameraRigTests
    {
        [Test]
        public void Bind_PositionsCameraBehindCenter()
        {
            var go = new GameObject("RigCam");
            var cam = go.AddComponent<Camera>();
            var rig = go.AddComponent<CenterFocusCameraRig>();

            var config = ScriptableObject.CreateInstance<CameraRigConfig>();
            config.pitch = 0f;
            config.yaw = 0f;
            config.distance = 10f;
            config.heightOffset = 0f;

            rig.Bind(Vector3.zero, config);

            Assert.AreEqual(0f, cam.transform.position.x, 0.01f);
            Assert.AreEqual(0f, cam.transform.position.y, 0.01f);
            Assert.AreEqual(-10f, cam.transform.position.z, 0.01f);

            Object.DestroyImmediate(go);
            Object.DestroyImmediate(config);
        }

        [Test]
        public void PitchSetter_ClampsToConfigRange()
        {
            var go = new GameObject("RigCam");
            go.AddComponent<Camera>();
            var rig = go.AddComponent<CenterFocusCameraRig>();

            var config = ScriptableObject.CreateInstance<CameraRigConfig>();
            config.pitchRange = new Vector2(10f, 60f);
            rig.Bind(Vector3.zero, config);

            rig.Pitch = 200f;
            Assert.AreEqual(60f, rig.Pitch, 0.001f);
            rig.Pitch = -50f;
            Assert.AreEqual(10f, rig.Pitch, 0.001f);

            Object.DestroyImmediate(go);
            Object.DestroyImmediate(config);
        }
    }
}
```

- [ ] **Step 2: 테스트 실패 확인**

Test Runner EditMode 실행.
Expected: 컴파일 에러("CenterFocusCameraRig 형식을 찾을 수 없음").

- [ ] **Step 3: 최소 구현 작성**

`Assets/Scripts/Systems/Visual/Camera/CenterFocusCameraRig.cs`:
```csharp
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
        [SerializeField] private Camera targetCamera;
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
        public float Yaw { get => currentYaw; set => currentYaw = value; }

        /// <summary> 런타임 거리. </summary>
        public float Distance { get => currentDistance; set => currentDistance = value; }

        /// <summary>
        /// 모드 부트스트랩이 중심과 설정을 주입합니다. config 값을 런타임 상태로 복사합니다.
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

        private void OnEnable() => CopyFromConfig();

        private void CopyFromConfig()
        {
            if (config == null) return;
            currentPitch = config.pitch;
            currentYaw = config.yaw;
            currentDistance = config.distance;
            currentHeightOffset = config.heightOffset;
        }

        private Camera ResolveCamera()
        {
            if (targetCamera != null) return targetCamera;
            targetCamera = GetComponent<Camera>();
            if (targetCamera == null) targetCamera = Camera.main;
            return targetCamera;
        }

        private Vector3 GetCenter()
        {
            if (target != null) return target.position;
            if (hasRuntimeCenter) return runtimeCenter;
            return transform.position;
        }

        private void Update()
        {
            // 에디터 프리뷰: 플레이 중이 아니면 config를 직접 반영
            if (!Application.isPlaying)
            {
                CopyFromConfig();
                ApplyCameraProjection();
                ApplyPose(GetCenter(), instant: true);
            }
        }

        private void LateUpdate()
        {
            // 런타임: 현재 상태로 추적(부드러움 적용)
            if (Application.isPlaying)
            {
                float lerp = config != null ? config.followLerp : 0f;
                ApplyPose(GetCenter(), instant: lerp <= 0f);
            }
        }

        private void ApplyCameraProjection()
        {
            Camera cam = ResolveCamera();
            if (cam == null || config == null) return;
            cam.orthographic = !config.perspective;
            if (config.perspective) cam.fieldOfView = config.fieldOfView;
            else cam.orthographicSize = config.orthoSize;
        }

        private void ApplyPose(Vector3 center, bool instant)
        {
            Camera cam = ResolveCamera();
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
```

- [ ] **Step 4: 테스트 통과 확인**

Test Runner EditMode 재실행.
Expected: `CenterFocusCameraRigTests` 2개 PASS.

- [ ] **Step 5: 에디터 수동 검증**

씬에 빈 GameObject 생성 → `CenterFocusCameraRig` 추가 → `targetCamera` 에 Main Camera, `config` 에 새 `CameraRigConfig` 에셋 할당. config 의 `pitch` 슬라이더를 움직이면 씬 카메라가 중심을 보며 즉시 각도 변경되는지 확인.
Expected: 플레이 없이 에디터에서 실시간 반영.

- [ ] **Step 6: 커밋** (사용자 승인 + `lint` 검증 후)

```bash
git add Assets/Scripts/Systems/Visual/Camera/CenterFocusCameraRig.cs Assets/Tests/EditMode/CenterFocusCameraRigTests.cs
git commit -m "feat(visual): 중앙 주시 카메라 리그 CenterFocusCameraRig 추가"
```

---

## Task 4: ModeBootstrap 카메라 주입 배선

**Files:**
- Modify: `Assets/Scripts/Systems/Mode/ModeBootstrap.cs`
- Modify: `Assets/Scripts/Systems/Mode/ArenaModeBootstrap.cs:31-32`
- Modify: `Assets/Scripts/Systems/Mode/GridDefenseModeBootstrap.cs:23-24`

> 이 태스크는 직렬화 필드 배선이라 단위 테스트 대신 컴파일 + 에디터 검증으로 확인한다.

- [ ] **Step 1: ModeBootstrap 에 카메라 주입 추가**

`ModeBootstrap.cs` 전체를 아래로 교체:
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
        [SerializeField] protected CameraRigConfig cameraConfig;
        [SerializeField] protected CenterFocusCameraRig cameraRig;

        /// <summary> 공통 입력을 받아 이 부트스트랩의 모드를 생성합니다. </summary>
        public abstract IGameMode CreateMode(ModeContext ctx);

        /// <summary> 이 모드의 적 수 표시 한계(HUD capacity)입니다. </summary>
        public abstract int EnemyDisplayCapacity { get; }

        /// <summary> 카메라 리그를 중심·설정으로 바인딩합니다. (비주얼 미설정 모드는 무시) </summary>
        protected void BindCamera(in ModeContext ctx)
        {
            if (cameraRig != null) cameraRig.Bind(ctx.CoreCenter, cameraConfig);
        }
    }
}
```

- [ ] **Step 2: ArenaModeBootstrap 에서 BindCamera 호출**

`ArenaModeBootstrap.cs` 의 `CreateMode` 끝부분(현재 31-32행)을 수정:
```csharp
            if (arenaView != null) arenaView.Bind(arenaModel);
            BindCamera(ctx);
            return new ArenaMode(arenaModel, ctx.CoreCenter, height);
```

- [ ] **Step 3: GridDefenseModeBootstrap 에서 BindCamera 호출**

`GridDefenseModeBootstrap.cs` 의 `CreateMode`(현재 23-24행)을 수정:
```csharp
            if (placement != null) placement.Bind(ctx.Economy, ctx.TargetFinder);
            BindCamera(ctx);
            return new GridDefenseMode(ctx.Core, mapData, ctx.SpawnOrigin);
```

- [ ] **Step 4: 컴파일 + 에디터 검증**

Unity 로 전환해 컴파일 에러 0 확인. 각 모드 부트스트랩 인스펙터에 `Camera Config` / `Camera Rig` 슬롯이 표시되는지 확인하고 할당.
Expected: 컴파일 통과, 플레이 시 모드 시작과 함께 카메라가 `ctx.CoreCenter` 를 바라봄.

- [ ] **Step 5: 커밋** (사용자 승인 + `lint` 검증 후)

```bash
git add Assets/Scripts/Systems/Mode/ModeBootstrap.cs Assets/Scripts/Systems/Mode/ArenaModeBootstrap.cs Assets/Scripts/Systems/Mode/GridDefenseModeBootstrap.cs
git commit -m "feat(visual): 모드 부트스트랩에 카메라 리그 바인딩 배선"
```

---

## Task 5: MapVisualizer 카메라 책임 제거

**Files:**
- Modify: `Assets/Scripts/Systems/Grid/MapVisualizer.cs:25-31, 36-41, 43-90`
- Modify: `Assets/Scripts/Editor/MapVisualizerEditor.cs:24-27`

> D1 결정: Phase 1에서는 fit-to-bounds 자동 맞춤을 제거하고 리그의 `config.distance` 를 사용한다. (자동 맞춤이 필요하면 후속 태스크에서 리그 옵션으로 추가)

- [ ] **Step 1: MapVisualizer 의 카메라 필드 제거**

`MapVisualizer.cs` 에서 다음 `[Header("Camera Settings")]` 블록(25-31행)을 **삭제**:
```csharp
        [Header("Camera Settings")]
        [SerializeField, Tooltip("카메라 포커스 시 적용할 여유 공간 배율")]
        private float cameraPadding = 1.1f;
        [SerializeField, Tooltip("쿼터뷰 각도 (Pitch)")]
        private float cameraPitch = 30f;
        [SerializeField, Tooltip("쿼터뷰 각도 (Yaw)")]
        private float cameraYaw = 45f;
```

- [ ] **Step 2: FocusCameraOnMap 메서드 및 호출 제거**

`MapVisualizer.cs` 의 `FocusCameraOnMap()` 메서드 전체(43-90행, `[ContextMenu("Focus Camera on Map")]` 포함)를 **삭제**하고, `SetupAndGenerate` 의 호출도 제거:
```csharp
        public void SetupAndGenerate(MapData data)
        {
            this.mapData = data;
            GenerateMap();
        }
```

- [ ] **Step 3: 에디터 버튼 제거**

`MapVisualizerEditor.cs` 의 "Focus Camera on Map" 버튼 블록(24-27행)을 **삭제**:
```csharp
            if (GUILayout.Button("Focus Camera on Map"))
            {
                visualizer.FocusCameraOnMap();
            }
```

- [ ] **Step 4: 컴파일 검증**

Unity 컴파일. `FocusCameraOnMap`/`cameraPitch` 등 잔존 참조가 없는지 확인.
Expected: 컴파일 에러 0. 그리드 모드의 카메라는 이제 `CenterFocusCameraRig` + `CameraRigConfig` 가 담당.

- [ ] **Step 5: 커밋** (사용자 승인 + `lint` 검증 후)

```bash
git add Assets/Scripts/Systems/Grid/MapVisualizer.cs Assets/Scripts/Editor/MapVisualizerEditor.cs
git commit -m "refactor(visual): MapVisualizer 카메라 책임을 리그로 이관"
```

---

## Task 6: 통합 검증 (수동)

**Files:** 없음 (씬/플레이 검증)

- [ ] **Step 1: 모드별 config 에셋 생성**

Project 창에서 우클릭 → Create > DefenseDot > CameraRigConfig 로 `Assets/Settings/HD2D/CameraRig_Arena.asset`, `CameraRig_Grid.asset` 생성. 각각 pitch/distance 를 모드에 맞게 설정(예: Arena pitch 22, Grid pitch 30).

- [ ] **Step 2: 씬 배선**

`AreanaScene` / `GridScene` 의 Main Camera(또는 별도 리그 오브젝트)에 `CenterFocusCameraRig` 부착, 해당 모드 부트스트랩의 `cameraRig`/`cameraConfig` 슬롯에 리그와 config 할당.

- [ ] **Step 3: 검증 시나리오 실행**

| # | 시나리오 | 기대 |
|---|---|---|
| 1 | 에디터에서 config `pitch` 조절 | 씬 카메라가 중앙 주시하며 즉시 변경 |
| 2 | 플레이 중 `rig.Pitch = X` (디버그) | 카메라 각도 변경, config 에셋 불변 |
| 3 | Arena/Grid 각각 플레이 | 각 모드 config 의 각도/거리로 중앙 주시 |

Expected: 3개 시나리오 모두 통과.

- [ ] **Step 4: 최종 커밋** (사용자 승인 후)

```bash
git add Assets/Settings/HD2D Assets/Scenes
git commit -m "chore(visual): 모드별 카메라 리그 씬 배선 및 config 에셋"
```

---

## Self-Review (작성자 점검 결과)

- **스펙 커버리지**: §4.1 카메라 리그의 모든 요구(중앙 주시 / pitch·거리 디자이너 조절 / 에디터·런타임 / 모드별)를 Task 1~6 이 구현. ✅
- **플레이스홀더 스캔**: "TODO/TBD" 없음. 모든 코드 단계에 실제 코드 포함. ✅
- **타입 일관성**: `CameraPose`, `CameraRigMath.Solve`, `CameraRigConfig`(pitch/yaw/distance/heightOffset/perspective/fieldOfView/orthoSize/followLerp/pitchRange), `CenterFocusCameraRig.Bind/Pitch/Yaw/Distance`, `ModeBootstrap.BindCamera` 명칭이 전 태스크에서 일치. ✅
- **범위**: Phase 1은 URP 의존 없이 독립적으로 동작·검증 가능한 단위. 포스트FX/빌보드/틸트시프트/셰이더는 후속 Phase 계획에서 작성. ✅

---

## 후속 Phase (별도 계획 예정)

| Phase | 계획 파일(예정) |
|---|---|
| P2 포스트FX(빌트인) | `docs/superpowers/plans/YYYY-MM-DD-hd2d-phase2-postfx.md` |
| P3 빌보드 MVP | `…-hd2d-phase3-billboard.md` |
| P4 커스텀 틸트시프트 | `…-hd2d-phase4-tiltshift.md` |
| P5 스프라이트 셰이더·아트 | `…-hd2d-phase5-sprite-shader.md` |
