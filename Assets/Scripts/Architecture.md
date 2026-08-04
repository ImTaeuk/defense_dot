# defense_dot 아키텍처 문서 (P0 전투 루프)

> 원본 웹 게임(dot-defense, 단일 `index.html`)을 Unity로 이식하는 프로젝트의 **P0 인게임 전투 루프**
> 아키텍처를 정리한 문서다. 이벤트 구조는 사용자 결정에 따라 **"Model 소유 순수형"** 으로 구현했고,
> 모든 구조 결정은 `unity-standards` 스킬(Microsoft / Unity / Nystrom)과 표준 디자인 패턴을 근거로 한다.

---

## 1. 레이어 구조와 의존성 방향

`unity-standards/architecture.md §5`의 권장 의존성 방향을 그대로 구현한다.

```
[UI] ──직접호출──▶ [Systems] ──직접호출──▶ [Domain]
 Presenter/View     Binder/Controller/System    Model (POCO, SSOT)
    ▲──────────── 이벤트(Observer, 역방향 통신) ───────────┘

            Composition Root = GameManager
     (모든 Model·하위 시스템 생성·주입, 상향 의존 차단)
```

- **직접 호출은 상위→하위 단방향만** 허용한다.
- **역방향 통신(상태 변경 알림)은 이벤트(Observer)** 로만 한다. Domain은 자신을 구독하는 상위를 모른다.
- **Domain은 Unity 의존을 최소화**(순수 C#)하여 단위 테스트와 직렬화(P2 저장)가 쉽다.
- 정적 전역 `GameEvents`는 **제거**했다. 단일 클래스에 모든 콜백이 모이면 SRP 위반·God Object가 되고
  Observer의 Lapsed Listener/추적 난이도 위험이 커지기 때문이다(patterns.md §2).

---

## 2. 레이어별 클래스

### 2.1 Domain — 상태와 사건의 단일 진실 원천(SSOT)
`Assets/Scripts/Domain/` · 순수 C# POCO · `[System.Serializable]` (P2 저장 대비)

| 클래스 | 상태(직렬화) | 사건(event) |
|---|---|---|
| `EconomyModel` | `Gold` | `OnGoldChanged` |
| `CoreModel` | `CurrentHp`/`MaxHp`/`HealthRatio` | `OnHealthChanged`, `OnCoreDestroyed` |
| `WaveModel` | `Current`/`Total`/`Remaining` | `OnWaveChanged`, `OnRemainingChanged`, `OnWaveCleared` |
| `GameFlowModel` | `Phase`(FSM) | `OnPhaseChanged` |
| `CombatModel` | `TotalKills` | `OnEnemyKilled(reward)` |
| `BaseModel` | — | `SetField` 헬퍼(값 변경 시에만 통지) |
| `GamePhase` | enum(Ready/Playing/GameOver/Victory) | — |

- **Rich Domain Model**: 상태뿐 아니라 불변식·연산을 Model이 캡슐화한다(예: `CoreModel.ApplyDamage`가
  HP를 0 미만으로 떨어뜨리지 않음). Anemic Domain Model(데이터만 있는 모델)을 피한다.
- event는 `[field: System.NonSerialized]`로 직렬화에서 제외 → 상태 필드만 JSON 저장 가능.

### 2.2 Binder / Controller / System — 잇기와 로직(SRP)
| 클래스 | 위치 | 책임(단일) |
|---|---|---|
| `EconomyEventBinder` (POCO) | `Systems/Economy/` | `CombatModel.OnEnemyKilled` 구독 → 골드 가산 |
| `CoreController` (MB) | `Systems/Core/` | 코어 GameObject ↔ `CoreModel` 연결, 월드 위치 제공 |
| `EnemySpawner` (MB) | `Systems/Enemy/` | 웨이브 소환·풀링, 처치/도달 분기, `WaveModel` 갱신 |
| `EnemyRegistry` (POCO) | `Systems/Enemy/` | 활성 적 목록(타겟 탐색·패배 판정용) |
| `TargetFinder` (POCO) | `Systems/Tower/` | 사거리 내 최근접 적 탐색(제곱거리) |
| `TowerPlacementController` (MB) | `Systems/Tower/` | 클릭→슬롯 검증·점유·골드 차감·타워 생성 |
| `GameManager` (MB) | `Systems/Management/` | **합성 루트** + 승패 판정 |

### 2.3 Adapter (Actor) — Unity ↔ Domain
| 클래스 | 인터페이스 | 비고 |
|---|---|---|
| `MonsterActor` | `IMovableActor`, `ITargetable`, `IPoolable` | 전략 기반 이동, 처치/도달 통지, 풀링 |
| `TowerActor` | `ICombatActor`, `IPoolable` | 타겟 탐색·공격 |

### 2.4 UI — 화면 갱신
| 클래스 | 구독 대상 |
|---|---|
| `HUDPresenter` | `EconomyModel`/`CoreModel`/`WaveModel` |
| `WaveHUDPresenter` | `WaveModel` |
| `HUDView` | (Presenter가 제어) |

---

## 3. 모드 추상화 (Strategy 패턴)

두 모드(원형 아레나 / 그리드 타워디펜스)를 좌표계 독립으로 분리한다.

```
IGameMode (Systems/Mode)
 ├─ ArenaMode        : 공전 전략 생성, 수용 한계 초과 시 패배
 └─ GridDefenseMode  : 경로추종 전략 생성, 코어 도달 시 코어 피해

IMovementStrategy (Systems/Enemy)
 ├─ ArenaOrbitLogic   : 중앙 코어 공전 (HasReachedGoal=false)
 └─ PathFollowerLogic : 셀 경로 추종 (HasReachedGoal=경로 끝 도달)
```

- **순환 의존 차단**: `MonsterActor`는 자신이 어떤 모드인지 모른다. `IMovementStrategy`만 들고 이동하고,
  도달은 `HasReachedGoal`로 노출할 뿐. 모드별 처리는 상위(`EnemySpawner`)가 `IGameMode`로 결정한다.
  → `Mode → Enemy(인터페이스)` 단방향만 남는다(DIP).
- `IGameMode.OnEnemyReachedGoal(float damage)`는 적 구체 타입 대신 데미지 값만 받아, Mode가 Enemy
  구체 클래스에 묶이지 않고 `CoreModel`(Domain)만 향한다.

### 두 모드 비교
| | 원형 아레나 | 그리드 타워디펜스 |
|---|---|---|
| 적 이동 | 중앙 공전(`ArenaOrbitLogic`) | 셀 경로(`PathFollowerLogic`) |
| 패배 조건 | 수용 한계 초과(`CheckDefeat`, 폴링) | 코어 HP 0(`OnCoreDestroyed`, 이벤트) |
| 코어 도달 | 없음(no-op) | 코어 피해 |

---

## 4. 핵심 흐름

### 4.1 골드 경제 (처치 → 보상)
```
MonsterActor 사망 → EnemySpawner.HandleEnemyKilled
   → CombatModel.RegisterKill(reward)        [사건 발행]
   → EconomyEventBinder.HandleEnemyKilled      [구독]
   → EconomyModel.AddGold(reward)             [상태 변경]
   → EconomyModel.OnGoldChanged               [통지]
   → HUDPresenter.HandleGoldChanged → HUDView [화면]
```
처치(보상)와 도달(코어 피해)은 `MonsterActor.Resolve(reached)`에서 분기하며, `resolved` 플래그로
이중 정산을 막는다.

### 4.2 합성 루트 생명주기 (GameManager)
- `Awake`: 도메인 Model 5개 생성(외부 의존 없는 최하위 먼저).
- `Start`: 하위 시스템 생성·주입, 모드 결정, 승패 구독, HUD 연결, `spawner.BeginWaves()`.
- `Update`: 아레나 수용 한계 패배 폴링.
- `OnDestroy`: 구독 해제(`Dispose`) — Lapsed Listener 방지.

---

## 5. 풀링 정책
- **적**: 빈번 생성/소멸 → `EnemySpawner`의 prefab별 경량 풀(`Dictionary<prefab, Queue>`)로 회수.
- **타워**: 배치 후 영속 → 풀링하지 않고 `Instantiate`. (Object Pool은 할당 빈도 높을 때만 — patterns.md §4)
- **임시 컬렉션**: `TargetFinder`는 단일 순회로 최근접 선택 → 임시 컬렉션 미생성.

---

## 6. 적용 디자인 패턴 · 표준 출처

| 패턴 | 적용처 | 출처 |
|---|---|---|
| Observer | 도메인 Model의 event 통지 | Nystrom *Observer* (gameprogrammingpatterns.com/observer.html) |
| Strategy | `IMovementStrategy`, `IGameMode` | Gang of Four *Design Patterns* (1994) |
| State (FSM) | `GamePhase`, `ActorState` | Nystrom *State* (.../state.html) |
| Object Pool | 적 회수 | Nystrom *Object Pool* (.../object-pool.html) |
| Component | Actor 인터페이스 분리 | Nystrom *Component* (.../component.html) |
| Composition Root / DI | `GameManager` | Mark Seemann *Dependency Injection in .NET* |
| MVP / SSOT | Presenter ← Model | Fowler GUI Architectures |
| SOLID(SRP/OCP/ISP/DIP) | 전반 | unity-standards/architecture.md §4 |
| 네이밍·접근제한자 | 전반 | Microsoft Framework Design Guidelines |

---

## 7. 확장 지점 (P1 / P2)

- **P1 전투 콘텐츠**: 어빌리티 시스템(`AbilityData` SO + 틱 구조, 89종), 적 타입·특수행동, 보스, 상태이상.
  - 어빌리티는 좌표계 독립으로 설계 → 두 모드 공유.
- **P2 메타 게임**: 도메인 Model이 이미 `[System.Serializable]` → `JsonUtility`로 묶어 저장(웹 원본 `META` 객체 대응).
  - 가챠·캐릭터·유물·영구강화·업적·승천. 재화 모델(`EconomyModel`)을 스타더스트·결정으로 확장.

---

## 8. 검증 필요 (수동)
Unity MCP 미연결로 자동 컴파일 미수행. Unity 에디터에서 다음을 확인할 것:
1. 컴파일 에러 0 (신규 네임스페이스 `DefenseDot.Domain`, `DefenseDot.Systems.Mode` 등 인식).
2. 씬에 `GameManager`·`CoreController`·`EnemySpawner`·`TowerPlacementController`·HUD 배치 및 인스펙터 참조 연결.
3. 플레이: 타워 배치(골드 차감)→적 처치(골드↑)→코어 도달(HP↓)→게임오버/승리.
