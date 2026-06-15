# Arena HUD 통합 Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** 새 `ArenaHUD_Panel`을 데이터 파이프라인에 통합 — 기능형 라운드 타이머 + 점수 모델을 신설하고 Arena 전용 HUD 스택으로 라운드·시간·골드·점수·적을 실시간 표시한다.

**Architecture:** 순수 도메인 모델 2개(`RoundTimerModel`·`ScoreModel`)를 EditMode TDD로 만든 뒤, `EnemySpawner`의 Arena 루프를 타이머/조기전멸 구동으로 재구성한다. HUD는 기존 MVP(`BasePresenter`/`IPresenter`) 패턴을 따르는 Arena 전용 뷰/프레젠터로 신설하고, `GameManager`가 모델 생성·주입·매 프레임 `TickRound`를 담당한다. Grid HUD·A0 구조는 무수정.

**Tech Stack:** Unity 6000.2, C#, UniTask, NUnit EditMode, uGUI MVP, TextMeshPro.

**참고**: 스펙 `docs/superpowers/specs/2026-06-14-arena-hud-integration-design.md`.

**커밋 규칙(사용자 환경)**: 각 Task의 커밋 스텝은 **사용자가 명시적으로 요청할 때만** `commit` 스킬을 통해 수행한다. 커밋 전 변경 `.cs`는 `lint` 스킬로 검증한다. 자동 커밋 금지.

**컴파일 체크포인트**: Task 5~7은 시그니처가 상호 의존(`SetContext`/`Inject`)하므로 **세 Task를 모두 적용한 뒤** Unity refresh로 컴파일을 검증한다(중간 상태는 일시적으로 컴파일되지 않음).

**핵심 타입 사실(확정)**:
- `DefenseDot.Domain.BaseModel` — `protected bool SetField<T>(ref T, T)`. 도메인 모델(`ScoreModel`/`RoundTimerModel`)의 베이스.
- `DefenseDot.UI.BaseModel` — 빈 베이스. 뷰 상태(`ArenaHudModel`)의 베이스.
- `BasePresenter<TView,TModel> where TView:IView where TModel:DefenseDot.UI.BaseModel` — ctor(view, model), virtual `Initialize`/`Dispose`.
- `IView` — `Show()`/`Hide()`. `IPresenter` — `Initialize()`/`Dispose()`.
- `WaveModel`: `OnWaveChanged(int,int)`, `OnRemainingChanged(int)`, `Current`/`Total`/`Remaining`, `SetWave`/`SetRemaining`/`MarkWaveCleared`.
- `EconomyModel`: `OnGoldChanged(int)`, `Gold`. `CombatModel`: `RegisterKill(int)`.
- EditMode 어셈블리는 단일 `DefenseDot` 어셈블리를 참조(asmdef 수정 불필요).

---

## File Structure

| 파일 | 책임 | 신규/수정 |
|---|---|---|
| `Assets/Scripts/Domain/Models/ScoreModel.cs` | 인-런 점수 보유·가산·통지 | 신규 |
| `Assets/Scripts/Domain/Models/RoundTimerModel.cs` | 라운드 제한시간 보유·Tick·만료 | 신규 |
| `Assets/Tests/EditMode/ScoreModelTests.cs` | ScoreModel 단위 테스트 | 신규 |
| `Assets/Tests/EditMode/RoundTimerModelTests.cs` | RoundTimerModel 단위 테스트 | 신규 |
| `Assets/Scripts/Data/WaveData.cs` | 웨이브 제한시간 필드 | 수정 |
| `Assets/Scripts/UI/HudContext.cs` | HUD 조립용 모델 묶음(파라미터 오브젝트) | 신규 |
| `Assets/Scripts/UI/Views/HudRoot.cs` | HUD 루트 공통 베이스(자기설치 위젯, `Bind`) | 신규 |
| `Assets/Scripts/UI/Models/ArenaHudModel.cs` | Arena HUD 표시 상태 스냅샷 | 신규 |
| `Assets/Scripts/UI/Views/ArenaHudView.cs` | Arena 패널 값 전용 갱신(`: HudRoot`) | 신규 |
| `Assets/Scripts/UI/Presenters/ArenaHudPresenter.cs` | 5개 모델 구독 → 뷰 갱신 | 신규 |
| `Assets/Scripts/UI/Views/HUDView.cs` | `: HudRoot` 전환 + `Bind` 추가(Show/Hide 상속) | 수정 |
| `Assets/Scripts/Systems/Enemy/EnemySpawner.cs` | Arena 루프 타이머/조기전멸 구동 | 수정 |
| `Assets/Scripts/Systems/Management/GameManager.cs` | 모델 생성·주입·TickRound·HudContext | 수정 |
| `Assets/Scripts/UI/InGame/UIRoot.cs` | 폴리모픽 단일 `HudRoot` 합성(모드 무지) | 수정 |

---

## Task 1: ScoreModel (TDD)

**Files:**
- Create: `Assets/Scripts/Domain/Models/ScoreModel.cs`
- Test: `Assets/Tests/EditMode/ScoreModelTests.cs`

- [ ] **Step 1: 실패 테스트 작성**

`Assets/Tests/EditMode/ScoreModelTests.cs`:
```csharp
using NUnit.Framework;
using DefenseDot.Domain.Models;

namespace DefenseDot.Tests.EditMode
{
    public class ScoreModelTests
    {
        [Test]
        public void AddKillScore_AddsTenTimesRound()
        {
            var model = new ScoreModel();
            model.AddKillScore(3);
            Assert.AreEqual(30, model.Score);
        }

        [Test]
        public void AddKillScore_Accumulates()
        {
            var model = new ScoreModel();
            model.AddKillScore(1);
            model.AddKillScore(2);
            Assert.AreEqual(30, model.Score);
        }

        [Test]
        public void AddTimeBonus_FloorsSavedTimesTenTimesRound()
        {
            var model = new ScoreModel();
            model.AddTimeBonus(2.5f, 4);   // floor(2.5 * 10 * 4) = 100
            Assert.AreEqual(100, model.Score);
        }

        [Test]
        public void AddTimeBonus_NonPositiveSaved_NoChange()
        {
            var model = new ScoreModel();
            model.AddTimeBonus(0f, 5);
            Assert.AreEqual(0, model.Score);
        }

        [Test]
        public void OnScoreChanged_FiresWithNewScore()
        {
            var model = new ScoreModel();
            int notified = -1;
            model.OnScoreChanged += s => notified = s;
            model.AddKillScore(2);
            Assert.AreEqual(20, notified);
        }

        [Test]
        public void Reset_ZeroesScore()
        {
            var model = new ScoreModel();
            model.AddKillScore(5);
            model.Reset();
            Assert.AreEqual(0, model.Score);
        }
    }
}
```

- [ ] **Step 2: 테스트 실패 확인**

Run: Unity Test Runner(EditMode) — `mcp__UnityMCP__run_tests` mode=EditMode, filter `ScoreModelTests`.
Expected: 컴파일 실패 또는 FAIL ("ScoreModel을 찾을 수 없음").

- [ ] **Step 3: 최소 구현 작성**

`Assets/Scripts/Domain/Models/ScoreModel.cs`:
```csharp
// 인-런 점수(처치·시간보너스)를 보유·통지하는 도메인 모델
using UnityEngine;

namespace DefenseDot.Domain.Models
{
    /// <summary>
    /// 인-런 점수를 보유하고 통지하는 도메인 모델입니다.
    /// 처치 점수와 라운드 조기 클리어 시간보너스를 가산합니다.
    /// </summary>
    [System.Serializable]
    public class ScoreModel : BaseModel
    {
        [SerializeField] private int score;

        /// <summary> 점수가 변경되면 발생합니다. (현재 점수) </summary>
        [field: System.NonSerialized]
        public event System.Action<int> OnScoreChanged;

        /// <summary> 현재 누적 점수입니다. </summary>
        public int Score => score;

        /// <summary> 처치 점수를 가산합니다. (floor(10 × 라운드 × 배율)) </summary>
        public void AddKillScore(int round, float multiplier = 1f)
        {
            int gained = Mathf.FloorToInt(10f * Mathf.Max(1, round) * Mathf.Max(0f, multiplier));
            if (gained <= 0) return;
            score += gained;
            OnScoreChanged?.Invoke(score);
        }

        /// <summary> 조기 클리어 시간보너스를 가산합니다. (floor(절약초 × 10 × 라운드 × 배율)) </summary>
        public void AddTimeBonus(float savedSeconds, int round, float multiplier = 1f)
        {
            int bonus = Mathf.FloorToInt(Mathf.Max(0f, savedSeconds) * 10f * Mathf.Max(1, round) * Mathf.Max(0f, multiplier));
            if (bonus <= 0) return;
            score += bonus;
            OnScoreChanged?.Invoke(score);
        }

        /// <summary> 점수를 0으로 초기화하고 통지합니다. (재시작용) </summary>
        public void Reset()
        {
            score = 0;
            OnScoreChanged?.Invoke(score);
        }
    }
}
```

- [ ] **Step 4: 테스트 통과 확인**

Run: `mcp__UnityMCP__run_tests` mode=EditMode, filter `ScoreModelTests`.
Expected: 6 PASS.

- [ ] **Step 5: 커밋 (사용자 요청 시)** — `lint` 후 `commit` 스킬. 메시지 예: `feat: 인-런 점수 모델(ScoreModel) 추가`

---

## Task 2: RoundTimerModel (TDD)

**Files:**
- Create: `Assets/Scripts/Domain/Models/RoundTimerModel.cs`
- Test: `Assets/Tests/EditMode/RoundTimerModelTests.cs`

- [ ] **Step 1: 실패 테스트 작성**

`Assets/Tests/EditMode/RoundTimerModelTests.cs`:
```csharp
using NUnit.Framework;
using DefenseDot.Domain.Models;

namespace DefenseDot.Tests.EditMode
{
    public class RoundTimerModelTests
    {
        [Test]
        public void StartWave_SetsRemainingToDuration()
        {
            var t = new RoundTimerModel();
            t.StartWave(30f);
            Assert.AreEqual(30f, t.Remaining, 0.0001f);
            Assert.AreEqual(30f, t.Duration, 0.0001f);
        }

        [Test]
        public void Tick_DecrementsRemaining()
        {
            var t = new RoundTimerModel();
            t.StartWave(10f);
            t.Tick(3f);
            Assert.AreEqual(7f, t.Remaining, 0.0001f);
        }

        [Test]
        public void Tick_ClampsAtZero_AndIsExpired()
        {
            var t = new RoundTimerModel();
            t.StartWave(2f);
            t.Tick(5f);
            Assert.AreEqual(0f, t.Remaining, 0.0001f);
            Assert.IsTrue(t.IsExpired);
        }

        [Test]
        public void Ratio_IsRemainingOverDuration()
        {
            var t = new RoundTimerModel();
            t.StartWave(8f);
            t.Tick(2f);
            Assert.AreEqual(0.75f, t.Ratio, 0.0001f);
        }

        [Test]
        public void OnTimeChanged_FiresOnTick()
        {
            var t = new RoundTimerModel();
            t.StartWave(10f);
            float gotRemaining = -1f, gotDuration = -1f;
            t.OnTimeChanged += (r, d) => { gotRemaining = r; gotDuration = d; };
            t.Tick(4f);
            Assert.AreEqual(6f, gotRemaining, 0.0001f);
            Assert.AreEqual(10f, gotDuration, 0.0001f);
        }

        [Test]
        public void Reset_ZeroesTimer()
        {
            var t = new RoundTimerModel();
            t.StartWave(10f);
            t.Reset();
            Assert.AreEqual(0f, t.Remaining, 0.0001f);
            Assert.AreEqual(0f, t.Duration, 0.0001f);
        }
    }
}
```

- [ ] **Step 2: 테스트 실패 확인**

Run: `mcp__UnityMCP__run_tests` mode=EditMode, filter `RoundTimerModelTests`.
Expected: 컴파일 실패 또는 FAIL ("RoundTimerModel을 찾을 수 없음").

- [ ] **Step 3: 최소 구현 작성**

`Assets/Scripts/Domain/Models/RoundTimerModel.cs`:
```csharp
// 라운드 제한시간(남은/총)을 보유·통지하는 도메인 모델
using UnityEngine;

namespace DefenseDot.Domain.Models
{
    /// <summary>
    /// 라운드 제한시간을 보유하고 통지하는 도메인 모델입니다.
    /// 외부(스포너)가 매 프레임 Tick하며, 만료 여부를 제공합니다.
    /// </summary>
    [System.Serializable]
    public class RoundTimerModel : BaseModel
    {
        [SerializeField] private float remaining;
        [SerializeField] private float duration;

        /// <summary> 남은/총 시간이 변경되면 발생합니다. (남은초, 총초) </summary>
        [field: System.NonSerialized]
        public event System.Action<float, float> OnTimeChanged;

        /// <summary> 남은 시간(초)입니다. </summary>
        public float Remaining => remaining;

        /// <summary> 이번 라운드의 총 제한시간(초)입니다. </summary>
        public float Duration => duration;

        /// <summary> 시간바 비율(남은/총)입니다. </summary>
        public float Ratio => duration > 0f ? remaining / duration : 0f;

        /// <summary> 시간이 만료되었는지 여부입니다. </summary>
        public bool IsExpired => remaining <= 0f;

        /// <summary> 새 라운드의 제한시간을 설정하고 통지합니다. </summary>
        public void StartWave(float waveDuration)
        {
            duration = Mathf.Max(0f, waveDuration);
            remaining = duration;
            OnTimeChanged?.Invoke(remaining, duration);
        }

        /// <summary> 경과 시간만큼 남은 시간을 줄이고 통지합니다. </summary>
        public void Tick(float deltaTime)
        {
            if (remaining <= 0f) return;
            remaining = Mathf.Max(0f, remaining - deltaTime);
            OnTimeChanged?.Invoke(remaining, duration);
        }

        /// <summary> 남은·총 시간을 0으로 초기화하고 통지합니다. (재시작용) </summary>
        public void Reset()
        {
            remaining = 0f;
            duration = 0f;
            OnTimeChanged?.Invoke(remaining, duration);
        }
    }
}
```

- [ ] **Step 4: 테스트 통과 확인**

Run: `mcp__UnityMCP__run_tests` mode=EditMode, filter `RoundTimerModelTests`.
Expected: 6 PASS. (누적 EditMode: 기존 57 + 12 = 69)

- [ ] **Step 5: 커밋 (사용자 요청 시)** — `lint` 후 `commit`. 메시지 예: `feat: 라운드 제한시간 모델(RoundTimerModel) 추가`

---

## Task 3: WaveData.duration 필드

**Files:**
- Modify: `Assets/Scripts/Data/WaveData.cs`

- [ ] **Step 1: duration 필드 추가**

`Assets/Scripts/Data/WaveData.cs` 의 `WaveData` 클래스에 `nextWaveDelay` 아래로 추가:
```csharp
    public class WaveData : ScriptableObject
    {
        public List<WaveEntry> entries = new List<WaveEntry>();
        public float nextWaveDelay = 5f;
        public float duration = 30f;              // 라운드 제한시간(초). Arena 전용 — 디자이너가 웨이브별 입력
        public float killScoreMultiplier = 1f;    // 이 웨이브 처치 점수 배율 (Arena)
        public float timeBonusMultiplier = 1f;    // 이 웨이브 조기클리어 시간보너스 배율 (Arena)
    }
```

- [ ] **Step 2: 컴파일 확인**

Run: `mcp__UnityMCP__refresh_unity` 후 `mcp__UnityMCP__read_console` (Error 필터).
Expected: 에러 없음.

- [ ] **Step 3: 커밋 (사용자 요청 시)** — `lint` 후 `commit`. (Task 4와 묶어도 됨)

---

## Task 4: Arena HUD 스택 (HudContext + HudRoot + Model/View/Presenter)

**Files:**
- Create: `Assets/Scripts/UI/HudContext.cs`
- Create: `Assets/Scripts/UI/Views/HudRoot.cs`
- Create: `Assets/Scripts/UI/Models/ArenaHudModel.cs`
- Create: `Assets/Scripts/UI/Views/ArenaHudView.cs`
- Create: `Assets/Scripts/UI/Presenters/ArenaHudPresenter.cs`
- Modify: `Assets/Scripts/UI/Views/HUDView.cs`

> 순수 MonoBehaviour/MVP라 단위 테스트 없음 — Task 8 Play 검증으로 확인.
> **합성 전략(사용자 확정 Option A)**: UIRoot은 모드를 모르고 `HudRoot.Bind(ctx)`만 호출. 각 HUD가 `HudContext`를 받아 자신의 프레젠터를 조립(자기설치 위젯).

- [ ] **Step 1: HudContext 작성 (파라미터 오브젝트)**

`Assets/Scripts/UI/HudContext.cs`:
```csharp
// HUD 프레젠터 조립에 필요한 도메인 모델 묶음(파라미터 오브젝트)
using DefenseDot.Domain.Models;

namespace DefenseDot.UI
{
    /// <summary>
    /// HUD 프레젠터 조립에 필요한 도메인 모델·설정을 묶은 파라미터 오브젝트입니다.
    /// 각 HudRoot가 필요한 항목만 선택해 사용합니다.
    /// </summary>
    public readonly struct HudContext
    {
        public readonly EconomyModel Economy;
        public readonly CoreModel Core;
        public readonly WaveModel Wave;
        public readonly ScoreModel Score;
        public readonly RoundTimerModel Timer;
        public readonly int EnemyCapacity;

        /// <summary> HUD 조립에 필요한 모델·설정을 묶습니다. </summary>
        public HudContext(EconomyModel economy, CoreModel core, WaveModel wave,
            ScoreModel score, RoundTimerModel timer, int enemyCapacity)
        {
            Economy = economy;
            Core = core;
            Wave = wave;
            Score = score;
            Timer = timer;
            EnemyCapacity = enemyCapacity;
        }
    }
}
```

- [ ] **Step 2: HudRoot 공통 베이스 작성**

`Assets/Scripts/UI/Views/HudRoot.cs`:
```csharp
// HUD 루트 공통 베이스 — 모델을 받아 자신의 프레젠터를 조립(자기설치 위젯)
using UnityEngine;
using DefenseDot.UI.Presenters;

namespace DefenseDot.UI.Views
{
    /// <summary>
    /// 모드별 HUD 루트의 공통 베이스입니다. 합성 루트(UIRoot)가 모드를 알지 못해도
    /// 각 HUD가 HudContext를 받아 자신의 프레젠터를 조립합니다.
    /// </summary>
    public abstract class HudRoot : MonoBehaviour, IView
    {
        /// <summary> 주어진 컨텍스트로 이 HUD의 프레젠터를 생성합니다. </summary>
        public abstract IPresenter Bind(in HudContext ctx);

        /// <summary> HUD를 화면에 표시합니다. </summary>
        public void Show() => gameObject.SetActive(true);

        /// <summary> HUD를 화면에서 숨깁니다. </summary>
        public void Hide() => gameObject.SetActive(false);
    }
}
```

- [ ] **Step 3: ArenaHudModel 작성**

`Assets/Scripts/UI/Models/ArenaHudModel.cs`:
```csharp
using DefenseDot.UI;

namespace DefenseDot.UI.Models
{
    /// <summary>
    /// 아레나 HUD 표시 상태 스냅샷 모델입니다. (표시용 캐시, 통지 없음)
    /// </summary>
    public class ArenaHudModel : BaseModel
    {
        public int CurrentWave { get; set; }
        public int RoundTotal { get; set; }
        public float TimeRemaining { get; set; }
        public int CurrentGold { get; set; }
        public int Score { get; set; }
        public int EnemyAlive { get; set; }
        public int EnemyCapacity { get; set; }
    }
}
```

- [ ] **Step 4: ArenaHudView 작성 (`: HudRoot`)**

`Assets/Scripts/UI/Views/ArenaHudView.cs`:
```csharp
// 아레나 HUD 뷰 — 패널의 value TMP·바를 값(숫자) 전용으로 갱신
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using DefenseDot.UI.Models;
using DefenseDot.UI.Presenters;

namespace DefenseDot.UI.Views
{
    /// <summary>
    /// 아레나 HUD 뷰입니다. 라벨은 패널이 직접 표시하므로 value(숫자)만 갱신합니다.
    /// </summary>
    public class ArenaHudView : HudRoot
    {
        [SerializeField] private TextMeshProUGUI roundValue;
        [SerializeField] private TextMeshProUGUI timeValue;
        [SerializeField] private TextMeshProUGUI goldValue;
        [SerializeField] private TextMeshProUGUI scoreValue;
        [SerializeField] private TextMeshProUGUI enemyValue;
        [SerializeField] private Image timeBarFill;
        [SerializeField] private Image enemyBarFill;

        /// <summary> 주어진 컨텍스트로 Arena HUD 프레젠터를 생성합니다. </summary>
        public override IPresenter Bind(in HudContext ctx)
            => new ArenaHudPresenter(this, new ArenaHudModel(),
                ctx.Wave, ctx.Economy, ctx.Score, ctx.Timer, ctx.EnemyCapacity);

        /// <summary> 라운드 표시를 갱신합니다. </summary>
        public void SetRound(int current, int total)
        {
            if (roundValue != null) roundValue.text = $"{current} / {total}";
        }

        /// <summary> 남은 시간 표시를 갱신합니다. </summary>
        public void SetTime(float remaining)
        {
            if (timeValue != null) timeValue.text = $"{Mathf.CeilToInt(remaining)}s";
        }

        /// <summary> 시간바를 갱신합니다. </summary>
        public void SetTimeBar(float ratio)
        {
            if (timeBarFill != null) timeBarFill.fillAmount = Mathf.Clamp01(ratio);
        }

        /// <summary> 골드 표시를 갱신합니다. </summary>
        public void SetGold(int amount)
        {
            if (goldValue != null) goldValue.text = amount.ToString();
        }

        /// <summary> 점수 표시를 갱신합니다. </summary>
        public void SetScore(int score)
        {
            if (scoreValue != null) scoreValue.text = score.ToString("N0");
        }

        /// <summary> 적 수 표시를 갱신합니다. </summary>
        public void SetEnemies(int alive, int capacity)
        {
            if (enemyValue != null) enemyValue.text = $"{alive} / {capacity}";
        }

        /// <summary> 적 바를 갱신합니다. </summary>
        public void SetEnemyBar(float ratio)
        {
            if (enemyBarFill != null) enemyBarFill.fillAmount = Mathf.Clamp01(ratio);
        }
    }
}
```
> Show/Hide는 `HudRoot`에서 상속.

- [ ] **Step 5: ArenaHudPresenter 작성**

`Assets/Scripts/UI/Presenters/ArenaHudPresenter.cs`:
```csharp
// Arena HUD 프레젠터 — Wave/Economy/Score/RoundTimer 구독해 Arena HUD 갱신
using DefenseDot.UI.Models;
using DefenseDot.UI.Views;
using DefenseDot.Domain.Models;

namespace DefenseDot.UI.Presenters
{
    /// <summary>
    /// 아레나 HUD 프레젠터입니다. Wave/Economy/Score/RoundTimer 모델을 구독해
    /// 라운드·시간·골드·점수·적을 갱신합니다. (Arena 패널은 체력 행이 없어 CoreModel 미사용)
    /// </summary>
    public class ArenaHudPresenter : BasePresenter<ArenaHudView, ArenaHudModel>, IPresenter
    {
        private readonly WaveModel wave;
        private readonly EconomyModel economy;
        private readonly ScoreModel score;
        private readonly RoundTimerModel timer;
        private readonly int enemyCapacity;

        /// <summary> ArenaHudPresenter의 생성자입니다. </summary>
        public ArenaHudPresenter(ArenaHudView view, ArenaHudModel model,
            WaveModel wave, EconomyModel economy, ScoreModel score, RoundTimerModel timer, int enemyCapacity)
            : base(view, model)
        {
            this.wave = wave;
            this.economy = economy;
            this.score = score;
            this.timer = timer;
            this.enemyCapacity = enemyCapacity;
        }

        /// <summary> 모델 변경 사건을 구독하고 초기값을 즉시 반영합니다. </summary>
        public override void Initialize()
        {
            wave.OnWaveChanged += HandleWaveChanged;
            wave.OnRemainingChanged += HandleRemainingChanged;
            economy.OnGoldChanged += HandleGoldChanged;
            score.OnScoreChanged += HandleScoreChanged;
            timer.OnTimeChanged += HandleTimeChanged;

            HandleWaveChanged(wave.Current, wave.Total);
            HandleRemainingChanged(wave.Remaining);
            HandleGoldChanged(economy.Gold);
            HandleScoreChanged(score.Score);
            HandleTimeChanged(timer.Remaining, timer.Duration);
        }

        /// <summary> 구독을 해제합니다. (Lapsed Listener 방지) </summary>
        public override void Dispose()
        {
            wave.OnWaveChanged -= HandleWaveChanged;
            wave.OnRemainingChanged -= HandleRemainingChanged;
            economy.OnGoldChanged -= HandleGoldChanged;
            score.OnScoreChanged -= HandleScoreChanged;
            timer.OnTimeChanged -= HandleTimeChanged;
        }

        private void HandleWaveChanged(int current, int total)
        {
            model.CurrentWave = current;
            model.RoundTotal = total;
            view.SetRound(current, total);
        }

        private void HandleRemainingChanged(int alive)
        {
            model.EnemyAlive = alive;
            model.EnemyCapacity = enemyCapacity;
            view.SetEnemies(alive, enemyCapacity);
            view.SetEnemyBar(enemyCapacity > 0 ? (float)alive / enemyCapacity : 0f);
        }

        private void HandleGoldChanged(int gold)
        {
            model.CurrentGold = gold;
            view.SetGold(gold);
        }

        private void HandleScoreChanged(int value)
        {
            model.Score = value;
            view.SetScore(value);
        }

        private void HandleTimeChanged(float remaining, float duration)
        {
            model.TimeRemaining = remaining;
            view.SetTime(remaining);
            view.SetTimeBar(duration > 0f ? remaining / duration : 0f);
        }
    }
}
```

- [ ] **Step 6: HUDView를 `: HudRoot`로 전환 (Grid)**

`Assets/Scripts/UI/Views/HUDView.cs` 전체를 아래로 교체(상속 베이스 변경 + `Bind` 추가, Show/Hide는 상속):
```csharp
using UnityEngine;
using DefenseDot.UI.Models;
using DefenseDot.UI.Presenters;

namespace DefenseDot.UI.Views
{
    /// <summary>
    /// 하위 View 4종을 통솔하는 통합 HUD 루트 View입니다. (Grid 모드)
    /// </summary>
    public class HUDView : HudRoot
    {
        [Header("Sub Views")]
        [SerializeField] private GoldView goldView;
        [SerializeField] private HealthView healthView;
        [SerializeField] private RoundView roundView;
        [SerializeField] private EnemyCountView enemyCountView;

        /// <summary> 주어진 컨텍스트로 Grid HUD 프레젠터를 생성합니다. </summary>
        public override IPresenter Bind(in HudContext ctx)
            => new HUDPresenter(this, new HUDModel(), ctx.Economy, ctx.Core, ctx.Wave, ctx.EnemyCapacity);

        /// <summary> 골드 표시를 갱신합니다. </summary>
        public void UpdateGold(int gold) => goldView?.SetGold(gold);

        /// <summary> 체력 표시를 갱신합니다. </summary>
        public void UpdateHealth(float current, float max, float ratio) => healthView?.SetHealth(current, max, ratio);

        /// <summary> 라운드 표시를 갱신합니다. </summary>
        public void UpdateRound(int current, int total) => roundView?.SetRound(current, total);

        /// <summary> 적 수 표시를 갱신합니다. </summary>
        public void UpdateEnemyCount(int alive, int capacity) => enemyCountView?.SetEnemyCount(alive, capacity);
    }
}
```

- [ ] **Step 7: 컴파일 확인**

Run: `mcp__UnityMCP__refresh_unity` 후 `mcp__UnityMCP__read_console` (Error 필터).
Expected: 에러 없음.

- [ ] **Step 8: 커밋 (사용자 요청 시)** — `lint` 후 `commit`. 메시지 예: `feat: Arena 전용 HUD 스택 및 폴리모픽 HudRoot 도입`

---

## Task 5: EnemySpawner — Arena 루프 타이머/조기전멸 구동

**Files:**
- Modify: `Assets/Scripts/Systems/Enemy/EnemySpawner.cs` (전체 교체)

> Task 6·7과 시그니처가 연동되므로 **Task 7 완료 후** 컴파일 검증.

- [ ] **Step 1: EnemySpawner 전체 교체**

`Assets/Scripts/Systems/Enemy/EnemySpawner.cs` 전체를 아래로 교체:
```csharp
// 적 스포너 — 웨이브 소환, 풀링, 처치/도달 분기, WaveModel 갱신
// Grid는 클리어 게이트, Arena는 라운드 제한시간(타이머)/조기전멸로 진행
using System.Collections.Generic;
using System.Threading;
using UnityEngine;
using Cysharp.Threading.Tasks;
using DefenseDot.Data;
using DefenseDot.Domain.Models;
using DefenseDot.Systems.Mode;

namespace DefenseDot.Systems.Enemy
{
    /// <summary>
    /// 웨이브 데이터 기반으로 적을 소환·풀링하고, 처치/도달을 분기하며 WaveModel을 갱신합니다.
    /// Grid는 클리어 후 다음 웨이브, Arena는 라운드 제한시간(타이머) 만료 또는 조기 전멸로 진행합니다.
    /// </summary>
    public class EnemySpawner : MonoBehaviour
    {
        [Header("Data References")]
        public WaveSequence waveSequence;

        [Header("Hierarchy")]
        [SerializeField] private Transform container;

        // 주입 의존성
        private IGameMode mode;
        private EnemyRegistry registry;
        private CombatModel combat;
        private WaveModel waveModel;
        private RoundTimerModel timer;
        private ScoreModel score;

        private int currentWaveIndex = -1;
        private int activeEnemyCount = 0;
        private bool isSpawning = false;
        private bool allWavesSpawned = false;       // Arena: 등록 웨이브 소진 여부
        private CancellationTokenSource waveCts;     // Arena: 라운드 진행 시 진행 중 스폰 취소

        // prefab별 경량 풀 (필드 보관 컬렉션 → 일반 new 허용)
        private readonly Dictionary<GameObject, Queue<MonsterActor>> pools = new Dictionary<GameObject, Queue<MonsterActor>>();

        /// <summary> 현재 활성 적 수입니다. (아레나 수용 한계 패배 판정용) </summary>
        public int ActiveEnemyCount => activeEnemyCount;

        /// <summary> Arena 여부(클리어로 승리하지 않는 모드)입니다. </summary>
        private bool IsArena => mode != null && !mode.WinsOnWaveClear;

        /// <summary> 현재 진행 중인 웨이브 데이터입니다. (범위 밖이면 null) </summary>
        private WaveData CurrentWave =>
            (waveSequence != null && currentWaveIndex >= 0 && currentWaveIndex < waveSequence.waves.Count)
                ? waveSequence.waves[currentWaveIndex] : null;

        /// <summary> 합성 루트에서 의존성을 주입합니다. </summary>
        public void SetContext(IGameMode gameMode, EnemyRegistry enemyRegistry, CombatModel combatModel,
            WaveModel wave, RoundTimerModel roundTimer, ScoreModel scoreModel)
        {
            mode = gameMode;
            registry = enemyRegistry;
            combat = combatModel;
            waveModel = wave;
            timer = roundTimer;
            score = scoreModel;
        }

        /// <summary> 웨이브 진행을 시작합니다. (주입 완료 후 GameManager가 호출) </summary>
        public void BeginWaves()
        {
            if (waveSequence == null || waveSequence.waves.Count == 0) return;
            if (IsArena) StartArenaWave(0);
            else StartNextWave();
        }

        // ─────────────── Grid: 클리어 게이트 진행 ───────────────

        /// <summary> Grid 전용 — 다음 웨이브로 진행합니다. </summary>
        public void StartNextWave()
        {
            if (isSpawning) return;

            currentWaveIndex++;
            if (currentWaveIndex >= waveSequence.waves.Count)
            {
                waveModel?.MarkWaveCleared();   // Grid: 즉시 승리 통지
                return;
            }
            waveModel?.SetWave(currentWaveIndex + 1, waveSequence.waves.Count);
            SpawnWaveRoutineAsync(waveSequence.waves[currentWaveIndex], destroyCancellationToken).Forget();
        }

        private async UniTask DelayedNextWaveAsync()
        {
            await UniTask.Delay(2000, cancellationToken: destroyCancellationToken);
            StartNextWave();
        }

        // ─────────────── Arena: 타이머/조기전멸 진행 ───────────────

        /// <summary> Arena 전용 — 지정 인덱스의 웨이브를 시작하고 라운드 타이머를 켭니다. </summary>
        private void StartArenaWave(int index)
        {
            currentWaveIndex = index;
            WaveData wave = waveSequence.waves[index];
            waveModel?.SetWave(index + 1, waveSequence.waves.Count);
            timer?.StartWave(wave.duration);

            waveCts?.Cancel();
            waveCts?.Dispose();
            waveCts = CancellationTokenSource.CreateLinkedTokenSource(destroyCancellationToken);
            SpawnWaveRoutineAsync(wave, waveCts.Token).Forget();
        }

        /// <summary> GameManager가 매 플레이 프레임 호출 — Arena 라운드 타이머를 진행합니다. </summary>
        public void TickRound(float deltaTime)
        {
            if (!IsArena || allWavesSpawned || timer == null) return;
            timer.Tick(deltaTime);
            if (timer.IsExpired) AdvanceArenaRound();
        }

        /// <summary> Arena — 다음 라운드로 진행합니다. (시간보너스는 호출자가 가산) </summary>
        private void AdvanceArenaRound()
        {
            int next = currentWaveIndex + 1;
            if (next >= waveSequence.waves.Count)
            {
                allWavesSpawned = true;     // 마지막 웨이브 소진 — 전멸 시 승리
                CheckWaveComplete();
                return;
            }
            StartArenaWave(next);
        }

        // ─────────────── 공통 스폰 ───────────────

        private async UniTask SpawnWaveRoutineAsync(WaveData wave, CancellationToken token)
        {
            isSpawning = true;
            try
            {
                foreach (var entry in wave.entries)
                {
                    for (int i = 0; i < entry.count; i++)
                    {
                        SpawnEnemy(entry.enemyData);
                        await UniTask.Delay(System.TimeSpan.FromSeconds(entry.spawnInterval), cancellationToken: token);
                    }
                }
            }
            catch (System.OperationCanceledException)
            {
                return;   // Arena 라운드 진행으로 취소됨 — 다음 웨이브가 isSpawning을 인계
            }

            isSpawning = false;
            CheckWaveComplete();
        }

        private void SpawnEnemy(EnemyData data)
        {
            if (mode == null) return;

            MonsterActor actor = GetFromPool(data);
            actor.SetSpawner(this);

            // 스폰 위치 모드 위임
            actor.transform.position = mode.GetSpawnWorldPosition(activeEnemyCount);

            actor.Initialize(data);

            // 이동 전략 모드 위임
            IMovementStrategy strategy = mode.CreateMovementStrategy(actor, data.moveSpeed, activeEnemyCount);
            actor.SetMovement(strategy);

            registry?.Register(actor);
            activeEnemyCount++;
            waveModel?.SetRemaining(activeEnemyCount);
        }

        /// <summary> 적 처치 처리 — 보상·점수 통지 후 회수합니다. </summary>
        public void HandleEnemyKilled(MonsterActor actor)
        {
            combat?.RegisterKill(actor.RewardGold);
            if (IsArena && score != null)
            {
                WaveData w = CurrentWave;
                score.AddKillScore(currentWaveIndex + 1, w != null ? w.killScoreMultiplier : 1f);
            }
            RemoveAndReturn(actor);
        }

        /// <summary> 적 코어 도달 처리 — 코어 피해 후 회수합니다. (보상 없음) </summary>
        public void HandleEnemyReached(MonsterActor actor)
        {
            mode?.OnEnemyReachedGoal(actor.CoreDamage);
            RemoveAndReturn(actor);
        }

        private void RemoveAndReturn(MonsterActor actor)
        {
            registry?.Unregister(actor);
            ReturnToPool(actor);
            activeEnemyCount--;
            waveModel?.SetRemaining(activeEnemyCount);
            CheckWaveComplete();
        }

        private void CheckWaveComplete()
        {
            if (IsArena)
            {
                if (activeEnemyCount == 0 && !isSpawning)
                {
                    if (allWavesSpawned)
                    {
                        waveModel?.MarkWaveCleared();   // 마지막 웨이브 후 전멸 → 승리
                        return;
                    }
                    WaveData w = CurrentWave;
                    score?.AddTimeBonus(timer != null ? timer.Remaining : 0f, currentWaveIndex + 1,
                        w != null ? w.timeBonusMultiplier : 1f);
                    AdvanceArenaRound();                // 조기 전멸 → 시간보너스 + 다음 라운드
                }
                return;
            }

            // Grid: 클리어 후 다음 웨이브
            if (activeEnemyCount == 0 && !isSpawning)
                DelayedNextWaveAsync().Forget();
        }

        private void OnDestroy()
        {
            waveCts?.Cancel();
            waveCts?.Dispose();
        }

        #region Pooling
        private MonsterActor GetFromPool(EnemyData data)
        {
            if (!pools.TryGetValue(data.prefab, out var queue))
            {
                queue = new Queue<MonsterActor>();
                pools[data.prefab] = queue;
            }

            MonsterActor actor;
            if (queue.Count > 0)
            {
                actor = queue.Dequeue();
                actor.gameObject.SetActive(true);
            }
            else
            {
                GameObject go = Instantiate(data.prefab, container != null ? container : transform);
                actor = go.GetComponent<MonsterActor>();
                if (actor == null) actor = go.AddComponent<MonsterActor>();
            }

            actor.OnSpawn();
            return actor;
        }

        private void ReturnToPool(MonsterActor actor)
        {
            actor.OnDespawn();
            actor.gameObject.SetActive(false);

            GameObject prefab = actor.Data != null ? actor.Data.prefab : null;
            if (prefab == null) { Destroy(actor.gameObject); return; }

            if (!pools.TryGetValue(prefab, out var queue))
            {
                queue = new Queue<MonsterActor>();
                pools[prefab] = queue;
            }
            queue.Enqueue(actor);
        }
        #endregion
    }
}
```

- [ ] **Step 2: (컴파일은 Task 7 후 검증)** — 이 시점엔 `SetContext` 호출부(GameManager)가 아직 옛 시그니처라 컴파일 안 됨. 다음 Task 진행.

---

## Task 6: GameManager — 모델 생성·주입·TickRound

**Files:**
- Modify: `Assets/Scripts/Systems/Management/GameManager.cs`

- [ ] **Step 1: 모델 프로퍼티 추가**

`GameManager` 의 `public CombatModel Combat { get; private set; }` 아래에 추가:
```csharp
        /// <summary>인-런 점수 모델입니다.</summary>
        public ScoreModel Score { get; private set; }

        /// <summary>라운드 제한시간 모델입니다.</summary>
        public RoundTimerModel RoundTimer { get; private set; }
```

- [ ] **Step 2: Awake에서 모델 생성**

`Awake()` 의 `Combat = new CombatModel();` 아래에 추가:
```csharp
            Score = new ScoreModel();
            RoundTimer = new RoundTimerModel();
```

- [ ] **Step 3: Start의 주입 2곳 갱신**

`spawner.SetContext(...)` 호출을:
```csharp
            if (spawner != null) spawner.SetContext(mode, registry, Combat, Wave, RoundTimer, Score);
```
`uiRoot.Inject(...)` 호출을 HudContext 묶음 기반으로:
```csharp
            if (uiRoot != null)
            {
                var hudContext = new DefenseDot.UI.HudContext(
                    Economy, Core, Wave, Score, RoundTimer, modeBootstrap.EnemyDisplayCapacity);
                uiRoot.Inject(hudContext, Flow, modeBootstrap.PlacementController);
            }
```

- [ ] **Step 4: Update에서 TickRound 호출**

`Update()` 의 `if (!Flow.IsPlaying || mode == null || spawner == null) return;` 바로 아래에 추가:
```csharp
            // 아레나: 라운드 제한시간 진행 (Grid는 내부 가드로 통과)
            spawner.TickRound(Time.deltaTime);
```

- [ ] **Step 5: (컴파일은 Task 7 후 검증)**

---

## Task 7: UIRoot — 폴리모픽 단일 HudRoot 합성 (모드 무지)

**Files:**
- Modify: `Assets/Scripts/UI/InGame/UIRoot.cs`

- [ ] **Step 1: hudView 필드를 폴리모픽 hud로 교체 (기존 씬 참조 자동 이관)**

`[SerializeField] private HUDView hudView;` 를 아래로 교체:
```csharp
        [UnityEngine.Serialization.FormerlySerializedAs("hudView")]
        [SerializeField] private HudRoot hud;   // Arena 또는 Grid HUD (씬에 1개)
```
> `FormerlySerializedAs("hudView")` 로 기존 Grid 씬의 `hudView`(HUDView) 참조가 `hud`로 자동 이관된다(HUDView : HudRoot 이므로 대입 가능).

- [ ] **Step 2: Inject 시그니처·합성 교체 (HudContext 기반, 모드 분기 제거)**

`Inject(...)` 전체를 아래로 교체:
```csharp
        /// <summary>
        /// 합성 루트가 HUD 컨텍스트·게임 흐름·배치 컨트롤러를 주입합니다.
        /// HUD는 자신이 자신의 프레젠터를 조립하므로 UIRoot은 모드를 알지 못합니다.
        /// </summary>
        public void Inject(in HudContext ctx, GameFlowModel flow, TowerPlacementController placement)
        {
            if (hud != null) presenters.Add(hud.Bind(ctx));

            if (placement != null && buildModalView != null && towerRoster != null)
                presenters.Add(new TowerBuildPresenter(buildModalView, towerRoster, ctx.Economy, placement));

            if (gameResultView != null)
                presenters.Add(new GameResultPresenter(gameResultView, flow));

            foreach (IPresenter presenter in presenters) presenter.Initialize();
        }
```
> `using` 정리: `HudContext`(DefenseDot.UI)·`HudRoot`(DefenseDot.UI.Views) 참조 추가 필요 시 보강. `CoreModel`/`ScoreModel` 등 개별 모델 파라미터가 사라지므로 미사용 using 정리.

- [ ] **Step 3: 전체 컴파일 검증 (Task 5~7 통합)**

Run: `mcp__UnityMCP__refresh_unity` 후 `mcp__UnityMCP__read_console` (Error 필터).
Expected: 에러 없음. (`SetContext`/`Inject` 신 시그니처가 호출부와 일치)

- [ ] **Step 4: EditMode 회귀 확인**

Run: `mcp__UnityMCP__run_tests` mode=EditMode (전체).
Expected: 69 PASS (기존 57 + 신규 12).

- [ ] **Step 5: 커밋 (사용자 요청 시)** — `lint` 후 `commit`. 메시지 예: `feat: Arena 라운드 타이머·점수 연동 및 HUD 분기 배선`

---

## Task 8: Unity 에디터 배선 + WaveData duration + Play 검증

**Files (에디터 에셋 — 코드 아님):**
- `Assets/Prefabs/UI/ArenaHUD_Panel.prefab` (ArenaHudView 부착·참조 연결)
- `Assets/Scenes/ArenaScene.unity` (UIRoot.hud = ArenaHudView 할당)
- Arena `WaveData` 에셋들 (duration 값 입력)

- [ ] **Step 1: ArenaHUD_Panel에 ArenaHudView 부착**

`ArenaHUD_Panel` 루트에 `ArenaHudView` 컴포넌트를 추가하고 7개 참조를 연결한다:
- `roundValue` → row.라운드/value (TMP)
- `timeValue` → row.시간/value (TMP)
- `goldValue` → row.골드/value (TMP)
- `scoreValue` → row.점수/value (TMP)
- `enemyValue` → row.적/value (TMP)
- `timeBarFill` → 시간바 fill (Image, type=Filled)
- `enemyBarFill` → 적바 fill (Image, type=Filled)

도구: `mcp__UnityMCP__manage_prefabs`(get_hierarchy로 자식 instanceID 확인) → `mcp__UnityMCP__manage_components`(AddComponent + SerializeField 참조 set). fillBar의 Image는 `Image.type = Filled`, `fillMethod = Horizontal` 확인.

- [ ] **Step 2: ArenaScene UIRoot에 hud = ArenaHudView 할당**

`ArenaScene` 의 UIRoot 컴포넌트 `hud` 필드에 씬의 ArenaHUD_Panel(ArenaHudView) 할당.
- Grid 씬: `FormerlySerializedAs("hudView")` 로 기존 HUDView 참조가 `hud`로 자동 이관 → **재배선 불필요**(로드 후 `hud`가 HUDView를 가리키는지 확인만).
- Arena 씬: 기존엔 hud 미할당이므로 ArenaHudView를 새로 할당.

도구: `mcp__UnityMCP__manage_scene`(load ArenaScene) → `mcp__UnityMCP__find_gameobjects`(UIRoot) → `mcp__UnityMCP__manage_components`(hud 참조 set). 저장. (Grid 씬도 로드해 `hud` 이관 확인.)

- [ ] **Step 3: Arena WaveData duration 값 입력**

Arena `WaveSequence`가 참조하는 각 `WaveData` 에셋의 `duration`을 설정(기본 30, 최종 웨이브는 더 길게 등). duration ≥ 해당 웨이브 스폰 총시간(Σ count×spawnInterval) 권장.

- [ ] **Step 4: Play 검증 (수동)**

`mcp__UnityMCP__manage_editor`(play) 후 다음 확인 — 콘솔 에러 0 + 화면:

| # | 시나리오 | 기대 |
|---|---|---|
| 1 | ArenaScene 진입 | HUD에 라운드/시간/골드/점수/적 표시, 시간바·적바 보임 |
| 2 | 라운드 진행 | 시간바 감소, 만료 시 라운드 +1, 시간바 리셋 |
| 3 | 조기 전멸 | 점수 급증(시간보너스) + 즉시 다음 라운드 |
| 4 | 적 처치 | 점수 +10×라운드 |
| 5 | 골드/적 | 골드·적 수·적바 실시간 반영 |
| 6 | 마지막 웨이브 전멸 | 승리 패널 |
| 7 | GridScene 회귀 | Grid HUD·웨이브 정상(타이머 미개입) |

검증: `mcp__UnityMCP__read_console`(에러) + `mcp__UnityMCP__manage_editor`(screenshot) 또는 사용자 직접 확인.

- [ ] **Step 5: 커밋 (사용자 요청 시)** — 프리팹·씬·WaveData 에셋. 메시지 예: `feat: ArenaHUD_Panel 데이터 배선 및 웨이브 제한시간 설정`

---

## Self-Review 결과

- **Spec coverage**: 신규 모델 2(Task1·2) / WaveData.duration(Task3) / HUD 스택+HudContext+HudRoot(Task4) / Arena 루프(Task5) / GameManager 배선·TickRound·HudContext(Task6) / UIRoot 폴리모픽 합성(Task7) / 에디터 배선·검증(Task8) — 스펙 2~5절 전 항목 매핑됨.
- **Placeholder scan**: 모든 코드 스텝에 실제 코드 포함, TODO/TBD 없음.
- **Type consistency**: `SetContext(mode,registry,combat,wave,roundTimer,score)` / `HudContext(economy,core,wave,score,timer,enemyCapacity)` / `UIRoot.Inject(in HudContext, GameFlowModel, TowerPlacementController)` / `HudRoot.Bind(in HudContext)→IPresenter` / `ArenaHudPresenter(view,model,wave,economy,score,timer,capacity)` / `TickRound(float)` — Task 간 시그니처 일치 확인. `IsArena` 정의(Task5)와 사용 일치.
- **합성 무지(Option A)**: UIRoot은 구체 HUD 타입을 모르고 `HudRoot.Bind` 만 호출. HUD 추가 시 UIRoot 무수정. Grid 씬은 `FormerlySerializedAs` 로 자동 이관.
- **엣지**: 스폰 중 타이머 만료 시 CTS 취소→OCE catch→다음 웨이브가 isSpawning 인계, 마지막 웨이브 비취소, 조기전멸 `!isSpawning` 가드 — Task5 코드에 반영.

---

## 사후 변경 (옵션 3 — 웨이브별 점수 배율, 사용자 승인)

- `WaveData`: `killScoreMultiplier`·`timeBonusMultiplier`(둘 다 기본 1f) 추가 — 처치/시간보너스를 웨이브별 독립 가중.
- `ScoreModel`: `AddKillScore(int round, float multiplier = 1f)`·`AddTimeBonus(float saved, int round, float multiplier = 1f)` — 공식에 `× 배율`, floor.
- `EnemySpawner`: `CurrentWave` 헬퍼로 현재 웨이브의 배율을 가산 시 전달.
- 테스트: `AddKillScore_AppliesMultiplier`·`AddTimeBonus_AppliesMultiplier` 추가 → **EditMode 71/71 PASS**.
- 기본값 1이라 기존 동작·테스트 무영향. 원작 단일 factor에서의 의도적 진화(웨이브 성격·후속 카드/메타 레버).
