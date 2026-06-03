# 아레나 전용 맵 시스템 Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** 원형 아레나(표준) 모드 전용 맵 시스템을 구축한다 — 데이터(`ArenaConfig`) · 동적 런타임 모델(`ArenaModel`) · 비주얼(`ArenaView`)의 3계층으로, 도넛 밴드 랜덤 스폰 + 코어 공전 + 동적 크기(압축)를 지원한다.

**Architecture:** `ArenaConfig`(SO, 정적 값) → `ArenaModel`(POCO, 현재 반경 + `Expand/Shrink` + `OnRadiusChanged`) → `ArenaView`(편집:`OnDrawGizmos` 가이드, 런타임:경계 비주얼) · `EnemySpawner`(스폰 위임) · `ArenaOrbitLogic`(비율 기반 공전). 적은 반경을 절대값이 아닌 비율(0~1)로 다뤄 아레나 축소 시 자동 압축된다.

**Tech Stack:** Unity 6000.2.10f1, C#, UniTask, Unity Test Framework(EditMode/NUnit). 도메인 모델은 `BaseModel` 상속 POCO, 모드는 `IGameMode` 전략 패턴.

---

## 설계 근거

- 설계 문서: [docs/superpowers/specs/2026-06-03-arena-map-system-design.md](../specs/2026-06-03-arena-map-system-design.md)
- 원본 충실: 도넛 밴드 랜덤 스폰 · 코어 공전 · 전체 범위 반경 진동 · 수용 한계 패배
- Unity 확장: 동적 크기(`Expand/Shrink`) · Scene 자유 꾸미기(`ArenaView`)
- 범위 밖(훅만/장식): 동적 크기 트리거 사건, 코어 비주얼

---

## File Structure

| 파일 | 책임 | 상태 |
|---|---|---|
| `Assets/Scripts/Data/ArenaConfig.cs` | 아레나 초기 형상·규칙 값(SO) | 생성 |
| `Assets/Scripts/Domain/Models/ArenaModel.cs` | 현재 반경 상태 + 변경 통지(POCO) | 생성 |
| `Assets/Scripts/Systems/Arena/ArenaView.cs` | 기즈모 가이드 + 런타임 경계 비주얼 | 생성 |
| `Assets/Scripts/Systems/Enemy/ArenaOrbitLogic.cs` | 비율 기반 공전 + 진동 | 수정 |
| `Assets/Scripts/Systems/Mode/IGameMode.cs` | 스폰 위치/이동 전략 위임 인터페이스 | 수정 |
| `Assets/Scripts/Systems/Mode/ArenaMode.cs` | 아레나 모드(공전·극좌표 스폰·수용 한계) | 수정 |
| `Assets/Scripts/Systems/Mode/GridDefenseMode.cs` | TD 모드(경로 스폰·이동) — 시그니처 일치 | 수정 |
| `Assets/Scripts/Systems/Enemy/EnemySpawner.cs` | 스폰 위치/전략을 모드에 위임 | 수정 |
| `Assets/Scripts/Systems/Management/GameManager.cs` | `ArenaConfig`→`ArenaModel`→모드/뷰 배선 | 수정 |
| `Assets/Tests/EditMode/ArenaModelTests.cs` | `ArenaModel` 단위 테스트 | 생성 |
| `Assets/Tests/EditMode/ArenaOrbitLogicTests.cs` | `ArenaOrbitLogic` 단위 테스트(스텁 사용) | 생성 |
| `Assets/Tests/EditMode/StubMovableActor.cs` | 테스트용 `IMovableActor` 스텁 | 생성 |

**의존 순서:** Task 1(Config) → Task 2(Model) → Task 3(OrbitLogic) → Task 4(IGameMode/모드) → Task 5(Spawner) → Task 6(GameManager) → Task 7(ArenaView) → Task 8(통합 검증).

**테스트 실행 방법(공통):** Unity 에디터에서 `Window > General > Test Runner` → `EditMode` 탭 → `Run All`. 녹색=PASS, 빨강=FAIL. (스크립트 수정 후 Unity로 포커스를 옮겨 재컴파일된 뒤 실행)

**커밋 주의:** 이 프로젝트는 사용자 규칙상 커밋이 명시적 승인 시에만 수행된다. 각 Task의 커밋 스텝은 그대로 따르되, 실제 `git commit`은 사용자 확인 후 실행한다.

---

### Task 1: ArenaConfig (ScriptableObject)

**Files:**
- Create: `Assets/Scripts/Data/ArenaConfig.cs`

- [ ] **Step 1: ArenaConfig 작성**

```csharp
using UnityEngine;

namespace DefenseDot.Data
{
    /// <summary>
    /// 원형 아레나의 초기 형상·규칙 값을 담는 ScriptableObject입니다.
    /// </summary>
    [CreateAssetMenu(fileName = "NewArenaConfig", menuName = "DefenseDot/ArenaConfig")]
    public class ArenaConfig : ScriptableObject
    {
        /// <summary> 초기 아레나 반경 </summary>
        public float arenaRadius = 29f;
        /// <summary> 코어 반경 </summary>
        public float coreRadius = 2.2f;
        /// <summary> 코어로부터 스폰 안쪽 여백 </summary>
        public float spawnInnerMargin = 4f;
        /// <summary> 경계로부터 스폰 바깥 여백 </summary>
        public float spawnOuterMargin = 2f;
        /// <summary> 동시 생존 적 수용 한계 </summary>
        public int maxAlive = 80;
        /// <summary> 기본 공전 각속도(라디안/초) </summary>
        public float baseAngularSpeed = 0.5f;
        /// <summary> 적 배치 높이(Y) </summary>
        public float enemyHeight = 0.8f;
    }
}
```

> 기본값은 원본 비율(290/22/40/20)을 Unity 월드 단위로 1/10 스케일한 값이다. 인스펙터에서 자유 조정 가능.

- [ ] **Step 2: Unity 재컴파일 확인**

Unity로 포커스 이동 → Console에 에러 없음 확인. `Assets/Create > DefenseDot > ArenaConfig` 메뉴가 생기는지 확인.

- [ ] **Step 3: Commit**

```bash
git add Assets/Scripts/Data/ArenaConfig.cs Assets/Scripts/Data/ArenaConfig.cs.meta
git commit -m "feat: ArenaConfig ScriptableObject 추가"
```

---

### Task 2: ArenaModel (TDD)

**Files:**
- Create: `Assets/Scripts/Domain/Models/ArenaModel.cs`
- Test: `Assets/Tests/EditMode/ArenaModelTests.cs`

- [ ] **Step 1: 실패 테스트 작성**

`Assets/Tests/EditMode/ArenaModelTests.cs`:

```csharp
using NUnit.Framework;
using DefenseDot.Domain.Models;

namespace DefenseDot.Tests.EditMode
{
    public class ArenaModelTests
    {
        private ArenaModel MakeModel()
        {
            var m = new ArenaModel();
            m.Initialize(29f, 2.2f, 4f, 2f, 80);
            return m;
        }

        [Test]
        public void Initialize_ComputesSpawnRange()
        {
            var m = MakeModel();
            Assert.AreEqual(6.2f, m.SpawnMinRadius, 0.001f);  // 2.2 + 4
            Assert.AreEqual(27f, m.SpawnMaxRadius, 0.001f);   // 29 - 2
            Assert.AreEqual(80, m.MaxAlive);
        }

        [Test]
        public void Shrink_ReducesArenaAndSpawnMax()
        {
            var m = MakeModel();
            m.Shrink(9f);
            Assert.AreEqual(20f, m.ArenaRadius, 0.001f);
            Assert.AreEqual(18f, m.SpawnMaxRadius, 0.001f);   // 20 - 2
        }

        [Test]
        public void Expand_IncreasesArena()
        {
            var m = MakeModel();
            m.Expand(1f);
            Assert.AreEqual(30f, m.ArenaRadius, 0.001f);
        }

        [Test]
        public void Shrink_RaisesOnRadiusChanged()
        {
            var m = MakeModel();
            bool raised = false;
            m.OnRadiusChanged += () => raised = true;
            m.Shrink(1f);
            Assert.IsTrue(raised);
        }

        [Test]
        public void Shrink_ClampsAtMinimum()
        {
            var m = MakeModel();
            m.Shrink(1000f);
            float min = 2.2f + 4f + 2f; // coreRadius + inner + outer = 8.2
            Assert.AreEqual(min, m.ArenaRadius, 0.001f);
        }
    }
}
```

- [ ] **Step 2: 테스트 실행 → 실패 확인**

Test Runner > EditMode > Run All.
Expected: 컴파일 에러 또는 FAIL ("ArenaModel을 찾을 수 없음").

- [ ] **Step 3: ArenaModel 구현**

`Assets/Scripts/Domain/Models/ArenaModel.cs`:

```csharp
// 현재 아레나/스폰 반경 상태를 소유·통지하는 도메인 모델
using DefenseDot.Domain;

namespace DefenseDot.Domain.Models
{
    /// <summary>
    /// 현재 아레나/스폰 반경 상태를 소유하고 변경을 통지하는 도메인 모델입니다.
    /// 반경은 동적으로 변하며(Expand/Shrink), 적은 이를 비율로 참조합니다.
    /// </summary>
    public class ArenaModel : BaseModel
    {
        private float arenaRadius;
        private float coreRadius;
        private float spawnInnerMargin;
        private float spawnOuterMargin;
        private int maxAlive;

        /// <summary> 반경이 변경되면 발생합니다. </summary>
        public event System.Action OnRadiusChanged;

        /// <summary> 현재 아레나 반경입니다. </summary>
        public float ArenaRadius => arenaRadius;

        /// <summary> 코어 반경입니다. </summary>
        public float CoreRadius => coreRadius;

        /// <summary> 스폰 최소 반경(코어 + 안쪽 여백)입니다. </summary>
        public float SpawnMinRadius => coreRadius + spawnInnerMargin;

        /// <summary> 스폰 최대 반경(아레나 - 바깥 여백)입니다. </summary>
        public float SpawnMaxRadius => arenaRadius - spawnOuterMargin;

        /// <summary> 수용 한계입니다. </summary>
        public int MaxAlive => maxAlive;

        /// <summary> 초기 형상 값을 설정합니다. </summary>
        public void Initialize(float arenaRadius, float coreRadius, float spawnInnerMargin, float spawnOuterMargin, int maxAlive)
        {
            this.arenaRadius = arenaRadius;
            this.coreRadius = coreRadius;
            this.spawnInnerMargin = spawnInnerMargin;
            this.spawnOuterMargin = spawnOuterMargin;
            this.maxAlive = maxAlive;
        }

        /// <summary> 아레나를 확장하고 통지합니다. </summary>
        public void Expand(float amount) => SetRadius(arenaRadius + amount);

        /// <summary> 아레나를 축소하고 통지합니다. </summary>
        public void Shrink(float amount) => SetRadius(arenaRadius - amount);

        private void SetRadius(float value)
        {
            // 스폰 범위가 음수가 되지 않도록 하한 제한
            float min = coreRadius + spawnInnerMargin + spawnOuterMargin;
            float clamped = UnityEngine.Mathf.Max(min, value);
            if (SetField(ref arenaRadius, clamped)) OnRadiusChanged?.Invoke();
        }
    }
}
```

- [ ] **Step 4: 테스트 실행 → 통과 확인**

Test Runner > EditMode > Run All.
Expected: `ArenaModelTests` 5개 PASS(녹색).

- [ ] **Step 5: Commit**

```bash
git add Assets/Scripts/Domain/Models/ArenaModel.cs Assets/Scripts/Domain/Models/ArenaModel.cs.meta Assets/Tests/EditMode/ArenaModelTests.cs Assets/Tests/EditMode/ArenaModelTests.cs.meta
git commit -m "feat: ArenaModel 도메인 모델 + 단위 테스트 추가"
```

---

### Task 3: ArenaOrbitLogic 비율 전환 (TDD)

**Files:**
- Create: `Assets/Tests/EditMode/StubMovableActor.cs`
- Create: `Assets/Tests/EditMode/ArenaOrbitLogicTests.cs`
- Modify: `Assets/Scripts/Systems/Enemy/ArenaOrbitLogic.cs` (전체 교체)

- [ ] **Step 1: 테스트 스텁 작성**

`Assets/Tests/EditMode/StubMovableActor.cs`:

```csharp
using UnityEngine;
using DefenseDot.Core;
using DefenseDot.Systems.Enemy;

namespace DefenseDot.Tests.EditMode
{
    /// <summary> 테스트용 IMovableActor 스텁입니다. </summary>
    public sealed class StubMovableActor : IMovableActor
    {
        public Vector3 LastPosition { get; private set; }
        public bool Movable = true;

        public Vector3 Position => LastPosition;
        public ActorState CurrentState => ActorState.Moving;
        public void SetState(ActorState newState) { }
        public void SetPosition(Vector3 newPosition) => LastPosition = newPosition;
        public bool IsMovableState() => Movable;
    }
}
```

- [ ] **Step 2: 실패 테스트 작성**

`Assets/Tests/EditMode/ArenaOrbitLogicTests.cs`:

```csharp
using NUnit.Framework;
using UnityEngine;
using DefenseDot.Domain.Models;
using DefenseDot.Systems.Enemy;

namespace DefenseDot.Tests.EditMode
{
    public class ArenaOrbitLogicTests
    {
        private ArenaModel MakeArena()
        {
            var m = new ArenaModel();
            m.Initialize(29f, 2.2f, 4f, 2f, 80); // min 6.2, max 27
            return m;
        }

        [Test]
        public void Tick_PlacesEnemyAtRatioRadius()
        {
            var actor = new StubMovableActor();
            var arena = MakeArena();
            // startAngle 0, startRatio 0.5, angularSpeed 0 → radius = Lerp(6.2, 27, 0.5) = 16.6
            var logic = new ArenaOrbitLogic(actor, Vector3.zero, arena, 0f, 0.5f, 0f, 0.8f);
            logic.Tick(0f);
            Assert.AreEqual(16.6f, actor.LastPosition.x, 0.01f);
            Assert.AreEqual(0.8f, actor.LastPosition.y, 0.01f);
            Assert.AreEqual(0f, actor.LastPosition.z, 0.01f);
        }

        [Test]
        public void Tick_CompressesWhenArenaShrinks()
        {
            var actor = new StubMovableActor();
            var arena = MakeArena();
            var logic = new ArenaOrbitLogic(actor, Vector3.zero, arena, 0f, 0.5f, 0f, 0.8f);
            logic.Tick(0f);
            float before = actor.LastPosition.x; // 16.6
            arena.Shrink(9f); // max 27→18, min 6.2 → Lerp(6.2,18,0.5)=12.1
            logic.Tick(0f);
            float after = actor.LastPosition.x;
            Assert.Less(after, before);
            Assert.AreEqual(12.1f, after, 0.01f);
        }

        [Test]
        public void Tick_DoesNotMoveWhenNotMovable()
        {
            var actor = new StubMovableActor { Movable = false };
            var arena = MakeArena();
            var logic = new ArenaOrbitLogic(actor, Vector3.zero, arena, 0f, 0.5f, 0f, 0.8f);
            logic.Tick(1f);
            Assert.AreEqual(Vector3.zero, actor.LastPosition); // SetPosition 미호출
        }

        [Test]
        public void HasReachedGoal_AlwaysFalse()
        {
            var logic = new ArenaOrbitLogic(new StubMovableActor(), Vector3.zero, MakeArena(), 0f, 0.5f, 0f, 0.8f);
            Assert.IsFalse(logic.HasReachedGoal);
        }
    }
}
```

- [ ] **Step 3: 테스트 실행 → 실패 확인**

Test Runner > EditMode > Run All.
Expected: 컴파일 에러(`ArenaOrbitLogic` 생성자 시그니처 불일치).

- [ ] **Step 4: ArenaOrbitLogic 전체 교체**

`Assets/Scripts/Systems/Enemy/ArenaOrbitLogic.cs`:

```csharp
// 적 공전 이동 전략(원형 아레나) — 반경을 비율로 관리하여 동적 크기에 자동 대응
using UnityEngine;
using DefenseDot.Domain.Models;

namespace DefenseDot.Systems.Enemy
{
    /// <summary>
    /// 원형 아레나에서 적이 코어 주위를 공전하는 이동 전략입니다. (POCO)
    /// 반경을 비율(0~1)로 관리하므로 아레나가 줄면 적도 비례해 압축됩니다.
    /// 반경은 원본처럼 전체 범위(0~1)를 천천히 진동합니다.
    /// </summary>
    public class ArenaOrbitLogic : IMovementStrategy
    {
        private readonly IMovableActor actor;
        private readonly Vector3 center;
        private readonly ArenaModel arena;
        private readonly float angularSpeed;
        private readonly float height;
        private readonly float ratioMoveSpeed;

        private float angle;
        private float currentRatio;
        private float targetRatio;
        private float ratioChangeTimer;

        /// <summary> 아레나 공전은 코어 도달 개념이 없으므로 항상 false입니다. </summary>
        public bool HasReachedGoal => false;

        public ArenaOrbitLogic(IMovableActor actor, Vector3 center, ArenaModel arena,
            float startAngle, float startRatio, float angularSpeed, float height, float ratioMoveSpeed = 0.15f)
        {
            this.actor = actor;
            this.center = center;
            this.arena = arena;
            this.angle = startAngle;
            this.currentRatio = startRatio;
            this.targetRatio = startRatio;
            this.angularSpeed = angularSpeed;
            this.height = height;
            this.ratioMoveSpeed = ratioMoveSpeed;
            this.ratioChangeTimer = 0f;
        }

        public void Tick(float deltaTime)
        {
            if (!actor.IsMovableState()) return;

            angle += angularSpeed * deltaTime;

            // 반경 진동: currentRatio가 targetRatio로 서서히 이동, 주기적으로 목표를 전체 범위에서 재추첨
            currentRatio = Mathf.MoveTowards(currentRatio, targetRatio, ratioMoveSpeed * deltaTime);
            ratioChangeTimer -= deltaTime;
            if (ratioChangeTimer <= 0f)
            {
                targetRatio = Random.value;
                ratioChangeTimer = Random.Range(1.5f, 4f);
            }

            float radius = Mathf.Lerp(arena.SpawnMinRadius, arena.SpawnMaxRadius, currentRatio);
            Vector3 pos = center + new Vector3(Mathf.Cos(angle) * radius, height, Mathf.Sin(angle) * radius);
            actor.SetPosition(pos);
        }
    }
}
```

> 주의: `deltaTime = 0`이면 `currentRatio`는 변하지 않으므로(MoveTowards 0) 테스트가 결정적이다. `targetRatio` 재추첨은 다음 프레임부터 반영된다.

- [ ] **Step 5: 테스트 실행 → 통과 확인**

Test Runner > EditMode > Run All.
Expected: `ArenaOrbitLogicTests` 4개 + `ArenaModelTests` 5개 모두 PASS.

- [ ] **Step 6: Commit**

```bash
git add Assets/Scripts/Systems/Enemy/ArenaOrbitLogic.cs Assets/Tests/EditMode/StubMovableActor.cs Assets/Tests/EditMode/StubMovableActor.cs.meta Assets/Tests/EditMode/ArenaOrbitLogicTests.cs Assets/Tests/EditMode/ArenaOrbitLogicTests.cs.meta
git commit -m "refactor: ArenaOrbitLogic 반경 비율 기반 전환 + 진동 + 테스트"
```

---

### Task 4: IGameMode 인터페이스 + ArenaMode + GridDefenseMode

> 세 파일을 함께 바꿔야 컴파일된다. 인터페이스 시그니처를 `BakedPath path` → `int spawnIndex`로 바꾸고, 스폰 위치 결정을 모드로 위임한다.

**Files:**
- Modify: `Assets/Scripts/Systems/Mode/IGameMode.cs` (전체 교체)
- Modify: `Assets/Scripts/Systems/Mode/ArenaMode.cs` (전체 교체)
- Modify: `Assets/Scripts/Systems/Mode/GridDefenseMode.cs` (전체 교체)

- [ ] **Step 1: IGameMode 교체**

```csharp
// 게임 모드 추상화 — 스폰 위치/이동 전략 생성·도달 처리·패배 판정을 모드별로 분기
using UnityEngine;
using DefenseDot.Systems.Enemy;

namespace DefenseDot.Systems.Mode
{
    /// <summary>
    /// 게임 모드별 동작(스폰 위치, 이동 전략 생성, 적 도달 처리, 패배 판정)을 캡슐화하는 인터페이스입니다.
    /// </summary>
    public interface IGameMode
    {
        /// <summary> 현재 모드 종류입니다. </summary>
        GameModeType ModeType { get; }

        /// <summary> spawnIndex번째 적의 월드 스폰 위치를 반환합니다. (아레나=극좌표, TD=경로 시작점) </summary>
        Vector3 GetSpawnWorldPosition(int spawnIndex);

        /// <summary> 적에게 주입할 이동 전략을 생성합니다. (아레나=공전, TD=경로추종) </summary>
        IMovementStrategy CreateMovementStrategy(IMovableActor actor, float moveSpeed, int spawnIndex);

        /// <summary> 적이 목표에 도달했을 때 처리합니다. (TD=코어 피해, 아레나=무시) </summary>
        void OnEnemyReachedGoal(float damage);

        /// <summary> 활성 적 수를 근거로 패배 여부를 판정합니다. (아레나=수용 한계, TD=false) </summary>
        bool CheckDefeat(int activeEnemyCount);
    }
}
```

- [ ] **Step 2: ArenaMode 교체**

```csharp
// 원형 아레나 모드 — 극좌표 도넛 밴드 스폰, 공전 전략 생성, 수용 한계 패배
using UnityEngine;
using DefenseDot.Domain.Models;
using DefenseDot.Systems.Enemy;

namespace DefenseDot.Systems.Mode
{
    /// <summary>
    /// 원형 아레나 모드입니다. 적이 도넛 밴드에 랜덤 스폰되어 중앙 코어를 공전하며,
    /// 동시 생존 적이 수용 한계를 넘으면 패배합니다.
    /// </summary>
    public class ArenaMode : IGameMode
    {
        private readonly ArenaModel arena;
        private readonly Vector3 center;
        private readonly float enemyHeight;

        public GameModeType ModeType => GameModeType.Arena;

        public ArenaMode(ArenaModel arena, Vector3 center, float enemyHeight)
        {
            this.arena = arena;
            this.center = center;
            this.enemyHeight = enemyHeight;
        }

        public Vector3 GetSpawnWorldPosition(int spawnIndex)
        {
            float angle = Random.value * Mathf.PI * 2f;
            float radius = Random.Range(arena.SpawnMinRadius, arena.SpawnMaxRadius);
            return center + new Vector3(Mathf.Cos(angle) * radius, enemyHeight, Mathf.Sin(angle) * radius);
        }

        public IMovementStrategy CreateMovementStrategy(IMovableActor actor, float moveSpeed, int spawnIndex)
        {
            float startAngle = Random.value * Mathf.PI * 2f;
            float startRatio = Random.value;
            return new ArenaOrbitLogic(actor, center, arena, startAngle, startRatio, moveSpeed, enemyHeight);
        }

        public void OnEnemyReachedGoal(float damage)
        {
            // 아레나는 코어 도달 패배가 없으므로 처리하지 않음
        }

        public bool CheckDefeat(int activeEnemyCount) => activeEnemyCount >= arena.MaxAlive;
    }
}
```

- [ ] **Step 3: GridDefenseMode 교체**

```csharp
// 그리드 타워디펜스 모드 — 경로 시작점 스폰, 경로추종 전략, 적 도달 시 코어 피해
using UnityEngine;
using DefenseDot.Data;
using DefenseDot.Domain.Models;
using DefenseDot.Systems.Enemy;
using DefenseDot.Systems.Pathfinding;

namespace DefenseDot.Systems.Mode
{
    /// <summary>
    /// 그리드 타워디펜스 모드입니다. 적이 셀 경로를 따라 이동하며, 코어 도달 시 코어 체력이 감소합니다.
    /// </summary>
    public class GridDefenseMode : IGameMode
    {
        private readonly CoreModel coreModel;
        private readonly MapData mapData;
        private readonly Vector3 origin;

        public GameModeType ModeType => GameModeType.GridDefense;

        public GridDefenseMode(CoreModel coreModel, MapData mapData, Vector3 origin)
        {
            this.coreModel = coreModel;
            this.mapData = mapData;
            this.origin = origin;
        }

        private BakedPath PathFor(int spawnIndex)
        {
            if (mapData == null || mapData.bakedPaths.Count == 0) return null;
            return mapData.bakedPaths[spawnIndex % mapData.bakedPaths.Count];
        }

        public Vector3 GetSpawnWorldPosition(int spawnIndex)
        {
            BakedPath path = PathFor(spawnIndex);
            if (path == null) return origin;
            return origin + new Vector3(path.spawnPos.x + 0.5f, 0.8f, path.spawnPos.y + 0.5f);
        }

        public IMovementStrategy CreateMovementStrategy(IMovableActor actor, float moveSpeed, int spawnIndex)
        {
            var follower = new PathFollowerLogic(actor, moveSpeed);
            BakedPath path = PathFor(spawnIndex);
            if (path != null) follower.SetPath(path.path);
            return follower;
        }

        public void OnEnemyReachedGoal(float damage) => coreModel.ApplyDamage(damage);

        public bool CheckDefeat(int activeEnemyCount) => false;
    }
}
```

- [ ] **Step 4: 컴파일 확인 (Task 5에서 호출부 수정 전까지 EnemySpawner/GameManager에 일시적 에러 발생 예상)**

Unity Console에서 `IGameMode` 자체 컴파일은 통과하되, `EnemySpawner`·`GameManager`가 옛 시그니처를 호출해 에러가 남는다. → Task 5·6에서 해소. (이 Task 단독 커밋은 Task 6 이후로 미룬다.)

> 이 Task는 단독으로 컴파일이 안 되므로 **Task 4~6을 하나의 묶음으로 구현 후 한 번에 커밋**한다.

---

### Task 5: EnemySpawner 스폰 위임

**Files:**
- Modify: `Assets/Scripts/Systems/Enemy/EnemySpawner.cs:98-124` (`SpawnEnemy` 메서드)

- [ ] **Step 1: SpawnEnemy 교체**

기존 `SpawnEnemy`(line 98-124)를 아래로 교체:

```csharp
        private void SpawnEnemy(EnemyData data)
        {
            if (mode == null) return;

            MonsterActor actor = GetFromPool(data);
            actor.SetSpawner(this);

            // 스폰 위치 결정을 모드에 위임 (아레나=극좌표, TD=경로 시작점)
            actor.transform.position = mode.GetSpawnWorldPosition(activeEnemyCount);

            actor.Initialize(data);

            // 모드가 이동 전략을 생성·주입 (아레나=공전, TD=경로추종)
            IMovementStrategy strategy = mode.CreateMovementStrategy(actor, data.moveSpeed, activeEnemyCount);
            actor.SetMovement(strategy);

            registry?.Register(actor);
            activeEnemyCount++;
            waveModel?.SetRemaining(activeEnemyCount);
        }
```

> 변경점: `mapData.bakedPaths` 직접 의존 제거(line 100, 103-104, 110-111). 스폰 위치·전략 모두 `mode`에 위임. `mapData` 필드는 `GameManager`가 `GridDefenseMode` 생성 시 읽도록 **public 유지**(이미 public).

- [ ] **Step 2: (Task 6과 함께 컴파일 확인)**

---

### Task 6: GameManager 배선

**Files:**
- Modify: `Assets/Scripts/Systems/Management/GameManager.cs:24-27` (필드), `:105-113` (`CreateMode`), `:82-86` (주입)

- [ ] **Step 1: 필드 교체**

`GameManager.cs` line 24-27의 startup 필드 영역에서 `arenaMaxAlive`를 `arenaConfig`로 교체하고 `arenaView` 참조를 추가:

```csharp
        [Header("Startup")]
        [SerializeField] private GameModeType modeType = GameModeType.GridDefense;
        [SerializeField] private int startGold = 300;
        [SerializeField] private float coreMaxHp = 40f;
        [SerializeField] private ArenaConfig arenaConfig;
```

`[Header("Scene References")]` 블록(line 29-34)에 추가:

```csharp
        [SerializeField] private DefenseDot.Systems.Arena.ArenaView arenaView;
```

클래스 필드 영역(line 55 `private IGameMode mode;` 부근)에 추가:

```csharp
        private ArenaModel arenaModel;
```

- [ ] **Step 2: using 추가**

`GameManager.cs` 상단 using 목록에 추가:

```csharp
using DefenseDot.Data;
```

- [ ] **Step 3: CreateMode 교체**

line 105-113의 `CreateMode()`를 교체:

```csharp
        private IGameMode CreateMode()
        {
            Vector3 origin = spawner != null ? spawner.transform.position : transform.position;

            if (modeType == GameModeType.Arena)
            {
                Vector3 center = coreController != null ? coreController.CorePosition : transform.position;
                arenaModel = new ArenaModel();
                if (arenaConfig != null)
                {
                    arenaModel.Initialize(arenaConfig.arenaRadius, arenaConfig.coreRadius,
                        arenaConfig.spawnInnerMargin, arenaConfig.spawnOuterMargin, arenaConfig.maxAlive);
                }
                float height = arenaConfig != null ? arenaConfig.enemyHeight : 0.8f;
                if (arenaView != null) arenaView.Bind(arenaModel);
                return new ArenaMode(arenaModel, center, height);
            }

            MapData mapData = spawner != null ? spawner.mapData : null;
            return new GridDefenseMode(Core, mapData, origin);
        }
```

- [ ] **Step 4: 전체 컴파일 확인 + EditMode 테스트 재실행**

Unity Console 에러 0개 확인. Test Runner > EditMode > Run All → 기존 9개 테스트 여전히 PASS.

- [ ] **Step 5: Commit (Task 4+5+6 묶음)**

```bash
git add Assets/Scripts/Systems/Mode/IGameMode.cs Assets/Scripts/Systems/Mode/ArenaMode.cs Assets/Scripts/Systems/Mode/GridDefenseMode.cs Assets/Scripts/Systems/Enemy/EnemySpawner.cs Assets/Scripts/Systems/Management/GameManager.cs
git commit -m "refactor: 스폰 위치/전략을 IGameMode로 위임, ArenaModel 배선"
```

---

### Task 7: ArenaView (기즈모 가이드 + 런타임 경계)

**Files:**
- Create: `Assets/Scripts/Systems/Arena/ArenaView.cs`

- [ ] **Step 1: ArenaView 작성**

```csharp
// 아레나 데이터를 Scene에 시각화 — 편집:기즈모 가이드 / 런타임:경계 갱신 구독
using UnityEngine;
using DefenseDot.Data;
using DefenseDot.Domain.Models;

namespace DefenseDot.Systems.Arena
{
    /// <summary>
    /// 아레나 데이터를 Scene에 시각화하는 컴포넌트입니다.
    /// 편집 중에는 OnDrawGizmos로 동심원 가이드를, 런타임에는 ArenaModel 반경 변화를 구독합니다.
    /// </summary>
    public class ArenaView : MonoBehaviour
    {
        [Header("Data")]
        [SerializeField] private ArenaConfig config;

        [Header("Gizmos")]
        [SerializeField] private bool showGizmos = true;

        private ArenaModel model;

        /// <summary> 런타임 모델을 바인딩하고 경계 갱신을 구독합니다. </summary>
        public void Bind(ArenaModel arenaModel)
        {
            model = arenaModel;
            model.OnRadiusChanged += HandleRadiusChanged;
            HandleRadiusChanged();
        }

        private void OnDestroy()
        {
            if (model != null) model.OnRadiusChanged -= HandleRadiusChanged;
        }

        private void HandleRadiusChanged()
        {
            // 런타임 경계 비주얼(LineRenderer 등) 갱신 지점. 장식·연출은 Scene 자식으로 확장.
        }

        private void OnDrawGizmos()
        {
            if (!showGizmos || config == null) return;

            Vector3 c = transform.position;
            float arenaR = model != null ? model.ArenaRadius : config.arenaRadius;
            float minR = model != null ? model.SpawnMinRadius : config.coreRadius + config.spawnInnerMargin;
            float maxR = model != null ? model.SpawnMaxRadius : config.arenaRadius - config.spawnOuterMargin;

            Gizmos.color = new Color(1f, 0.95f, 0.5f, 0.9f);   // 아레나 경계
            DrawCircle(c, arenaR);
            Gizmos.color = new Color(0.5f, 0.9f, 1f, 0.9f);    // 코어
            DrawCircle(c, config.coreRadius);
            Gizmos.color = new Color(1f, 0.3f, 0.3f, 0.6f);    // 스폰 밴드 안/밖
            DrawCircle(c, minR);
            DrawCircle(c, maxR);
        }

        private void DrawCircle(Vector3 center, float radius)
        {
            const int seg = 48;
            Vector3 prev = center + new Vector3(radius, 0f, 0f);
            for (int i = 1; i <= seg; i++)
            {
                float a = (i / (float)seg) * Mathf.PI * 2f;
                Vector3 next = center + new Vector3(Mathf.Cos(a) * radius, 0f, Mathf.Sin(a) * radius);
                Gizmos.DrawLine(prev, next);
                prev = next;
            }
        }
    }
}
```

- [ ] **Step 2: 컴파일 확인**

Unity Console 에러 0개.

- [ ] **Step 3: Commit**

```bash
git add Assets/Scripts/Systems/Arena/ArenaView.cs Assets/Scripts/Systems/Arena/ArenaView.cs.meta
git commit -m "feat: ArenaView 기즈모 가이드 + 런타임 경계 구독 추가"
```

---

### Task 8: Unity 통합 검증 (수동)

> 자동 테스트로 못 잡는 MonoBehaviour/SO/배선/시각을 Unity 에디터에서 검증한다.

**Files:** (코드 변경 없음, 에셋·Scene 설정)

- [ ] **Step 1: ArenaConfig 에셋 생성**

Project 창에서 `Assets/Settings/` 폴더(없으면 생성) 우클릭 → `Create > DefenseDot > ArenaConfig` → 이름 `Arena_Default`. 값 확인(arenaRadius 29 등). 경로: `Assets/Settings/Arena_Default.asset`.

- [ ] **Step 2: Scene에 ArenaView 배치**

빈 GameObject `ArenaRoot` 생성(위치 = 코어 위치) → `ArenaView` 부착 → `config`에 `Arena_Default` 할당. Scene 뷰에서 **노랑/하늘/빨강 동심원 기즈모**가 보이는지 확인.

- [ ] **Step 3: GameManager 설정**

`GameManager`의 `modeType = Arena`, `arenaConfig = Arena_Default`, `arenaView = ArenaRoot` 할당.

- [ ] **Step 4: Play — 아레나 동작 검증**

Play 실행 후 확인:
- 적이 도넛 밴드(코어와 경계 사이)에 랜덤 위치로 스폰되는가
- 적이 코어 주위를 공전하는가
- 적이 너무 많아지면(수용 한계) 패배하는가
- Console 에러 없음

- [ ] **Step 5: 동적 축소 검증 (임시)**

`GameManager`에 임시 디버그 코드로 키 입력 시 `arenaModel.Shrink(5f)` 호출(또는 인스펙터 우클릭 ContextMenu)을 걸고 Play → 적들이 안쪽으로 **압축**되는지 확인. 확인 후 임시 코드 제거.

- [ ] **Step 6: GridDefense 회귀 검증**

`modeType = GridDefense`로 변경, 기존 격자 맵 `MapData` 할당 → Play → 적이 경로를 따라 이동하고 코어 도달 시 피해를 주는지 확인(기존 동작 유지).

- [ ] **Step 7: 최종 커밋 (에셋·Scene)**

```bash
git add Assets/Settings/Arena_Default.asset Assets/Settings/Arena_Default.asset.meta Assets/Scenes/SampleScene.unity
git commit -m "chore: 아레나 모드 Scene/에셋 설정"
```

---

## 완료 기준 (Definition of Done)

- [ ] EditMode 테스트 9개 전부 PASS (`ArenaModelTests` 5 + `ArenaOrbitLogicTests` 4)
- [ ] Arena 모드: 도넛 밴드 스폰 + 공전 + 수용 한계 패배 동작
- [ ] `ArenaModel.Shrink` 시 적 압축 + 경계 비주얼 갱신
- [ ] Scene 편집 시 `ArenaView` 동심원 기즈모 표시
- [ ] GridDefense 모드 회귀 없음
- [ ] Unity Console 에러 0개
