# 디버그용 공격 타입 3종 설계 (단일/범위/투사체)

**작성일**: 2026-06-08
**상태**: 설계 승인됨 (스펙 사용자 검토 대기)
**브랜치**: `feature/arena-map-system`
**선행 문서**: [플레이 루프 완성](../../tasks/active/TASK-002-play-loop-completion.md) · [HD-2D 비주얼 설계](2026-06-07-hd2d-visual-design.md)

---

## 1. 목표 & 스코프

플레이 루프가 실제로 도는지 **런타임에 눈으로 검증**하기 위해, 타워에 **공격 타입 3종(단일/범위/투사체)** 을 디버그용으로 구현한다. 개발자는 플레이 중 타워에 각 타입을 **하나씩 추가/삭제**(능력 카드 스택 느낌)하며 루프를 확인한다.

**확정 결정**
| # | 항목 | 결정 |
|---|---|---|
| D1 | 아키텍처 | `IAttackBehavior` 전략 패턴 — 기존 `IMovementStrategy`(이동)와 동형 |
| D2 | 타입 3종 | 단일 타겟(즉시) / 범위(반경 내 전체) / 투사체(이동→도달 데미지) |
| D3 | 런타임 토글 | `TowerActor`의 인스펙터 bool(`debugSingle/debugAoe/debugProjectile`)을 플레이 중 on/off → 해당 behavior add/remove |
| D4 | 디버그 비주얼 | 단일=라인 / 범위=원(세그먼트) / 투사체=이동하는 구 자체 |
| D5 | 영속 데이터 | `TowerData` **불변** — 디버그 파라미터(투사체 속도 등)는 behavior가 자체 보유 |
| D6 | 수명 | **throwaway** — 실제 능력 시스템 구현 시 3 behavior + 토글 전부 삭제. 모든 신규 코드에 `// DEBUG` 표식 |

---

## 2. 범위 밖 (TODO로 명시)

- **능력 카드 획득 시스템**(UI·로직·프리팹) — 원작의 "공격 방식 + 이펙트 = 능력 카드". 별도 TODO
- 데미지 숫자 팝업
- 실제 VFX/파티클 연출 — HD-2D 작업(특히 §4.3 빌보드 P3 `SpriteActorView` attack 애니메이션) 이후
- **재화 획득 로직은 이미 동작**(`CombatModel.RegisterKill → EconomyController → AddGold`) → 변경 없음

---

## 3. 아키텍처

기존 이동 전략(`IMovementStrategy`)과 동형. 공격을 behavior 단위로 분리하고, 타워는 활성 behavior **리스트**를 순회 실행한다.

```
TowerActor.Update → CombatLogic.Tick(쿨다운 경과?) → PerformAttack()
   → foreach (활성 IAttackBehavior b) b.Execute(ctx)
        SingleTargetAttack : finder.FindNearest → TakeDamage(damage)         + DrawLine
        AoeAttack          : 반경(=attackRange) 내 전체 → TakeDamage(damage)  + DrawCircle
        ProjectileAttack   : 디버그 구 발사 → 이동→도달 시 TakeDamage(관통)   + 구 이동이 곧 비주얼
```

활성 behavior 리스트는 각 공격 시 D3 토글 bool 상태를 읽어 구성한다(공격 빈도 ~1/초라 비용 무시 가능).

---

## 4. 컴포넌트

### 4.1 신규 (전부 throwaway · `// DEBUG`)

| 구성 | 위치(안) | 책임 |
|---|---|---|
| `IAttackBehavior` | `Systems/Tower/Debug/` | `Execute(in AttackContext ctx)` + 디버그 드로. 공격 1회 수행 계약 |
| `SingleTargetAttack` | 〃 | 최근접 1체 즉시 데미지(현 `PerformAttack` 로직 이관) + 라인 |
| `AoeAttack` | 〃 | 반경 내 전체 즉시 데미지 + 원(세그먼트 라인). 반경 = `TowerData.attackRange` |
| `ProjectileAttack` | 〃 | 디버그 투사체 발사. 관통 수 등은 behavior 필드 |
| `DebugProjectile` | 〃 | 코드 생성 구(`GameObject.CreatePrimitive`)에 부착되는 mover. 타겟으로 이동, 근접 시 `TakeDamage`, 관통/수명 종료 시 파괴. **에셋 없음** |

`AttackContext`: 공격 1회에 필요한 입력 묶음(타워 위치, 주 타겟, 타겟 질의 수단, `TowerData`). behavior가 host MonoBehaviour 참조가 필요하면(투사체 spawn) 함께 전달.

### 4.2 수정 (최소)

| 파일 | 변경 |
|---|---|
| `Systems/Tower/TowerActor.cs` | `PerformAttack`이 활성 `List<IAttackBehavior>` 순회 실행으로 위임. 인스펙터 토글 3종(`debugSingle/debugAoe/debugProjectile`) → 활성 리스트 재구성. 기존 단일 로직은 `SingleTargetAttack`로 이관 |
| `Systems/Tower/TargetFinder.cs` | 범위 질의용 `FindAllInRange(origin, range, results)` 추가(풀링 리스트 출력). `FindNearest`와 같은 레지스트리 순회 — 일반 유틸이라 영속 허용 |

### 4.3 불변
- `TowerData`, 도메인 모델 5종, `CombatModel`/`EconomyController`, HUD, 9개 EditMode 테스트

---

## 5. 디버그 비주얼

| 타입 | 비주얼 | 구현 |
|---|---|---|
| 단일 | 타워→타겟 라인 | `Debug.DrawLine`(짧은 duration) |
| 범위 | 타워 중심 원(반경) | 세그먼트 `Debug.DrawLine` 루프 |
| 투사체 | 이동하는 구 | 실제 GameObject(코드 생성) — Game 뷰에 그대로 보임 |

- 단일/범위는 Game 뷰 우상단 **Gizmos 토글** 시 표시. 투사체는 실제 오브젝트라 토글 무관.
- 색상으로 타입 구분(예: 단일=cyan, 범위=magenta, 투사체=구 머티리얼).

---

## 6. 런타임 토글 흐름 (D3)

1. 타워 프리팹/인스턴스 인스펙터에 `debugSingle/debugAoe/debugProjectile` 3 bool 노출(기본 단일만 true).
2. 플레이 중 bool 변경 → `TowerActor`가 활성 behavior 리스트를 재구성(켜진 타입만 포함).
3. 다음 공격 틱부터 켜진 타입 전부 실행 → 루프에 미치는 효과를 즉시 관찰.
4. "하나씩 추가/삭제" = 카드 장착/해제처럼 토글.

---

## 7. 테스트

- **회귀**: 도메인·`TowerData` 시그니처 불변 → 기존 9개 EditMode 회귀 0.
- **신규 EditMode(순수 로직)**: `AoeAttack`의 반경 내 판정(경계값), `TargetFinder.FindAllInRange` 결과 집합. MonoBehaviour 의존 없는 계산부만.
- **수동/PlayMode**: 단일→라인+처치, 범위→반경 내 동시 처치+원, 투사체→구 이동→도달 데미지(관통), 런타임 토글 add/remove 즉시 반영.

---

## 8. 컨벤션 / 정리

- 네임스페이스 `DefenseDot.Systems.Tower`(또는 하위 `.Debug`), 기존 규칙 일치.
- 모든 멤버 명시적 접근 제한자(IDE0040), private 필드 순수 camelCase, `System.*` 풀패스, 임시 컬렉션은 `CollectionPool`.
- 비동기 불필요(투사체는 mover의 `Update` 기반). Coroutine/Task 금지.
- 신규 throwaway 파일·필드는 `// DEBUG` 표식으로 일괄 식별 → 실제 능력 시스템 구현 시 검색·삭제 용이.
- 커밋 전 `lint` 스킬 검증.

---

## 9. 향후 연결 (삭제 시점)

실제 능력 카드 시스템(§2 TODO) 구현 시:
1. `Systems/Tower/Debug/` 의 3 behavior + `DebugProjectile` 삭제
2. `TowerActor`의 `debugSingle/debugAoe/debugProjectile` 토글 제거
3. `IAttackBehavior` 패턴 자체는 실제 능력 시스템이 재사용할 수 있음(판단은 그 시점 설계에서)
