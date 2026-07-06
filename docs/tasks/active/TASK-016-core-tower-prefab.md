# TASK-016: 코어 전용 프리팹 정리 (런타임 컴포넌트 Destroy 제거)

**작성일**: 2026-07-05
**상태**: 분석 완료 (구현 대기)
**우선순위**: 중간
**출처**: 이펙트 풀링 마이그레이션 코드 리뷰 중 사용자 지적

---

## 1. 문제 정의

아레나 코어 타워를 생성할 때, **일반 타워 프리팹(`centerTowerData.prefab`)을 재활용**하면서 코어에 맞지 않는 컴포넌트를 **런타임에 뜯어내고** 능력 시스템을 붙인다.

```csharp
// ArenaModeBootstrap.SpawnCenterTower() L87-90
TowerBehaviorTree debugBt = go.GetComponent<TowerBehaviorTree>();
if (debugBt != null) Destroy(debugBt);           // ← 일반 타워의 단일공격 AI 제거
coreAbility = go.AddComponent<CoreAbilitySystem>();  // ← 능력 시스템 부착
```

### 1.1 근본 원인
- **코어 전용 프리팹이 없다.** 일반 타워 프리팹을 빌려 쓰고, 코어에 안 맞는 `TowerBehaviorTree`(사거리 내 단일 공격 AI, [TowerBehaviorTree.cs](../../../Assets/Scripts/Systems/Tower/TowerBehaviorTree.cs))를 런타임에 제거한다.
- 즉 **프리팹 구성의 오류를 코드로 교정하는 땜빵**이다. `git log`상 `75d67b85`(Arena 씬 통합) 때 도입됐고, `centerTowerData` 주석도 "(추후 선택 UI 주입점)"으로 미완성임을 명시한다.

### 1.2 안티패턴
- 런타임 `GetComponent` + `Destroy`로 프리팹에 딸려온 컴포넌트를 제거 → 프리팹만 봐서는 실제 런타임 구성이 무엇인지 알 수 없다(가시성 저하).
- 코어 비주얼도 별도 `Aris_CoreTower` 프리팹을 붙이고 원본 스프라이트를 숨기는(`SetupArisVisual`) 이중 구조 → 재활용의 흔적.

## 2. 해결 방안

### 방안 A (권장) — 코어 전용 프리팹 배선
- 코어 전용 프리팹을 신규 제작(또는 타워 프리팹 variant): `TowerBehaviorTree` **없이** 필요한 컴포넌트만 구성.
- `ArenaModeBootstrap`에 `[SerializeField] private GameObject coreTowerPrefab;` 추가, 이를 인스턴스화.
- 런타임 `Destroy(debugBt)` **제거**. `CoreAbilitySystem`은 프리팹에 미리 포함하거나 명시적으로 부착.

### 방안 B — 기존 프리팹 정리
- `centerTowerData.prefab`에서 에디터로 `TowerBehaviorTree`를 빼고, 코어/일반 타워 용도를 데이터로 분기.

> 방안 A가 "코어와 일반 타워의 책임 분리"에 더 명확. 코어는 능력 시스템, 일반 타워는 단일 공격으로 프리팹 단계에서 구분.

## 3. TODO

- [ ] A-1. 코어 전용 프리팹 제작 (에디터/디자이너) — `TowerBehaviorTree` 제외, 필요한 코어 컴포넌트 구성
- [ ] A-2. `ArenaModeBootstrap`에 `coreTowerPrefab` 필드 추가 + 인스턴스화 경로 전환
- [ ] A-3. 런타임 `GetComponent<TowerBehaviorTree>` + `Destroy` 제거
- [ ] A-4. `Aris_CoreTower` 이중 비주얼 구조 재검토(코어 프리팹에 통합 가능한지)
- [ ] A-5. PlayMode 검증 — 코어가 능력으로 정상 공격, 단일공격 AI 잔재 없음

## 4. 영향도

| 항목 | 분석 |
|---|---|
| 변경 규모 | 프리팹 1개 신설 + `ArenaModeBootstrap` 수정 |
| 위험도 | 중간 — 코어 생성 경로 변경, PlayMode 검증 필요 |
| 이펙트 풀링 마이그레이션과의 관계 | **무관** — 독립 실행 가능 |

## 5. 비고
- 본 태스크는 이펙트 풀링 마이그레이션(예열·발동 견고성)과 분리됐다. 마이그레이션 커밋에 포함하지 않는다.
