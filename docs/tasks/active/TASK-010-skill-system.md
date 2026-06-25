# TASK-010: Skill 시스템 (원작 가챠·능력)

**작성일**: 2026-06-11 (구 TASK-005 C 분리)
**상태**: 분석 완료 (백로그)
**우선순위**: 중간 (큼)
**출처**: 구 TASK-005 타워 시스템 C

---

## 1. 개요

공격 방식을 **Skill** 이라는 이름의 능력으로 분리·정식화한다. 원작 dot-defense 의 타워 건설은 곧 **가챠+능력** — 빈 슬롯 클릭 시 3장 카드 뽑기(가중 등급 R/SR/SSR), 골드는 뽑기 시점 차감, 카드 = 능력(ability). 현재 단일/범위/투사체는 `TowerActor` 의 디버그 토글(구 TASK-004)로 임시 구현된 상태.

> 원작 근거: `index.html` `openTowerPlaceModal`(L14929, 3장 카드), `tdRollWeightedCards`, `placeTower(slot, abilityId, cost)`.

## 2. TODO

### A. 데이터·획득·장착
- [ ] **A-1.** Skill 데이터(SO) + 획득(드랍/레벨업/가챠) + 장착(타워에 Skill 부착)
- [ ] **A-2.** **디버그 공격 behavior 3종(단일/범위/투사체)을 Skill 로 대체·삭제** (`SingleTargetAttack`/`AoeAttack`/`ProjectileAttack`/`DebugProjectile` + `TowerActor` 토글 제거)
- [ ] **A-3.** `aoeRadius` 등 디버그용 `TowerData` 임시 필드를 Skill 데이터로 이관

### B. 가챠·강화
- [ ] **B-1.** 슬롯 클릭 → 3장 카드 뽑기 모달 (SP1 빌드 모달의 고정 목록을 가챠로 교체, 슬롯별 카드 캐시·무한 리롤 방지)
- [ ] **B-2.** Skill 강화/조합 (원작 능력 카드 합성·레벨업)

## 3. 선행/연계

- SP1(TASK-005) 의 슬롯→모달→설치 파이프라인 위에 **모달 내용만 가챠로 교체**하면 수렴.
- Arena 중앙 타워의 능력 장착(구 TASK-005 A-2)도 이 시스템에 묶임.
- 메타 상점(TASK-011)과 자원·강화 측면에서 연계.

## 참고
- 레퍼런스: `Assets/Reference/dot-defense-main/.../index.html`
- 상위 분리: [TASK-005 Grid 타워 배치](TASK-005-tower-placement-system.md)
