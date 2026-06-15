# Arena HUD 통합 설계 (시간·점수 데이터 + 기능형 라운드 타이머)

**작성일**: 2026-06-14
**상태**: 설계 확정
**관련 로드맵**: TASK-012 Arena 모드 (A0 종료규칙 후속 — HUD 데이터 통합)

---

## 1. 목적 / 배경

새로 제작한 `ArenaHUD_Panel.prefab`(라운드/시간/골드/점수/적 + 시간바·적바)을 데이터 파이프라인에 통합한다.

- **라운드·골드·적**: 기존 모델(`WaveModel`/`EconomyModel`)로 즉시 공급 가능.
- **시간·점수**: 백킹 데이터 없음 → 신규 도메인 모델 필요.
- 기존 MVP(`HUDPresenter`/`HUDView`/하위뷰)는 풀스트링 포맷("라운드 3/10", "잔여 적 X/Y")이라 label/value 분리 패널과 불일치 → Arena 전용 뷰/프레젠터 신설.

### 확정된 설계 결정

1. **타이머 = 기능형(원작 충실)**: 라운드별 제한시간(`WaveData.duration`). 만료 시 적이 남아도 다음 웨이브 자동 진행, 조기 전멸 시 시간보너스 점수. (원작 `index.html:20951-20967`, `duration` `:15737`)
2. **점수 = 처치 + 시간보너스**: 처치당 `10 × 라운드`(원작 `:19589`), 조기 클리어 시 `floor(절약시간 × 10 × 라운드)`(원작 `:20954`). 메타 배수(asc/metaMods/행운)는 A7 메타 레이어로 제외. 인-런 표시 전용(기록 저장 X). **원작은 처치·시간보너스가 factor `10`을 공유하나, 본 포팅은 웨이브별 `killScoreMultiplier`·`timeBonusMultiplier`(기본 1)로 분리 가중 — 원작에서의 의도적 진화(웨이브 성격 부여, 후속 카드/메타 레버).**
3. **아키텍처 = 접근 1(전용 Arena HUD 스택 + 스포너=라운드 러너 + 순수 모델 2개)**. A0 구조 무수정.
4. **HUD 합성 = Option A(폴리모픽 `HudRoot` + `HudContext`)**: UIRoot이 모드를 모르고 `HudRoot.Bind(ctx)`만 호출(자기설치 위젯). HUD 추가 시 UIRoot 무수정. `[FormerlySerializedAs]`로 기존 Grid 씬 자동 이관. (사용자가 UIRoot의 모드 하드코딩 분기를 지적해 채택)

### 기각된 대안

- **접근 2(공유 `IHudView` + 통합 모델)**: Grid가 time/score 더미 구현 강제, 통합 모델 응집 약화, 프레젠터 분기 → 기각.
- **접근 3(`ArenaRoundController` 분리)**: A0 웨이브 진행 로직을 스포너 밖으로 이전하는 큰 리팩터 + 작동 중 A0 회귀 위험 → A2 이후 재검토로 보류.
- **HUD 합성 — 옵셔널 뷰 null 체크 / ModeBootstrap 공급**: 전자는 UIRoot이 구체 HUD 타입 참조 유지(HUD 추가 시 편집), 후자는 게임플레이 모드 계층에 UI 타입 결합 → Option A 대비 확장성·계층 분리 열위로 기각.
- **표시 전용 타이머 / 처치만 점수**: 원작 충실도 미달 → 기각.

---

## 2. 컴포넌트

### 2.1 신규 도메인 모델 (`DefenseDot.Domain.Models`, `BaseModel` 상속)

```
RoundTimerModel : BaseModel
  float Remaining, Duration
  float Ratio => Duration > 0 ? Remaining / Duration : 0
  event Action<float,float> OnTimeChanged(remaining, duration)
  void StartWave(float duration)    // remaining = duration, 통지
  void Tick(float deltaTime)        // remaining = Max(0, remaining - dt), 통지
  bool IsExpired => Remaining <= 0
  void Reset()                      // remaining = duration = 0

ScoreModel : BaseModel
  int Score
  event Action<int> OnScoreChanged(score)
  void AddKillScore(int round, float multiplier = 1f)               // Score += FloorToInt(10 * Max(1,round) * mult)
  void AddTimeBonus(float savedSec, int round, float multiplier = 1f) // Score += FloorToInt(savedSec * 10 * Max(1,round) * mult)
  void Reset()                               // Score = 0
```

> 카운트다운·가산 로직을 모델 안에 둬 EditMode 단위 테스트가 가능하다(스포너는 호출만 한다).

### 2.2 데이터 변경

- `WaveData.cs`: 디자이너가 웨이브별 입력하는 Arena 전용 필드 3종 추가.
  - `public float duration = 30f;` — 라운드 제한시간(초)
  - `public float killScoreMultiplier = 1f;` — 이 웨이브 처치 점수 배율
  - `public float timeBonusMultiplier = 1f;` — 이 웨이브 조기클리어 시간보너스 배율
  - → 처치/시간보너스를 **독립적으로** 웨이브별 가중. 기본값 1이라 미설정 웨이브는 원작 동작과 동일. (처치=파밍 성격, 시간=러시 성격을 분리 저작 가능 — A3~A7 카드/콤보/메타가 당길 레버)

### 2.3 HUD 합성 추상화 — 폴리모픽 HudRoot + HudContext (사용자 확정 Option A)

UIRoot이 "모드별 어떤 뷰인지"를 분기하는 하드코딩을 제거한다. 각 HUD가 **자기 프레젠터를 스스로 조립(자기설치 위젯)**하고, UIRoot은 `HudRoot.Bind(ctx)`만 호출한다.

```
HudContext (readonly struct, DefenseDot.UI)   // 파라미터 오브젝트(AbilityContext 패턴과 일관)
  EconomyModel Economy; CoreModel Core; WaveModel Wave;
  ScoreModel Score; RoundTimerModel Timer; int EnemyCapacity

HudRoot : MonoBehaviour, IView                  // 공통 베이스
  abstract IPresenter Bind(in HudContext ctx)
  Show()/Hide()                                 // SetActive (하위뷰 공통 상속)

HUDView : HudRoot                               // Grid (기존, : HudRoot 로 전환 + Bind 추가)
  Bind(ctx) => new HUDPresenter(this, new HUDModel(), ctx.Economy, ctx.Core, ctx.Wave, ctx.EnemyCapacity)

ArenaHudView : HudRoot                          // Arena (신규, ArenaHUD_Panel 에 부착, 값 전용)
  [SF] TMP roundValue, timeValue, goldValue, scoreValue, enemyValue
  [SF] Image timeBarFill, enemyBarFill
  Bind(ctx) => new ArenaHudPresenter(this, new ArenaHudModel(), ctx.Wave, ctx.Economy, ctx.Score, ctx.Timer, ctx.EnemyCapacity)
  SetRound/SetTime/SetTimeBar/SetGold/SetScore/SetEnemies/SetEnemyBar

ArenaHudModel : BaseModel                        // 뷰 상태(HUDModel 대응)
  int CurrentWave, RoundTotal, CurrentGold, Score, EnemyAlive, EnemyCapacity; float TimeRemaining

ArenaHudPresenter : BasePresenter<ArenaHudView, ArenaHudModel>, IPresenter
  Initialize() 구독:
    wave.OnWaveChanged      -> SetRound(cur, total)
    wave.OnRemainingChanged -> SetEnemies(alive, cap) + SetEnemyBar(alive / cap)
    economy.OnGoldChanged   -> SetGold
    score.OnScoreChanged    -> SetScore
    timer.OnTimeChanged     -> SetTime(remain) + SetTimeBar(remain / duration)
  Dispose() 전체 해제
```

> Arena 패널엔 체력 행이 없으므로 ArenaHudPresenter는 `CoreModel` 미사용. 적 바가 수용 압박(alive/capacity)을 직접 표현한다. (HudContext는 Grid가 쓰는 Core까지 superset으로 운반)

### 2.4 합성 루트 배선

- `GameManager.cs`: `ScoreModel`·`RoundTimerModel` 생성(전 모드 공통). `spawner.SetContext(...)`에 timer·score 추가 주입. `HudContext` 묶어 `uiRoot.Inject(in HudContext, GameFlowModel, TowerPlacementController)` 호출.
- `UIRoot.cs`: `hudView`(HUDView) 필드를 폴리모픽 `[SF] HudRoot hud` 로 교체(`[FormerlySerializedAs("hudView")]`로 기존 Grid 씬 참조 자동 이관). `Inject`은 `if (hud != null) presenters.Add(hud.Bind(ctx));` — **모드 분기 없음**. HUD 추가 시 UIRoot 무수정.

---

## 3. 데이터 흐름 — 타이머 구동 웨이브 루프 + 점수

**틱 구동점**: `GameManager.Update`가 이미 `Flow.IsPlaying` 게이트를 가지므로 거기서 `spawner.TickRound(Time.deltaTime)` 호출 → 일시정지·게임오버 후 타이머 자동 정지(스포너가 Flow를 알 필요 없음).

**Arena 웨이브 루프 재구성** (`EnemySpawner.cs`, Grid 경로 무수정)

```
StartWave(index):
  currentWaveIndex = index
  waveModel.SetWave(index + 1, count)
  timer.StartWave(wave.duration)
  이전 스폰 취소(웨이브별 CTS) -> SpawnWaveRoutineAsync(wave) fire-forget   // 다음 웨이브 체이닝 X

TickRound(dt):  // GameManager가 매 플레이 프레임 호출
  Arena & 진행중 & !allWavesSpawned 일 때만:
    timer.Tick(dt);  if (timer.IsExpired) AdvanceRound(earlyClear: false)

HandleEnemyKilled(actor):                          // Arena 한정
  combat.RegisterKill(reward)
  if (Arena) score.AddKillScore(currentWaveIndex + 1)
  회수

CheckWaveComplete():  // Arena 조기전멸
  !isSpawning && activeEnemyCount == 0 && !allWavesSpawned -> AdvanceRound(earlyClear: true)

AdvanceRound(earlyClear):
  if earlyClear: score.AddTimeBonus(timer.Remaining, currentWaveIndex + 1)
  next = currentWaveIndex + 1
  if next >= count: allWavesSpawned = true; CheckWaveComplete()   // 전멸 시 승리
  else: StartWave(next)
```

> 기존 Arena의 `DelayedNextWaveAsync`(스폰 후 2초 → 다음)는 제거, 타이머/조기전멸 구동으로 대체. Grid는 `WinsOnWaveClear == true`라 `TickRound` 내부 가드로 통과(타이머 미사용)하고, 점수도 Arena에서만 가산한다.

---

## 4. 엣지 케이스 / 에러 처리

| 상황 | 처리 |
|---|---|
| 타이머가 스폰 중 만료 (duration < 스폰시간) | `AdvanceRound`가 웨이브별 CTS 취소 → 남은 스폰 폐기, 다음 웨이브 시작 (원작 `advanceRound`의 spawnPlan 교체와 동일). 디자이너 권장: duration ≥ 스폰시간 |
| 마지막 웨이브 | 만료 → `allWavesSpawned`(타이머 미재시작), 전멸 시 승리 / 조기전멸 → 시간보너스 + 즉시 승리 |
| 조기전멸 오탐 (스폰 전 적 0) | `!isSpawning` 가드로 차단 |
| 재시작 | `GameManager.Awake`가 모델 새로 생성(씬 리로드 시 자연 초기화). 인-플레이스 재시작이면 `ScoreModel/RoundTimerModel.Reset()` 호출 |
| 일시정지/게임오버 | `GameManager.Update`의 `IsPlaying` 게이트가 `TickRound` 미호출 → 타이머 정지 |
| Grid 모드 | `WinsOnWaveClear == true`로 타이머/점수 경로 미진입, 기존 클리어 게이트 유지 |

---

## 5. 테스트 / 검증

### 5.1 EditMode 단위 테스트 (순수 모델 — 기존 57개에 추가)

- `RoundTimerModelTests`: StartWave→remaining=duration / Tick 감소 / 0에서 IsExpired / OnTimeChanged 발화 / Reset
- `ScoreModelTests`: AddKillScore(r)=10×r / AddTimeBonus(s,r)=floor(s×10×r) / 누적 / OnScoreChanged / Reset

### 5.2 Play 검증 (Critical Path — 웨이브 루프·HUD)

| # | 시나리오 | 기대 |
|---|---|---|
| 1 | 웨이브 시작 | 시간바 감소, 만료 시 라운드 표시 +1 |
| 2 | 조기 전멸 | 시간보너스 점수 가산 + 즉시 다음 라운드 |
| 3 | 적 처치 | 점수 +10×라운드 |
| 4 | 골드/적 | 골드·적 수·적바 실시간 반영 |
| 5 | 마지막 웨이브 전멸 | 승리 패널 |
| 6 | Grid 회귀 | Grid HUD·웨이브 정상(타이머 미개입) |

---

## 6. 영향도 / 위험도

| 항목 | 내용 |
|---|---|
| 신규 파일 | `RoundTimerModel.cs`, `ScoreModel.cs`, `HudContext.cs`, `HudRoot.cs`, `ArenaHudView.cs`, `ArenaHudModel.cs`, `ArenaHudPresenter.cs`, 테스트 2종 |
| 수정 파일 | `WaveData.cs`(필드 1), `HUDView.cs`(`: HudRoot` 전환 + Bind), `EnemySpawner.cs`(Arena 루프), `GameManager.cs`(모델 생성·주입·틱·HudContext), `UIRoot.cs`(폴리모픽 합성) |
| 회귀 위험 | Arena 웨이브 루프 재구성 — A0 종료/수용 규칙과 상호작용. Grid HUDView는 베이스 전환(추가 변경)이라 회귀 검증 필요(#6). `FormerlySerializedAs`로 Grid 씬 참조 자동 이관 |
| 비고 | A0의 `TryGetCapacityHp → Core.SetCurrent`는 새 Arena 패널에선 HUD 표시에 불필요(게임오버 판정은 `ArenaMode.CheckDefeat` 독립). 본 작업 범위에선 제거하지 않고 잔존 표기만 — 별도 정리 태스크 |

---

## 7. 설계 패턴 메모

- **MVP 일관성**: 신규 Arena 스택은 기존 `BasePresenter<TView,TModel>` + `IPresenter` 패턴을 그대로 따른다(구독은 Initialize, 해제는 Dispose).
- **순수 모델 + 외부 틱**: 카운트다운/가산을 모델에 캡슐화하고 프레임 구동은 외부(`GameManager.Update → spawner.TickRound`)가 담당해 테스트 용이성과 라이프사이클 게이트를 분리한다.
- **모드별 분기 최소화**: 타이머/점수는 `WinsOnWaveClear` 한 플래그로 Arena 한정. Grid는 경로 무진입.
- **자기설치 위젯 + 파라미터 오브젝트(Option A)**: UIRoot은 합성 주체가 아니라 수명 관리자에 가깝게 — HUD가 `Bind(in HudContext)`로 자신을 조립하고 UIRoot은 구체 타입을 모른다(개방-폐쇄). `HudContext`는 A1 `AbilityContext`와 동일한 Context Object 패턴으로 Inject 시그니처 비대를 막는다.
