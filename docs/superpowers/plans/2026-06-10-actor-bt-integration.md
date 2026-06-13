# Actor BT Integration (Phase 2) Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Phase 1 BT 프레임워크를 액터에 연결한다 — `ActorBehaviorTree` 러너 + `EnemyBehaviorTree`/`TowerBehaviorTree`를 추가하고, `MonsterActor`/`TowerActor`의 자체 `Update` 루프를 BT로 이관하여 **BT를 `ActorState`의 단독 writer**로 만든다.

**Architecture:** 각 액터 GameObject에 `ActorBehaviorTree` 파생 컴포넌트를 둔다. 러너가 매 tick 트리를 평가하고, 리프가 기존 POCO(`IMovementStrategy`/`CombatLogic`)와 액터 메서드를 호출한 뒤 `actor.SetState(...)`로 상태를 기록한다. 액터는 더 이상 스스로 상태를 쓰지 않는다(Dead 터미널 제외).

**Tech Stack:** C# (Unity 6000.2), NUnit EditMode(GameObject 기반 통합 테스트), Unity MCP `run_tests`/`manage_components`.

**Commits:** 각 Task의 commit 단계는 **`commit` 스킬 호출**로 수행한다 (직접 `git commit` 금지).

**Spec:** `docs/superpowers/specs/2026-06-10-actor-bt-animator-design.md` §2, §4, §5. **선행:** Phase 1 (`9d5d6df7`, 커밋됨).

> **폐기**: `docs/superpowers/plans/2026-06-11-actor-bt-animator-plan.md`(미추적 초안)는 부정확(CombatLogic API 불일치, 자가-상태 제거 누락)하므로 본 계획이 대체한다.

---

## 핵심 설계 결정 (스펙 §4·§5 구체화)

1. **단일 writer 전환**: `MonsterActor.Update`(이동 tick)·`TowerActor.Update`(타겟탐색+전투)를 제거하고 BT 리프로 이관. `TowerActor.PerformAttack`의 `SetState` 호출 제거(공격 실행만 담당, 상태는 BT가 기록).
2. **POCO 재사용**: 이동은 `MonsterActor.CurrentMovement`(신규 getter)를 리프가 live-read. 공격은 `TowerActor.UpdateCombat(dt)`(기존) 호출 → `CombatLogic.Tick` → `PerformAttack`(상태 미기록).
3. **브레인 배치**: 액터와 **같은 GameObject** 루트에 부착 → `Awake`에서 `GetComponent<IActor>()`로 액터 참조. (Binder는 Visual 자식이라 `GetComponentInParent` — 구분)
4. **Blackboard 공개**: `public Blackboard Blackboard => blackboard;` — 테스트 주입 + 미래 CC 시스템의 `stunTimer` 기록 통로.
5. **틱 공개**: `public void Tick()` (Update가 호출) — EditMode 통합 테스트가 구동.

---

## File Structure (Phase 2)

| 파일 | 책임 |
|---|---|
| `Assets/Scripts/Core/ActorState.cs` | **수정** — enum 순서=계약 주석 |
| `Assets/Scripts/Systems/Actor/ActorBehaviorTree.cs` | **신규** — 추상 러너(BuildTree 골격·tick·stun 게이트) |
| `Assets/Scripts/Systems/Enemy/MonsterActor.cs` | **수정** — `CurrentMovement`/`HandleReachedGoal` 노출, `Update` 이동 tick 제거 |
| `Assets/Scripts/Systems/Enemy/EnemyBehaviorTree.cs` | **신규** — 이동 primary |
| `Assets/Scripts/Systems/Tower/TowerActor.cs` | **수정** — `HasValidTarget`/`AcquireTarget` 노출, `PerformAttack` 자가 SetState 제거, `Update` 제거 |
| `Assets/Scripts/Systems/Tower/TowerBehaviorTree.cs` | **신규** — 공격 primary |
| `Assets/Tests/EditMode/ActorBehaviorTreeTests.cs` | **신규** — stun 게이트 |
| `Assets/Tests/EditMode/EnemyBehaviorTreeTests.cs` | **신규** — 이동/도달 전이 |
| `Assets/Tests/EditMode/TowerBehaviorTreeTests.cs` | **신규** — 공격/Idle 전이 |
| Enemy/Tower 프리팹 | **수정** — 브레인 컴포넌트 부착 |

---

## Task 1: ActorState 계약 주석

**Files:**
- Modify: `Assets/Scripts/Core/ActorState.cs`

- [ ] **Step 1: enum에 계약 주석 추가**

`enum ActorState` 선언 바로 위에 추가:
```csharp
    // 순서 = Animator State int 매핑. 재정렬 금지.
    public enum ActorState
```
(기존 멤버 Idle/Moving/Attacking/Stunned/Dead 순서·값 유지)

- [ ] **Step 2: 컴파일 확인** — `refresh_unity`(compile) → `read_console`(error 0).

- [ ] **Step 3: 커밋** — `commit` 스킬. 예: `docs: ActorState enum 순서 계약 주석 추가`

---

## Task 2: ActorBehaviorTree 추상 러너 (TDD)

**Files:**
- Create: `Assets/Scripts/Systems/Actor/ActorBehaviorTree.cs`
- Test: `Assets/Tests/EditMode/ActorBehaviorTreeTests.cs`

- [ ] **Step 1: 실패 테스트 작성**

`Assets/Tests/EditMode/ActorBehaviorTreeTests.cs`:
```csharp
using NUnit.Framework;
using UnityEngine;
using DefenseDot.Core;
using DefenseDot.Systems.Actor;

namespace DefenseDot.Tests.EditMode
{
    public class ActorBehaviorTreeTests
    {
        /// <summary> IActor를 구현하고 상태를 기록하는 테스트용 MonoBehaviour. </summary>
        private sealed class StubActorComponent : MonoBehaviour, IActor
        {
            public ActorState LastSet { get; private set; } = ActorState.Idle;
            public Vector3 Position => transform.position;
            public ActorState CurrentState => LastSet;
            public void SetState(ActorState newState) { LastSet = newState; StateChanged?.Invoke(newState); }
            public event System.Action<ActorState> StateChanged;
        }

        /// <summary> primary가 Moving을 쓰는 최소 파생 트리. </summary>
        private sealed class TestBehaviorTree : ActorBehaviorTree
        {
            protected override BTNode BuildPrimary()
            {
                return BT.Action(bb => { actor.SetState(ActorState.Moving); return NodeStatus.Running; });
            }
        }

        [Test]
        public void StunActive_SetsStunned_SkipsPrimary()
        {
            var go = new GameObject("a");
            go.AddComponent<StubActorComponent>();
            var brain = go.AddComponent<TestBehaviorTree>();
            brain.Blackboard.stunTimer = 1f;     // stun 의도 주입
            brain.Tick();
            Assert.AreEqual(ActorState.Stunned, brain.Actor.CurrentState, "stun 활성 시 Stunned, primary(Moving) 차단");
            Object.DestroyImmediate(go);
        }

        [Test]
        public void NoStun_RunsPrimary()
        {
            var go = new GameObject("a");
            go.AddComponent<StubActorComponent>();
            var brain = go.AddComponent<TestBehaviorTree>();
            brain.Tick();
            Assert.AreEqual(ActorState.Moving, brain.Actor.CurrentState, "stun 없으면 primary 실행");
            Object.DestroyImmediate(go);
        }
    }
}
```

- [ ] **Step 2: 테스트 실패 확인** — `run_tests` EditMode (`ActorBehaviorTreeTests`). Expected: `ActorBehaviorTree` 미정의 RED.

- [ ] **Step 3: ActorBehaviorTree.cs 구현**

```csharp
using UnityEngine;
using DefenseDot.Core;

namespace DefenseDot.Systems.Actor
{
    /// <summary>
    /// 액터의 행동을 BT로 구동하는 추상 러너입니다. (액터와 같은 GameObject에 부착)
    /// 매 tick 트리를 평가하며, ActorState의 유일한 writer입니다.
    /// </summary>
    public abstract class ActorBehaviorTree : MonoBehaviour
    {
        protected IActor actor;
        private readonly Blackboard blackboard = new Blackboard();
        private BTNode root;

        /// <summary> 노드 간 공유 데이터(외부 CC가 stunTimer 기록, 테스트 주입). </summary>
        public Blackboard Blackboard => blackboard;

        /// <summary> 구동 대상 액터. </summary>
        public IActor Actor => actor;

        /// <summary> 액터 참조를 캐싱합니다. </summary>
        protected virtual void Awake() { actor = GetComponent<IActor>(); }

        /// <summary> 스폰(재활성) 시 blackboard를 초기화하고 트리를 1회 빌드합니다. </summary>
        protected virtual void OnEnable()
        {
            blackboard.target = null;
            blackboard.stunTimer = 0f;
            if (root == null) root = BuildTree();
        }

        private void Update() { Tick(); }

        /// <summary> 트리를 1회 평가합니다. (Dead면 정지) </summary>
        public void Tick()
        {
            if (actor == null) actor = GetComponent<IActor>();   // 생명주기 미보장(EditMode) 대비
            if (actor == null || actor.CurrentState == ActorState.Dead) return;
            if (root == null) root = BuildTree();
            root.Evaluate(blackboard);
        }

        /// <summary> 공통 골격: stun 처리(우선) → 액터별 primary. </summary>
        protected virtual BTNode BuildTree()
        {
            return BT.Selector(
                BT.Sequence(
                    BT.Condition(bb => bb.stunTimer > 0f),
                    BT.Action(TickStun)),
                BuildPrimary());
        }

        private NodeStatus TickStun(Blackboard bb)
        {
            bb.stunTimer -= Time.deltaTime;       // 외부가 기록한 기절 소비
            actor.SetState(ActorState.Stunned);
            return NodeStatus.Running;             // primary 차단
        }

        /// <summary> 액터별 주 행동을 조립합니다. (오버라이드 지점) </summary>
        protected abstract BTNode BuildPrimary();
    }
}
```

- [ ] **Step 4: 테스트 통과 확인** — `run_tests` EditMode. Expected: `ActorBehaviorTreeTests` 2개 PASS.

- [ ] **Step 5: 커밋** — `commit` 스킬. 예: `feat: ActorBehaviorTree 러너(트리 tick·stun 게이트) 추가`

---

## Task 3: MonsterActor 이관 + EnemyBehaviorTree (TDD)

**Files:**
- Modify: `Assets/Scripts/Systems/Enemy/MonsterActor.cs`
- Create: `Assets/Scripts/Systems/Enemy/EnemyBehaviorTree.cs`
- Test: `Assets/Tests/EditMode/EnemyBehaviorTreeTests.cs`

- [ ] **Step 1: 실패 테스트 작성**

`Assets/Tests/EditMode/EnemyBehaviorTreeTests.cs`:
```csharp
using NUnit.Framework;
using UnityEngine;
using DefenseDot.Core;
using DefenseDot.Data;
using DefenseDot.Systems.Enemy;

namespace DefenseDot.Tests.EditMode
{
    public class EnemyBehaviorTreeTests
    {
        /// <summary> 도달 여부를 제어하는 테스트용 이동 전략. </summary>
        private sealed class FakeMovement : IMovementStrategy
        {
            public bool Reached;
            public int TickCount { get; private set; }
            public void Tick(float deltaTime) { TickCount++; }
            public bool HasReachedGoal => Reached;
        }

        private static MonsterActor MakeMonster(FakeMovement mv)
        {
            var go = new GameObject("enemy");
            var actor = go.AddComponent<MonsterActor>();
            var data = ScriptableObject.CreateInstance<EnemyData>();
            data.health = 10f;
            actor.Initialize(data);
            actor.OnSpawn();                 // 상태 Idle
            actor.SetMovement(mv);
            return actor;
        }

        [Test]
        public void WithMovement_SetsMoving()
        {
            var mv = new FakeMovement { Reached = false };
            var actor = MakeMonster(mv);
            var brain = actor.gameObject.AddComponent<EnemyBehaviorTree>();
            brain.Tick();
            Assert.AreEqual(ActorState.Moving, actor.CurrentState);
            Assert.AreEqual(1, mv.TickCount, "리프가 이동 전략을 tick 해야 함");
            Object.DestroyImmediate(actor.gameObject);
        }

        [Test]
        public void ReachedGoal_TransitionsToDead()
        {
            var mv = new FakeMovement { Reached = true };
            var actor = MakeMonster(mv);
            var brain = actor.gameObject.AddComponent<EnemyBehaviorTree>();
            brain.Tick();
            Assert.AreEqual(ActorState.Dead, actor.CurrentState, "도달 시 HandleReachedGoal→Dead");
            Object.DestroyImmediate(actor.gameObject);
        }
    }
}
```

- [ ] **Step 2: 테스트 실패 확인** — `run_tests` EditMode. Expected: `EnemyBehaviorTree`/`CurrentMovement` 미정의 RED.

- [ ] **Step 3: MonsterActor 수정**

`MonsterActor`의 `private IMovementStrategy movement;` 아래에 getter 추가:
```csharp
        /// <summary> 현재 주입된 이동 전략(브레인 리프가 live-read). </summary>
        public IMovementStrategy CurrentMovement => movement;
```
`Resolve` 위(또는 region 내부)에 도달 처리 공개 래퍼 추가:
```csharp
        /// <summary> 경로 끝 도달 처리(브레인 리프가 호출). </summary>
        public void HandleReachedGoal() => Resolve(reached: true);
```
기존 `Update`에서 **이동 tick·도달 판정 제거** (브레인이 담당). `Update` 메서드 전체 삭제:
```csharp
        // (삭제) private void Update() { movement.Tick; HasReachedGoal→Resolve }
```
(`TakeDamage`→`Resolve(false)` 경로는 유지.)

- [ ] **Step 4: EnemyBehaviorTree.cs 구현**

```csharp
using UnityEngine;
using DefenseDot.Core;
using DefenseDot.Systems.Actor;

namespace DefenseDot.Systems.Enemy
{
    /// <summary> 적의 주 행동: 주입된 이동 전략으로 이동(비공격). </summary>
    public sealed class EnemyBehaviorTree : ActorBehaviorTree
    {
        protected override BTNode BuildPrimary()
        {
            return BT.Action(bb =>
            {
                MonsterActor monster = actor as MonsterActor;     // Awake 캐시 대신 리프 캐스팅(생명주기 무의존)
                if (monster == null) return NodeStatus.Failure;
                IMovementStrategy mv = monster.CurrentMovement;   // live-read
                if (mv == null) return NodeStatus.Failure;
                mv.Tick(Time.deltaTime);
                actor.SetState(ActorState.Moving);
                if (mv.HasReachedGoal)
                {
                    monster.HandleReachedGoal();
                    return NodeStatus.Success;
                }
                return NodeStatus.Running;
            });
        }
    }
}
```

- [ ] **Step 5: 테스트 통과 확인** — `run_tests` EditMode. Expected: `EnemyBehaviorTreeTests` 2개 PASS, 회귀 0.

- [ ] **Step 6: 린트 + 커밋** — `commit` 스킬. 예: `feat: 적 이동을 EnemyBehaviorTree로 이관(단일 writer)`

---

## Task 4: TowerActor 이관 + TowerBehaviorTree (TDD)

**Files:**
- Modify: `Assets/Scripts/Systems/Tower/TowerActor.cs`
- Create: `Assets/Scripts/Systems/Tower/TowerBehaviorTree.cs`
- Test: `Assets/Tests/EditMode/TowerBehaviorTreeTests.cs`

- [ ] **Step 1: 실패 테스트 작성**

`Assets/Tests/EditMode/TowerBehaviorTreeTests.cs`:
```csharp
using NUnit.Framework;
using UnityEngine;
using DefenseDot.Core;
using DefenseDot.Data;
using DefenseDot.Systems.Tower;

namespace DefenseDot.Tests.EditMode
{
    public class TowerBehaviorTreeTests
    {
        private sealed class FakeTarget : ITargetable   // ITargetable : IActor 이므로 IActor 멤버도 구현
        {
            public Vector3 Pos;
            public Vector3 Position => Pos;
            public bool IsActive => true;
            public ActorState CurrentState => ActorState.Moving;
            public void SetState(ActorState newState) { }
            public event System.Action<ActorState> StateChanged;
        }

        [Test]
        public void NoTarget_SetsIdle()
        {
            var go = new GameObject("tower");
            var actor = go.AddComponent<TowerActor>();
            var data = ScriptableObject.CreateInstance<TowerData>();
            data.attackRange = 5f; data.attackSpeed = 1f;
            actor.Initialize(data);
            actor.OnSpawn();
            var brain = go.AddComponent<TowerBehaviorTree>();
            brain.Tick();
            Assert.AreEqual(ActorState.Idle, actor.CurrentState);
            Object.DestroyImmediate(go);
        }

        [Test]
        public void TargetInRange_SetsAttacking()
        {
            var go = new GameObject("tower");
            go.transform.position = Vector3.zero;
            var actor = go.AddComponent<TowerActor>();
            var data = ScriptableObject.CreateInstance<TowerData>();
            data.attackRange = 5f; data.attackSpeed = 1f;
            actor.Initialize(data);
            actor.OnSpawn();
            actor.SetTarget(new FakeTarget { Pos = new Vector3(1f, 0f, 0f) });  // 사거리 내
            var brain = go.AddComponent<TowerBehaviorTree>();
            brain.Tick();
            Assert.AreEqual(ActorState.Attacking, actor.CurrentState);
            Object.DestroyImmediate(go);
        }
    }
}
```

- [ ] **Step 2: 테스트 실패 확인** — `run_tests` EditMode. Expected: `TowerBehaviorTree`/`HasValidTarget` 미정의 RED.

- [ ] **Step 3: TowerActor 수정**

공개 래퍼 추가(기존 private `IsTargetValid`/`SearchTarget` 활용):
```csharp
        /// <summary> 현재 타겟이 유효(생존+사거리)한지(브레인 조건). </summary>
        public bool HasValidTarget() => IsTargetValid();

        /// <summary> 사거리 내 타겟을 탐색·설정(브레인 액션). </summary>
        public void AcquireTarget() => SearchTarget();
```
`PerformAttack`에서 **자가 `SetState` 제거** (상태는 브레인이 기록):
```csharp
        public void PerformAttack()
        {
            if (currentTarget == null || !currentTarget.IsActive) return;   // (삭제) SetState(Idle)
            // (삭제) SetState(ActorState.Attacking);
            activeBehaviors.Clear();
            if (debugSingle) activeBehaviors.Add(singleBehavior);
            if (debugAoe) activeBehaviors.Add(aoeBehavior);
            if (debugProjectile) activeBehaviors.Add(projectileBehavior);
            if (activeBehaviors.Count == 0) return;
            AttackContext ctx = new AttackContext(this, Position, targetFinder, data);
            for (int i = 0; i < activeBehaviors.Count; i++) activeBehaviors[i].Execute(in ctx);
        }
```
기존 `Update` **전체 삭제** (타겟탐색·전투를 브레인이 담당):
```csharp
        // (삭제) private void Update() { IsTargetValid ? UpdateCombat : SearchTarget }
```
(`UpdateCombat(dt)`·`combatLogic`·`SetTarget`·`Awake`/`Initialize`의 combatLogic 생성은 유지.)

- [ ] **Step 4: TowerBehaviorTree.cs 구현**

```csharp
using UnityEngine;
using DefenseDot.Core;
using DefenseDot.Systems.Actor;

namespace DefenseDot.Systems.Tower
{
    /// <summary> 타워의 주 행동: 사거리 내 타겟 공격(없으면 탐색·Idle). </summary>
    public sealed class TowerBehaviorTree : ActorBehaviorTree
    {
        protected override BTNode BuildPrimary()
        {
            return BT.Selector(
                BT.Sequence(
                    BT.Condition(bb => { TowerActor t = actor as TowerActor; return t != null && t.HasValidTarget(); }),
                    BT.Action(bb =>
                    {
                        actor.SetState(ActorState.Attacking);
                        (actor as TowerActor)?.UpdateCombat(Time.deltaTime);   // 쿨다운 시 PerformAttack
                        return NodeStatus.Running;
                    })),
                BT.Action(bb =>
                {
                    (actor as TowerActor)?.AcquireTarget();
                    actor.SetState(ActorState.Idle);
                    return NodeStatus.Success;
                }));
        }
    }
}
```

- [ ] **Step 5: 테스트 통과 확인** — `run_tests` EditMode. Expected: `TowerBehaviorTreeTests` 2개 PASS, 회귀 0.

- [ ] **Step 6: 린트 + 커밋** — `commit` 스킬. 예: `feat: 타워 전투를 TowerBehaviorTree로 이관(단일 writer)`

---

## Task 5: 프리팹 배선 + Play 검증

> 액터의 `Update` 자가 루프를 제거했으므로, 브레인 컴포넌트가 부착돼야 동작한다. 이 Task로 프리팹에 배선한다.

**Files:**
- Modify: `Assets/Prefabs/Game/Enemies/Enemy_Placeholder.prefab`
- Modify: Tower 프리팹 (`Assets/Prefabs/...` 또는 `Tower_Test`)

- [ ] **Step 1: 적 프리팹 배선** — Unity MCP `manage_components`(또는 에디터)로 `Enemy_Placeholder.prefab` 루트(`MonsterActor` 보유 GO)에 `EnemyBehaviorTree` 추가.

- [ ] **Step 2: 타워 프리팹 배선** — 타워 프리팹 루트(`TowerActor` 보유 GO)에 `TowerBehaviorTree` 추가.

- [ ] **Step 3: 컴파일·일괄 테스트** — `refresh_unity` → `read_console`(0) → `run_tests` EditMode 전체(회귀 0 확인).

- [ ] **Step 4: Play 검증 (수동/MCP)** — Grid/Arena 씬 Play:
  - 적 스폰 → 경로 이동 중 `CurrentState == Moving`, 코어 도달 시 Dead
  - 타워 → 사거리 내 적에게 Attacking(공격 수행), 없으면 Idle
  - (회귀) 기존 전투/이동이 BT 경유로도 동일 동작

- [ ] **Step 5: 커밋** — `commit` 스킬. 예: `chore: 적·타워 프리팹에 BehaviorTree 컴포넌트 배선`

---

## Self-Review (작성자 체크)

- **Spec §2(단일 writer)**: TowerActor/MonsterActor 자가 SetState·Update 제거 = Task3/4. stun 게이트(외부 stunTimer→BT가 Stunned) = Task2. Dead 예외(Update에서 `!= Dead` 정지) = Task2. ✓
- **Spec §4(오버라이드)**: `BuildPrimary` = Enemy(이동)/Tower(공격) = Task3/4. ✓
- **Spec §5(통합·풀링)**: live-read(`CurrentMovement`) = Task3. OnEnable bb 리셋 = Task2. 브레인 같은 GO·GetComponent = Task2/3/4. ✓
- **Placeholder 스캔**: 없음 — 전 코드 단계에 실제 코드.
- **타입 일관성**: `Tick()`/`Blackboard`/`Actor` 프로퍼티, `CurrentMovement`/`HandleReachedGoal`/`HasValidTarget`/`AcquireTarget`/`UpdateCombat` 시그니처가 테스트·구현·다른 Task에서 일치. `blackboard.stunTimer`/`target` camelCase(Phase 1 확정). ✓

## 후속 (Phase 3)
- `ActorAnimatorBinder`(상태 구독 → State/Direction push) + 적/타워 AnimatorController(State+Direction 규약) + `ActorAnimatorView` 삭제.
