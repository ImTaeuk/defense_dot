# TASK-005: 타워 등장·배치 정식 시스템 (백로그)

**작성일**: 2026-06-09
**상태**: 분석 완료 (백로그 — 추후 작업)
**우선순위**: 중간 (코어 루프·치트 검증 이후)

---

## 1. 문제 정의

원작은 모드별로 타워가 다르게 등장하나, 현재 **전부 미구현**(검증 완료):

| 기능 | 원작 | 현재 |
|---|---|---|
| Arena 중앙 단일 타워 + 능력 카드 장착 | 있음 | ❌ `ArenaModeBootstrap`에 타워 없음 |
| Grid 슬롯 좌클릭 배치 | 있음 | ⚠ `TowerPlacementController`(P0 단일 타워) **미결선** |
| 타워 구매/선택 UI | 있음 | ❌ 컨트롤러는 타워 1종 하드코딩, UI 전무 |
| 능력 카드 시스템 (인게임 공격 획득) | 있음 | ❌ |
| 메타 상점(강화소, 스타더스트) | 있음 | ❌ (장기) |

## 2. 원작 사양 (레퍼런스 근거)

`Assets/Reference/dot-defense-main/.../index.html`:
- `drawDefenseTowers()`(defense 타워 다수), `state.tower`(아레나 중앙 타워), `state.selectedTower`(타워 선택/강화 모달)
- `능력 카드`(인게임 공격 획득·장착), `#shop`(강화소 — 스타더스트 영구 메타)

## 3. TODO (카테고리)

### A. Arena 중앙 타워 ⭐ **치트 툴 직후 즉시** (가벼움)
- [ ] **A-1.** `ArenaModeBootstrap`이 중앙에 단일 타워 배치 + 의존성(targetFinder/data) 주입 — 치트로 관찰하던 "중앙 타워 1개"의 정식 버전
- [ ] **A-2.** 타워에 능력(공격 behavior) 장착 슬롯

### B. Grid 슬롯 배치 + 구매
- [ ] **B-1.** `GridDefenseModeBootstrap` + `TowerPlacementController` 씬 결선 (모드 분기 정상화)
- [ ] **B-2.** 타워 구매/선택 UI (현 P0 단일 → 다종 선택·골드 비용)
- [ ] **B-3.** `TowerData` 다종 + 비용 밸런싱

### C. 능력 카드 시스템
- [ ] **C-1.** 능력 카드 데이터·획득(드랍/레벨업)·장착 — **TASK-004 디버그 공격 behavior 3종을 이 시점에 삭제(대체)**

### D. (장기) 메타 상점(강화소)
- [ ] **D-1.** 스타더스트 영구 업그레이드 (원작 자원/전투력/빌드/생존/디펜스 카테고리)

## 4. 선행/연계

- **치트 툴**(별도 브레인스토밍 진행 중)로 본 시스템 완성 전까지 플레이테스트 대체.
- **디버그 공격 behavior**(TASK-004)는 C-1(능력 카드) 구현 시 throwaway 삭제.
- 새 기능 다수 포함 → 착수 시 항목별 `superpowers:brainstorming`으로 설계.

## 참고 문서
- 상위: [TASK-002 플레이 루프](TASK-002-play-loop-completion.md) · [TASK-004 디버그 공격](TASK-004-debug-attack-types-resume.md)
- 레퍼런스: `Assets/Reference/dot-defense-main`
