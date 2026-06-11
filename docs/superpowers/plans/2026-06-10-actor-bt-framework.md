# Actor BT Framework (Phase 1) Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** ActorState를 구동할 순수 C# Behavior Tree 프레임워크(노드·Composite·Leaf·Blackboard·빌더)를 EditMode 테스트와 함께 구축한다.

**Architecture:** MonoBehaviour에 의존하지 않는 순수 POCO 노드 트리. `BTNode.Evaluate(Blackboard)`로 컨텍스트가 트리를 관통하며, 반응형(매 tick 루트 재평가) Composite를 사용한다. 통합·연출은 후속 Phase.

**Tech Stack:** C# (Unity 6000.2), NUnit EditMode, 어셈블리 `DefenseDot`.

**Commits:** 프로젝트 정책상 각 Task의 commit 단계는 **`commit` 스킬 호출**로 수행한다 (직접 `git commit` 금지).

**Spec:** `docs/superpowers/specs/2026-06-10-actor-bt-animator-design.md` §3, §7. **Diagram:** `diagram.drawio`.

---

## File Structure (Phase 1)

| 파일 | 책임 |
|---|---|
| `Assets/Scripts/Systems/Actor/NodeStatus.cs` | 노드 결과 enum |
| `Assets/Scripts/Systems/Actor/BTNode.cs` | 노드 베이스 (`Evaluate(Blackboard)`) |
| `Assets/Scripts/Systems/Actor/Blackboard.cs` | 노드 간 공유 데이터 |
| `Assets/Scripts/Systems/Actor/Sequence.cs` | Composite (AND) |
| `Assets/Scripts/Systems/Actor/Selector.cs` | Composite (OR) |
| `Assets/Scripts/Systems/Actor/ConditionLeaf.cs` | 술어 리프 |
| `Assets/Scripts/Systems/Actor/ActionLeaf.cs` | 동작 리프 |
| `Assets/Scripts/Systems/Actor/BT.cs` | fluent 빌더 |
| `Assets/Scripts/Systems/Actor/BehaviorTree.cs` | **삭제** (기존 스텁 `MoveToTargetNode`) |
| `Assets/Tests/EditMode/BTSequenceTests.cs` | Sequence 진리표 |
| `Assets/Tests/EditMode/BTSelectorTests.cs` | Selector 진리표 |
| `Assets/Tests/EditMode/BTLeafBuilderTests.cs` | 리프 + 빌더 |

> 모든 신규 타입은 어셈블리 `DefenseDot`(=`Assets/Scripts`)에 속하므로 기존 EditMode 테스트 asmdef가 이미 참조한다(추가 asmdef 작업 불필요).
> 파일명 동기화 훅: 각 파일의 **첫 번째 타입명 = 파일명**이 되도록 작성한다(예: `BTNode.cs`의 첫 타입은 `BTNode`).

---

## Task 1: 기존 스텁 제거 + 프레임워크 기반 타입

**Files:**
- Delete: `Assets/Scripts/Systems/Actor/BehaviorTree.cs` (+ `.meta`)
- Create: `Assets/Scripts/Systems/Actor/NodeStatus.cs`
- Create: `Assets/Scripts/Systems/Actor/BTNode.cs`
- Create: `Assets/Scripts/Systems/Actor/Blackboard.cs`

- [ ] **Step 1: 기존 스텁이 외부에서 참조되지 않는지 확인**

Run (Grep): `MoveToTargetNode` 를 `Assets/` 전체에서 검색.
Expected: 정의처(`BehaviorTree.cs`) 외 참조 없음. 참조가 있으면 중단하고 보고.

- [ ] **Step 2: 기존 스텁 삭제**

`Assets/Scripts/Systems/Actor/BehaviorTree.cs` 와 `BehaviorTree.cs.meta` 삭제.
(같은 네임스페이스에 `NodeStatus`/`BTNode`가 재정의되므로 중복 정의 컴파일 오류 방지를 위해 먼저 삭제.)

- [ ] **Step 3: NodeStatus.cs 작성**

```csharp
namespace DefenseDot.Systems.Actor
{
    /// <summary> BT 노드 1회 평가 결과입니다. </summary>
    public enum NodeStatus
    {
        /// <summary> 아직 수행 중. </summary>
        Running,
        /// <summary> 성공으로 종료. </summary>
        Success,
        /// <summary> 실패로 종료. </summary>
        Failure
    }
}
```

- [ ] **Step 4: BTNode.cs 작성**

```csharp
namespace DefenseDot.Systems.Actor
{
    /// <summary> 모든 BT 노드의 베이스입니다. 평가 시 공유 Blackboard를 전달받습니다. </summary>
    public abstract class BTNode
    {
        /// <summary> 노드를 1회 평가하고 결과 상태를 반환합니다. </summary>
        public abstract NodeStatus Evaluate(Blackboard blackboard);
    }
}
```

- [ ] **Step 5: Blackboard.cs 작성**

```csharp
using DefenseDot.Core;

namespace DefenseDot.Systems.Actor
{
    /// <summary>
    /// BT 노드 간 공유 데이터입니다. (한 행동의 내부 상태는 POCO가 보유 — 여기엔 노드 간 공유만)
    /// </summary>
    public sealed class Blackboard
    {
        /// <summary> 현재 타겟(노드 간 공유 캐시). </summary>
        public ITargetable target;

        /// <summary> 남은 기절 시간(초). 외부 CC 시스템이 기록하고 BT가 소비합니다. </summary>
        public float stunTimer;
    }
}
```

- [ ] **Step 6: 컴파일 확인**

Unity 에디터로 전환하여 컴파일. Expected: 오류 0 (콘솔 `Unity.GetConsoleLogs` 확인).

- [ ] **Step 7: 커밋** — `commit` 스킬 호출.
메시지 예: `feat: BT 프레임워크 기반 타입(NodeStatus/BTNode/Blackboard) 추가 및 스텁 제거`

---

## Task 2: Sequence Composite (TDD)

**Files:**
- Test: `Assets/Tests/EditMode/BTSequenceTests.cs`
- Create: `Assets/Scripts/Systems/Actor/Sequence.cs`

- [ ] **Step 1: 실패 테스트 작성**

`Assets/Tests/EditMode/BTSequenceTests.cs`:
```csharp
using NUnit.Framework;
using DefenseDot.Systems.Actor;

namespace DefenseDot.Tests.EditMode
{
    public class BTSequenceTests
    {
        /// <summary> 스크립트된 결과를 반환하고 평가 횟수를 세는 테스트용 노드. </summary>
        private sealed class StubNode : BTNode
        {
            private readonly NodeStatus status;
            public int EvalCount { get; private set; }
            public StubNode(NodeStatus status) { this.status = status; }
            public override NodeStatus Evaluate(Blackboard blackboard) { EvalCount++; return status; }
        }

        [Test]
        public void AllSuccess_ReturnsSuccess()
        {
            var seq = new Sequence(new BTNode[] { new StubNode(NodeStatus.Success), new StubNode(NodeStatus.Success) });
            Assert.AreEqual(NodeStatus.Success, seq.Evaluate(new Blackboard()));
        }

        [Test]
        public void FirstFailure_ReturnsFailure_AndSkipsRest()
        {
            var second = new StubNode(NodeStatus.Success);
            var seq = new Sequence(new BTNode[] { new StubNode(NodeStatus.Failure), second });
            Assert.AreEqual(NodeStatus.Failure, seq.Evaluate(new Blackboard()));
            Assert.AreEqual(0, second.EvalCount, "Failure 이후 자식은 평가되지 않아야 함");
        }

        [Test]
        public void RunningChild_ReturnsRunning_AndSkipsRest()
        {
            var second = new StubNode(NodeStatus.Success);
            var seq = new Sequence(new BTNode[] { new StubNode(NodeStatus.Running), second });
            Assert.AreEqual(NodeStatus.Running, seq.Evaluate(new Blackboard()));
            Assert.AreEqual(0, second.EvalCount);
        }
    }
}
```

- [ ] **Step 2: 테스트 실패 확인**

Unity Test Runner(EditMode) 실행. Expected: `Sequence` 미정의로 컴파일 실패(RED).

- [ ] **Step 3: Sequence.cs 구현**

```csharp
using System.Collections.Generic;

namespace DefenseDot.Systems.Actor
{
    /// <summary> 자식을 순서대로 평가하다 Success가 아닌 결과(Failure/Running)를 만나면 그 결과로 중단하는 Composite입니다. </summary>
    public sealed class Sequence : BTNode
    {
        private readonly IReadOnlyList<BTNode> children;

        /// <summary> 평가할 자식 노드 목록을 받습니다. </summary>
        public Sequence(IReadOnlyList<BTNode> children) { this.children = children; }

        /// <summary> 전부 Success면 Success, 아니면 첫 비-Success 결과를 반환합니다. </summary>
        public override NodeStatus Evaluate(Blackboard blackboard)
        {
            for (int i = 0; i < children.Count; i++)
            {
                NodeStatus status = children[i].Evaluate(blackboard);
                if (status != NodeStatus.Success) return status;
            }
            return NodeStatus.Success;
        }
    }
}
```

- [ ] **Step 4: 테스트 통과 확인**

Unity Test Runner(EditMode). Expected: `BTSequenceTests` 3개 PASS(GREEN).

- [ ] **Step 5: 커밋** — `commit` 스킬. 예: `test: Sequence Composite 진리표 + 구현`

---

## Task 3: Selector Composite (TDD)

**Files:**
- Test: `Assets/Tests/EditMode/BTSelectorTests.cs`
- Create: `Assets/Scripts/Systems/Actor/Selector.cs`

- [ ] **Step 1: 실패 테스트 작성**

`Assets/Tests/EditMode/BTSelectorTests.cs`:
```csharp
using NUnit.Framework;
using DefenseDot.Systems.Actor;

namespace DefenseDot.Tests.EditMode
{
    public class BTSelectorTests
    {
        private sealed class StubNode : BTNode
        {
            private readonly NodeStatus status;
            public int EvalCount { get; private set; }
            public StubNode(NodeStatus status) { this.status = status; }
            public override NodeStatus Evaluate(Blackboard blackboard) { EvalCount++; return status; }
        }

        [Test]
        public void AllFailure_ReturnsFailure()
        {
            var sel = new Selector(new BTNode[] { new StubNode(NodeStatus.Failure), new StubNode(NodeStatus.Failure) });
            Assert.AreEqual(NodeStatus.Failure, sel.Evaluate(new Blackboard()));
        }

        [Test]
        public void FirstSuccess_ReturnsSuccess_AndSkipsRest()
        {
            var second = new StubNode(NodeStatus.Failure);
            var sel = new Selector(new BTNode[] { new StubNode(NodeStatus.Success), second });
            Assert.AreEqual(NodeStatus.Success, sel.Evaluate(new Blackboard()));
            Assert.AreEqual(0, second.EvalCount, "Success 이후 자식은 평가되지 않아야 함");
        }

        [Test]
        public void RunningChild_ReturnsRunning_AndSkipsRest()
        {
            var second = new StubNode(NodeStatus.Failure);
            var sel = new Selector(new BTNode[] { new StubNode(NodeStatus.Running), second });
            Assert.AreEqual(NodeStatus.Running, sel.Evaluate(new Blackboard()));
            Assert.AreEqual(0, second.EvalCount);
        }
    }
}
```

- [ ] **Step 2: 테스트 실패 확인** — Test Runner(EditMode). Expected: `Selector` 미정의 RED.

- [ ] **Step 3: Selector.cs 구현**

```csharp
using System.Collections.Generic;

namespace DefenseDot.Systems.Actor
{
    /// <summary> 자식을 순서대로 평가하다 Failure가 아닌 결과(Success/Running)를 만나면 그 결과로 중단하는 Composite입니다. </summary>
    public sealed class Selector : BTNode
    {
        private readonly IReadOnlyList<BTNode> children;

        /// <summary> 평가할 자식 노드 목록을 받습니다. </summary>
        public Selector(IReadOnlyList<BTNode> children) { this.children = children; }

        /// <summary> 전부 Failure면 Failure, 아니면 첫 비-Failure 결과를 반환합니다. </summary>
        public override NodeStatus Evaluate(Blackboard blackboard)
        {
            for (int i = 0; i < children.Count; i++)
            {
                NodeStatus status = children[i].Evaluate(blackboard);
                if (status != NodeStatus.Failure) return status;
            }
            return NodeStatus.Failure;
        }
    }
}
```

- [ ] **Step 4: 테스트 통과 확인** — Test Runner. Expected: `BTSelectorTests` 3개 PASS.

- [ ] **Step 5: 커밋** — `commit` 스킬. 예: `test: Selector Composite 진리표 + 구현`

---

## Task 4: Leaf 노드 + 빌더 (TDD)

**Files:**
- Test: `Assets/Tests/EditMode/BTLeafBuilderTests.cs`
- Create: `Assets/Scripts/Systems/Actor/ConditionLeaf.cs`
- Create: `Assets/Scripts/Systems/Actor/ActionLeaf.cs`
- Create: `Assets/Scripts/Systems/Actor/BT.cs`

- [ ] **Step 1: 실패 테스트 작성**

`Assets/Tests/EditMode/BTLeafBuilderTests.cs`:
```csharp
using NUnit.Framework;
using DefenseDot.Systems.Actor;

namespace DefenseDot.Tests.EditMode
{
    public class BTLeafBuilderTests
    {
        [Test]
        public void Condition_True_Success_False_Failure()
        {
            var t = new ConditionLeaf(bb => true);
            var f = new ConditionLeaf(bb => false);
            Assert.AreEqual(NodeStatus.Success, t.Evaluate(new Blackboard()));
            Assert.AreEqual(NodeStatus.Failure, f.Evaluate(new Blackboard()));
        }

        [Test]
        public void Action_ReturnsProvidedStatus()
        {
            var a = new ActionLeaf(bb => NodeStatus.Running);
            Assert.AreEqual(NodeStatus.Running, a.Evaluate(new Blackboard()));
        }

        [Test]
        public void Leaf_CanReadAndWriteBlackboard()
        {
            var bb = new Blackboard { stunTimer = 1.5f };
            var cond = new ConditionLeaf(b => b.stunTimer > 0f);
            var act = new ActionLeaf(b => { b.stunTimer = 0f; return NodeStatus.Success; });
            Assert.AreEqual(NodeStatus.Success, cond.Evaluate(bb));
            act.Evaluate(bb);
            Assert.AreEqual(0f, bb.stunTimer);
        }

        [Test]
        public void Builder_ComposesEquivalentTree()
        {
            // Selector[ Sequence[ Condition(false), Action(Success) ], Action(Success) ]
            // 첫 Sequence 는 Condition(false)로 Failure → Selector 가 둘째 Action(Success) 채택
            var tree = BT.Selector(
                BT.Sequence(
                    BT.Condition(bb => false),
                    BT.Action(bb => NodeStatus.Success)),
                BT.Action(bb => NodeStatus.Success));
            Assert.AreEqual(NodeStatus.Success, tree.Evaluate(new Blackboard()));
        }
    }
}
```

- [ ] **Step 2: 테스트 실패 확인** — Test Runner(EditMode). Expected: `ConditionLeaf`/`ActionLeaf`/`BT` 미정의 RED.

- [ ] **Step 3: ConditionLeaf.cs 구현**

```csharp
namespace DefenseDot.Systems.Actor
{
    /// <summary> 술어가 참이면 Success, 거짓이면 Failure를 반환하는 리프입니다. </summary>
    public sealed class ConditionLeaf : BTNode
    {
        private readonly System.Func<Blackboard, bool> predicate;

        /// <summary> 평가할 술어를 받습니다. </summary>
        public ConditionLeaf(System.Func<Blackboard, bool> predicate) { this.predicate = predicate; }

        /// <summary> 술어 결과를 Success/Failure로 변환합니다. </summary>
        public override NodeStatus Evaluate(Blackboard blackboard)
        {
            return predicate(blackboard) ? NodeStatus.Success : NodeStatus.Failure;
        }
    }
}
```

- [ ] **Step 4: ActionLeaf.cs 구현**

```csharp
namespace DefenseDot.Systems.Actor
{
    /// <summary> 주어진 동작을 실행하고 그 결과 상태를 그대로 반환하는 리프입니다. </summary>
    public sealed class ActionLeaf : BTNode
    {
        private readonly System.Func<Blackboard, NodeStatus> action;

        /// <summary> 실행할 동작을 받습니다. </summary>
        public ActionLeaf(System.Func<Blackboard, NodeStatus> action) { this.action = action; }

        /// <summary> 동작을 실행하고 반환된 상태를 그대로 돌려줍니다. </summary>
        public override NodeStatus Evaluate(Blackboard blackboard)
        {
            return action(blackboard);
        }
    }
}
```

- [ ] **Step 5: BT.cs 구현**

```csharp
namespace DefenseDot.Systems.Actor
{
    /// <summary> BT 트리를 코드로 조립하는 fluent 정적 빌더입니다. </summary>
    public static class BT
    {
        /// <summary> 자식을 순서대로 평가하는 Sequence를 만듭니다. </summary>
        public static BTNode Sequence(params BTNode[] children) { return new Sequence(children); }

        /// <summary> 자식을 순서대로 평가하는 Selector를 만듭니다. </summary>
        public static BTNode Selector(params BTNode[] children) { return new Selector(children); }

        /// <summary> 술어 조건 리프를 만듭니다. </summary>
        public static BTNode Condition(System.Func<Blackboard, bool> predicate) { return new ConditionLeaf(predicate); }

        /// <summary> 동작 리프를 만듭니다. </summary>
        public static BTNode Action(System.Func<Blackboard, NodeStatus> action) { return new ActionLeaf(action); }
    }
}
```

- [ ] **Step 6: 테스트 통과 확인** — Test Runner. Expected: `BTLeafBuilderTests` 4개 PASS.

- [ ] **Step 7: 린트 + 커밋** — `commit` 스킬(내부 `lint` 자동). 예: `test: BT 리프(Condition/Action)·빌더 + 구현`

---

## Self-Review (작성자 체크)

- **Spec 커버리지(§3)**: NodeStatus/BTNode(Evaluate(Blackboard))=Task1, Sequence=Task2, Selector=Task3, Condition/Action 리프=Task4, BT 빌더=Task4, Blackboard(Target/stunTimer 씨앗)=Task1. §3 전 항목 커버. (반응형 Composite=무상태 재평가는 Sequence/Selector 구현에 반영.)
- **Spec §7 테스트**: Composite 진리표(Task2/3), 리프(Task4), 빌더 합성(Task4) — 순수 계층 전부. (트리 구동 상태전환·State→param 매핑은 Phase 2/3.)
- **Placeholder 스캔**: 없음 — 모든 코드 단계에 실제 코드 포함.
- **타입 일관성**: `Evaluate(Blackboard)` 시그니처가 모든 노드/테스트에서 동일. `Sequence`/`Selector` 생성자 `IReadOnlyList<BTNode>`에 배열 전달(배열은 IReadOnlyList 구현) 일관.

---

## 후속 Phase (별도 계획으로 작성 예정)

### Phase 2 — BT 통합 리팩토링 (위험: 작동 코드 변경)
- `ActorBehaviorTree`(추상 러너: `BuildTree` stun골격 + `Update` tick + `OnEnable` bb리셋), `BuildPrimary()` 오버라이드 지점.
- `EnemyBehaviorTree`: `MonsterActor.Update`의 이동 tick 흡수, **신규 `SetState(Moving)`**(현재 적은 Moving 미사용), 도달 처리 `HandleReachedGoal`.
- `TowerBehaviorTree`: **`TowerActor.Update`의 타겟탐색·전투 루프 흡수** + `PerformAttack`의 자가 `SetState` 제거 → BT가 단독 writer. 기존 디버그 공격 토글/`TargetFinder`/`CombatLogic` 보존.
- 통합 테스트: `StubMovableActor` 등으로 트리 구동 상태전환 검증.
- **주의**: 최근 커밋된 `TowerActor`(디버그 공격 타입·치트 연동)를 건드리므로 회귀 위험. 단일 writer 전환 시 `PerformAttack` 책임 분리 설계 필요.

### Phase 3 — ActorAnimatorBinder + 컨트롤러
- `ActorAnimatorBinder`(상태 구독 → `State` int + `Direction` int push), `ActorAnimatorView` 삭제.
- 적/타워 AnimatorController(State+Direction 규약) 제작, 프리팹 배선.
- `(int)ActorState` 매핑 단위 테스트, 수동 PlayMode 확인.
