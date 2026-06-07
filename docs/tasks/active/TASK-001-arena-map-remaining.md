# TASK-001: 아레나 맵 시스템 — 남은 작업

**작성일**: 2026-06-04
**상태**: 진행 중
**우선순위**: 중간

---

## 1. 개요

원형 아레나 맵 시스템 구축 및 모드별 합성 루트 리팩토링의 후속 작업 추적 문서.
핵심 시스템·리팩토링·SO 분리·**HUD 통합(A)**·**TowerData 테스트 에셋(C-1)** 은 **완료·커밋**되었고, 남은 작업은 **Arena HUD 프리팹 결선(designer 후속)** 과 **설계상 보류한 미래 항목(B)** 뿐이다.

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
- **HUD 통합 (A-1~A-4 완료, 커밋 `e629265e` · main 병합)**: uGUI 통합 HUD + 하위 View 4종(Gold/Health/Round/EnemyCount) Composite, `UIRoot` 합성 루트, `WaveHUDPresenter`(UI Toolkit) 제거, `ModeBootstrap.EnemyDisplayCapacity` 도입, `HudSetupTool` 결선 도구
- **TowerData 테스트 에셋 (C-1 완료)**: `Assets/Data/Towers/TowerData.asset` 생성 (Tower Test / dmg 5·range 3·spd 1·cost 50, prefab 미할당)

---

## 3. TODO

### A. Arena HUD 프리팹 결선 (designer 후속 · 코드 준비 완료)
- [ ] **A-1.** `Panel_Arena` 프리팹 신설 + 게임 씬 배치·결선 — capacity 공급점 `ArenaModeBootstrap.EnemyDisplayCapacity => arenaView.Config.maxAlive` 는 이미 커밋됨. 프리팹만 생기면 Arena HUD 즉시 동작. (`HudSetupTool` 을 Arena용으로 확장 가능)

### B. 미래 (설계상 범위 밖으로 의도적 보류)
- [ ] **B-1.** Arena 코어 강화 시스템 (원본 능력·가챠) — `ArenaModeBootstrap` 이 소유할 자리, 현재 미구현. **게임 디자인 결정 필요**
- [ ] **B-2.** 동적 아레나 크기 트리거 연결 — `ArenaModel.Expand/Shrink` + `OnRadiusChanged` 훅은 존재. 호출 사건(보스 등장 등) 미연결
- [ ] **B-3.** 코어 비주얼 (헥사 링·펄스 등) — `ArenaView` 장식으로 분류

---

## 4. 다음 세션 시작 가이드

1. 이 문서에서 남은 TODO 확인 — 현재 **A(Arena HUD 결선)** · **B(미래)** 만 남음
2. **A** 는 designer 작업 — 코드는 준비 완료, `Panel_Arena` 프리팹·결선만 필요
3. B는 게임 디자인 결정이 선행돼야 하므로 사용자 의향 확인 후 진행 (B-2 동적 크기는 훅이 이미 있어 트리거 정의만 필요)
4. 작업 완료 시 해당 TODO 항목 제거, 모두 소진되면 `docs/tasks/done/` 으로 이동
