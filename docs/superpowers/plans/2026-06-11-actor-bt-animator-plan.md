# HD-2D Actor Behavior Tree + Animator 통합 구현 계획

> **작성일**: 2026-06-11  
> **스펙 문서**: [2026-06-10-actor-bt-animator-design.md](../specs/2026-06-10-actor-bt-animator-design.md)  
> **다이어그램**: [diagram.drawio](../../diagram.drawio) (4페이지)

---

## Goal

ActorState(Idle/Moving/Attacking/Stunned/Dead) 기반 BT 시스템과 Animator를 통합하여:
- **비공격 베이스**(적): 이동 → 기절 → 사망
- **공격 오버라이드**(타워): BuildPrimary()로 공격 로직 추가
- **단일 writer**: ActorBehaviorTree가 ActorState 유일 소유 → Animator는 reader
- **재사용성**: 새 액터는 BuildPrimary() 메서드 1개 추가로 끝

---

## Task 분류 & 순서 (6개)

| # | 작업 | 산출물 | 방식 | 의존 |
|---|---|---|---|---|
| **1** | ActorState enum 확장 | 순서 확정 | 조사 + 편집 | — |
| **2** | BTNode 기초 + Composite/Leaf | TDD(4) | 서브에이전트 | 1 |
| **3** | 빌더 API `BT` | 테스트 통과(2) | 서브에이전트 | 2 |
| **4** | ActorBehaviorTree(런너) | 구현 + 기초 테스트(2) | 서브에이전트 | 2,3 |
| **5** | EnemyBehaviorTree / TowerBehaviorTree | BuildPrimary 구현 + 통합 테스트(4) | 서브에이전트 | 4 |
| **6** | ActorAnimatorBinder + Animator 컨트롤러 | 에디터(프리팹 + AC 에셋) + Play 검증 | 사용자 | 5 |

예상 코드: ~600줄, 테스트: 12개, 모드: main(직접 작업)

---

## Task 1 — ActorState enum 확인 및 확장

**기존**: `Idle=0, Moving=1, Attacking=2, Stunned=3, Dead=4` (Assets/Scripts/Core/ActorState.cs)

**확인 사항**:
- [ ] enum 순서가 스펙과 일치 (State int = (int)ActorState 직결)
- [ ] 주석 추가: "enum 순서 = Animator State int 매핑. 재정렬 금지."

**산출물**: ActorState.cs (주석 추가 버전)

---

## Task 2 — BTNode 프레임워크 (TDD: Red)

**테스트 작성** (계약 검증):

```csharp
// BTNodeTests.cs
[Test] public void Sequence_AllSuccess_ReturnsSuccess() { ... }
[Test] public void Sequence_FirstFailure_ReturnsFailure() { ... }
[Test] public void Selector_FirstSuccess_ReturnsSuccess() { ... }
[Test] public void Selector_AllFailure_ReturnsFailure() { ... }
```

**구현** (스펙 §3 그대로):
```csharp
public abstract class BTNode { abstract NodeStatus Evaluate(Blackboard bb); }
public sealed class Sequence : BTNode { ... }
public sealed class Selector : BTNode { ... }
public sealed class ConditionLeaf : BTNode { Func<Blackboard,bool> predicate; ... }
public sealed class ActionLeaf : BTNode { Func<Blackboard,NodeStatus> action; ... }

public sealed class Blackboard {
    public ITargetable Target;
    public float StunTimer;
}
```

**파일**: 
- `Assets/Scripts/Systems/Actor/BehaviorTree.cs` (노드 클래스들 이전 코드 정리)
- `Assets/Tests/EditMode/BTNodeTests.cs` (4개 테스트)

**기대**: 4 테스트 PASS

---

## Task 3 — 빌더 API (TDD: Red)

**테스트**:
```csharp
// BTBuilderTests.cs
[Test] public void BT_Sequence_Builds() { var node = BT.Sequence(...); Assert.IsInstanceOf<Sequence>(node); }
[Test] public void BT_Condition_Success() { ... }
[Test] public void BT_Action_Running() { ... }
// + 조합 1개
```

**구현**:
```csharp
public static class BT {
    public static BTNode Sequence(params BTNode[] children) => new Sequence(children);
    public static BTNode Selector(params BTNode[] children) => new Selector(children);
    public static BTNode Condition(Func<Blackboard,bool> pred) => new ConditionLeaf(pred);
    public static BTNode Action(Func<Blackboard,NodeStatus> act) => new ActionLeaf(act);
}
```

**파일**: `Assets/Tests/EditMode/BTBuilderTests.cs` (2개)

**기대**: 2 PASS

---

## Task 4 — ActorBehaviorTree (런너) + 기초 테스트

**구현**:
```csharp
public abstract class ActorBehaviorTree : MonoBehaviour {
    protected IActor actor;
    protected Blackboard blackboard;
    protected BTNode root;

    protected virtual void Awake() { actor = GetComponent<IActor>(); }
    protected virtual void OnEnable() { blackboard = new(); root = BuildTree(); }
    protected virtual void Update() {
        if (actor.CurrentState == ActorState.Dead) return;
        root.Evaluate(blackboard);
    }
    protected virtual BTNode BuildTree() => BT.Selector(
        BT.Sequence(
            BT.Condition(bb => actor.CurrentState == ActorState.Stunned),
            BT.Action(SetIdleDuringStun)),
        BuildPrimary());
    
    protected abstract BTNode BuildPrimary();
    NodeStatus SetIdleDuringStun(Blackboard bb) {
        actor.SetState(ActorState.Stunned);
        return NodeStatus.Running;
    }
}
```

**테스트** (EditMode):
```csharp
// ActorBehaviorTreeTests.cs
[Test] public void BuildTree_BaseStructure_ContainsStunAndPrimary() { ... }
[Test] public void Update_StunConditionTrue_DoesNotInvokePrimary() { 
    // StubMovableActor + bb.StunTimer > 0 → stun 가지 실행, Primary 무시 확인
}
```

**파일**:
- `Assets/Scripts/Systems/Actor/ActorBehaviorTree.cs`
- `Assets/Tests/EditMode/ActorBehaviorTreeTests.cs` (2개)

**기대**: 2 PASS + 컴파일 0 에러

---

## Task 5 — EnemyBehaviorTree / TowerBehaviorTree 구현 + 통합 테스트

### 5-A. EnemyBehaviorTree

```csharp
public sealed class EnemyBehaviorTree : ActorBehaviorTree {
    protected override BTNode BuildPrimary() {
        return BT.Action(bb => {
            var monster = actor as MonsterActor;
            var mv = monster.CurrentMovement;
            if (mv == null) return NodeStatus.Failure;
            
            mv.Tick(Time.deltaTime);
            actor.SetState(ActorState.Moving);
            
            if (mv.HasReachedGoal) {
                monster.HandleReachedGoal();
                return NodeStatus.Success;
            }
            return NodeStatus.Running;
        });
    }
}
```

### 5-B. TowerBehaviorTree

```csharp
public sealed class TowerBehaviorTree : ActorBehaviorTree {
    CombatLogic combat;  // 기존 TowerActor.combatLogic 재사용
    
    protected override void Awake() {
        base.Awake();
        combat = GetComponent<TowerActor>().combatLogic;  // 기존 로직 접근
    }
    
    protected override BTNode BuildPrimary() {
        return BT.Selector(
            BT.Sequence(
                BT.Condition(bb => combat.HasTargetInRange()),
                BT.Action(bb => {
                    combat.TickAttack(Time.deltaTime);
                    actor.SetState(ActorState.Attacking);
                    return NodeStatus.Running;
                })),
            BT.Action(bb => {
                actor.SetState(ActorState.Idle);
                return NodeStatus.Success;
            }));
    }
}
```

**통합 테스트**:
```csharp
// ActorBehaviorTreeIntegrationTests.cs
[Test] public void Enemy_InitialState_IsIdle() { ... }
[Test] public void Enemy_WithMovement_TransitionsToMoving() { ... }
[Test] public void Tower_WithTargetInRange_TransitionsToAttacking() { ... }
[Test] public void Tower_WithoutTarget_StaysIdle() { ... }
```

**파일**:
- `Assets/Scripts/Systems/Enemy/EnemyBehaviorTree.cs`
- `Assets/Scripts/Systems/Tower/TowerBehaviorTree.cs`
- `Assets/Tests/EditMode/ActorBehaviorTreeIntegrationTests.cs` (4개)

**기대**: 4 PASS

---

## Task 6 — ActorAnimatorBinder + 프리팹 배선 (에디터 작업)

### 6-A. ActorAnimatorBinder 구현 (서브에이전트)

스펙 §6 그대로:
```csharp
public sealed class ActorAnimatorBinder : MonoBehaviour {
    [SerializeField] Animator animator;
    static readonly int stateHash = Animator.StringToHash("State");
    static readonly int dirHash = Animator.StringToHash("Direction");
    IActor actor;

    void OnEnable() {
        actor = GetComponentInParent<IActor>();
        actor.StateChanged += OnStateChanged;
        OnStateChanged(actor.CurrentState);
    }
    void OnDisable() {
        if (actor != null) actor.StateChanged -= OnStateChanged;
    }

    void OnStateChanged(ActorState s) => animator.SetInteger(stateHash, (int)s);
    void Update() {
        if (actor.CurrentState != ActorState.Moving) return;
        animator.SetInteger(dirHash, ResolveDirection());
    }
    protected virtual int ResolveDirection() {
        // 기존 ActorAnimatorView의 BillboardMath.DirectionIndex 로직 재사용
    }
}
```

**파일**: `Assets/Scripts/Systems/Visual/Billboard/ActorAnimatorBinder.cs`

### 6-B. 프리팹 배선 (사용자)

| 프리팹 | 작업 |
|---|---|
| **Enemy_Placeholder.prefab** | 루트에 `EnemyBehaviorTree` 추가. 기존 `ActorAnimatorView` → `ActorAnimatorBinder`로 교체 (슬롯 동일) |
| **Tower 프리팹(기존)** | 루트에 `TowerBehaviorTree` 추가. `ActorAnimatorBinder` 추가(또는 기존 View 교체) |
| **AnimatorController** | AC_Enemy/AC_Tower 각각: State (int) + Direction (int) 파라미터 추가, 상태 전환 로직 구성 (idle→move, idle→attack 등) |

### 6-C. 검증 (Play)

- [ ] Enemy 스폰 → Idle → 이동하며 Moving + 방향 애니
- [ ] 코어 도달 → Dead (상태 전환만, 애니 없음 가능)
- [ ] Tower 스폰 → Idle → 타겟 범위 내 → Attacking + 공격 애니
- [ ] Stun (외부에서 blackboard.StunTimer 세팅 → 다음 tick에 Stunned 전환)

**파일**:
- `Assets/Scripts/Systems/Visual/Billboard/ActorAnimatorBinder.cs` (구현)
- `Assets/Prefabs/...` 프리팹 배선
- `Assets/Animations/...` 컨트롤러 (새 또는 기존 강화)

---

## 테스트 검증 경로

**Phase A: EditMode (코드 Task 1~5)**
1. Task 1 확인(enum)
2. Task 2 Run All → 4 PASS
3. Task 3 Run All → 2 PASS
4. Task 4·5 Run All → 6 PASS (누적)
5. **컴파일 0 에러**

**Phase B: Play (에디터 Task 6)**
1. 프리팹 배선 완료
2. Enemy/Tower 스폰 후 Play
3. 상태 전환 + 애니 재생 육안 확인
4. Blackboard stun 주입 → 다음 tick 반응 확인

---

## 위험 & 결정

| 항목 | 결정 |
|---|---|
| **Blackboard 초기값** | OnEnable에서 `bb = new()`로 리셋 → 재풀링 시 stun 잔여 제거 |
| **live-read 캐싱 금지** | Enemy의 movement는 리프가 매 tick `actor.CurrentMovement` 접근(캐싱 안 함) |
| **Animator 오버로드** | ActorAnimatorBinder는 공통, 컨트롤러 에셋만 프리팹별(Monster vs Tower) |
| **Dead 즉시성** | BT는 `Update`에서 `!= Dead` 체크로 정지(예외) — 즉시 필요 |

---

## 향후 (Post-이 계획)

- **TASK-006 완료**: 방향 애니 + Stun/Attack 실제 구동 원(CC 시스템, 타워 발사) 연결
- **Phase 4**: 커스텀 틸트시프트 Renderer Feature
- **중앙 스케줄러**: 80마리 이상 규모 시 per-actor Update를 일괄 tick으로 (지금은 YAGNI)

---

## 실행 방식

**권장**: 서브에이전트 주도
- Task 1~5: 서브에이전트 2회 (1+2+3 / 4+5)
- Task 6: 사용자 에디터 작업 + Play 검증
- 테스트 실행: 사용자 Unity Test Runner
