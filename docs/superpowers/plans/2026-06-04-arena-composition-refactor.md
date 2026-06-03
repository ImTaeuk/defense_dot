# 아레나 합성 루트 리팩토링 Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** `GameManager`의 모드별 책임(모드 생성·스폰 셋업·뷰 바인딩·config)을 `ModeBootstrap`(모드별 부트스트랩)으로 분리해, ModeType 분기·config 중복·ArenaView 누수를 제거한다.

**Architecture:** 공통 합성 루트(`GameManager`)는 `ModeBootstrap` 하나만 알고, 모드별 구체 부트스트랩이 `IGameMode`를 생성한다(컴포지션). `config`는 `ArenaView`가 단일 소유한다. 게임 로직(`IGameMode`/`ArenaMode`/`ArenaOrbitLogic`) 시그니처는 불변이라 기존 EditMode 테스트 9개가 회귀 안전망이 된다.

**Tech Stack:** Unity 6000.2.10f1, C#, Unity Test Framework(EditMode). MonoBehaviour 배선은 Unity Play로 검증.

---

## 설계 근거

- 설계 문서: [docs/superpowers/specs/2026-06-03-arena-map-system-design.md](../specs/2026-06-03-arena-map-system-design.md) **§13. 합성 루트 재설계**
- 전제: 1차 구현(§5)이 완료된 상태 — `GameManager`에 `modeType`·`arenaConfig`·`arenaView`·`arenaModel`이 있고, `EnemySpawner`에 `mapData` 필드가 있다.
- 범위 밖: 스테이지 동적 로딩(Scene 정적 배치 유지).

---

## File Structure

| 파일 | 책임 | 상태 |
|---|---|---|
| `Assets/Scripts/Systems/Mode/ModeContext.cs` | 모드 생성 공통 입력(struct) | 생성 |
| `Assets/Scripts/Systems/Mode/ModeBootstrap.cs` | 모드별 부트스트랩 베이스(abstract MB) | 생성 |
| `Assets/Scripts/Systems/Mode/ArenaModeBootstrap.cs` | ArenaView→Model→ArenaMode 생성 | 생성 |
| `Assets/Scripts/Systems/Mode/GridDefenseModeBootstrap.cs` | MapData→GridDefenseMode 생성 | 생성 |
| `Assets/Scripts/Systems/Arena/ArenaView.cs` | `Config` 프로퍼티 노출 | 수정 |
| `Assets/Scripts/Systems/Management/GameManager.cs` | 분기·중복·누수 제거, modeBootstrap 위임 | 수정 |
| `Assets/Scripts/Systems/Enemy/EnemySpawner.cs` | `mapData` 필드 제거 | 수정 |

**의존 순서:** Task 1(Context+Bootstrap base) → Task 2(ArenaView.Config) → Task 3(모드별 Bootstrap) → Task 4(GameManager) → Task 5(EnemySpawner) → Task 6(검증·Scene 재배선).

> Task 1~5는 함께 컴파일돼야 한다(GameManager가 ModeBootstrap에 의존). Task 6에서 한 번에 컴파일·테스트·Play 검증.

**커밋 주의:** 사용자 승인 시에만 `git commit` 실행.

---

### Task 1: ModeContext + ModeBootstrap

**Files:**
- Create: `Assets/Scripts/Systems/Mode/ModeContext.cs`
- Create: `Assets/Scripts/Systems/Mode/ModeBootstrap.cs`

- [ ] **Step 1: ModeContext 작성**

```csharp
// 모드 생성에 필요한 공통 입력 묶음
using UnityEngine;
using DefenseDot.Domain.Models;

namespace DefenseDot.Systems.Mode
{
    /// <summary>
    /// 모드(IGameMode) 생성에 필요한 공통 입력을 담는 구조체입니다.
    /// </summary>
    public readonly struct ModeContext
    {
        /// <summary> 코어 체력 모델 (TD 모드의 코어 피해용) </summary>
        public readonly CoreModel Core;
        /// <summary> 스폰 기준 원점 (스포너 위치) </summary>
        public readonly Vector3 SpawnOrigin;
        /// <summary> 아레나 중심 (코어 위치) </summary>
        public readonly Vector3 CoreCenter;

        public ModeContext(CoreModel core, Vector3 spawnOrigin, Vector3 coreCenter)
        {
            Core = core;
            SpawnOrigin = spawnOrigin;
            CoreCenter = coreCenter;
        }
    }
}
```

- [ ] **Step 2: ModeBootstrap 작성**

```csharp
// 모드별 합성 루트 베이스 — 모드(IGameMode)를 생성한다
using UnityEngine;

namespace DefenseDot.Systems.Mode
{
    /// <summary>
    /// 모드별 부트스트랩의 베이스입니다. 모드 고유 자원(뷰·맵 데이터)을 보유하고
    /// 해당 모드의 IGameMode를 생성합니다. (인터페이스 대신 추상 MonoBehaviour — 인스펙터 직렬화)
    /// </summary>
    public abstract class ModeBootstrap : MonoBehaviour
    {
        /// <summary> 공통 입력을 받아 이 부트스트랩의 모드를 생성합니다. </summary>
        public abstract IGameMode CreateMode(ModeContext ctx);
    }
}
```

- [ ] **Step 3: Unity 재컴파일 확인** — Console 에러 0개. (이 둘은 아직 아무도 참조 안 하므로 독립 컴파일됨)

---

### Task 2: ArenaView.Config 노출

**Files:**
- Modify: `Assets/Scripts/Systems/Arena/ArenaView.cs`

- [ ] **Step 1: Config 프로퍼티 추가**

`ArenaView.cs`에서 `private ArenaModel model;` 선언 위(필드 영역)에 프로퍼티 추가:

```csharp
        /// <summary> 이 뷰가 단일 소유하는 아레나 설정입니다. (모드 부트스트랩이 참조) </summary>
        public ArenaConfig Config => config;
```

- [ ] **Step 2: 컴파일 확인** — Console 에러 0개.

---

### Task 3: ArenaModeBootstrap + GridDefenseModeBootstrap

**Files:**
- Create: `Assets/Scripts/Systems/Mode/ArenaModeBootstrap.cs`
- Create: `Assets/Scripts/Systems/Mode/GridDefenseModeBootstrap.cs`

- [ ] **Step 1: ArenaModeBootstrap 작성**

```csharp
// 아레나 모드 부트스트랩 — ArenaView config로 모델 생성·바인딩 후 ArenaMode 생성
using UnityEngine;
using DefenseDot.Data;
using DefenseDot.Domain.Models;
using DefenseDot.Systems.Arena;

namespace DefenseDot.Systems.Mode
{
    /// <summary>
    /// 아레나 모드 합성 루트입니다. ArenaView가 소유한 config로 ArenaModel을 만들어
    /// 바인딩한 뒤 ArenaMode를 생성합니다.
    /// </summary>
    public class ArenaModeBootstrap : ModeBootstrap
    {
        [SerializeField] private ArenaView arenaView;

        public override IGameMode CreateMode(ModeContext ctx)
        {
            var arenaModel = new ArenaModel();
            ArenaConfig config = arenaView != null ? arenaView.Config : null;
            if (config != null)
            {
                arenaModel.Initialize(config.arenaRadius, config.coreRadius,
                    config.spawnInnerMargin, config.spawnOuterMargin, config.maxAlive);
            }
            float height = config != null ? config.enemyHeight : 0.8f;
            if (arenaView != null) arenaView.Bind(arenaModel);
            return new ArenaMode(arenaModel, ctx.CoreCenter, height);
        }
    }
}
```

- [ ] **Step 2: GridDefenseModeBootstrap 작성**

```csharp
// 그리드 디펜스 모드 부트스트랩 — MapData로 GridDefenseMode 생성
using UnityEngine;
using DefenseDot.Data;

namespace DefenseDot.Systems.Mode
{
    /// <summary>
    /// 그리드 타워디펜스 모드 합성 루트입니다. 맵 데이터를 보유하고 GridDefenseMode를 생성합니다.
    /// </summary>
    public class GridDefenseModeBootstrap : ModeBootstrap
    {
        [SerializeField] private MapData mapData;

        public override IGameMode CreateMode(ModeContext ctx)
        {
            return new GridDefenseMode(ctx.Core, mapData, ctx.SpawnOrigin);
        }
    }
}
```

- [ ] **Step 3: 컴파일 확인** — `ArenaMode`/`GridDefenseMode` 생성자 시그니처와 일치하는지(불변). 에러 0개.

---

### Task 4: GameManager 리팩토링

**Files:**
- Modify: `Assets/Scripts/Systems/Management/GameManager.cs`

- [ ] **Step 1: using 정리**

상단 using에서 아래 두 줄을 **삭제**(더 이상 `ArenaConfig`/`MapData`/`ArenaView`를 직접 쓰지 않음):

```csharp
using DefenseDot.Data;
using DefenseDot.Systems.Arena;
```

- [ ] **Step 2: 필드 교체**

`[SerializeField] private ArenaConfig arenaConfig;`를 삭제하고, startup 필드 영역의 `modeType` 줄도 삭제. 대신 `ModeBootstrap` 참조 추가:

```csharp
        [Header("Startup")]
        [SerializeField] private ModeBootstrap modeBootstrap;
        [SerializeField] private int startGold = 300;
        [SerializeField] private float coreMaxHp = 40f;
```

Scene References 영역에서 `[SerializeField] private ArenaView arenaView;` **삭제**.

클래스 필드 영역에서 `private ArenaModel arenaModel;` **삭제**.

- [ ] **Step 3: CreateMode 교체**

```csharp
        private IGameMode CreateMode()
        {
            if (modeBootstrap == null)
            {
                Debug.LogError("[GameManager] ModeBootstrap이 할당되지 않았습니다.");
                return null;
            }
            Vector3 origin = spawner != null ? spawner.transform.position : transform.position;
            Vector3 center = coreController != null ? coreController.CorePosition : transform.position;
            var ctx = new ModeContext(Core, origin, center);
            return modeBootstrap.CreateMode(ctx);
        }
```

> `modeType`·`GameModeType` 분기가 완전히 사라진다. `GameModeType` enum 자체는 `IGameMode.ModeType` 식별용으로 유지된다(삭제하지 않음).

- [ ] **Step 4: (Task 5와 함께 컴파일 확인)**

---

### Task 5: EnemySpawner mapData 제거

**Files:**
- Modify: `Assets/Scripts/Systems/Enemy/EnemySpawner.cs`

- [ ] **Step 1: mapData 필드 삭제**

`EnemySpawner.cs`의 `[Header("Data References")]` 영역에서 아래 줄을 **삭제**(맵 데이터는 이제 `GridDefenseModeBootstrap`이 소유):

```csharp
        public MapData mapData;
```

> `waveSequence`는 유지. `using DefenseDot.Data;`도 유지(`EnemyData`를 계속 사용).

- [ ] **Step 2: 전체 컴파일 확인** — Console 에러 0개. `GameManager`가 `spawner.mapData`를 더는 참조하지 않는지 확인(Task 4에서 이미 제거됨).

---

### Task 6: 검증 — lint · 테스트 · Scene 재배선 · Play

- [ ] **Step 1: lint 컨벤션 검증**

`lint` 스킬 실행 → 변경 `.cs` 파일 컨벤션 통과 확인(인라인 주석 20자 등).

- [ ] **Step 2: EditMode 테스트 회귀 확인**

Unity `Test Runner > EditMode > Run All` → **기존 9개 여전히 PASS**(`ArenaModelTests` 5 + `ArenaOrbitLogicTests` 4). 배선만 바꿨으므로 POCO 로직은 불변이어야 한다.

- [ ] **Step 3: Scene 재배선**

- `ArenaRoot`(ArenaView)에 `config` = `Arena_Default` 확인(유일한 config 소유처)
- 빈 GameObject 또는 `ArenaRoot`에 **ArenaModeBootstrap** 컴포넌트 추가 → `arenaView` = `ArenaRoot` 할당
- `GameManager`: `Mode Bootstrap` = `ArenaModeBootstrap` 할당 (기존 `Mode Type`/`Arena Config`/`Arena View` 칸은 사라짐)

- [ ] **Step 4: Play — Arena 동작 재검증**

- ✅ 적이 도넛 밴드에 스폰 + 코어 공전 (1차 구현과 동일하게 동작)
- ✅ Console 에러 0개

- [ ] **Step 5: GridDefense 회귀 재검증**

- 빈 GameObject에 **GridDefenseModeBootstrap** 추가 → `mapData` 할당
- `GameManager.modeBootstrap` = `GridDefenseModeBootstrap`로 교체 → Play
- ✅ 적이 경로 이동 + 코어 도달 피해 (기존 동작 유지)

- [ ] **Step 6: Commit**

```bash
git add Assets/Scripts/Systems/Mode/ModeContext.cs Assets/Scripts/Systems/Mode/ModeBootstrap.cs Assets/Scripts/Systems/Mode/ArenaModeBootstrap.cs Assets/Scripts/Systems/Mode/GridDefenseModeBootstrap.cs Assets/Scripts/Systems/Arena/ArenaView.cs Assets/Scripts/Systems/Management/GameManager.cs Assets/Scripts/Systems/Enemy/EnemySpawner.cs
git add Assets/Scripts/Systems/Mode/*.meta
git commit -m "refactor: 합성 루트를 모드별 ModeBootstrap으로 분리 (컴포지션)"
```

---

## 완료 기준

- [ ] `GameManager`에 `if (modeType == …)` 분기·`ArenaConfig`·`ArenaView` 참조가 없다
- [ ] `config`는 `ArenaView`만 소유한다
- [ ] EditMode 테스트 9개 PASS (회귀 없음)
- [ ] Arena·GridDefense 모드 모두 Play 정상
- [ ] Console 에러 0개
