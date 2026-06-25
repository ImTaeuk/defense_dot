# TASK-002: 게임 종료·결과·재시작 (플레이 루프 클로징)

**작성일**: 2026-06-07 (2026-06-11 분리)
**상태**: 분석 완료 (착수 예정)
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

### A. 결과·재시작
- [ ] **A-1.** `GameResultPresenter`(POCO·IPresenter) → `OnPhaseChanged` 구독 → 승/패 처리. `UIRoot` 에 한 줄(HUDPresenter 패턴). **이벤트 이미 존재, 소비자만 추가**
- [ ] **A-2.** 결과 패널 프리팹 (승/패 메시지 + 재시작 버튼, uGUI 공통 HUD 컨벤션)
- [ ] **A-3.** 재시작 = `SceneManager.LoadScene(현재 씬)` 또는 도메인 리셋
- [ ] **A-4.** 게임오버 후 스폰·입력 정지 (`Flow.IsPlaying` 체크 확장 — 현재 `GameManager.Update` 1곳뿐)

### B. Arena 종료 규칙
- [ ] **B-1.** **Arena 무한화** — Arena 에서 `OnWaveCleared`→Victory 미발생하도록 모드별 분기(원작=무한 생존). **선행: Arena 방향(a/b) 결정**

## 3. 선행/연계

- A 는 즉시 가능(이벤트 존재). B 는 Arena "플레이란?" — (a)메타 진행형 / (b)인런 조작형 결정 후 확정.
- **Grid 모드는 A 만으로 루프 완결.**

## 참고
- 원작: Arena 패배 `index.html:13378`(enemies>=maxAlive), Grid 패배 `:13476`(towerHP<=0)
- 분리 문서: [TASK-008 진입·일시정지](TASK-008-entry-flow-pause.md) · [TASK-009 코드 정리](TASK-009-code-cleanup.md)
