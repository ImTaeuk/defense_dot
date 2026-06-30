# TASK-002: 게임 종료·결과·재시작 (플레이 루프 클로징)

**작성일**: 2026-06-07 (2026-06-11 분리)
**상태**: 구현 완료 (코드 + ArenaScene 배선 확인 / Play 시각검증·Grid 씬 배선 확인 남음)
**우선순위**: 높음 — 게임 성립의 마지막 관문
**출처**: 구 TASK-002 플레이 루프 P1 (진입=TASK-008, 정리=TASK-009 로 분리)

---

## 1. 문제 정의

"시작 → 플레이 → 다시 시작"의 완결 루프가 **종료에서 막다른 길**이다. 결과 UI·재시작이 없고, Arena 는 유한 웨이브 소진 시 잘못된 Victory 가 발생(원작=무한 생존).

- `GameFlowModel.OnPhaseChanged` **발행만, 구독자 0** (`GameFlowModel.cs:35`)
- `SceneManager.LoadScene` 호출 0, 결과 패널 0
- `EnemySpawner` 가 양 모드 공통으로 `waveSequence` 소진 시 `MarkWaveCleared`→`HandleVictory` (Arena 오발생, F3)

> 이미 해소: 타워 프리팹(`Tower_Test`)·Grid 배치(SP1, 767db56a)·`CoreDamage`(EnemyData 이관).

## 2. TODO

### A. 결과·재시작 ✅ 구현 완료 (코드 + ArenaScene 배선)
- [x] **A-1.** `GameResultPresenter`(IPresenter) → `OnPhaseChanged` 구독 → Victory/GameOver 분기. `UIRoot.Inject` 에서 조립·등록.
- [x] **A-2.** 결과 패널 — `GameResultView`(panel/messageText/restartButton) + ArenaScene `GameResult` 오브젝트에 Panel/Message/RestartButton 배선 확인.
- [x] **A-3.** 재시작 = `SceneManager.LoadScene(현재 씬 buildIndex)` (`HandleRestart`).
- [x] **A-4.** 게임오버 후 정지 — `Time.timeScale=0`(Victory/GameOver), 재시작·Initialize 시 1f 복구. 스폰·이동 일괄 정지.

### B. Arena 종료 규칙
- [x] **B-1.** Arena 종료 규칙 → [[TASK-012]] A0(유한 연속 웨이브 + 전멸 시 승리)로 교정·흡수·완료.

### C. 잔여 검증 (구현 외)
- [ ] **C-1.** Play 시각 검증 — 승/패 발생 시 패널 표시·재시작 동작 (사용자 Unity).
- [ ] **C-2.** Grid 씬(GridScene)에도 동일 배선 여부 확인 (Arena 우선이라 후순위).
- [ ] **C-3.** `Message` 텍스트 폰트 neodgm 적용 여부 확인.

## 3. 선행/연계

- A 는 즉시 가능(이벤트 존재). B 는 Arena "플레이란?" — (a)메타 진행형 / (b)인런 조작형 결정 후 확정.
- **Grid 모드는 A 만으로 루프 완결.**

## 참고
- 원작: Arena 패배 `index.html:13378`(enemies>=maxAlive), Grid 패배 `:13476`(towerHP<=0)
- 분리 문서: [TASK-008 진입·일시정지](TASK-008-entry-flow-pause.md) · [TASK-009 코드 정리](TASK-009-code-cleanup.md)
