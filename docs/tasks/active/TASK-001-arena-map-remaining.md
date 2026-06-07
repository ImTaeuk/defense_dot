# TASK-001: 아레나 맵 시스템 — 남은 작업

**작성일**: 2026-06-04
**상태**: 진행 중
**우선순위**: 중간

---

## 1. 개요

원형 아레나 맵 시스템 구축 및 모드별 합성 루트 리팩토링의 후속 작업 추적 문서.
핵심 시스템·리팩토링·SO 분리는 **완료·커밋**되었고, 남은 작업은 **UI 통합(다음)** 과 **설계상 보류한 미래 항목**들이다.

**관련 문서**
- 설계: [docs/superpowers/specs/2026-06-03-arena-map-system-design.md](../../superpowers/specs/2026-06-03-arena-map-system-design.md)
- 계획: [arena-map-system](../../superpowers/plans/2026-06-03-arena-map-system.md) · [arena-composition-refactor](../../superpowers/plans/2026-06-04-arena-composition-refactor.md)
- 커밋: `c8354d5d`(코드), `1c5b22c7`(Reference) — 브랜치 `feature/arena-map-system`

---

## 2. 완료된 작업 (참고)

- 아레나 맵 시스템: `ArenaConfig`·`ArenaModel`·`ArenaView`·`ArenaOrbitLogic` (도넛 밴드 랜덤 스폰 · 코어 공전 · 반경 비율 기반 동적 압축)
- 모드별 합성 루트 분리: `ModeBootstrap`·`ModeContext`·`ArenaModeBootstrap`·`GridDefenseModeBootstrap` (ModeType 분기 · config 중복 · ArenaView 누수 제거)
- `TowerPlacementController`(placement) → `GridDefenseModeBootstrap` 이전
- `EnemyData`/`TowerData` SO 파일 분리 + Missing Script 복구
- asmdef 3종(런타임/에디터/테스트) + EditMode 테스트 9개 (PASS)

---

## 3. TODO

### A. UI 통합 (다음 작업 · 방향 합의됨)
- [ ] **A-1.** uGUI HUD 통합 + 하위 View 분리 (`GoldView`/`HealthView`/`WaveView`/`EnemyCountView`) — Composite 구조
- [ ] **A-2.** UI Toolkit 제거 — `WaveHUDPresenter`(UIDocument) → uGUI로 통일
- [ ] **A-3.** WaveHUD MVP 비대칭 해소 — MonoBehaviour Presenter → POCO Presenter + View 분리 (HUD와 동일 구조)
- [ ] **A-4.** `HUDView` NRE 근본 해결 — 빈 텍스트 참조 → 새 uGUI HUD로 교체

> **시작 전 `superpowers:brainstorming` 으로 요구사항·하위 View 분리 단위를 확정**할 것. (현재 `HUDView`=uGUI/TMP, `WaveHUDPresenter`=UI Toolkit 혼재 상태)

### B. 미래 (설계상 범위 밖으로 의도적 보류)
- [ ] **B-1.** Arena 코어 강화 시스템 (원본 능력·가챠) — `ArenaModeBootstrap` 이 소유할 자리, 현재 미구현. **게임 디자인 결정 필요**
- [ ] **B-2.** 동적 아레나 크기 트리거 연결 — `ArenaModel.Expand/Shrink` + `OnRadiusChanged` 훅은 존재. 호출 사건(보스 등장 등) 미연결
- [ ] **B-3.** 코어 비주얼 (헥사 링·펄스 등) — `ArenaView` 장식으로 분류

### C. 정리
- [ ] **C-1.** `TowerData` SO 에셋 생성 — 클래스만 있고 `.asset` 없음

---

## 4. 다음 세션 시작 가이드

1. 이 문서(`docs/tasks/active/TASK-001`)에서 남은 TODO 확인
2. **A(UI 통합)** 부터 — `superpowers:brainstorming` 으로 설계 시작
3. B는 게임 디자인 결정이 선행돼야 하므로 사용자 의향 확인 후 진행
4. 작업 완료 시 해당 TODO 항목 제거, 모두 소진되면 `docs/tasks/done/` 으로 이동
