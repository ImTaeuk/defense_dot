# TASK-004: 디버그 공격 타입 구현 재개 (Task 3~6)

**작성일**: 2026-06-08
**상태**: 진행 중 (Task 1~2 완료 · 3~6 남음)
**우선순위**: 높음
**재개 예정**: 2026-06-09

---

## 0. 재개 프로토콜 (먼저 읽기)

- **브랜치**: **`main`** 에서 작업. (사용자가 feature/arena-map-system → main 병합 후 main에서 재개 결정)
- **커밋 정책**: **구현만 — 태스크별 커밋 금지.** 전부 끝나면 diff 제시 → 사용자 **명시 승인** → `commit` 스킬로 scoped 일괄 커밋. (CLAUDE.md "명시적 요청 없이 커밋 금지" + 하네스가 무단 커밋을 경고한 이력 있음)
- **Unity 실행 불가**: Claude 세션은 컴파일/EditMode/PlayMode 를 **돌릴 수 없음**. 코드만 작성하고 **"테스트 통과/컴파일 됨" 등 미검증 주장 금지.** 검증은 사용자 Unity 에서.
- **scoped staging**: `git add .` / `-A` 금지 — 만든 파일 경로만 명시 (현재 main 작업 트리에 다른 미커밋 변경이 섞일 수 있음).
- **신규 `.cs`**: Unity 미실행이므로 `.cs.meta` 를 직접 생성(충돌 검사한 32-hex GUID). 폴더 신설 시 폴더 `.meta` 도.
- **네임스페이스**: `DefenseDot.Systems.Tower.Debugging` — **`.Debug` 금지**(`UnityEngine.Debug` 충돌).
- **상세 코드**: 아래 각 항목은 [구현 계획](../../superpowers/plans/2026-06-08-debug-attack-types.md) 의 Task 3~6 에 완전한 코드가 있음. 그대로 사용.

---

## 1. 현재 상태 (`main` = `2dd31a43`)

완료·커밋됨:
- HD-2D Phase 1 중앙 주시 카메라 리그 (`529441c8`, 사용자)
- 공격 타입 전략 계약 `IAttackBehavior` / `AttackContext` (`ce061739`, Task 2)
- `TargetFinder.FindAllInRange` + EditMode 테스트 (`1a58371c`, Task 1)
- 코어 도달 피해 → `EnemyData.coreDamage` 분리 (`f7170de7`)
- 테스트 타워 프리팹·데이터 (`b4b6ea60`) · 테스트 맵(TestMap) 구성 (`2dd31a43`)

> main 은 `origin/main` 보다 6커밋 앞섬 (로컬 전용, 미push).

---

## 2. 남은 TODO

### A. 구현 (구현만 · 커밋은 C 에서 일괄)
- [ ] **A-1.** `SingleTargetAttack` — 최근접 1체 즉시 데미지 + 디버그 라인 (계획 Task 3)
- [ ] **A-2.** `AoeAttack` — 반경(=attackRange) 내 전체 데미지 + 원 비주얼, **+ EditMode 테스트** (계획 Task 4)
- [ ] **A-3.** `ProjectileAttack` + `DebugProjectile` — 코드 생성 구가 타겟으로 이동→도달 데미지(관통) (계획 Task 5)
- [ ] **A-4.** `TowerActor` 통합 — `PerformAttack` 을 활성 behavior 리스트 위임으로 교체 + 인스펙터 토글 3종(`debugSingle/debugAoe/debugProjectile`) (계획 Task 6)

### B. 검증 (사용자 Unity · A 이후)
- [ ] **B-1.** 컴파일 무결 + EditMode 전체(기존 9 + FindAllInRange + AoE) PASS 확인
- [ ] **B-2.** PlayMode: 단일(라인)·범위(원)·투사체 각각 동작, 런타임 토글 add/remove 즉시 반영, 적 처치→골드·코어 피해→체력·웨이브·승패까지 풀 루프 (계획 Task 7 V1~V5)
> Game 뷰에서 라인/원은 우상단 **Gizmos 토글** 필요. 투사체는 실제 오브젝트라 무관.

### C. 일괄 커밋 (B 통과 후 · 명시 승인 하)
- [ ] **C-1.** `commit` 스킬로 A 의 신규/수정 파일만 scoped 커밋 (`.cs` 포함 → lint 는 **본 작업 파일만** 범위로)

---

## 3. 이후 (본 TODO 범위 밖 · 참고)

- **플레이 루프 잔여** ([TASK-002](TASK-002-play-loop-completion.md)): P1 결과 UI + 재시작, F3 Arena 무한화(유한 웨이브 "승리" 오발생 수정).
- 실제 **능력 카드 시스템** 구현 시 본 디버그 3타입은 **삭제**(throwaway 전제).

---

## 참고 문서
- 구현 계획(완전한 코드): [2026-06-08-debug-attack-types.md](../../superpowers/plans/2026-06-08-debug-attack-types.md)
- 설계: [2026-06-08-debug-attack-types-design.md](../../superpowers/specs/2026-06-08-debug-attack-types-design.md)
- 상위 맥락: [TASK-002 플레이 루프 완성](TASK-002-play-loop-completion.md)
