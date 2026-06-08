# HD-2D Phase 3 구현 계획 — 픽셀 빌보드 스프라이트 (MVP)

> **For agentic workers:** REQUIRED SUB-SKILL — 실행 시 `superpowers:subagent-driven-development`(권장) 또는 `superpowers:executing-plans`. 코드 작성 전 `superpowers:test-driven-development`, 완료/커밋 전 `superpowers:verification-before-completion`.
>
> **프로젝트 규칙:** 사용자 명시 승인 없이 git 커밋 금지(커밋은 `commit` 스킬로만). 커밋 전 `lint` 스킬 필수. 컨벤션 준수([SerializeField] private camelCase, 모든 멤버 접근제한자 명시, 한국어 `<summary>`, System.* 풀패스, 비동기는 UniTask만).

**Goal:** 적/액터를 3D 메시 대신 **카메라를 바라보는 2D 스프라이트(빌보드)** 로 렌더하고, 액터 상태(Idle/Moving/Attacking/Dead)에 따라 스프라이트 애니메이션을 전환하는 구조를 플레이스홀더 스프라이트로 구축·검증한다. (HD-2D의 "2D 캐릭터 + 3D 배경" 본질 구조의 MVP)

**Architecture:**
- **논리 ↔ 비주얼 분리**: 액터 루트(MonsterActor + 콜라이더 + SpriteActorView, XZ 이동·피격)와 `Visual` 자식(SpriteRenderer + BillboardSprite, 빌보드 회전)을 분리. 루트에 빌보드 회전을 걸면 이동·콜라이더·자식 좌표가 휩쓸리므로 자식으로 격리.
- **상태→애니메이션 디커플**: `IActor` 에 `StateChanged` 이벤트 추가 → `SpriteActorView` 가 액터 타입(Monster/Tower) 무관하게 구독. `ActorBase.SetState` 가 invoke.
- **경량 프레임 플레이어**: `SpriteAnimationSet`(상태별 `Sprite[]` + fps) 을 `Update` 에서 `spriteRenderer.sprite` 교체로 재생. `Animator` 미사용(적 80개 오버헤드·풀링 친화).
- **순수 로직 분리**: 빌보드 Y축 각도(`BillboardMath`)와 프레임 인덱스(`SpriteFrameMath`)를 static 순수 함수로 분리 → EditMode TDD.
- **정렬(MVP)**: SpriteRenderer + URP 기본 Sprite-Unlit(투명). 불투명 3D 아레나에 의해 ZTest로 정상 가려짐. 스프라이트-스프라이트는 sortingOrder/거리. **완전한 알파클립 ZWrite 깊이참여 + 림/노멀맵은 P5 커스텀 셰이더그래프로 이관**(URP에 스프라이트용 불투명-알파클립 빌트인 셰이더 부재 — §설계 보강 참조).

**Tech Stack:** Unity 6000.2.x / URP 17.4. C#. NUnit EditMode. 네임스페이스 `DefenseDot.Systems.Visual.Billboard`.

**관련 스펙:** `docs/superpowers/specs/2026-06-07-hd2d-visual-design.md` §4.3(빌보드 시스템), §6(P3/P5).

> **설계 보강(§4.3 재조정)**: 설계는 P3에 "알파클립+ZWrite"를 명시했으나, URP에는 SpriteRenderer 호환 불투명-알파클립 빌트인 셰이더가 없다(SpriteRenderer는 `_MainTex`, URP/Unlit은 `_BaseMap` 이라 텍스처 미표시). 따라서 P3 MVP는 투명 스프라이트로 빌보드·애니메이션·연동을 확립하고, 알파클립 ZWrite 깊이참여는 P5 `SpriteBillboard.shadergraph` 에서 구현한다. 투명 스프라이트도 불투명 지오메트리에 의해 ZTest로 정상 가려지므로 MVP 검증에 충분하다.

---

## File Structure

| 파일 | 책임 | 상태 |
|---|---|---|
| `Assets/Scripts/Core/Interfaces/IActor.cs` | `StateChanged` 이벤트 선언 추가 | 수정 |
| `Assets/Scripts/Core/ActorBase.cs` | `StateChanged` 이벤트 구현 + `SetState` 에서 invoke | 수정 |
| `Assets/Scripts/Systems/Visual/Billboard/BillboardMath.cs` | 카메라 향 Y축 각도 순수 계산 | 신규 |
| `Assets/Scripts/Systems/Visual/Billboard/BillboardSprite.cs` | LateUpdate 빌보드 회전 컴포넌트 | 신규 |
| `Assets/Scripts/Systems/Visual/Billboard/SpriteFrameMath.cs` | 경과시간→프레임 인덱스 순수 계산 | 신규 |
| `Assets/Scripts/Systems/Visual/Billboard/SpriteAnimationSet.cs` | 상태별 프레임 배열 SO | 신규 |
| `Assets/Scripts/Systems/Visual/Billboard/SpriteActorView.cs` | 액터 상태 구독 → 프레임 재생 | 신규 |
| `Assets/Tests/EditMode/BillboardMathTests.cs` | BillboardMath 단위 테스트 | 신규 |
| `Assets/Tests/EditMode/SpriteFrameMathTests.cs` | SpriteFrameMath 단위 테스트 | 신규 |
| `Assets/Tests/EditMode/ActorStateEventTests.cs` | ActorBase.StateChanged 이벤트 테스트 | 신규 |
| `Assets/Prefabs/Game/Enemies/Enemy_Placeholder.prefab` | 3D 메시 → Visual 자식(스프라이트 빌보드)으로 재구성 | 수정(에디터) |
| `Assets/Art/Sprites/(placeholder)` | MVP용 플레이스홀더 스프라이트 | 신규(에디터) |

---

## Task 1 — IActor + ActorBase 에 StateChanged 이벤트 (TDD)

**Files:** `Assets/Scripts/Core/Interfaces/IActor.cs`, `Assets/Scripts/Core/ActorBase.cs`, `Assets/Tests/EditMode/ActorStateEventTests.cs`

- [ ] **Step 1: 실패 테스트 작성** (`ActorStateEventTests.cs`)
```csharp
using NUnit.Framework;
using UnityEngine;
using DefenseDot.Core;
using DefenseDot.Systems.Enemy;

namespace DefenseDot.Tests.EditMode
{
    /// <summary> ActorBase.StateChanged 이벤트 발화를 검증합니다. </summary>
    public sealed class ActorStateEventTests
    {
        [Test]
        public void SetState_DifferentState_RaisesStateChanged()
        {
            var go = new GameObject("Enemy");
            var actor = go.AddComponent<MonsterActor>();
            ActorState received = ActorState.Idle;
            int count = 0;
            ((IActor)actor).StateChanged += s => { received = s; count++; };

            ((IActor)actor).SetState(ActorState.Moving);

            Assert.AreEqual(1, count);
            Assert.AreEqual(ActorState.Moving, received);
            Object.DestroyImmediate(go);
        }

        [Test]
        public void SetState_SameState_DoesNotRaise()
        {
            var go = new GameObject("Enemy");
            var actor = go.AddComponent<MonsterActor>();
            int count = 0;
            ((IActor)actor).StateChanged += _ => count++;

            ((IActor)actor).SetState(ActorState.Idle); // 초기값과 동일

            Assert.AreEqual(0, count);
            Object.DestroyImmediate(go);
        }
    }
}
```

- [ ] **Step 2: 실패 확인(Red)** — `IActor.StateChanged` 미존재로 컴파일 실패.

- [ ] **Step 3: IActor 에 이벤트 선언 추가** — `IActor.cs` 의 인터페이스 본문에 추가:
```csharp
        /// <summary> 상태 변경 시 발생 (변경된 새 상태 전달) </summary>
        event System.Action<ActorState> StateChanged;
```
(기존 Position/CurrentState/SetState 유지)

- [ ] **Step 4: ActorBase 에 이벤트 구현 + invoke** — `ActorBase.cs` 의 `currentState` 필드 아래에 이벤트 추가:
```csharp
        /// <summary> 상태 변경 시 발생 (View가 구독해 애니메이션 전환) </summary>
        public event System.Action<ActorState> StateChanged;
```
그리고 `SetState` 를 수정:
```csharp
        public virtual void SetState(ActorState newState)
        {
            if (currentState == newState) return;
            currentState = newState;
            OnStateChanged(newState);
            StateChanged?.Invoke(newState);
        }
```

- [ ] **Step 5: 통과 확인(Green)** — EditMode 실행, `ActorStateEventTests` 2건 PASS, 기존 테스트 영향 없음.

---

## Task 2 — BillboardMath + BillboardSprite (TDD)

**Files:** `Assets/Scripts/Systems/Visual/Billboard/BillboardMath.cs`, `BillboardSprite.cs`, `Assets/Tests/EditMode/BillboardMathTests.cs`

- [ ] **Step 1: 실패 테스트** (`BillboardMathTests.cs`)
```csharp
using NUnit.Framework;
using UnityEngine;
using DefenseDot.Systems.Visual.Billboard;

namespace DefenseDot.Tests.EditMode
{
    /// <summary> 카메라 향 Y축 각도 순수 계산을 검증합니다. </summary>
    public sealed class BillboardMathTests
    {
        [Test]
        public void YawTowardCamera_CameraOnNegativeZ_Returns180()
        {
            float yaw = BillboardMath.YawTowardCamera(Vector3.zero, new Vector3(0f, 0f, -10f));
            Assert.AreEqual(180f, Mathf.Abs(yaw), 0.01f);
        }

        [Test]
        public void YawTowardCamera_CameraOnPositiveX_Returns90()
        {
            float yaw = BillboardMath.YawTowardCamera(Vector3.zero, new Vector3(10f, 0f, 0f));
            Assert.AreEqual(90f, yaw, 0.01f);
        }

        [Test]
        public void YawTowardCamera_IgnoresHeight()
        {
            float flat = BillboardMath.YawTowardCamera(Vector3.zero, new Vector3(10f, 0f, 0f));
            float high = BillboardMath.YawTowardCamera(Vector3.zero, new Vector3(10f, 50f, 0f));
            Assert.AreEqual(flat, high, 0.01f);
        }

        [Test]
        public void YawTowardCamera_DirectlyAbove_ReturnsZero()
        {
            float yaw = BillboardMath.YawTowardCamera(Vector3.zero, new Vector3(0f, 10f, 0f));
            Assert.AreEqual(0f, yaw, 0.01f);
        }
    }
}
```

- [ ] **Step 2: 실패 확인(Red)**.

- [ ] **Step 3: BillboardMath 구현** (`BillboardMath.cs`)
```csharp
// 카메라를 수평으로 바라보는 Y축 각도 순수 계산
using UnityEngine;

namespace DefenseDot.Systems.Visual.Billboard
{
    /// <summary> 빌보드 회전 각도 순수 계산 모음입니다. </summary>
    public static class BillboardMath
    {
        /// <summary>
        /// 스프라이트가 카메라를 수평(Y축)으로 바라보는 각도(도)를 계산합니다.
        /// 높이 차이는 무시하여 스프라이트가 직립(서 있는)을 유지합니다.
        /// </summary>
        public static float YawTowardCamera(Vector3 spritePosition, Vector3 cameraPosition)
        {
            Vector3 dir = cameraPosition - spritePosition;
            dir.y = 0f;
            if (dir.sqrMagnitude < 1e-6f) return 0f;
            return Mathf.Atan2(dir.x, dir.z) * Mathf.Rad2Deg;
        }
    }
}
```

- [ ] **Step 4: BillboardSprite 구현** (`BillboardSprite.cs`)
```csharp
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
```
> 주의: 스프라이트가 카메라를 등지면(뒤집힘) 에디터에서 `mode`/스프라이트 방향을 확인해 Visual 자식의 초기 Y회전을 180° 보정(Task 5 검증에서 조정). 순수 함수는 각도만 책임지고, 실제 정면 여부는 Play 검증.

- [ ] **Step 5: 통과 확인(Green)** — `BillboardMathTests` 4건 PASS.

---

## Task 3 — SpriteFrameMath + SpriteAnimationSet (TDD + SO)

**Files:** `Assets/Scripts/Systems/Visual/Billboard/SpriteFrameMath.cs`, `SpriteAnimationSet.cs`, `Assets/Tests/EditMode/SpriteFrameMathTests.cs`

- [ ] **Step 1: 실패 테스트** (`SpriteFrameMathTests.cs`)
```csharp
using NUnit.Framework;
using DefenseDot.Systems.Visual.Billboard;

namespace DefenseDot.Tests.EditMode
{
    /// <summary> 경과시간→프레임 인덱스 순수 계산을 검증합니다. </summary>
    public sealed class SpriteFrameMathTests
    {
        [Test]
        public void FrameIndex_Start_ReturnsZero()
        {
            Assert.AreEqual(0, SpriteFrameMath.FrameIndex(0f, 8f, 4, true));
        }

        [Test]
        public void FrameIndex_Loops()
        {
            // fps 8 → 0.5s = 4프레임 경과, 4프레임 클립 → 인덱스 0 (wrap)
            Assert.AreEqual(0, SpriteFrameMath.FrameIndex(0.5f, 8f, 4, true));
            // 0.375s = 3프레임 → 인덱스 3
            Assert.AreEqual(3, SpriteFrameMath.FrameIndex(0.375f, 8f, 4, true));
        }

        [Test]
        public void FrameIndex_NoLoop_ClampsToLast()
        {
            Assert.AreEqual(3, SpriteFrameMath.FrameIndex(10f, 8f, 4, false));
        }

        [Test]
        public void FrameIndex_EmptyOrZeroFps_ReturnsZero()
        {
            Assert.AreEqual(0, SpriteFrameMath.FrameIndex(1f, 8f, 0, true));
            Assert.AreEqual(0, SpriteFrameMath.FrameIndex(1f, 0f, 4, true));
        }
    }
}
```

- [ ] **Step 2: 실패 확인(Red)**.

- [ ] **Step 3: SpriteFrameMath 구현** (`SpriteFrameMath.cs`)
```csharp
// 경과시간 → 프레임 인덱스 순수 계산
using UnityEngine;

namespace DefenseDot.Systems.Visual.Billboard
{
    /// <summary> 프레임 애니메이션 인덱스 순수 계산 모음입니다. </summary>
    public static class SpriteFrameMath
    {
        /// <summary>
        /// 경과시간(초)과 fps, 프레임 수로 현재 프레임 인덱스를 계산합니다.
        /// loop=true 면 순환, false 면 마지막 프레임에서 고정.
        /// </summary>
        public static int FrameIndex(float elapsed, float fps, int frameCount, bool loop)
        {
            if (frameCount <= 0 || fps <= 0f) return 0;
            int raw = Mathf.FloorToInt(elapsed * fps);
            if (raw < 0) raw = 0;
            if (loop) return raw % frameCount;
            return raw >= frameCount ? frameCount - 1 : raw;
        }
    }
}
```

- [ ] **Step 4: SpriteAnimationSet 구현** (`SpriteAnimationSet.cs`)
```csharp
// 액터 상태별 스프라이트 프레임 묶음 (디자이너 에셋)
using UnityEngine;
using DefenseDot.Core;

namespace DefenseDot.Systems.Visual.Billboard
{
    /// <summary>
    /// 액터 상태별 스프라이트 프레임 배열과 재생 속도를 담는 설정입니다.
    /// </summary>
    [CreateAssetMenu(fileName = "NewSpriteAnimationSet", menuName = "DefenseDot/SpriteAnimationSet")]
    public class SpriteAnimationSet : ScriptableObject
    {
        /// <summary> 상태별 프레임 묶음. </summary>
        [System.Serializable]
        public struct StateClip
        {
            /// <summary> 대상 액터 상태 </summary>
            public ActorState state;
            /// <summary> 순서대로 재생할 프레임 </summary>
            public Sprite[] frames;
            /// <summary> 순환 재생 여부 </summary>
            public bool loop;
        }

        /// <summary> 초당 프레임 수 </summary>
        public float framesPerSecond = 8f;
        /// <summary> 상태별 클립 목록 </summary>
        public StateClip[] clips;

        /// <summary>
        /// 상태에 해당하는 클립을 찾습니다. 없으면 found=false.
        /// </summary>
        public StateClip GetClip(ActorState state, out bool found)
        {
            if (clips != null)
            {
                for (int i = 0; i < clips.Length; i++)
                {
                    if (clips[i].state == state)
                    {
                        found = true;
                        return clips[i];
                    }
                }
            }
            found = false;
            return default;
        }
    }
}
```

- [ ] **Step 5: 통과 확인(Green)** — `SpriteFrameMathTests` 4건 PASS.

---

## Task 4 — SpriteActorView (상태 구독 → 프레임 재생)

**Files:** `Assets/Scripts/Systems/Visual/Billboard/SpriteActorView.cs`

> 단위 테스트는 Task 3 의 `SpriteFrameMath`(프레임 로직)로 커버. View 자체는 MonoBehaviour 배선이라 Play/에디터 검증(Task 6).

- [ ] **Step 1: 구현** (`SpriteActorView.cs`)
```csharp
// 액터 상태를 구독해 스프라이트 프레임 애니메이션을 재생
using UnityEngine;
using DefenseDot.Core;

namespace DefenseDot.Systems.Visual.Billboard
{
    /// <summary>
    /// IActor 의 StateChanged 를 구독해 상태별 스프라이트 클립을 재생합니다.
    /// 경량 프레임 플레이어(Animator 미사용, 풀링 친화).
    /// </summary>
    public sealed class SpriteActorView : MonoBehaviour
    {
        [SerializeField] private SpriteRenderer spriteRenderer;
        [SerializeField] private SpriteAnimationSet animationSet;

        private IActor actor;
        private SpriteAnimationSet.StateClip currentClip;
        private bool hasClip;
        private float elapsed;

        private void Awake()
        {
            actor = GetComponentInParent<IActor>();
            if (spriteRenderer == null) spriteRenderer = GetComponentInChildren<SpriteRenderer>();
        }

        private void OnEnable()
        {
            if (actor == null) actor = GetComponentInParent<IActor>();
            if (actor != null)
            {
                actor.StateChanged += HandleStateChanged;
                ApplyState(actor.CurrentState); // 활성화 시 현재 상태 동기화
            }
        }

        private void OnDisable()
        {
            if (actor != null) actor.StateChanged -= HandleStateChanged;
        }

        private void HandleStateChanged(ActorState newState)
        {
            ApplyState(newState);
        }

        private void ApplyState(ActorState state)
        {
            if (animationSet == null) return;
            currentClip = animationSet.GetClip(state, out hasClip);
            elapsed = 0f;
            UpdateFrame();
        }

        private void Update()
        {
            if (!hasClip) return;
            elapsed += Time.deltaTime;
            UpdateFrame();
        }

        private void UpdateFrame()
        {
            if (!hasClip || spriteRenderer == null) return;
            Sprite[] frames = currentClip.frames;
            if (frames == null || frames.Length == 0) return;
            int idx = SpriteFrameMath.FrameIndex(elapsed, animationSet.framesPerSecond, frames.Length, currentClip.loop);
            spriteRenderer.sprite = frames[idx];
        }
    }
}
```
> 좌우 플립(이동 방향↔카메라 right)은 MVP 범위 밖 — 후속 조정(이동 전략에서 방향 노출 필요).

- [ ] **Step 2: 컴파일 확인** — 콘솔 에러 0.

---

## Task 5 — 적 프리팹 재구성 + 플레이스홀더 스프라이트 (에디터)

3D 구체 메시를 제거하고 빌보드 스프라이트 Visual 자식으로 재구성한다. 루트는 논리(MonsterActor + 콜라이더) 유지.

**Files:** `Assets/Prefabs/Game/Enemies/Enemy_Placeholder.prefab`, `Assets/Art/Sprites/`(플레이스홀더)

- [ ] **Step 1: 플레이스홀더 스프라이트 준비** — `Assets/Art/Sprites/` 폴더 생성. 간단한 캐릭터형 PNG(예: 32×48, 2~3프레임) 또는 단색 사각 스프라이트를 임포트(Texture Type=Sprite). idle 2프레임, moving 2프레임 정도면 검증 충분. (손그림 정식 아트는 추후 교체)
- [ ] **Step 2: SpriteAnimationSet 에셋 생성** — Create › DefenseDot › SpriteAnimationSet → `Assets/Art/Sprites/EnemyAnim_Placeholder.asset`. clips 에 Idle(frames, loop ON) / Moving(frames, loop ON) 추가, framesPerSecond 6~8.
- [ ] **Step 3: 프리팹 재구성** — `Enemy_Placeholder.prefab` 열기:
  - 루트에서 **MeshFilter, MeshRenderer 제거**(3D 구체 비주얼 제거). MonsterActor, SphereCollider, Transform 유지.
  - 루트에 빈 자식 **`Visual`** 생성 → `SpriteRenderer`(sprite = 플레이스홀더 idle 0프레임) + `BillboardSprite`(mode=YAxisUpright) 추가. Visual 의 local position 을 살짝 위로(예: y=0.5) 올려 스프라이트 발밑이 지면에 닿게.
  - 루트(또는 Visual)에 **`SpriteActorView`** 추가 → `spriteRenderer` ← Visual 의 SpriteRenderer, `animationSet` ← `EnemyAnim_Placeholder.asset`.
  - SpriteRenderer 머티리얼 = URP 기본 Sprite-Unlit(투명). (알파클립 불투명은 P5)
- [ ] **Step 4: 정면 방향 확인** — 씬에서 적을 두고 카메라를 돌려보며 스프라이트가 항상 카메라를 정면으로 보는지 확인. 등지면 Visual 의 초기 Y회전 180° 보정 또는 스프라이트 좌우 반전.
- [ ] **Step 5: 검증** — 프리팹에 MeshFilter/MeshRenderer 없음, Visual 자식에 SpriteRenderer+BillboardSprite, 루트에 MonsterActor+SpriteActorView+SphereCollider 확인.

---

## Task 6 — 통합 검증 (Play)

**Files:** (없음 — 런타임 검증)

- [ ] **6-A EditMode 테스트:** 전체 PASS (`ActorStateEventTests` 2 + `BillboardMathTests` 4 + `SpriteFrameMathTests` 4 + 기존).
- [ ] **6-B Play(아레나):** 적 스폰 후 — 콘솔 에러 0 / 스프라이트가 **카메라를 정면으로** 봄(빌보드) / 카메라 회전·이동에도 정면 유지 / **서 있는** 느낌(Y축 직립).
- [ ] **6-C 상태 애니메이션:** 적이 Idle↔Moving 전환 시 스프라이트 클립이 바뀌는지(이동 시 walk). 사망(Dead) 시 클립/소멸 확인.
- [ ] **6-D 정렬:** 스프라이트가 불투명 아레나/타워 뒤로 가면 정상 가려지는지(ZTest). 스프라이트끼리 겹칠 때 큰 깨짐 없는지(투명 정렬 한계는 P5에서 개선).
- [ ] **6-E 풀링:** 적 처치 후 재스폰 시 idle 클립부터 정상 재생(상태 이벤트 재구독 확인).
- [ ] **6-F 성능:** 적 다수(수십) 스폰 시 프레임 저하 없음(LateUpdate 빌보드 + 프레임 교체).

---

## Task 7 — 커밋 (사용자 승인 + lint, commit 스킬)

- [ ] `superpowers:verification-before-completion` 으로 6-A~6-F 결과 재확인.
- [ ] `lint` 스킬로 변경 `.cs`(IActor, ActorBase, BillboardMath, BillboardSprite, SpriteFrameMath, SpriteAnimationSet, SpriteActorView, 테스트 3개) 검증.
- [ ] **`commit` 스킬로 커밋**(사용자 승인 후). 병행 작업 혼재 시 Phase 코드/에셋만 선별 스테이징.
- [ ] 커밋 메시지 예(Phase 식별자 금지): `feat: 액터 빌보드 스프라이트 렌더링 및 상태 애니메이션 도입`

---

## 위험 및 주의

- **R1 정렬(투명)**: MVP는 투명 스프라이트. 불투명 지오메트리 가림은 ZTest로 정상이나, 반투명-반투명 정렬은 sortingOrder/거리 의존. 완전한 깊이참여(알파클립 ZWrite)는 P5 셰이더.
- **R2 빌보드 정면**: 스프라이트 면 방향에 따라 180° 보정 필요할 수 있음 — Task 5-4 에서 Play로 확인.
- **R3 IActor 변경 파급**: `IActor` 에 이벤트 추가 → 모든 구현체(ActorBase)가 자동 충족(field-like event). 다른 구현체가 있으면 컴파일 에러로 드러남(현재 ActorBase 단일).
- **R4 풀링 재구독**: SpriteActorView 는 OnEnable 구독/OnDisable 해제 + OnEnable 시 현재 상태 동기화 → 풀 재활성 안전.
- **R5 네임스페이스**: `Visual.Billboard` 는 `Camera` segment 충돌 없음. BillboardSprite 는 `UnityEngine.Camera` 풀패스 사용(일관성).

## Self-Review

- **스펙 커버리지(§4.3)**: 논리/비주얼 분리(Task 5), BillboardSprite YAxisUpright(Task 2), SpriteActorView 상태연동(Task 4), SpriteAnimationSet 경량 플레이어(Task 3·4), ActorBase 상태이벤트(Task 1). 정렬은 MVP(투명)+P5 이관 명시. ✓
- **플레이스홀더**: 코드 Task(1~4) 전문 포함. 에디터 Task(5)는 단계·검증 기준 구체화. ✓
- **타입 일관성**: `IActor.StateChanged`(System.Action<ActorState>), `BillboardMath.YawTowardCamera`, `SpriteFrameMath.FrameIndex`, `SpriteAnimationSet.GetClip(out bool)`, `SpriteActorView`. Task 간 일치. ✓
- **컨벤션**: [SerializeField] private camelCase, 접근제한자 명시, 한국어 `<summary>`, System.Action 풀패스, Animator/Coroutine 미사용. ✓

## 후속 Phase
- **Phase 5**: `SpriteBillboard.shadergraph`(알파클립 ZWrite + 림 + 옵션 노멀맵 라이팅) → 완전한 깊이참여·라이팅, 셰이더 빌보드 전환(CPU LateUpdate 제거), 접지 그림자, 손그림 아트 교체.
- **Phase 3 후속**: 좌우 플립(이동 방향), 타워에도 SpriteActorView 적용, 8방향 스프라이트(아트 분량에 따라).
