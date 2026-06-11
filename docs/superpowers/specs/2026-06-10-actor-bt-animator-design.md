# Actor BT + Animator 설계 (Spec)

**작성일**: 2026-06-10
**상태**: 설계 확정 (구현 전)
**관련**: `docs/tasks/active/TASK-006-actor-animator-redesign.md`, `Assets/Docs/Architecture_Guidelines.md` §3, `diagram.drawio`

---

## 0. 목표 / 비목표

### 목표
- Actor의 행동(AI)을 **풀 Behavior Tree 프레임워크**로 구동하고, 그 결과를 `ActorState`로 표현하며, 애니메이션이 그 상태를 반영하도록 **단방향 파이프라인**을 구축한다.
- 상태: **Idle / Moving / Attacking / Stunned / Dead** (기존 `ActorState` enum 재사용).
- **구체화된 Actor가 생기면 오버라이드 가능**: BT는 `BuildPrimary()` 한 점, 애니는 AnimatorController 에셋 교체로 확장.
- Enemy는 **비공격(이동만)**을 베이스로 쓰고, Tower는 그 위에 **공격을 추가**한다.

### 비목표 (YAGNI — 이번 범위 제외, 확장점만 확보)
- 데이터 주도(SO) 트리 저작 / 비주얼 BT 에디터.
- 중앙 BT 스케줄러 / time-slicing (per-actor tick으로 충분, 80마리 규모).
- `Inverter`/`Parallel`/`Cooldown` 등 미사용 데코레이터 (노드 베이스만 확장 가능하게 설계).
- 상태이상(CC) 시스템 자체 구현, 몬스터의 실제 공격 능력 (이음새만 마련).

---

## 1. 아키텍처 — 3계층 + 1 이음새

```
[두뇌] ActorBehaviorTree  ── actor.SetState() ──▶ [이음새] ActorState ── StateChanged ──▶ [연출] ActorAnimatorBinder ──▶ AnimatorController 에셋
   └ 리프가 POCO(PathFollower/Combat) 호출 (🅰)      └ ActorBase 소유, BT 단독 writer        └ State/Direction 파라미터 push   └ 전환 규칙 소유, 프리팹별 교체=오버라이드
```

- 의존은 **위→아래 단방향**(순환 없음) → 각 계층 독립 교체·테스트 가능.
- 통합 방식 **🅰**: BT 리프가 기존·검증된 POCO(`PathFollowerLogic`/`ArenaOrbitLogic`/`CombatLogic`)를 **호출**한다. BT=결정, POCO=계산, ActorState=BT가 단독 소유.
- 상세 시각화: `diagram.drawio` 4페이지 (아키텍처 / 시퀀스 / BT 트리 / 클래스 계층).

---

## 2. ActorState 계약 & 단일 writer 원칙

- `ActorState` enum = **공유 슈퍼셋**. 액터는 부분집합만 사용한다.
  - Enemy: Idle / Moving / Stunned / Dead
  - Tower: Idle / Attacking / (Stunned) / Dead
- **`ActorState`를 쓰는 주체는 BT 하나** — 같은 프레임 상태 경쟁을 제거(연출 깜빡임 방지).
- **예외 처리**:
  | 상태 | writer | 방식 |
  |---|---|---|
  | Idle/Moving/Attacking/Stunned | BT만 | 외부 시스템은 `SetState`를 직접 호출하지 않고 **조건(Blackboard)만 기록**, BT가 읽고 상태 전환을 결정 |
  | Dead | lifecycle (데미지/디스폰) | 터미널·즉시 전환 예외. BT는 `Update`에서 `CurrentState != Dead`로 정지하여 존중 |
- **(중요) enum 순서가 곧 계약**: `ActorAnimatorBinder`가 `(int)ActorState`를 Animator `State` 파라미터로 직결하므로, **enum 멤버 재정렬 금지** (회귀 방지).

---

## 3. BT 프레임워크 (코드 조립)

### 3.1 노드 타입
```csharp
public enum NodeStatus { Running, Success, Failure }   // 기존 유지

public abstract class BTNode
{
    public abstract NodeStatus Evaluate(Blackboard bb); // 컨텍스트가 tick 시점에 트리를 관통
}

// Composite (자식 List 보유, bb를 자식에 그대로 전달)
public sealed class Sequence : BTNode { /* Failure→Failure, Running→Running, all Success→Success */ }
public sealed class Selector : BTNode { /* Success·Running 즉시 반환, all Failure→Failure */ }

// Leaf (람다로 actor/POCO/bb 클로저 → 노드 클래스 폭발 방지)
public sealed class ConditionLeaf : BTNode { System.Func<Blackboard,bool> predicate; }
public sealed class ActionLeaf    : BTNode { System.Func<Blackboard,NodeStatus> action; }
```
- **반응형(reactive) Composite**: 매 tick 루트부터 재평가(running 자식 인덱스 미기억). 풀링·우선순위 급변(stun 진입)에 즉각 반응. 무상태라 풀 재사용 시 리셋 부담 없음.
- 노드는 **MonoBehaviour가 아닌 순수 POCO** → EditMode 단위 테스트 대상.

### 3.2 빌더 API
```csharp
public static class BT
{
    public static BTNode Sequence(params BTNode[] children);
    public static BTNode Selector(params BTNode[] children);
    public static BTNode Condition(System.Func<Blackboard,bool> p);
    public static BTNode Action(System.Func<Blackboard,NodeStatus> a);
}
```

### 3.3 Blackboard (1급 통로, 데이터는 씨앗만)
```csharp
public sealed class Blackboard
{
    public ITargetable target;   // 노드 간 공유의 첫 필드(타겟 캐싱)
    public float stunTimer;      // stun 이음새 — 외부 CC가 기록, BT가 소비
    // 이후 Threat, PerceivedAllies ... 필드 추가만으로 확장
}
```
- **경계**: Blackboard = "노드 간 공유" 데이터(타겟·위협도·CC 의도). POCO = "한 행동의 내부" 상태(경로 진행도·공격 쿨다운). → Blackboard가 만능 전역변수로 비대해지지 않게 한다.
- 통로(`Evaluate(bb)`)는 지금 깔고, 데이터는 `Target`/`stunTimer` 씨앗만 둔다. 나중에 필드 추가 시 **프레임워크 시그니처 변경 0**.
- `stunTimer`는 **이음새**일 뿐 — 현재 이를 기록하는 주체(CC 시스템)는 없으므로 stun 가지는 지금은 휴면 상태(항상 false). CC 시스템 도입 시 `stunTimer`만 기록하면 동작.

### 3.4 러너 — `ActorBehaviorTree`
```csharp
public abstract class ActorBehaviorTree : MonoBehaviour
{
    protected IActor actor;
    protected Blackboard blackboard = new();
    private BTNode root;

    protected virtual void Awake()   { actor = GetComponentInParent<IActor>(); }
    protected virtual void OnEnable(){ blackboard.Target = null; if (root == null) root = BuildTree(); } // 스폰 시 bb 리셋
    private void Update()             { if (actor.CurrentState != ActorState.Dead) root.Evaluate(blackboard); }

    // 모든 액터 공통 골격: stun 처리(우선) → 액터별 primary
    protected virtual BTNode BuildTree() => BT.Selector(
        BT.Sequence(
            BT.Condition(bb => bb.stunTimer > 0f),                       // 외부가 기록한 stun 의도
            BT.Action(bb => { bb.stunTimer -= Time.deltaTime; actor.SetState(ActorState.Stunned); return NodeStatus.Running; })), // 전환·홀드(primary 차단)
        BuildPrimary());

    protected abstract BTNode BuildPrimary(); // ◀ 오버라이드 지점
}
```

---

## 4. 오버라이드 — EnemyBehaviorTree / TowerBehaviorTree

```csharp
// EnemyBehaviorTree : ActorBehaviorTree  — 비공격(이동만)
protected override BTNode BuildPrimary() => BT.Action(bb => {
    var mv = monster.CurrentMovement;                 // live read (풀링 안전)
    if (mv == null) return NodeStatus.Failure;
    mv.Tick(Time.deltaTime);
    actor.SetState(ActorState.Moving);
    if (mv.HasReachedGoal) { monster.HandleReachedGoal(); return NodeStatus.Success; }
    return NodeStatus.Running;
});

// TowerBehaviorTree : ActorBehaviorTree  — 공격 추가
protected override BTNode BuildPrimary() => BT.Selector(
    BT.Sequence(
        BT.Condition(bb => combat.HasTargetInRange()),
        BT.Action(bb => { combat.TickAttack(Time.deltaTime); actor.SetState(ActorState.Attacking); return NodeStatus.Running; })),
    BT.Action(bb => { actor.SetState(ActorState.Idle); return NodeStatus.Success; }));
```
- **공격은 베이스에 없고 TowerBehaviorTree에서만 추가** — Enemy 컨트롤러엔 Attack 스테이트 불필요.
- 메서드명(`CurrentMovement`/`HandleReachedGoal`/`HasTargetInRange`/`TickAttack`)은 구현 시 기존 POCO·액터 API에 맞춰 확정.

---

## 5. 통합 & 풀링

### 5.1 컴포넌트 배치 / 배선
```
Enemy 프리팹:  [MonsterActor] + [EnemyBehaviorTree]
Tower 프리팹:  [TowerActor]   + [TowerBehaviorTree]
```
- 트리 컴포넌트는 액터와 **같은 GameObject의 별도 컴포넌트**, `GetComponentInParent<IActor>()`로 액터 참조.
- **이동 전략**: 스폰마다 모드가 주입(`SetMovement`) → 리프가 액터에서 **live read**(캐싱 금지).
- **CombatLogic**: 타워 내재, 스폰 불변 → **TowerBehaviorTree가 소유·생성**(기존 `CombatLogic` 재사용).
- 비대칭 근거: "누가 생성하느냐"는 "변동성이 어디서 오느냐"를 따른다(이동=모드별 주입, 공격=타워 내재).

### 5.2 `MonsterActor.Update` 흡수
| 기존 | 변경 후 |
|---|---|
| `MonsterActor.Update`가 `movement.Tick` 직접 호출 | EnemyBehaviorTree 리프가 호출 (이동 tick 흡수) |
| 도달 `Resolve(reached:true)` | `monster.HandleReachedGoal()`로 노출, 리프가 호출 |
| 피격사 `TakeDamage→Resolve(false)` | MonsterActor 유지 (BT 밖, 데미지 이벤트 경로) |

### 5.3 풀링 (`IPoolable`)
- 트리는 **1회 빌드**(`OnEnable` 첫 진입), 리프 live-read라 스폰마다 재빌드 불필요.
- `OnEnable`(스폰 재활성)에서 `blackboard.Target = null` 초기화 — 이전 생애 잔여 제거.
- 반응형 Composite 무상태 → 추가 리셋 불필요.

---

## 6. ActorAnimatorBinder (상태 기반 연출)

```csharp
public sealed class ActorAnimatorBinder : MonoBehaviour
{
    [SerializeField] private Animator animator;
    private static readonly int stateHash = Animator.StringToHash("State");
    private static readonly int dirHash   = Animator.StringToHash("Direction");
    private IActor actor;

    private void OnEnable()  { actor = GetComponentInParent<IActor>(); actor.StateChanged += HandleStateChanged; HandleStateChanged(actor.CurrentState); }
    private void OnDisable() { if (actor != null) actor.StateChanged -= HandleStateChanged; }

    private void HandleStateChanged(ActorState s) { animator.SetInteger(stateHash, (int)s); } // Idle0/Move1/Atk2/Stun3/Dead4

    private void Update()
    {
        if (actor == null || actor.CurrentState != ActorState.Moving) return;
        animator.SetInteger(dirHash, ResolveDirection());   // 연속 방향이 필요한 상태에서만
    }

    protected virtual int ResolveDirection() { /* 위치 델타 → BillboardMath.DirectionIndex(카메라 기준 4방향) */ }
}
```

### 6.1 파라미터 규약 (AnimatorController 에셋이 전환 소유)
| 파라미터 | 타입 | 의미 |
|---|---|---|
| `State` | int | `(int)ActorState` 직결 — Idle0/Moving1/Attacking2/Stunned3/Dead4 |
| `Direction` | int | 0=S / 1=N / 2=E / 3=W (`BillboardMath.DirectionIndex` 재사용) |

- 컨트롤러는 `State`로 1차 분기, Moving/Attacking은 `Direction`으로 서브 분기.
- 기존 Cainos `IsMoving` bool은 `State`로 흡수(중복 제거). 적/타워는 각자 컨트롤러, **오버라이드 = 컨트롤러 교체**(코드 0). 특수 연출 액터만 `ResolveDirection`/훅 virtual 오버라이드.
- 기존 `ActorAnimatorView`(속도 기반)는 `ActorAnimatorBinder`로 **대체**(삭제).

---

## 7. 테스트 전략 (EditMode, 순수함수 패턴)

| 계층 | 대상 | 방식 |
|---|---|---|
| 순수 | Sequence/Selector 평가, Condition/Action 리프 | 스텁 노드(스크립트된 status)로 진리표 — [S,F]→Fail, [F,Running]→Running 등 |
| 순수 | State→param 매핑 | `(int)ActorState` 단위 검증 |
| 순수 | Direction | `BillboardMath.DirectionIndex` (기존 테스트 유지) |
| 통합 | 트리 구동 상태전환 | `StubMovableActor`(기존)+가짜 전략/Combat로 트리 Evaluate → `actor.CurrentState` 전이 검증 (stun 진입 시 primary 차단 등) |
| 수동/PlayMode | 실제 클립 재생·컨트롤러 전환 | Unity Test Runner / 에디터 확인 |

- 핵심: BT 노드를 MonoBehaviour 밖 순수 POCO로 두어 트리 전체를 EditMode에서 검증(`CameraRigMath`/`BillboardMath`와 동일 패턴).

---

## 8. 산출물 (구현 시 생성/수정)

**신규**
- `Assets/Scripts/Systems/Actor/BTNode.cs` 보강(또는 분리): `Evaluate(Blackboard)` 시그니처, `Sequence`/`Selector`/`ConditionLeaf`/`ActionLeaf`
- `Assets/Scripts/Systems/Actor/BT.cs` (빌더 정적 클래스)
- `Assets/Scripts/Systems/Actor/Blackboard.cs`
- `Assets/Scripts/Systems/Actor/ActorBehaviorTree.cs` (추상 러너)
- `Assets/Scripts/Systems/Enemy/EnemyBehaviorTree.cs`
- `Assets/Scripts/Systems/Tower/TowerBehaviorTree.cs`
- `Assets/Scripts/Systems/Visual/Animation/ActorAnimatorBinder.cs`
- AnimatorController 에셋: 적/타워용 (State+Direction 규약)
- EditMode 테스트: `BTCompositeTests`, `BrainStateTransitionTests`, (필요 시) `AnimatorStateParamTests`

**수정**
- `Assets/Scripts/Systems/Actor/BehaviorTree.cs` (기존 `MoveToTargetNode` 정리 — 람다 리프로 대체)
- `Assets/Scripts/Systems/Enemy/MonsterActor.cs` (Update 이동 tick 제거, `HandleReachedGoal`/`CurrentMovement` 노출)
- 적/타워 프리팹 (Brain·Binder 컴포넌트 부착, 컨트롤러 교체)

**삭제**
- `Assets/Scripts/Systems/Visual/Billboard/ActorAnimatorView.cs` (Binder로 대체)

---

## 9. 향후 확장 (이번 범위 밖, 설계상 열어둠)

- Blackboard 데이터 확장(Threat/Perception/stunTimer) — 필드 추가만.
- 데코레이터(`Inverter`/`Cooldown`/`Parallel`) — 노드 1개 추가.
- 상태이상(CC) 시스템 → `blackboard.stunTimer` 기록 → BT stun 가지가 소비.
- 공격형 적 → `EnemyData` 공격 스탯 + EnemyBehaviorTree primary에 공격 가지.
- 중앙 BT 스케줄러/time-slicing (성능 한계 도달 시).
- 데이터 주도 트리(SO) / 비주얼 에디터.
