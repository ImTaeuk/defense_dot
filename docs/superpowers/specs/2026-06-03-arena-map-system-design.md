# 아레나 전용 맵 시스템 — 설계 문서

**작성일**: 2026-06-03
**상태**: 설계 승인 완료 → 구현 계획 대기
**대상 모드**: `GameModeType.Arena` (원형 아레나 / 표준 모드)
**원본 레퍼런스**: `Assets/Reference/dot-defense-main` (HTML/JS 프로토타입)

---

## 1. 목적

원형 아레나(표준) 모드 전용 맵 시스템을 구축한다. 게임플레이 메커니즘은 **원본 `dot-defense`에 충실**하게 재현하고, 그 위에 **Unity 확장 두 가지(동적 아레나 크기, Scene 자유 꾸미기)** 를 얹는다.

핵심 원칙: **데이터를 단일 진실 공급원(SSOT)으로 두고, 게임 경계는 코드가 데이터대로 생성하며, 분위기 장식만 디자이너가 자유 배치한다.** → 데이터와 Scene이 구조적으로 어긋날 수 없게 만든다.

---

## 2. 배경

### 2.1 기존 코드의 문제 (현황)

| 위치 | 문제 |
|---|---|
| `ArenaMode.CreateMovementStrategy` | `BakedPath path` 인자를 받지만 **사용하지 않음**. `startRadius=9`, `minRadius=2` 하드코딩(주석에 "임시 기본값") |
| `EnemySpawner.SpawnEnemy` (line 100) | `mapData.bakedPaths`가 비면 **Arena 모드라도 스폰 불가** — 격자 맵을 강제 |
| 스폰 위치 | 격자 `Spawn` 셀에서 좌표를 가져오나, 공전은 `center` 기준 → 첫 `Tick`에서 위치가 덮어써져 **스폰 위치가 무의미** |

→ Arena 모드는 "이동 전략만 분리된 반쪽 통합" 상태. 전용 맵 데이터·스폰·시각화가 없다.

### 2.2 원본 `dot-defense` 분석 결과 (표준 모드)

| 항목 | 원본 값 / 동작 | 코드 근거 |
|---|---|---|
| 아레나 반경 `ARENA_R` | 290 (고정 상수) | `const ARENA_R = 290` |
| 코어 반경 `CORE_R` | 22 (고정 상수) | `const CORE_R = 22` |
| 중심 | 화면 정중앙 고정 | `CX = W/2, CY = H/2` |
| 스폰 반경 범위 | 62 ~ 270 (`CORE_R+40` ~ `ARENA_R-20`) | `ENEMY_MIN_R`, `ENEMY_MAX_R` |
| 스폰 방식 | 각도 랜덤(0~2π) + 반경 랜덤 | `a = random()*2π`, `r = MIN + random()*(MAX-MIN)` |
| 이동 | 공전 (각도 증가 + 반경 진동) | `e.angle += angularSpeed`, `e.radius`가 `targetRadius`로 진동 |
| 패배 | 동시 생존 적 수 `maxAlive`(기본 80) 초과 | `enemies.length >= maxAlive` |
| "링" 개념 | **없음** (하나의 넓은 도넛 범위) | — |
| 장애물 / 타워 배치 | 없음 (코어가 자동 발사) | — |

**원본 충실 = 링이 없는 단일 아레나 + 공전 + 수용 한계 패배.** 동적 크기/꾸미기는 원본에 없는 Unity 확장이다.

---

## 3. 핵심 결정 요약

| 항목 | 결정 | 근거 |
|---|---|---|
| 스폰 영역 | 도넛 밴드(`SpawnMin~SpawnMax`) 균등 랜덤 | 원본 충실 |
| 적 이동 | 코어 공전 | 원본 충실, `ArenaOrbitLogic` 재사용 |
| 반경 진동 | 전체 범위 진동(원본 C) — 적이 도넛 전체를 천천히 떠돎 | 원본 충실 |
| 패배 조건 | 동시 생존 적 수 `maxAlive` 초과 | 원본 충실 |
| 맵 데이터 | `ArenaConfig`(SO) 값 5~6개 — **전용 에디터 윈도우 없음** | 요구① (데이터가 단순) |
| 동적 크기 | `ArenaModel.Expand/Shrink` + `OnRadiusChanged` 이벤트 | 요구② |
| 데이터 ↔ Scene | 데이터 주도 + 기즈모 가이드(`ArenaView`) | 요구③ + 동기화 버그 원천 차단 |
| 축소 규칙 | 함께 압축 (반경 비율 유지) | 자연스럽고 구현 깔끔 |
| 아레나 수 | 단일 | 원본 충실 (동적 크기로 변화 부여) |

---

## 4. 아키텍처 — 3계층 분리

```
[계층 1] 데이터 (정적)
  ArenaConfig (ScriptableObject)
        │  시작값을 읽어 초기화
        ▼
[계층 2] 런타임 모델 (동적)
  ArenaModel (POCO, Domain)  ── OnRadiusChanged 이벤트 ──┐
        │  "현재" 반경 제공                              │
        ├───────────────┬──────────────────┐            │
        ▼               ▼                  ▼            ▼
[계층 3] 비주얼 & 게임플레이 (반응)
  EnemySpawner      ArenaOrbitLogic      (구독) ArenaView
  현재 반경 참조해   공전 + 반경 비율로     편집: OnDrawGizmos 가이드
  도넛 밴드 스폰     위치 계산(압축 자동)    런타임: 경계 비주얼 스케일
```

- **초록(원본 충실)**: 스폰 방식, 공전, 패배 조건
- **확장(Unity)**: 동적 크기(`ArenaModel`), Scene 꾸미기(`ArenaView`)

---

## 5. 컴포넌트 상세

### 5.1 신규 컴포넌트 (3개)

#### `ArenaConfig` — ScriptableObject (Data 계층)
- **책임**: 아레나의 초기값을 담는 정적 데이터.
- **주요 필드(가칭)**:
  - `float arenaRadius` — 초기 아레나 반경
  - `float coreRadius` — 코어 반경
  - `float spawnInnerMargin` — 코어로부터 스폰 안쪽 여백 (원본의 `+40`)
  - `float spawnOuterMargin` — 경계로부터 스폰 바깥 여백 (원본의 `-20`)
  - `int maxAlive` — 수용 한계
  - `float baseAngularSpeed` — 기본 공전 각속도
- `[CreateAssetMenu]` 로 에셋 생성. 인스펙터에서 편집(전용 윈도우 없음).

#### `ArenaModel` — POCO (Domain.Models 계층)
- **책임**: 게임 중 변하는 **현재** 아레나/스폰 반경 상태 보유 및 변경 통지.
- **프로퍼티**: `ArenaRadius`, `CoreRadius`, `SpawnMinRadius`, `SpawnMaxRadius`, `MaxAlive` (get)
  - `SpawnMinRadius = CoreRadius + spawnInnerMargin`
  - `SpawnMaxRadius = ArenaRadius − spawnOuterMargin`
  - `MaxAlive`는 `ArenaConfig`에서 초기화되며 정적(동적 크기와 무관)
- **이벤트**: `event System.Action OnRadiusChanged`
- **메서드**: `Initialize(ArenaConfig)`, `Expand(float amount)`, `Shrink(float amount)`
  - `Expand/Shrink`는 `ArenaRadius`를 조정 → 스폰 반경 재계산 → `OnRadiusChanged` 발행.
- 기존 `CoreModel`·`WaveModel`과 동일한 도메인 계층 패턴(외부 의존 없는 POCO).

#### `ArenaView` — MonoBehaviour (Systems 계층)
- **책임**: 데이터를 화면에 투영 + 디자이너 배치 가이드.
- **편집 중**: `OnDrawGizmos()`가 `ArenaConfig` 반경을 읽어 Scene에 **동심원(경계·스폰 밴드·코어)** 을 그림 → 디자이너가 그 안/주변에 장식 프리팹을 자식으로 배치.
- **런타임**: `Bind(ArenaModel)` 후 `OnRadiusChanged` 구독. 실제 경계 비주얼(예: `LineRenderer`/메시)을 현재 반경에 맞춰 생성·스케일.
- 기존 `MapVisualizer`의 `OnDrawGizmos` 패턴을 "격자 → 동심원"으로 대체한 형태.

### 5.2 기존 코드 변경 (4곳)

#### `ArenaOrbitLogic` (수정)
- 생성자에 `ArenaModel` 참조 추가.
- 적 위치를 **반경 비율 `radiusRatio(0~1)`** 로 관리.
  - 실제 반경 = `Mathf.Lerp(model.SpawnMinRadius, model.SpawnMaxRadius, radiusRatio)`
  - 위치 = `center + (cos(angle), 0, sin(angle)) × 실제반경`
- **반경 진동(원본 방식 · C 확정)**: 각 적이 `currentRatio`·`targetRatio`(둘 다 0~1)를 보유. 매 `Tick`에 `currentRatio`가 `targetRatio`로 서서히 이동하고, 일정 주기마다 `targetRatio`를 [0, 1] 전체에서 재추첨 → 적이 도넛 전체를 천천히 떠돎. (원본의 `radius`/`targetRadius`/`radiusChangeAt` 로직을 비율 공간으로 옮긴 것)
- **압축 효과**: 아레나가 줄어 `SpawnMaxRadius`가 작아지면, `radiusRatio` 유지만으로 실제 반경이 자동 축소 → 적이 안으로 끌려옴.

#### `ArenaMode` (수정)
- 하드코딩 `startRadius=9 / minRadius=2 / startRadius 9f` 제거.
- `ArenaModel`(또는 `ArenaConfig`) 참조를 보유하고 `CreateMovementStrategy`에서 `ArenaOrbitLogic`에 전달.
- `CheckDefeat`은 `ArenaModel.MaxAlive`를 사용.

#### `EnemySpawner` (수정)
- 스폰 위치 결정을 **`IGameMode`에 위임**한다 (모드별 분기 제거).
  - `IGameMode`에 `Vector3 GetSpawnPosition()` 류의 메서드 추가.
  - `ArenaMode`: `center + 극좌표(랜덤 각도, 랜덤 반경 ∈ [SpawnMin, SpawnMax])` 반환.
  - `GridDefenseMode`: 기존 `bakedPath.spawnPos` 기반 반환.
- 이로써 [EnemySpawner.cs:100](Assets/Scripts/Systems/Enemy/EnemySpawner.cs#L100)의 `bakedPaths` 강제 의존이 Arena 모드에서 해소된다.

#### `GameManager` (수정)
- `[SerializeField] ArenaConfig arenaConfig` 추가.
- `CreateMode()`에서 `ArenaModel` 생성 → `Initialize(arenaConfig)` → `ArenaMode`에 주입.
- `ArenaView`에 `ArenaModel` `Bind` 주입.

---

## 6. 데이터 흐름

### 6.1 초기화 (게임 시작)
1. `GameManager.Start` → `ArenaModel.Initialize(arenaConfig)`
2. `ArenaMode`에 `ArenaModel` 주입, `EnemySpawner`/`ArenaView`에 배선
3. `ArenaView.Bind(arenaModel)` → 경계 비주얼 생성

### 6.2 적 스폰
1. `EnemySpawner.SpawnEnemy` → `mode.GetSpawnPosition()` 호출
2. `ArenaMode`가 `center + 극좌표(랜덤 각도, 랜덤 반경)` 반환
3. `mode.CreateMovementStrategy`가 `ArenaOrbitLogic`(`ArenaModel` 참조) 생성·주입

### 6.3 동적 크기 변화
1. (확장 지점) 어떤 사건이 `ArenaModel.Shrink(amount)` 호출
2. `SpawnMaxRadius` 갱신 → `OnRadiusChanged` 발행
3. `ArenaView`가 경계 비주얼 스케일 갱신
4. 모든 적은 `radiusRatio` 유지 → `ArenaOrbitLogic.Tick`에서 실제 반경 자동 축소(압축)

---

## 7. 동적 크기 + 압축 메커니즘 (핵심)

- 각 적은 절대 좌표가 아니라 **`radiusRatio(0~1)`** 를 보유.
- 실제 반경 = `SpawnMin + radiusRatio × (SpawnMax − SpawnMin)`
- `Shrink()` → `SpawnMax` 감소 → **모든 적의 실제 반경이 비율 그대로 자동 축소.**
- 별도 순회/강제 이동 로직 불필요 — 다음 `Tick`에서 자연 반영. "압축이 공짜로 따라온다".

---

## 8. 확장 지점 (이번 구현 범위 밖)

- `Expand/Shrink`를 호출하는 **구체적 트리거 사건**(보스 등장, 특정 웨이브 클리어 등)은 **메서드/이벤트 훅만 열어두고** 실제 연결은 후속 태스크로 둔다. (YAGNI · 확정)
- **코어(중앙 플레이어) 비주얼**(원본의 헥사 링·펄스 글로우 등)은 `ArenaView`의 Scene 장식 오브젝트로 처리한다. 이번 시스템은 게임 로직(스폰·공전·동적 크기)에만 집중. (확정)
- 다중 아레나(스테이지) 교체도 범위 밖. 단일 아레나 + 동적 크기로 충분한 변화를 준다.

---

## 9. 파일 구조 (예정)

```
Assets/Scripts/
  Data/
    ArenaConfig.cs                  (신규)
  Domain/Models/
    ArenaModel.cs                   (신규)
  Systems/Arena/
    ArenaView.cs                    (신규)
  Systems/Mode/
    ArenaMode.cs                    (수정)
    IGameMode.cs                    (수정 — GetSpawnPosition 추가)
    GridDefenseMode.cs              (수정 — GetSpawnPosition 구현)
  Systems/Enemy/
    ArenaOrbitLogic.cs              (수정 — 반경 비율 전환)
    EnemySpawner.cs                 (수정 — 스폰 위치 위임)
  Systems/Management/
    GameManager.cs                  (수정 — Config/Model 배선)
```

---

## 10. 테스트 시나리오

| # | 시나리오 | 기대 결과 |
|---|---|---|
| 1 | Arena 모드 스폰 | 적이 `[SpawnMin, SpawnMax]` 도넛 밴드 + 랜덤 각도에 생성됨 (bakedPaths 불필요) |
| 2 | 공전 | 적이 코어 주위를 회전 |
| 3 | `Shrink()` 호출 | 경계 비주얼 축소 + 모든 적이 비율 유지하며 안으로 압축 |
| 4 | `Expand()` 호출 | 경계 확장 + 적이 바깥으로 비례 이동 |
| 5 | 수용 한계 | 동시 생존 적 수 ≥ `maxAlive` 시 패배 |
| 6 | 편집 기즈모 | Scene에서 `ArenaView`가 동심원 가이드 표시 |
| 7 | GridDefense 회귀 | 기존 격자 모드 스폰·경로 동작 정상 유지 |

---

## 11. 위험 / 영향도

| 항목 | 평가 |
|---|---|
| `EnemySpawner` 스폰 위임 변경 | **중** — GridDefense 모드 회귀 위험. 시나리오 #7 필수 |
| `IGameMode` 인터페이스 확장 | 낮 — 구현체 2개(Arena/GridDefense)만 수정 |
| `ArenaOrbitLogic` 비율 전환 | 낮 — 단일 클래스 내부 로직 |
| 신규 컴포넌트 3개 | 낮 — 기존 도메인/비주얼 패턴 답습 |

---

## 12. 미해결 / 확인 필요

- [x] 반경 진동 → **C(원본 전체 범위) 확정** (§3, §5.2 반영)
- [x] 동적 크기 트리거 → **훅만 열고 범위 밖 확정** (§8)
- [x] 코어 비주얼 → **ArenaView 장식으로 미룸 확정** (§8)

> 모든 미해결 항목이 결정되어 **설계가 확정**되었습니다.

---

## 13. 합성 루트 재설계 (방안 B · 컴포지션)

> 초기 구현(§5) 후 코드 리뷰에서 세 가지 구조 문제가 확인되어 합성 루트를 재설계한다. 게임플레이 로직(스폰·공전·압축)은 그대로 두고 **배선 구조만** 개선한다.

### 13.1 해결 대상 문제
| 문제 | 위반 |
|---|---|
| `ArenaConfig`를 `ArenaView`·`GameManager` 양쪽이 `[SerializeField]`로 소유 | SSOT / DRY |
| `GameManager.CreateMode()`의 `if (modeType == Arena)` 분기 | OCP |
| `GameManager`가 Arena 전용 `ArenaView`를 직접 참조 | SRP / 모드 격리 |

### 13.2 방안: 공통 매니저 + 모드별 부트스트랩 (컴포지션)
- 스테이지 동적 로딩은 **범위 밖**(Scene 정적 배치 유지).
- `GameManager`를 "모드를 모르는" 순수 공통 합성 루트로 축소하고, 모드별 셋업을 `ModeBootstrap`으로 분리한다. (상속 대신 컴포지션)

### 13.3 신규 컴포넌트
- **`ModeContext` (struct)**: 모드 생성 공통 입력 `{ CoreModel Core, Vector3 SpawnOrigin, Vector3 CoreCenter }`
- **`ModeBootstrap` (abstract MonoBehaviour)**: `public abstract IGameMode CreateMode(ModeContext ctx);`
  - 인터페이스(`IModeBootstrap`) 대신 추상 MonoBehaviour — Unity는 인터페이스를 `[SerializeField]`로 못 받지만 추상 MonoBehaviour는 인스펙터 할당이 되어 타입 안전 유지.
- **`ArenaModeBootstrap : ModeBootstrap`**: `[SerializeField] ArenaView`. `CreateMode`에서 `ArenaView.Config`로 `ArenaModel` 생성 → `ArenaView.Bind(model)` → `ArenaMode` 반환.
- **`GridDefenseModeBootstrap : ModeBootstrap`**: `[SerializeField] MapData`. `CreateMode`에서 `GridDefenseMode` 반환.

### 13.4 기존 수정
- **`GameManager`**: `modeType`·`arenaConfig`·`arenaView`·`arenaModel` 제거 → `[SerializeField] ModeBootstrap modeBootstrap`. `CreateMode()`는 `modeBootstrap.CreateMode(new ModeContext(...))` 한 줄. **if 분기 소멸.**
- **`ArenaView`**: `config` 단일 소유 확정 + `public ArenaConfig Config => config;` 노출.
- **`EnemySpawner`**: `mapData` 필드 제거(`GridDefenseModeBootstrap`이 소유).

### 13.5 데이터 흐름
1. `GameManager.Start`: 공통 도메인 모델·서비스·HUD 생성 (불변)
2. `ModeContext` 구성 (`Core`, `spawner.position`, `coreController.CorePosition`)
3. `mode = modeBootstrap.CreateMode(ctx)` — **다형성, 분기 없음**
4. (Arena) Bootstrap이 `ArenaView`의 config로 `ArenaModel` 생성·바인딩 후 `ArenaMode` 반환

### 13.6 Scene 설정
- `ArenaView.config` ← `Arena_Default` *(유일한 config 소유처)*
- `ArenaModeBootstrap.arenaView` ← `ArenaView` 오브젝트
- `GameManager.modeBootstrap` ← `ArenaModeBootstrap` *(모드 전환 = 이 한 칸 교체)*

### 13.7 영향 범위
- 신규 4파일, 수정 3파일(`GameManager`·`ArenaView`·`EnemySpawner`).
- `IGameMode`/`ArenaMode`/`GridDefenseMode` 시그니처 **불변** — Bootstrap이 생성 책임만 가져감.
- EditMode 테스트(`ArenaModel`·`ArenaOrbitLogic`)는 POCO 불변이라 **영향 없음**.
