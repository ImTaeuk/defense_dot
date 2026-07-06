# 이펙트 예열·발동 견고성 설계

**작성일**: 2026-07-05
**상태**: 설계 승인 진행 중 (개선 1 TryGet 방식으로 재확정)
**범위**: 이펙트 풀링 마이그레이션 후속 개선 3건 (코드리뷰·사용자 지적에서 도출)

---

## 1. 배경

이펙트 풀링 마이그레이션(Task 3~5) 이후, 다차원 코드리뷰와 사용자 검토에서 **실패 처리**·**로드 효율**·**함수 책임 분리**에 대한 지적이 나왔다. 이미 반영한 것(soft-lock·dead tower의 `try/finally`)은 "예열 실패 시 정리·장착을 보장"하는 방어였고, 본 설계는 그 방어를 **런타임 발동 경로까지 확장**하고, **예열 로드를 병렬화**하며, **복합 함수를 단일 책임으로 분리**한다.

세 개선은 하나의 철학으로 수렴한다 — **실패를 삼키지 말고, `try`·예외 없이 값으로 다뤄 그 지점만 우아하게 넘긴다. 한 함수는 한 가지만 한다(SRP).**

### 런타임 발동 실패 지점 (현재 예외 → TryGet의 false 로 전환)

| 지점 | 조건 | 성격 |
|---|---|---|
| `PoolManager.Get<T>` (미예열) | 예열 배선 누락 | 개발자 실수 |
| `PoolManager.Get<T>` (컴포넌트 없음) | 프리팹 오구성 | 에셋 오구성 |

이 실패들은 **정상 배선·정상 흐름에선 발생하지 않는다**(PlayMode 60초 검증 시 에러 0). 현재는 예외를 던지지만, 이를 **`try`·예외 없이 반환값(false)으로** 다뤄 해당 발동만 스킵한다.

> `Pool.Get`의 `IPoolableObject` 없음 예외는 **예열 시점(Prewarm)에서만** 발생한다(런타임 Get은 예열로 검증된 풀만 다루므로 안전). 이는 프리팹 루트 미구성이라는 극히 드문 개발 오류이므로 예외를 유지한다.

---

## 2. 개선 1 — 발동 실패의 우아한 스킵 (TryGet 패턴)

**대상 파일**: `PoolManager.cs`, `PooledEffectSpawner.cs`, `ProjectileAbilityData.cs`, `OrbitalAbilityData.cs`, `AreaWaveAbilityData.cs`

### 결정 사항 (승인됨 — 사용자 선택)
- **`try`·예외를 쓰지 않는다.** `Get<T>`(예외 던짐)를 `TryGet<T>(out)`(bool 반환)으로 전환
- 실패는 값으로: `TryGet`이 실패 시 `false` + 원인 로그, 성공 시 `true` + `out`
- 발동 지점(각 능력 `Fire`/`OnEquip`)이 `if (fx == null) return;`으로 그 발동만 스킵
- **`AbilityRunner`는 변경 없음** — 예외가 안 나므로 격리(try-catch)도, 실패 명단(disabled)도 불필요

### 동작
```csharp
// PoolManager — Get<T>(예외) → TryGet<T>(bool + out)
public bool TryGet<T>(AssetReference reference, out T item, object owner = null)
    where T : Component, IPoolableObject
{
    item = null;
    if (!pools.TryGetValue(reference.RuntimeKey, out Pool pool))
    {
        UnityEngine.Debug.LogWarning($"예열되지 않은 풀: {reference.RuntimeKey}");
        return false;
    }
    GameObject instance = pool.Get();            // 예열로 검증된 풀 → IPoolableObject 보장
    item = instance.GetComponent<T>();
    if (item == null)
    {
        pool.Return(instance);                   // 누수 방지
        UnityEngine.Debug.LogWarning($"프리팹에 {typeof(T).Name} 없음");
        return false;
    }
    Retain(item, pool, owner);
    return true;
}

// PooledEffectSpawner — 실패를 값으로 전달
public T Spawn<T>(AssetReferenceGameObject asset) where T : AbilityEffect
{
    if (!pool.TryGet<T>(asset, out T fx)) return null;   // 실패 시 null
    fx.Bind(this);
    return fx;
}
public void PlayOneShot(AssetReferenceGameObject asset, Vector3 pos, Quaternion rot)
{
    if (!pool.TryGet<VfxPlayer>(asset, out VfxPlayer vp)) return;   // 실패 시 스킵
    vp.transform.SetPositionAndRotation(pos, rot);
    vp.PlayThenReturn();
}

// 각 능력 Fire/OnEquip — 스폰 실패 시 그 발동만 무산 (try 없음)
ProjectileEffect fx = ctx.Effects.Spawn<ProjectileEffect>(projectileAsset);
if (fx == null) return;
fx.Activate(...);
```

### 효과
- `try`·예외 없이, 실패를 `Dictionary.TryGetValue` 같은 **C# 표준 관용구**로 처리
- 한 능력의 스폰 실패 = 그 발동만 스킵, 다른 능력·게임은 정상 (A안의 "뒤 능력 막힘" 부작용 없음)
- Task 5-A의 `Get<T>` 예외 가드는 `TryGet`의 `false + 로그`로 흡수

### 변경 범위
| 파일 | 변경 |
|---|---|
| `PoolManager.cs` | `Get<T>` → `TryGet<T>(out)` |
| `PooledEffectSpawner.cs` | `Spawn`(실패 null) · `PlayOneShot`(실패 스킵) |
| `ProjectileAbilityData.cs` L45 | `Spawn` 결과 null 체크 |
| `OrbitalAbilityData.cs` L29 | `Spawn` 결과 null 체크 |
| `AreaWaveAbilityData.cs` L38 | `Spawn` 결과 null 체크 |

---

## 3. 개선 2 — 병렬 예열 로드

**대상 파일**: `Assets/Scripts/Core/Pooling/PoolManager.cs` (`WarmupAsync`)

### 결정 사항 (승인됨)
- 현재 `foreach { await WarmupOneAsync }` (순차) → `UniTask.WhenAll` (병렬)
- 배치 내 중복 가드: `RuntimeKey` 기준 `HashSet<object> seen`

### 동작
```csharp
public async UniTask WarmupAsync(IEnumerable<AssetReferenceGameObject> assets, int count = 3)
{
    using (UnityEngine.Pool.HashSetPool<object>.Get(out HashSet<object> seen))
    using (UnityEngine.Pool.ListPool<UniTask>.Get(out List<UniTask> tasks))
    {
        foreach (AssetReferenceGameObject asset in assets)
        {
            object key = asset.RuntimeKey;
            if (pools.ContainsKey(key) || !seen.Add(key)) continue;  // 예열됨 or 배치 내 중복
            tasks.Add(WarmupOneAsync(asset, count));
        }
        await UniTask.WhenAll(tasks);
    }
}
```

### 방식 근거 (judge — Unity 공식 문서 확인)
- **`UniTask.WhenAll`(개별 병렬) 채택**: 각 프리팹마다 별도 `Pool`을 생성·배선해야 하므로 로드 후 개별 제어가 필요
- **`Addressables.LoadAssetsAsync`(배치) 기각**: 결과를 `IList<T>` 하나로만 반환 → 프리팹별 `Pool` 생성 배선이 어려움
- **경합 가드**: 서로 다른 `AssetReference`가 같은 `RuntimeKey`를 가리키면 배치 내 중복 로드 가능 → `seen`으로 차단

### 실익 범위 (개발 단계 관점)
- 현재 예열 대상 5개는 모두 `Default Local Group`(로컬, 소수)이라 **당장의 로드 시간 이득은 작다**. 이는 프로젝트가 초기라 그룹이 하나뿐이기 때문이지, 병렬화가 무의미하다는 뜻은 아니다.
- 콘텐츠가 늘며 **그룹 분리·원격(CDN) 번들**이 도입되면 순차 await는 번들 로드가 직렬 누적되어 병렬 대비 확연히 느려진다.
- 따라서 **지금 병렬 구조와 경합 가드를 잡아두는 것이 합리적**이다.

---

## 4. 개선 3 — `WarmupAndEquipAsync` 분리 (컨벤션 적용)

**대상 파일**: `CoreAbilitySystem.cs`, `ArenaModeBootstrap.cs`

### 배경 (사용자 지적)
`WarmupAndEquipAsync`는 "예열(Warmup) + 장착(Equip)" 두 동작을 한 함수에 묶은 복합 함수다. 함수명의 "And"는 둘 이상의 책임을 진다는 SRP 위반 신호다.

### 결정 사항 (승인됨 — 프로젝트 컨벤션으로 확립)
- 컴포넌트(`CoreAbilitySystem`) API는 **단일 책임 메서드만 노출**: `WarmupStartersAsync`(예열) / `EquipAll`(장착)
- **순서 조율은 호출부(합성 루트 `ArenaModeBootstrap`)가** 목적지향 메서드로 표현
- 컨벤션 저장: `convention-no-composite-and-functions`

### 동작
```csharp
// CoreAbilitySystem — 단일 책임 2개
public async UniTask WarmupStartersAsync() { ...예열만... }
public void EquipAll() => runner?.EquipAll();   // 장착만 노출

// ArenaModeBootstrap — 순서 조율은 호출부 책임 (목적지향 이름)
StartCoreAbilities(coreAbility).Forget();

private static async UniTaskVoid StartCoreAbilities(CoreAbilitySystem core)
{
    try { await core.WarmupStartersAsync(); }
    finally { core.EquipAll(); }   // 예열 실패해도 장착 보장(dead tower 방지)
}
```

> 이 `try/finally`는 개선 1(발동)과 성격이 다르다 — 발동은 매 프레임 반복이라 값 기반(TryGet)이 맞지만, 여기 예열은 **1회성 순차 의존(예열→장착)** 이라 "예열이 실패해도 장착은 반드시"를 보장하는 `finally`가 자연스럽다. (사용자 확인 필요: 이 한 곳의 `try/finally`도 지양할지)

### 효과
- 컴포넌트 API가 단일 책임 → 재사용·단위 테스트 용이, 의도 명확
- dead tower 방지는 **조율 지점(호출부)** 으로 이동

---

## 5. 검증 계획

| 검증 | 방법 | 기대 |
|---|---|---|
| 회귀 | EditMode 136/136 | 그린 유지 |
| 컴파일 | refresh_unity → read_console | 에러 0 |
| 정상 발동 | PlayMode 진입 → 발사·오비탈·명중 VFX | 기존과 동일(TryGet 정상 경로 불변) |
| 실패 스킵 | (선택) 의도적 미배선 능력 발동 | 그 능력만 스킵 + 로그, 다른 능력·게임 정상 |
| 병렬 로드 | PlayMode 진입 예열 | 정상 예열, 콘솔 에러 0 |
| 분리 배선 | PlayMode 진입 → 스타터 예열·장착 | 오비탈 즉시 등장(순서 유지), 에러 0 |

> 세 개선 모두 **정상 배선 케이스에서는 동작이 불변**하므로 회귀 위험이 낮다.

---

## 6. 비고

- **별도 태스크(TASK-016)**: `ArenaModeBootstrap`의 `TowerBehaviorTree` 런타임 `Destroy` — 코어 전용 프리팹으로 정리. 본 마이그레이션과 무관.
- Task 5-A에서 넣은 `Get<T>` 예외 가드는 `TryGet<T>`의 `false + 로그`로 대체된다(예외 → 값).
- `Get<T>`의 호출자는 `PooledEffectSpawner`뿐이고 EditMode 테스트는 `Retain` seam으로 우회하므로, `Get<T>` → `TryGet<T>` 전환은 테스트 회귀가 없다.
