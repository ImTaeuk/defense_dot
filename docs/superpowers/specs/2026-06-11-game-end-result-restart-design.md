# 게임 종료·결과·재시작 설계 (TASK-002)

**작성일**: 2026-06-11
**상태**: 설계 (사용자 검토 대기)
**우선순위**: 높음 — 플레이 루프 클로징

---

## 1. 목표 / 범위

게임이 `GameOver`/`Victory`에 도달하면 **결과 패널(승/패)** 을 띄우고, **재시작 버튼**으로 현재 씬을 재로드한다. 더불어 Arena가 웨이브 클리어로 잘못 '승리'하지 않도록 **무한 스폰**으로 보정한다.

**범위**
- A. 결과 패널 + 재시작 + 게임 종료 시 정지(`Time.timeScale=0`)
- B. Arena 무한화(최소): `IGameMode.WinsOnWaveClear` + 스포너 웨이브 루프

**범위 밖**: 타이틀·모드 선택·일반 일시정지(TASK-008), Arena 메타 진행·밸런싱(Arena 방향 결정 후), 점수/통계.

---

## 2. 아키텍처

기존 HUD와 동일한 MVP. `GameFlowModel.OnPhaseChanged`는 **이미 발행되나 구독자가 0** — 여기 결과 Presenter를 붙인다.

```
[패배] Core.OnCoreDestroyed / mode.CheckDefeat ─┐
[승리] Wave.OnWaveCleared (Grid만) ─────────────┤→ GameManager → Flow.SetPhase(GameOver/Victory)
                                                        │ OnPhaseChanged(phase)
                                                        ▼
                                          GameResultPresenter ── Show(won) + timeScale=0 ──▶ GameResultView
                                                        ▲ OnRestart                                  │ 재시작 버튼
                                                        └──────────────────────────────────────────┘
                                            HandleRestart: timeScale=1 + SceneManager.LoadScene(활성 씬)
```

---

## 3. 컴포넌트

### 3.1 `GameResultView` (신규, uGUI View)
- `[SerializeField] GameObject panel; TextMeshProUGUI messageText; Button restartButton;`
- `Awake()`: `restartButton.onClick.AddListener(() => OnRestart?.Invoke());` + `panel.SetActive(false)` (라이프사이클 메서드는 블록 본문, 리스너는 델리게이트 — 컨벤션 준수)
- `public event System.Action OnRestart;`
- `public void Show(bool won)`: `messageText.text = won ? "승리!" : "패배"; panel.SetActive(true);`
- `public void Hide()`: `panel.SetActive(false);`

### 3.2 `GameResultPresenter` (신규, `IPresenter` 직접 구현)
- 생성자: `(GameResultView view, GameFlowModel flow)`
- `Initialize()`: `Time.timeScale = 1f;`(전 세션 잔여 0 방어) → `flow.OnPhaseChanged += HandlePhaseChanged; view.OnRestart += HandleRestart; view.Hide();`
- `Dispose()`: 구독 해제
- `HandlePhaseChanged(GamePhase phase)`:
  - `Victory` → `Time.timeScale = 0f; view.Show(true);`
  - `GameOver` → `Time.timeScale = 0f; view.Show(false);`
  - (그 외 Ready/Playing 무시)
- `HandleRestart()`: `Time.timeScale = 1f; SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);`
- using: `UnityEngine`(Time), `UnityEngine.SceneManagement`, `DefenseDot.Domain`(GamePhase), `DefenseDot.Domain.Models`(GameFlowModel), `DefenseDot.UI.Views`

### 3.3 `IGameMode.WinsOnWaveClear` (신규 프로퍼티)
- `bool WinsOnWaveClear { get; }`
- `GridDefenseMode`: `public bool WinsOnWaveClear => true;`
- `ArenaMode`: `public bool WinsOnWaveClear => false;`

### 3.4 `EnemySpawner.StartNextWave` (수정)
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
→ Arena는 `MarkWaveCleared`를 호출하지 않으므로 `Wave.OnWaveCleared`가 발생하지 않음 → `GameManager.HandleVictory` 무변경(승리는 Grid만).

### 3.5 배선 변경
- `UIRoot`: `[SerializeField] GameResultView gameResultView;` 추가. `Inject` 시그니처에 `GameFlowModel flow` 추가:
```csharp
public void Inject(EconomyModel economy, CoreModel core, WaveModel wave, GameFlowModel flow,
                   int enemyCapacity, TowerPlacementController placement)
{
    presenters.Add(new HUDPresenter(...));
    if (placement != null && buildModalView != null && towerRoster != null)
        presenters.Add(new TowerBuildPresenter(...));
    if (gameResultView != null)
        presenters.Add(new GameResultPresenter(gameResultView, flow));
    foreach (...) presenter.Initialize();
}
```
- `GameManager.Start`: `uiRoot.Inject(Economy, Core, Wave, Flow, modeBootstrap.EnemyDisplayCapacity, modeBootstrap.PlacementController);` (Flow 프로퍼티 이미 보유)

---

## 4. 데이터 흐름

| 모드 | 패배 | 승리 |
|---|---|---|
| Grid | 코어 HP 0 → GameOver → 패 패널 | 전 웨이브 클리어 → Victory → 승 패널 |
| Arena | 무한 스폰 → 수용 한계 초과 → GameOver → 패 패널 | (없음 — 무한 생존) |

재시작 → `timeScale=1` + 활성 씬 재로드(도메인·UI·타워 전부 초기화).

---

## 5. 엣지 케이스

| 상황 | 처리 |
|---|---|
| `Time.timeScale` 잔여 0 (이전 세션 종료 후 미재시작) | `GameResultPresenter.Initialize`에서 `=1` 방어 |
| 재시작 후에도 정지 | `HandleRestart`가 `LoadScene` **전에** `timeScale=1` 복구 |
| 종료 후 입력/스폰 | `timeScale=0`이 gameplay·스폰(UniTask Delay는 scaled) 정지. uGUI 버튼은 동작 |
| `mode == null`(주입 전) | 스포너는 `MarkWaveCleared`로 폴백(무한 spin 방지) |
| 중복 SetPhase | `GameFlowModel.SetField` 가 동일값 무시 → 결과 패널 1회만 |
| 결과 패널뷰 미할당 | `UIRoot.Inject` 가드로 Presenter 미생성(NRE 방지) |

---

## 6. 테스트

- **PlayMode(수동)**: ① Grid 코어 파괴 → 패 패널 → 재시작(씬 리셋) ② Grid 전 웨이브 클리어 → 승 패널 ③ Arena 오래 버티다 수용 한계 초과 → 패 패널 ④ Arena 웨이브 다 돌아도 승리 안 뜸(무한 스폰 지속) ⑤ 재시작 후 timeScale·게임 정상.
- **EditMode(선택)**: `EnemySpawner` 루프 분기는 `mode.WinsOnWaveClear` 의존이라 MonoBehaviour·UniTask 결합으로 단위 테스트 비용 큼 → PlayMode 우선.

---

## 7. 영향 파일

**신규**: `GameResultView.cs`, `GameResultPresenter.cs`
**수정**: `IGameMode.cs`(프로퍼티), `ArenaMode.cs`, `GridDefenseMode.cs`, `EnemySpawner.cs`(StartNextWave), `UIRoot.cs`(Inject+필드), `GameManager.cs`(Inject 호출)
**에디터/씬(사용자)**: 결과 패널 uGUI 프리팹(메시지+재시작 버튼) 제작, `UIRoot.gameResultView` 결선

---

## 8. 확정 / 미해결

- ✅ `GameFlowModel`(`OnPhaseChanged`/`Phase`/`SetPhase`)·`GamePhase`(Ready/Playing/GameOver/Victory)·`IGameMode`·`EnemySpawner.StartNextWave` 확인.
- 미해결 없음. 결과 패널의 시각 디자인(색·레이아웃)은 에디터 제작 시 자유.
