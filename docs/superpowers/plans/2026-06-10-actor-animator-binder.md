# Actor Animator Binder (Phase 3) Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** ActorState를 Animator 파라미터로 번역하는 **액터 독립적 공용 바인더**(`ActorAnimatorBinder`)와 State 파라미터 규약을 구축하여, 애니메이션을 velocity 추측이 아닌 상태 기반으로 구동하고 컨트롤러 교체만으로 액터별 오버라이드가 되게 한다.

**Architecture:** `IActor.StateChanged` 구독 → `State`(int)=`(int)ActorState` 푸시. 이동 중에는 위치 델타로 `Direction`(int) 푸시. 전환 규칙은 AnimatorController 에셋이 소유 → **프리팹별 컨트롤러 교체 = 오버라이드**(코드 0). 특수 액터는 `ResolveDirection` virtual 오버라이드.

**Tech Stack:** C# (Unity 6000.2), Unity MCP(`manage_prefabs`/`execute_code`/`manage_editor`), 어셈블리 `DefenseDot`.

**Commits:** 각 Task commit 단계는 **`commit` 스킬**로 수행(직접 git commit 금지).

**Spec:** `docs/superpowers/specs/2026-06-10-actor-bt-animator-design.md` §6. **선행:** Phase 2(`dfbf9d82`, 커밋·Play 검증 완료 — 적 Moving·타워 Attacking 상태가 BT로 기록됨).

---

## 영구 vs 임시 (스코프 경계)

| 구분 | 내용 | 수명 |
|---|---|---|
| **영구** | `ActorAnimatorBinder`, State/Direction 파라미터 규약, 오버라이드(컨트롤러 교체) 구조 | 모든 애니 액터가 재사용 |
| **임시** | 현 적 프리팹의 레퍼런스 AnimatorController(Cainos 클립 기반) | 캐릭터 기획 구체화 시 교체 |

> 현 에셋(Cainos 스프라이트·Tower_Test 메시)은 테스트용. 본 Phase는 **파이프라인**을 만들고, 임시 적 프리팹으로 **검증만** 한다. 타워가 일반공격·스킬 캐릭터로 구체화될 때(별도 기획) **같은 Binder + 타워 전용 컨트롤러**를 붙인다 — `ActorState.Attacking` 신호는 Phase 2에서 이미 BT가 기록 중이므로 컨트롤러만 추가하면 된다.

---

## File Structure (Phase 3)

| 파일 | 책임 |
|---|---|
| `Assets/Scripts/Systems/Visual/Billboard/ActorAnimatorBinder.cs` | **신규** — 상태→Animator 파라미터 공용 바인더 |
| `Assets/Scripts/Systems/Visual/Billboard/ActorAnimatorView.cs` | **삭제** — velocity 기반 구버전 대체 |
| `Assets/Animations/AC_Enemy_State.controller` | **신규(임시 레퍼런스)** — State 기반 적 컨트롤러 |
| `Assets/Prefabs/Game/Enemies/Enemy_Placeholder.prefab` | **수정** — Visual: View→Binder, 컨트롤러 교체 |

> `ActorAnimatorBinder`는 `BillboardMath`와 같은 네임스페이스(`DefenseDot.Systems.Visual.Billboard`)에 두어 `DirectionIndex`를 using 없이 사용.
> **테스트 정책**: Binder는 Animator 글루 컴포넌트라 EditMode 단위테스트 가치가 낮음(순수 로직 `BillboardMath.DirectionIndex`는 기존 테스트 유지). 검증은 **Play**로 수행한다.

---

## Task 1: ActorAnimatorBinder + 구버전 제거

**Files:**
- Create: `Assets/Scripts/Systems/Visual/Billboard/ActorAnimatorBinder.cs`
- Delete: `Assets/Scripts/Systems/Visual/Billboard/ActorAnimatorView.cs` (+ `.meta`)

- [ ] **Step 1: 구버전 외부 참조 확인**

Grep `ActorAnimatorView` 를 `Assets/`(prefab 포함)에서 검색.
Expected: 정의처 + `Enemy_Placeholder.prefab`(컴포넌트 참조)만. 프리팹 참조는 Task 3에서 교체하므로, 스크립트 삭제 전 프리팹에서 먼저 제거하거나 삭제 후 재배선한다(여기선 Step 2에서 스크립트만 삭제, 프리팹의 끊긴 참조는 Task 3에서 Binder로 교체).

- [ ] **Step 2: ActorAnimatorBinder.cs 작성**

```csharp
using UnityEngine;
using DefenseDot.Core;

namespace DefenseDot.Systems.Visual.Billboard
{
    /// <summary>
    /// ActorState 변화를 Animator 파라미터로 번역해 푸시하는 공용 바인더입니다.
    /// State(int)=(int)ActorState, 이동 중에는 Direction(int)을 푸시합니다.
    /// 전환 규칙은 AnimatorController 에셋이 소유 — 프리팹별 컨트롤러 교체가 곧 오버라이드입니다.
    /// </summary>
    public class ActorAnimatorBinder : MonoBehaviour
    {
        [SerializeField] private Animator animator;
        [SerializeField] private UnityEngine.Camera targetCamera;
        [SerializeField] private float moveThreshold = 0.01f;

        private static readonly int stateHash = Animator.StringToHash("State");
        private static readonly int directionHash = Animator.StringToHash("Direction");

        private IActor actor;
        private Vector3 lastPosition;

        private void Awake()
        {
            if (animator == null) animator = GetComponent<Animator>();
            actor = GetComponentInParent<IActor>();
        }

        private void OnEnable()
        {
            if (actor == null) actor = GetComponentInParent<IActor>();
            if (actor == null) return;
            lastPosition = actor.Position;
            actor.StateChanged += HandleStateChanged;
            HandleStateChanged(actor.CurrentState);
        }

        private void OnDisable()
        {
            if (actor != null) actor.StateChanged -= HandleStateChanged;
        }

        private void HandleStateChanged(ActorState state)
        {
            if (animator != null) animator.SetInteger(stateHash, (int)state);
        }

        private void LateUpdate()
        {
            if (actor == null || animator == null) return;
            if (actor.CurrentState != ActorState.Moving)
            {
                lastPosition = actor.Position;
                return;
            }
            Vector3 pos = actor.Position;
            Vector3 delta = pos - lastPosition;
            lastPosition = pos;
            delta.y = 0f;
            if (delta.sqrMagnitude < moveThreshold * moveThreshold) return;
            animator.SetInteger(directionHash, ResolveDirection(delta));
        }

        /// <summary> 이동 델타 → 카메라 기준 4방향 인덱스. 특수 액터는 오버라이드. </summary>
        protected virtual int ResolveDirection(Vector3 worldDelta)
        {
            UnityEngine.Camera cam = targetCamera != null ? targetCamera : UnityEngine.Camera.main;
            float yaw = cam != null ? cam.transform.eulerAngles.y : 0f;
            return BillboardMath.DirectionIndex(worldDelta, yaw);
        }
    }
}
```

- [ ] **Step 3: 구버전 스크립트 삭제**

`Assets/Scripts/Systems/Visual/Billboard/ActorAnimatorView.cs` 와 `.meta` 삭제.

- [ ] **Step 4: 컴파일 확인** — `refresh_unity`(compile) → `read_console`(error 0).
Expected: 0 에러. (프리팹의 ActorAnimatorView 참조는 "Missing script"로 남으나 Task 3에서 교체.)

- [ ] **Step 5: 커밋** — `commit` 스킬. 예: `feat: 상태구동 ActorAnimatorBinder 도입 및 velocity View 제거`

---

## Task 2: 임시 레퍼런스 컨트롤러(State 기반) 생성

**Files:**
- Create: `Assets/Animations/AC_Enemy_State.controller`

> Cainos 클립(`AM Player Idle`/`AM Player Move` 등)을 모션으로 사용하되, **분기는 `State` int로** 한다. 임시 검증용이며 캐릭터 구체화 시 교체된다.

- [ ] **Step 1: execute_code로 컨트롤러 생성**

`execute_code`(roslyn)로 다음 실행:
```csharp
using UnityEditor.Animations;
var dir = "Assets/Animations";
if (!UnityEditor.AssetDatabase.IsValidFolder(dir)) UnityEditor.AssetDatabase.CreateFolder("Assets", "Animations");
var path = dir + "/AC_Enemy_State.controller";
var ac = AnimatorController.CreateAnimatorControllerAtPath(path);
ac.AddParameter("State", AnimatorControllerParameterType.Int);
ac.AddParameter("Direction", AnimatorControllerParameterType.Int);
var idle = UnityEditor.AssetDatabase.LoadAssetAtPath<UnityEngine.AnimationClip>("Assets/Cainos/Pixel Art Top Down - Basic/Animation/AM Player Idle.anim");
var move = UnityEditor.AssetDatabase.LoadAssetAtPath<UnityEngine.AnimationClip>("Assets/Cainos/Pixel Art Top Down - Basic/Animation/AM Player Move.anim");
var sm = ac.layers[0].stateMachine;
var sIdle = sm.AddState("Idle");  sIdle.motion = idle;
var sMove = sm.AddState("Move");  sMove.motion = move;
sm.defaultState = sIdle;
// Idle(State==0) <-> Move(State==1)
var toMove = sIdle.AddTransition(sMove); toMove.hasExitTime=false; toMove.duration=0f; toMove.AddCondition(AnimatorConditionMode.Equals, 1, "State");
var toIdle = sMove.AddTransition(sIdle); toIdle.hasExitTime=false; toIdle.duration=0f; toIdle.AddCondition(AnimatorConditionMode.NotEqual, 1, "State");
UnityEditor.AssetDatabase.SaveAssets();
return "created " + path;
```
Expected: `created Assets/Animations/AC_Enemy_State.controller`.

- [ ] **Step 2: read_console**(error 0) — 클립 로드 실패 시 경로 확인 후 재실행.

- [ ] **Step 3: 커밋** — `commit` 스킬. 예: `chore: 상태 기반 적 임시 AnimatorController 추가`

---

## Task 3: Enemy_Placeholder 재배선 + Play 검증

**Files:**
- Modify: `Assets/Prefabs/Game/Enemies/Enemy_Placeholder.prefab`

- [ ] **Step 1: Visual의 컴포넌트 교체** — `manage_prefabs modify_contents`:
  - `components_to_remove`: `["ActorAnimatorView"]` (target: `Enemy_Placeholder/Visual`)
  - `components_to_add`: `["ActorAnimatorBinder"]`
  (한 호출로 안 되면 remove 후 add 분리 호출. target 경로 `Enemy_Placeholder/Visual` 지정.)

- [ ] **Step 2: Animator 컨트롤러 교체** — Visual의 `Animator.runtimeAnimatorController` 를 `AC_Enemy_State.controller`로 설정.
`manage_prefabs modify_contents` `component_properties`:
```
{"Animator": {"runtimeAnimatorController": {"path": "Assets/Animations/AC_Enemy_State.controller"}}}
```

- [ ] **Step 3: Binder 필드 배선** — Visual의 `ActorAnimatorBinder.animator` = 같은 Visual의 Animator.
`component_properties`: `{"ActorAnimatorBinder": {"animator": {"path": ...}}}` 가 자기참조라 어려우면, Awake의 `GetComponent<Animator>()` 폴백이 처리하므로 생략 가능(animator 미할당 시 자동 획득).

- [ ] **Step 4: 컴파일·refresh** — `refresh_unity` → `read_console`(0, Missing script 경고 해소 확인).

- [ ] **Step 5: Play 검증** — `manage_editor play` → 적 스폰 후 `execute_code`로 확인:
```csharp
var mon = GameObject.FindObjectOfType<DefenseDot.Systems.Enemy.MonsterActor>();
var anim = mon.GetComponentInChildren<Animator>();
return string.Format("state={0} animState={1} dir={2}", mon.CurrentState, anim.GetInteger("State"), anim.GetInteger("Direction"));
```
Expected: `state=Moving animState=1 dir=<0~3>` (BT가 Moving 기록 → Binder가 State=1 푸시 → 컨트롤러 Move 전이). → `manage_editor stop`.

- [ ] **Step 6: 커밋** — `commit` 스킬. 예: `chore: 적 프리팹 Visual을 ActorAnimatorBinder로 재배선`

---

## Self-Review (작성자 체크)

- **Spec §6 커버리지**: State/Direction 규약 = Task1 Binder. 컨트롤러 소유 전환 = Task2. 컨트롤러 교체=오버라이드 = Task3 + 향후. virtual ResolveDirection = Task1. View 삭제 = Task1. ✓
- **Placeholder 스캔**: 없음 — 코드/스크립트 전부 실값.
- **타입 일관성**: `stateHash`/`directionHash`, `ResolveDirection(Vector3)`, 파라미터명 `State`/`Direction`이 Binder·컨트롤러·검증 코드에서 일치. `(int)ActorState`(Idle0/Moving1...)와 컨트롤러 조건(State==1=Move) 일치. ✓
- **임시/영구 분리**: Binder·규약=영구, AC_Enemy_State=임시(향후 캐릭터별 교체) 명시. ✓

## 향후 (별도 기획)
- **타워/캐릭터 애니 구체화**: 일반공격·스킬 캐릭터가 정해지면 캐릭터별 AnimatorController(State: Idle/Move/**Attacking**/Stunned/Dead, 스킬 트리거 등) 제작 + 해당 프리팹 Visual에 동일 `ActorAnimatorBinder` 부착. 코드 변경 불필요(컨트롤러 교체만). `Attacking` 상태는 Phase 2의 `TowerBehaviorTree`가 이미 기록.
- Stun/Dead 전용 클립, 스킬별 트리거 파라미터 확장.
