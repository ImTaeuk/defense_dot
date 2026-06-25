# 게임 종료·결과·재시작 Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** 게임이 GameOver/Victory 에 도달하면 결과 패널(승/패)을 띄우고 재시작으로 씬을 재로드하며, Arena 가 웨이브 클리어로 잘못 승리하지 않게 무한 스폰으로 보정한다.

**Architecture:** 기존 HUD 와 동일한 MVP — 이미 발행되는 `GameFlowModel.OnPhaseChanged` 를 신규 `GameResultPresenter` 가 구독해 `GameResultView` 를 띄운다. Arena 보정은 `IGameMode.WinsOnWaveClear` 를 모드가 선언하고 `EnemySpawner` 가 존중한다.

**Tech Stack:** Unity 6000.x / C# / uGUI + TextMeshPro / UniTask / SceneManager.

**선행 스펙:** [2026-06-11-game-end-result-restart-design.md](../specs/2026-06-11-game-end-result-restart-design.md)

---

## 이 계획 공통 규칙

- **브랜치 `main`** 에서 작업.
- **컴파일 검증**: Unity MCP 연결됨 → 구현 후 `read_console`(error 필터)로 컴파일 확인 가능. **PlayMode 실제 동작은 사용자 검증.** "동작함"을 임의 주장하지 말 것.
- **TDD 비적용 사유**: 스펙 §6 — Presenter 는 `Time.timeScale`/`SceneManager`(static) 의존, 스포너 루프는 MonoBehaviour+UniTask 결합이라 깨끗한 단위 테스트 시임이 없다. PlayMode 수동 검증 우선. 가짜 테스트 금지.
- **커밋 정책**: **구현만 — 태스크별 커밋 금지.** 전부 끝나면 컴파일 확인 → 사용자 PlayMode → 명시 승인 후 `commit` 스킬로 scoped 일괄 커밋.
- **신규 `.cs`**: `.cs.meta` 직접 생성(충돌 검사한 32-hex GUID). 형식은 기존 스크립트 `.cs.meta`(`MonoImporter` 블록) 참고.
- **컨벤션**: event `On*`/핸들러 `Handle*`, 라이프사이클 함수 `=>` 금지, 명시적 접근 제한자, 한국어 `<summary>`.

---

## File Structure

**신규**
- `Assets/Scripts/UI/Views/GameResultView.cs` — 결과 패널 uGUI dumb View
- `Assets/Scripts/UI/Presenters/GameResultPresenter.cs` — OnPhaseChanged 구독·정지·재시작

**수정**
- `Assets/Scripts/Systems/Mode/IGameMode.cs` — `WinsOnWaveClear` 프로퍼티
- `Assets/Scripts/Systems/Mode/ArenaMode.cs` / `GridDefenseMode.cs` — 구현
- `Assets/Scripts/Systems/Enemy/EnemySpawner.cs` — `StartNextWave` 모드별 승리/루프
- `Assets/Scripts/UI/InGame/UIRoot.cs` — `Inject` 에 flow 인자 + 결과 Presenter
- `Assets/Scripts/Systems/Management/GameManager.cs` — `Inject` 호출에 Flow

**에디터/씬(사용자)** — 결과 패널 uGUI 프리팹(panel + 메시지 TMP + 재시작 Button), `UIRoot.gameResultView` 결선

---

## Task 1: IGameMode.WinsOnWaveClear + 모드 구현

**Files:** Modify `IGameMode.cs`, `ArenaMode.cs`, `GridDefenseMode.cs`

- [ ] **Step 1: 인터페이스에 프로퍼티 추가** — `IGameMode` 의 `CheckDefeat` 선언 옆에:
```csharp
        /// <summary> 웨이브를 모두 클리어하면 승리하는지 여부. (Arena=false: 무한 생존) </summary>
        bool WinsOnWaveClear { get; }
```

- [ ] **Step 2: GridDefenseMode** — `CheckDefeat` 근처에 추가:
```csharp
        public bool WinsOnWaveClear => true;
```

- [ ] **Step 3: ArenaMode** — `CheckDefeat` 근처에 추가:
```csharp
        public bool WinsOnWaveClear => false;
```

---

## Task 2: EnemySpawner.StartNextWave — 모드별 승리/루프

**Files:** Modify `Assets/Scripts/Systems/Enemy/EnemySpawner.cs:63-78`

- [ ] **Step 1: StartNextWave 교체** — 기존 메서드 전체를 아래로:
```csharp
        public void StartNextWave()
        {
            if (isSpawning) return;

            currentWaveIndex++;
            if (currentWaveIndex >= waveSequence.waves.Count)
            {
                if (mode == null || mode.WinsOnWaveClear)
                {
                    waveModel?.MarkWaveCleared();   // Grid: 승리 통지
                    return;
                }
                currentWaveIndex = 0;               // Arena: 무한 루프
            }
            waveModel?.SetWave(currentWaveIndex + 1, waveSequence.waves.Count);
            SpawnWaveRoutineAsync(waveSequence.waves[currentWaveIndex]).Forget();
        }
```

---

## Task 3: GameResultView (신규 uGUI View)

**Files:** Create `Assets/Scripts/UI/Views/GameResultView.cs` (+ `.meta`)

- [ ] **Step 1: 작성**
```csharp
using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace DefenseDot.UI.Views
{
    /// <summary> 승/패 결과 패널과 재시작 버튼을 표시하는 View 입니다. (dumb) </summary>
    public class GameResultView : MonoBehaviour
    {
        [SerializeField] private GameObject panel;
        [SerializeField] private TextMeshProUGUI messageText;
        [SerializeField] private Button restartButton;

        /// <summary> 재시작 버튼이 눌림. </summary>
        public event System.Action OnRestart;

        private void Awake()
        {
            if (restartButton != null) restartButton.onClick.AddListener(() => OnRestart?.Invoke());
            if (panel != null) panel.SetActive(false);
        }

        /// <summary> 결과 패널을 표시합니다. won=true 승리, false 패배. </summary>
        public void Show(bool won)
        {
            if (messageText != null) messageText.text = won ? "승리!" : "패배";
            if (panel != null) panel.SetActive(true);
        }

        /// <summary> 결과 패널을 숨깁니다. </summary>
        public void Hide()
        {
            if (panel != null) panel.SetActive(false);
        }
    }
}
```

- [ ] **Step 2: .meta 생성** — 32-hex GUID 로 `GameResultView.cs.meta`.

---

## Task 4: GameResultPresenter (신규 IPresenter)

**Files:** Create `Assets/Scripts/UI/Presenters/GameResultPresenter.cs` (+ `.meta`)

- [ ] **Step 1: 작성**
```csharp
using UnityEngine;
using UnityEngine.SceneManagement;
using DefenseDot.Domain;
using DefenseDot.Domain.Models;
using DefenseDot.UI.Views;

namespace DefenseDot.UI.Presenters
{
    /// <summary>
    /// 게임 단계 변화를 구독해 결과 패널을 띄우고, 정지·재시작을 처리하는 Presenter 입니다.
    /// </summary>
    public class GameResultPresenter : IPresenter
    {
        private readonly GameResultView view;
        private readonly GameFlowModel flow;

        /// <summary> 결과 뷰와 게임 진행 모델을 주입받습니다. </summary>
        public GameResultPresenter(GameResultView view, GameFlowModel flow)
        {
            this.view = view;
            this.flow = flow;
        }

        /// <summary> 단계 변화·재시작을 구독하고, 잔여 timeScale 을 복구합니다. </summary>
        public void Initialize()
        {
            Time.timeScale = 1f;
            flow.OnPhaseChanged += HandlePhaseChanged;
            view.OnRestart += HandleRestart;
            view.Hide();
        }

        /// <summary> 구독을 해제합니다. </summary>
        public void Dispose()
        {
            flow.OnPhaseChanged -= HandlePhaseChanged;
            view.OnRestart -= HandleRestart;
        }

        private void HandlePhaseChanged(GamePhase phase)
        {
            if (phase == GamePhase.Victory) { Time.timeScale = 0f; view.Show(true); }
            else if (phase == GamePhase.GameOver) { Time.timeScale = 0f; view.Show(false); }
        }

        private void HandleRestart()
        {
            Time.timeScale = 1f;
            SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
        }
    }
}
```

- [ ] **Step 2: .meta 생성** — 32-hex GUID 로 `GameResultPresenter.cs.meta`.
- [ ] **Step 3: 정적 확인** — `GamePhase`(DefenseDot.Domain), `GameFlowModel.OnPhaseChanged`, `IPresenter`(동일 네임스페이스) 일치.

---

## Task 5: UIRoot.Inject — 결과 Presenter 합류

**Files:** Modify `Assets/Scripts/UI/InGame/UIRoot.cs`

- [ ] **Step 1: 직렬화 필드 추가** — `buildModalView`/`towerRoster` 직렬화 필드 옆에:
```csharp
        [SerializeField] private GameResultView gameResultView;
```

- [ ] **Step 2: Inject 시그니처 + Presenter** — `Inject` 를 교체:
```csharp
        public void Inject(EconomyModel economy, CoreModel core, WaveModel wave, GameFlowModel flow,
                           int enemyCapacity, TowerPlacementController placement)
        {
            presenters.Add(new HUDPresenter(hudView, new HUDModel(), economy, core, wave, enemyCapacity));

            if (placement != null && buildModalView != null && towerRoster != null)
                presenters.Add(new TowerBuildPresenter(buildModalView, towerRoster, economy, placement));

            if (gameResultView != null)
                presenters.Add(new GameResultPresenter(gameResultView, flow));

            foreach (IPresenter presenter in presenters) presenter.Initialize();
        }
```
> `GameFlowModel` 은 `DefenseDot.Domain.Models` — `UIRoot` 에 이미 `using DefenseDot.Domain.Models;` 존재. `GameResultView`(DefenseDot.UI.Views)·`GameResultPresenter`(DefenseDot.UI.Presenters) 도 이미 import 됨.

---

## Task 6: GameManager.Start — Flow 전달

**Files:** Modify `Assets/Scripts/Systems/Management/GameManager.cs`

- [ ] **Step 1: Inject 호출 변경** — `Start()` 의 UI 연결부:
```csharp
            if (uiRoot != null)
                uiRoot.Inject(Economy, Core, Wave, Flow, modeBootstrap.EnemyDisplayCapacity, modeBootstrap.PlacementController);
```
(`Flow` 프로퍼티 이미 보유)

---

## Task 7: 컴파일 검증 (MCP)

- [ ] **Step 1:** Unity MCP `read_console`(types=["error"]) 로 컴파일 에러 0 확인. (또는 `editor_state.isCompiling` 폴링 후 확인)
- [ ] **Step 2:** 에러 시 해당 파일 수정 후 재확인.

---

## Task 8: 에디터/씬 작업 (사용자) + PlayMode 검증

- [ ] **결과 패널 uGUI** 제작: Canvas 하위 `GameResult`(`GameResultView`) → `panel`(배경) → 메시지 `TextMeshProUGUI` + `Button`(재시작). 세 필드 결선. 패널 시작 비활성.
- [ ] **UIRoot 결선**: `gameResultView` ← 위 GameResult 오브젝트.
- [ ] **PlayMode 검증** (스펙 §6): ① Grid 코어 파괴→패+재시작 ② Grid 전 웨이브→승 ③ Arena 수용 한계 초과→패 ④ Arena 웨이브 다 돌아도 승리 안 뜸 ⑤ 재시작 후 timeScale 정상.

---

## Task 9: 일괄 커밋 (검증 후 · 사용자 명시 승인)

- [ ] **Step 1: lint** — 본 작업 신규/수정 `.cs` 만 범위로 `lint` 스킬.
- [ ] **Step 2: scoped 커밋** (사용자 "커밋해줘" 후):
```bash
git add Assets/Scripts/UI/Views/GameResultView.cs Assets/Scripts/UI/Views/GameResultView.cs.meta \
        Assets/Scripts/UI/Presenters/GameResultPresenter.cs Assets/Scripts/UI/Presenters/GameResultPresenter.cs.meta \
        Assets/Scripts/Systems/Mode/IGameMode.cs Assets/Scripts/Systems/Mode/ArenaMode.cs Assets/Scripts/Systems/Mode/GridDefenseMode.cs \
        Assets/Scripts/Systems/Enemy/EnemySpawner.cs Assets/Scripts/UI/InGame/UIRoot.cs Assets/Scripts/Systems/Management/GameManager.cs
git commit -m "feat: 게임 종료 결과 패널·재시작 및 아레나 무한 생존 보정"
```
(결과 패널 프리팹·씬은 사용자 결선분 — 지시에 따라 포함)

---

## Self-Review (작성자 점검)

- **스펙 커버리지**: 결과·정지·재시작(Task 3·4), Arena 무한화(Task 1·2), 배선(Task 5·6), 검증(Task 7·8) — 전부 대응.
- **플레이스홀더 스캔**: 모든 코드 단계 완전 코드. 없음.
- **타입 일관성**: `WinsOnWaveClear`(IGameMode/Arena/Grid/EnemySpawner), `GameResultView.Show(bool)`/`Hide()`/`OnRestart`, `GameResultPresenter(view, flow)`, `Inject(...,GameFlowModel flow,...)`, `HandlePhaseChanged`/`HandleRestart` — 선언·호출 일치. `GamePhase`(Victory/GameOver) 확인됨.
