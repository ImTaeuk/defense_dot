# TASK-015: Addressables 에셋 참조 인프라 도입

**작성일**: 2026-07-01
**상태**: 구현 완료 (2026-07-01) — AssetLoader·패키지·샘플 검증 완료. 후속 TASK-013(풀링) 대기
**우선순위**: **높음 (상)** — 풀링·VFX 작업의 최선행 인프라

> 설계·핸드오프 전문: `docs/superpowers/specs/2026-07-01-pooling-addressables-design.md`
> **확정 설계(정련본)**: `docs/superpowers/specs/2026-07-01-addressables-asset-loader-design.md`(+HTML) / 계획: `docs/superpowers/plans/2026-07-01-addressables-asset-loader.md`

> **구현 완료 요약(2026-07-01, 커밋)**: `com.unity.addressables` 3.1.0 + `Boot`(앱수명)/`Arena`(런수명) 그룹. `AssetLoader`(`LoadAsync`→`UniTask<GameObject>` / `Release` / `ReleaseAll`, RuntimeKey 기준 핸들 dedup) 구현. `Hit_Water` 를 Addressable(Arena, 라벨 vfx/hit)로 마킹해 PlayMode 왕복 검증(loadOk·dedup=1·release=0), EditMode 127/127. 브레인스토밍 정련: Provisioner 폐기→`PoolManager.PreloadAsync`(TASK-013), `EffectType` enum + `EffectEntry[]`, 그룹=로드스코프·라벨=용도 직교. 남은 것: C-2(Addressables Build 산출)는 배포 시점 처리.

---

## 1. 배경 / 출발점 (맥락 체인)

이 작업은 **"피격 이펙트가 안 보인다"는 코멘트에서 시작**됐다. 추적 과정에서 의존 체인이 드러났다:

```
피격 이펙트 안 보임 (TASK-014 B-3, 증상)
   └─ SpawnOneShot 이 Instantiate/Destroy → 공용 풀링 필요 (TASK-013)
        └─ 풀이 프리팹을 강한참조/SerializeField 집중 보유는 안티패턴
             └─ 프리팹 약한참조 = Addressables 필요 (본 TASK, 최선행)
```

즉 작은 이펙트 이슈가 **에셋 로딩 전략이라는 인프라 결정으로 자연 확장**됐고, "Repository 가 모든 프리팹을 강하게 들고 있으면 전부 메모리 상주 + 단일 결합 지점"이라는 사용자 우려가 Addressables 도입의 직접 계기다.

## 2. 문제 정의

- 프로젝트에 **Addressables 미도입**(`Packages/manifest.json` 부재).
- 현재 프리팹 참조는 ScriptableObject(예: `EnemyData.prefab`)가 **강한참조로 분산 보유** → 참조 에셋이 항상 메모리.
- 공용 풀링(TASK-013)의 `PoolRepository` 가 프리팹을 약하게 참조하려면 `AssetReferenceGameObject`(Addressables)가 전제.

## 3. 범위 (본 TASK)

- A. `com.unity.addressables` 패키지 도입 (`manifest.json`).
- B. 그룹·라벨·번들 정책 결정 (로컬 빌드 기준, 원격 CDN 은 범위 외).
- C. `AssetReferenceGameObject` 로드/언로드 래퍼 작성 — **UniTask 기반**(`.ToUniTask()`), Coroutine·Task 금지.
- D. 로드 실패·해제 누락 방지(핸들 추적) 설계.
- E. 빌드(Addressables Build)·플레이 검증 — 약한참조 lazy 로드/언로드 동작 확인.

## 4. TODO

- **A. 패키지 도입**
  - A-1. `manifest.json` 에 `com.unity.addressables` 추가, 초기 설정(Default Group) 생성.
- **B. 로드 래퍼**
  - B-1. `AssetReferenceGameObject` → `UniTask<GameObject>` 로드 헬퍼.
  - B-2. 핸들 추적·언로드(Release) API.
- **C. 검증**
  - C-1. 샘플 프리팹 1종을 Addressable 로 마킹 → 로드/언로드 왕복 검증.
  - C-2. Addressables Build 포함 빌드 산출 확인.

## 5. 영향도 / 위험도

| 항목 | 내용 |
|---|---|
| 변경 규모 | 패키지 + 인프라 신규 (기존 코드 영향 적음, 마이그레이션은 TASK-013에서) |
| 파급 | `Pull<T>()` 가 비동기(`UniTask<T>`)가 됨 — TASK-013 설계에 반영 |
| 위험 | Addressables 초기 설정 오류 시 빌드 깨짐 — 샘플 1종으로 선검증 후 확대 |

## 6. 후속 의존

- **TASK-013**(공용 풀링): 본 TASK 완료 후 `PrefabCreator<T>` 가 `AssetReferenceGameObject` 로 프리팹 로드.
- **TASK-014 B-3**(피격 VFX): 풀링 위에서 Hit_Water 풀링·확대.
