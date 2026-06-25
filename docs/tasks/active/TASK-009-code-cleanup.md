# TASK-009: 코드 정리·일관성

**작성일**: 2026-06-11 (구 TASK-002 분리)
**상태**: 분석 완료 (백로그)
**우선순위**: 낮음
**출처**: 구 TASK-002 플레이 루프 C

---

## 1. 개요

게임플레이엔 영향 없으나 혼란·부채를 만드는 dead code·빈 껍데기·네이밍을 정리한다.

## 2. TODO

- [ ] **A-1.** JPS `PathfindingService`/`JPSJob` 스텁(dead code) — 제거 또는 실제 구현 결정. `Execute()` 가 start/end 만 반환(`PathfindingService.cs:103-117` "TODO: 실제 JPS 구현"), **호출처 없음** (게임플레이엔 baked path 사용)
- [ ] **A-2.** `ActorBase.Initialize`(체력 미설정 주석만)·`Die`(빈 본문) 빈 껍데기 — 보완 또는 추상화 정리 (`ActorBase.cs:45-58`)
- [ ] **A-3.** `Panel_Grid` → `Panel_HUD` 리네임 (공통 HUD 이므로)

## 3. 위험도

- 낮음 — A-1 은 dead code 제거, A-3 은 단순 리네임. A-2 는 파생 클래스가 보완 중이라 영향 적음.

## 참고
- 상위 분리: [TASK-002 게임 종료·결과·재시작](TASK-002-game-end-result-restart.md)
