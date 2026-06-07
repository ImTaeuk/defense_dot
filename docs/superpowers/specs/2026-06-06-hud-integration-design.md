# 인게임 HUD 통합 설계 (TASK-001 A)

**작성일**: 2026-06-06
**상태**: 설계 협의 완료 (사용자 리뷰 대기)
**브랜치**: `feature/arena-map-system`
**선행 문서**: [TASK-001 남은 작업](../../tasks/active/TASK-001-arena-map-remaining.md) · [아레나 맵 시스템 설계](2026-06-03-arena-map-system-design.md)

---

## 1. 목표

두 모드(Grid 타워 디펜스 / Arena 코어 디펜스)가 공유하는 **단일 통합 인게임 HUD** 를 구축한다. TASK-001 §3-A 의 4개 항목을 해소한다.

- **A-1** uGUI HUD 통합 + 하위 View 분리
- **A-2** UI Toolkit 제거 (`WaveHUDPresenter`/`UIDocument`)
- **A-3** WaveHUD MVP 비대칭 해소
- **A-4** `HUDView` NRE 근본 해결

설계는 멀티에이전트 패스(4 차원 설계 + Unity API·복잡도·회귀 3중 검증)로 도출했고, 검증이 잡은 Unity API 오류 1건·과설계 4건을 반영해 단순화했다.

---

## 2. 확정 결정

| # | 항목 | 결정 | 근거 |
|---|---|---|---|
| D1 | 배선 아키텍처 | **게임 씬 내 `UIRoot` UI 합성 루트** (별도 UI 씬 미채택) | 단일 게임 씬 규모에서 별도 씬 additive 는 과설계. **UI 계층 합성 루트 1개**가 모든 프레젠터를 조립(패널마다 Bootstrap 만들지 않음). 향후 분리 필요 시 `UIRoot` 만 이동 |
| D2 | 모드 공통성 | HUD 구조·컴포넌트는 두 모드 공통, **바인딩(capacity)만 차등** | 사용자 확정 |
| D3 | RoundView 표기 | `라운드 {current}/{total}` | 사용자 선택 (원작·프리팹 용어 '라운드') |
| D4 | EnemyCountView 의미 | **(원본 충실)** `적 {alive}/{capacity}`, 게이지 위험 방향 채움 + 색상 임계 | 원작 실측(아래 §3) |
| D5 | capacity 출처 | Grid=설정값(기본 80) · Arena=`ArenaModel.MaxAlive` | 원작은 단일 maxAlive(80) 를 양 모드 공통 표시 |
| D6 | 작업 범위 | 이번엔 **Grid HUD 완성**, Panel_Arena 프리팹+Arena 결선은 designer 후속 | 사용자 선택. 코드는 모드 무관(capacity 주입점)으로 준비 |
| D7 | 하위 View 인터페이스 | `IValueView`/`IGaugeView` **도입 안 함** | Unity 직렬화 제약상 `[SerializeField]` 가 인터페이스 타입 불가 → 다형성 이점 없음(검증 지적) |
| D8 | 도메인·테스트 | 도메인 모델 이벤트 시그니처 **불변**, 9개 EditMode 테스트 보존 | 회귀 0 |

---

## 3. 원작 HUD 실측 (D4·D5 근거)

레퍼런스 `Assets/Reference/dot-defense-main/.../index.html` `renderStats()` 실측:

| 라인 | 표시 | 의미 |
|---|---|---|
| 16160 | `s-enemies = ${state.enemies.length} / ${getMaxAlive()}` | **적 = 살아있는 적 수 / 수용 한계** — 조건 없이 두 모드 공통 |
| 16180-16184 | `s-enemybar` = `alive/maxAlive` | 위험 시 **채워짐**, 색상 임계: `>0.75` danger(빨강) / `>0.5` warn(주황) |
| 16152 | `s-round = state.round` | 라운드 번호 (별도 타이머 `s-time` 존재) |
| 16167-16176 | `s-towerhp` | **defense(TD) 모드 전용** 본진 HP 바 |

**결론**: 원작의 "잔여 적"은 `살아있는 적 / 수용 한계(maxAlive)` 이며 **두 모드 동일**. Grid 를 "웨이브 잔여/총량" 으로 나누는 것은 원작에 없는 설계였으므로 폐기하고, 두 모드를 `alive/capacity` 로 통일한다(더 단순). `EnemySpawner.CurrentWaveTotalCount` 추가 불필요 → 제거.

---

## 4. 아키텍처

```
GameManager (합성 루트, 게임 씬)
  │  도메인 POCO 모델 소유: Economy / Core / Wave / Combat / (Arena)
  │  Start 후반: hudBootstrap.Inject(economy, core, wave, modeBootstrap.EnemyDisplayCapacity)   ← 동기 호출(같은 씬)
  ▼
UIRoot (UI 합성 루트, 게임 씬 GameObject)   ← GameManager 는 HUD 를 직접 모름
  │  직렬화 참조: HUDView (+ 향후 다른 UI View)
  │  List<IPresenter> 로 모든 프레젠터 생성·Initialize/Dispose 일괄 관리
  ▼
HUDPresenter (POCO, BasePresenter<HUDView, HUDModel>)
  │  도메인 이벤트 구독(Observer) → HUDModel 캐시 → HUDView 위임 메서드 호출
  ▼
HUDView (Composite 루트, IView)
  │  하위 View 4종 [SerializeField] 보유, Update* 위임 분배
  ▼
GoldView · HealthView · RoundView · EnemyCountView (각자 TMP/Image 만 보유)
```

- **이벤트는 도메인 POCO 모델에 그대로** (`EconomyModel.OnGoldChanged` 등, 시그니처 불변).
- **싱글톤 금지, SO 이벤트 채널 금지** — 모든 배선은 합성 루트 단방향 주입.
- 별도 씬이 아니므로 `LoadSceneAsync`/`UnloadSceneAsync`/`GetRootGameObjects`/UniTask await **불필요** (검증의 `ToUniTask` 오류도 자연 소거).

---

## 5. 컴포넌트

| 구분 | 컴포넌트 | 파일 | 책임 |
|---|---|---|---|
| 신규 | `GoldView` | `Assets/Scripts/UI/Views/GoldView.cs` | `Grid_Gold` 부착. 자식 TMP 보유, `SetGold(int)`. 자기검증+null-guard |
| 신규 | `HealthView` | `Assets/Scripts/UI/Views/HealthView.cs` | `Grid_Health` 부착. TMP+`HealthBar_Fill` Image. `SetHealth(cur,max,ratio)` |
| 신규 | `RoundView` | `Assets/Scripts/UI/Views/RoundView.cs` | `Grid_Round` 부착. TMP. `SetRound(cur,total)` → `라운드 {cur}/{total}` |
| 신규 | `EnemyCountView` | `Assets/Scripts/UI/Views/EnemyCountView.cs` | `Grid_EnemyCount` 부착. TMP+`EnemyBar_Fill`. `SetEnemyCount(alive,capacity)` → `적 {alive}/{capacity}` + 위험 채움 게이지 + 색상 임계 |
| 신규 | `UIRoot` | `Assets/Scripts/UI/InGame/UIRoot.cs` | **UI 합성 루트**. `Inject(...)` 로 `List<IPresenter>` 조립·일괄 Initialize/Dispose. 새 UI 는 여기 한 줄 추가 |
| 신규 | `IPresenter` | `Assets/Scripts/UI/Presenters/IPresenter.cs` | 프레젠터 공통 수명 계약(Initialize/Dispose). `HUDPresenter` 가 구현 |
| 수정 | `HUDView` | `Assets/Scripts/UI/Views/HUDView.cs` | Composite 루트로 재작성. 기존 3 TMP 직접 필드 폐기(A-4 원인 제거), 하위 View 4 보유, 위임 메서드 |
| 수정 | `HUDModel` | `Assets/Scripts/UI/Models/HUDModel.cs` | 최소 보강(`RoundTotal`/`EnemyAlive`/`EnemyCapacity`). 통지 없는 표시 캐시 |
| 수정 | `HUDPresenter` | `Assets/Scripts/UI/Presenters/HUDPresenter.cs` | wave/remaining 책임 흡수(A-3), 생성자에 `int enemyCapacity` 추가. `Dispose` 에서 `OnRemainingChanged` 포함 전 구독 해제 |
| 수정 | `GameManager` | `Assets/Scripts/Systems/Management/GameManager.cs` | `hudView`/`waveHud` 필드·인라인 생성 제거, `[SerializeField] UIRoot uiRoot` 추가, Start 에서 `Inject(.., modeBootstrap.EnemyDisplayCapacity)` 호출 |
| 수정 | `ModeBootstrap` | `.../Mode/ModeBootstrap.cs` | 추상 프로퍼티 `int EnemyDisplayCapacity { get; }` 추가 — 모드별 capacity 공급점 |
| 수정 | `GridDefenseModeBootstrap` | `.../Mode/GridDefenseModeBootstrap.cs` | `[SerializeField] int enemyDisplayCapacity = 80` + `EnemyDisplayCapacity` override |
| 수정 | `ArenaModeBootstrap` | `.../Mode/ArenaModeBootstrap.cs` | `EnemyDisplayCapacity => arenaView.Config.maxAlive` override (Arena capacity 공급, 코드 한정) |
| 삭제 | `WaveHUDPresenter` | `.../UI/InGame/WaveHUDPresenter.cs` (+.meta) | A-2: UI Toolkit 제거. 기능은 Round/EnemyCount View 가 흡수 |
| 수정 | `DefenseDot.asmdef` · `DefenseDot.Editor.asmdef` | — | **`UnityEngine.UI` 참조 추가**(신규 `Image` 사용 → 없으면 컴파일 실패). Editor 엔 TMP·UI 추가(결선 도구용) |
| 신규 | `HudSetupTool` | `Assets/Scripts/Editor/HudSetupTool.cs` | 프리팹/씬 결선 자동화 에디터 메뉴(편의 도구) |
| 불변 | 도메인 모델 5종, `BasePresenter`/`IView`, 9 EditMode 테스트 | — | 시그니처·참조 보존 |

> **드롭됨**: `HudBindingMode` enum(모드 차이가 capacity int 하나뿐 → 불필요), `EnemySpawner.CurrentWaveTotalCount`(원작에 없음), `IValueView`/`IGaugeView`(직렬화 제약), `IEnemyCountSource` Strategy(모드 2개엔 과설계), 별도 UI 씬·UniTask 로드.

---

## 6. 데이터 흐름

1. **부팅**: `GameManager.Awake` 도메인 모델 new + 초기 통지. `Start`: 서비스 배선, `mode = CreateMode(ctx)`, 승패 구독, `BeginWaves`.
2. **UI 주입(동기)**: `Start` 후반 `hudBootstrap.Inject(economy, core, wave, modeBootstrap.EnemyDisplayCapacity)`. (같은 씬·동기, 비동기 로드 없음. 모드 분기 없이 capacity int 하나만 차등)
3. **Presenter 초기화**: `UIRoot` 이 `new HUDModel()` + `new HUDPresenter(hudView, model, economy, core, wave, enemyCapacity)` 를 `List<IPresenter>` 에 담아 `Initialize()`: 구독 + **현재값 즉시 pull**(누락 통지 복구).
4. **런타임 분배** (Presenter → HUDView 위임 → 하위 View):
   - 골드: `economy.OnGoldChanged(int)` → `GoldView.SetGold`
   - 체력: `core.OnHealthChanged(ratio)` + `core.CurrentHp/MaxHp` 조립 → `HealthView.SetHealth` (텍스트 + `HealthBar_Fill.fillAmount`)
   - 라운드: `wave.OnWaveChanged(cur,total)` → `RoundView.SetRound`
   - 적 수: `wave.OnRemainingChanged(alive)` → `EnemyCountView.SetEnemyCount(alive, enemyCapacity)` (텍스트 + 위험 채움 게이지 + 색상). **두 모드 공통 경로**, capacity 만 모드별.
5. **종료**: `UIRoot.OnDestroy` → 보유 프레젠터 일괄 `Dispose`(전 구독 해제, Lapsed Listener 방지).
6. **부수**: `GameManager.Update:120` 의 `mode.CheckDefeat(spawner.ActiveEnemyCount)` 패배 폴링은 같은 SSOT 를 읽는 독립 소비자라 충돌 없음(유지).

---

## 7. 오류 처리 (A-4 NRE 근본 해결)

검증으로 정정된 실제 원인: `AreanaScene` 에 직접 배치된 `HUDView` 인스턴스의 TMP 3필드가 모두 미할당(fileID:0) + 갱신부 null-guard 부재. `GameManager.hudView` 도 미할당이라 현재는 **잠복**.

**3층 방어** (모두 적용):
1. **직렬화 고정**: 하위 View 4종을 Panel 프리팹 노드에 부착, TMP/Image 를 프리팹에 직렬화 → '인스펙터 비우기' 휘발성 제거.
2. **자기검증**: 각 View `Awake`(또는 `OnValidate`)에서 미할당 시 `Debug.LogError` 조기 차단(정책 통일).
3. **런타임 보조**: 각 `SetXxx` 및 HUDView 위임 메서드에 null-guard.

**구독 누수 방지**: `HUDPresenter.Dispose` 에서 economy/core/wave 전 이벤트 해제(기존 누락된 `OnRemainingChanged` 해제 포함).

---

## 8. 프리팹·에디터 작업 (코드 외, 한 세트로 진행)

- `Panel_Grid.prefab` 노드에 4 View 부착 + TMP/Image 슬롯 직렬화: `Grid_Gold→GoldView`, `Grid_Health→HealthView`(+`HealthBar_Fill`), `Grid_Round→RoundView`, `Grid_EnemyCount→EnemyCountView`(+`EnemyBar_Fill`), 루트 `Panel_Grid→HUDView`.
- **`EnemyBar_Fill` 의 Image `m_Type` 을 `1`(Sliced)→`3`(Filled)** 로 변경 (현재 `fillAmount` 무시됨). `HealthBar_Fill` 은 이미 Filled(무변경).
- 게임 씬에 `UIRoot` GameObject + Canvas + `Panel_Grid` 인스턴스 배치, `UIRoot.hudView`/`GameManager.uiRoot` 결선.
- 두 씬의 기존 `HUDView`/`WaveHUDPresenter`/`UIDocument` GameObject 제거(missing script·이중 HUD 방지), `PanelSettings` 참조 정리.

---

## 9. 테스트

- **기존 9개 EditMode**(`ArenaModelTests` 5 + `ArenaOrbitLogicTests` 4): 도메인 시그니처 보존 → 회귀 0. ⚠ 단 `DefenseDot.Tests.EditMode.asmdef` 가 `DefenseDot` 어셈블리 전체를 참조하므로, **컴파일 무결 상태로 커밋**해야 테스트 어셈블리 빌드 성립.
- **수동/PlayMode**(Grid): 골드→Gold, 코어 피해→Health(텍스트+게이지), 웨이브 전환→Round, 적 증감→EnemyCount(텍스트+위험 게이지+색상), 미할당 시 LogError, 종료 시 구독 해제.

---

## 10. 범위 경계

**포함(이번 작업)**: 위 §5 컴포넌트 전체 + `Panel_Grid` 프리팹 결선 + `EnemyBar_Fill` 수정. **Grid HUD 완전 동작.**

**제외**:
- 도메인 모델 이벤트 시그니처 변경 일절 금지. `WaveModel.OnRemainingChanged` 2-인자화 기각, `EnemySpawner.OnActiveCountChanged` 이벤트 추가 기각, 패배 판정 폴링→이벤트 전환 기각.
- **Panel_Arena 프리팹 신설 + 게임 씬 배치·결선 = designer 후속 태스크.** (Arena capacity 공급 코드 `ArenaModeBootstrap.EnemyDisplayCapacity` 는 이번에 포함 → 프리팹만 생기면 Arena HUD 즉시 동작)
- 원작의 라운드 타이머·본진 HP 분리 표기 등 추가 위젯은 범위 밖.
- `MapEditor` 계열 UI Toolkit 에디터 도구 불변.

---

## 11. 미결(사용자 리뷰 시 확인)

- EnemyCountView 게이지 색상 임계(원작 `>0.75` danger / `>0.5` warn) 그대로 적용할지.
- Grid `enemyDisplayCapacity` 기본값 80(원작) 유지 여부.
- Arena capacity 는 `ArenaModeBootstrap.EnemyDisplayCapacity => arenaView.Config.maxAlive` 로 해결(런타임 `ArenaModel` 노출 불필요). 이 방식이 적절한지.
