# 이펙트 풀링 마이그레이션 설계 (서브 마이그레이션 ①)

**작성일**: 2026-07-03
**상태**: 설계 (승인 대기)
**선행**: TASK-013 풀링 코어(완료·push, 커밋 68b99482), TASK-015 Addressables(완료)
**후속**: 서브 마이그레이션 ② 액터(Monster/Tower/EnemySpawner)

---

## 0. 목적과 범위

### 0.1 목적
풀링 코어(`Pool` + `PoolManager` + `IPoolableObject`/`IActivatable`/`PooledBehaviour`)를 **이펙트 계층의 실제 소비자**에 연결한다. 이펙트는 지금 `Instantiate`/`Destroy`로 매번 생성·파괴되어 GC를 유발하고, "피격 이펙트 안 보임"의 출발점이었다. 이번 마이그레이션으로 이펙트를 풀링하고, 코어를 **PlayMode로 실검증**하며, 유예된 게이트 **D-5/6/7을 실제 프리팹으로 닫는다.**

### 0.2 범위 (이번 서브 마이그레이션)
- **AbilityEffect 엔티티**: `ProjectileEffect`·`OrbiterSetEffect`·`AreaZoneEffect`(자가구동 효과, `Update`로 시간축 거동).
- **일회성 VFX**: `VfxPlayer.SpawnOneShot` 경로 — 명중 VFX(`hitVfxPrefab`)·머즐 VFX(`muzzlePrefab`).
- **스포너**: `IEffectSpawner`/`SimpleEffectSpawner` → PoolManager 어댑터.
- **데이터**: 능력 정의(`ProjectileAbilityData`·`OrbitalAbilityData`·`AreaWaveAbilityData`)의 이펙트 프리팹 → Addressable.
- **예열**: 능력 획득 시점 증분 예열.

### 0.3 범위 밖
- 액터(Monster/Tower) 풀링, `EnemySpawner` 자체 풀 흡수, `ObjectPool<T>` 제거 → **서브 마이그레이션 ②**.
- 소유 연쇄(owner cascade): 이번엔 이펙트가 전부 **자가반납**. 소유는 액터가 풀링된 뒤(부모가 될 수 있을 때) ②에서 도입.

---

## 1. 확정된 설계 결정 (브레인스토밍 합의)

| # | 결정 | 근거 |
|---|---|---|
| A | 풀 대상 프리팹 **전부 Addressable 전환** | 원래 동기("강참조 상주 금지 → 약한참조")와 정합. 코어의 `AssetReference` 키와 일치. |
| ii | **능력 획득(카드 확정) 시 그 능력 이펙트만 증분 예열** | 낭비 0(가진 능력만). 예열 await는 카드 적용 흐름에서 처리 → 전투 히칭 없음. 코어 동기 Get 유지. |
| b | `AbilityEffect`·`VfxPlayer`가 **`PooledBehaviour` 상속** | 계약 보일러플레이트(활성 토글 + 반납 배선) 중복 제거. 코어 재작업 없음. |
| — | `IEffectSpawner` **유지 + 구현을 PoolManager 어댑터로 교체** | 능력 정의(`ctx.Effects`) 변경 최소, 능력 로직을 풀 메커니즘에서 분리. |
| — | 이펙트 **자가반납**(`Dispose`) | 이펙트가 자기 종료 지점을 안다(`Update`). 코어가 반납 동작 주입(`IReturnBindable`). |

---

## 2. 컴포넌트 설계

### 2.1 데이터 Addressable화 + 능력의 이펙트 자산 열거

능력 정의가 자기 이펙트 프리팹을 **직접 타입 참조**로 들고 있다. 이를 `AssetReferenceGameObject`로 바꾸고, 능력이 자기 **모든** 이펙트 자산을 한 곳에 모아 노출한다(예열 열거용).

**`AbilityData`(베이스)에 추가:**
```csharp
// 이 능력이 (전이적으로) 사용하는 모든 풀링 프리팹. 예열 대상.
public virtual System.Collections.Generic.IEnumerable<AssetReferenceGameObject> EffectAssets
    => System.Array.Empty<AssetReferenceGameObject>();
```

**각 능력 정의의 필드 전환 (예: `ProjectileAbilityData`):**
| 기존 | 신규 |
|---|---|
| `ProjectileEffect projectilePrefab` | `AssetReferenceGameObject projectileAsset` |
| `GameObject muzzlePrefab` | `AssetReferenceGameObject muzzleAsset` |
| (`ProjectileEffect.hitVfxPrefab` — 효과 프리팹 내부) | `AssetReferenceGameObject hitVfxAsset` — **능력으로 끌어올림** |

`EffectAssets`는 `projectileAsset`·`muzzleAsset`·`hitVfxAsset` 중 non-null을 반환. `Orbital`/`AreaWave`도 동일 패턴(각자 자기 엔티티 자산).

> **명중 VFX 끌어올림(합의된 세부):** 명중 VFX가 지금 `ProjectileEffect` 프리팹의 SerializeField라 능력이 그 ref를 열거할 수 없다. 능력이 자기 모든 이펙트 ref를 소유하도록 `hitVfxAsset`을 능력 데이터로 올리고, 발사 시 `ProjectileEffect.Activate(...)`에 넘겨 효과가 명중 순간 일회성 재생하게 한다.

### 2.2 `IEffectSpawner` → `PooledEffectSpawner` (PoolManager 어댑터)

`IEffectSpawner`는 두 가지를 제공한다: **엔티티 스폰**(풀링된 `AbilityEffect`)과 **일회성 VFX 재생**. 구현은 `PoolManager`를 감싼다.

```csharp
public interface IEffectSpawner
{
    // 풀링된 효과 엔티티를 꺼내 스포너를 배선해 돌려준다.
    T Spawn<T>(AssetReferenceGameObject asset) where T : AbilityEffect;

    // 일회성 VFX: 꺼내 위치잡고 재생 → 수명 뒤 자동 반납.
    void PlayOneShot(AssetReferenceGameObject asset, Vector3 pos, Quaternion rot);

    // 효과를 풀로 반납(= fx.Dispose()).
    void Release(AbilityEffect fx);
}
```

`PooledEffectSpawner` 구현:
- `Spawn<T>` → `poolManager.Get<T>(asset)` + `fx.Bind(this)`(효과가 중첩 VFX를 스폰할 수 있도록 스포너 주입).
- `PlayOneShot` → `poolManager.Get<VfxPlayer>(asset)` → transform 세팅 → `vp.PlayThenReturn()`(재생 + 수명 뒤 자가 Dispose).
- `Release(fx)` → `fx.Dispose()`.

`SimpleEffectSpawner`(Instantiate/Destroy)는 **삭제**. `CoreAbilitySystem.Setup`이 `new SimpleEffectSpawner()` 대신 `new PooledEffectSpawner(poolManager)`를 생성. → **Setup 시그니처에 `PoolManager` 추가**(합성 루트가 `GameContext.PoolManager` 전달).

### 2.3 `AbilityEffect : PooledBehaviour`

```csharp
public abstract class AbilityEffect : PooledBehaviour   // was: MonoBehaviour, IPoolable
{
    private IEffectSpawner spawner;                       // 중첩 VFX 스폰용(반납 아님)
    public void Bind(IEffectSpawner effectSpawner) => spawner = effectSpawner;
    protected IEffectSpawner Spawner => spawner;

    // 종료 시 반납. 기존 Release() 를 대체.
    protected void ReturnToPool() => Dispose();           // PooledBehaviour.Dispose = 풀 반납
    // OnSpawn/OnDespawn 은 PooledBehaviour virtual 오버라이드로 서브클래스가 리셋.
}
```
- 반납 경로: 기존 `Release()`(스포너 위임 파괴) → `Dispose()`(코어가 주입한 반납). 서브클래스의 종료 호출부를 `ReturnToPool()`로 교체.
- `Bind`는 **반납이 아니라 중첩 일회성 VFX 스폰**을 위해 유지(예: `ProjectileEffect`가 명중 VFX 재생).

### 2.4 `VfxPlayer : PooledBehaviour` (일회성)

현재 `SpawnOneShot`은 static + `Instantiate`/`Destroy`. 풀링 후:
- VFX 프리팹 루트에 `VfxPlayer`가 **미리** 붙어 있어야 한다(런타임 `AddComponent` 제거). Addressable 프리팹 준비 시 보장.
- `PlayThenReturn()`(인스턴스 메서드): `Play()` → `UniTask.Delay(ResolveDuration())` → `Dispose()`. 반납 시 `OnDespawn`에서 진행 중 delay 취소(재사용 인스턴스 오반납 방지).
- `EnsurePlay`(지속 VFX, AreaZone/Orbiter의 자식) 경로는 **그대로** — 효과 엔티티와 함께 풀링되므로 별도 처리 불필요. 단 `AddComponent` 폴백은 프리팹에 컴포넌트 보장으로 대체.

### 2.5 예열 오케스트레이션

**획득 예열 (증분):** `CoreAbilitySystem.AddAbility(data)`가 성공하면 그 능력의 `EffectAssets`를 예열. `AddAbility`는 동기라 예열은 **카드 적용 흐름에서 await**:
```
카드 선택 확정
  → await poolManager.WarmupAsync(data.EffectAssets, count)   // 이미 있으면 스킵
  → core.AddAbility(data)
  → 전투 재개
```
`CardChoiceApplier`(현재 static·sync)를 **async 경로로** 바꿔 `WarmupAsync`를 await한 뒤 `AddAbility` 호출. 레벨업 카드(기존 능력)는 새 프리팹이 없어 예열 스킵(`WarmupAsync`가 `pools.ContainsKey`로 no-op).

**스타터 예열:** `Setup`이 스타터를 `loadout.TryAdd`로 넣는 경로는 `AddAbility`를 안 거친다. 아레나 진입(첫 웨이브 전)에 스타터 전부의 `EffectAssets`를 모아 한 번 `WarmupAsync`로 예열(await).

### 2.6 게이트 구현 (D-5/6/7) — 실제 프리팹으로 닫기

- **D-5**: `PoolManager.Get<T>` 의 `pools[key]` → `TryGetValue` 실패 시 `InvalidOperationException($"WarmupAsync 예열 안 됨: {asset.RuntimeKey}")`.
- **D-6**: (1) `Pool.Get` 의 `GetComponent<IPoolableObject>()` null → 프리팹명 담은 명시 예외. (2) `PoolManager.Get<T>` 의 `GetComponent<T>()` null → 인스턴스 풀 반납 후 명시 예외(`item==null` 가드, 누수 방지). (3) 이펙트/VFX 프리팹 루트에 `IPoolableObject` 컴포넌트 1개 규약.
- **D-7**: 이펙트 코드에 "반납·`Dispose` 후 참조 사용 금지" 계약 주석. VfxPlayer의 delay 취소가 실사례 방어.

---

## 3. 데이터 흐름 (발사체 능력 예)

```
[획득]  카드 확정 → WarmupAsync(projectile·muzzle·hitVfx) → AddAbility → 재개
[발사]  NotifyFireFrame → ProjectileAbilityData.Fire
          ├ ctx.Effects.Spawn<ProjectileEffect>(projectileAsset)  → Get + Bind + OnSpawn + Activate
          │    └ ProjectileEffect.Activate(..., hitVfxAsset)       // 명중 VFX ref 주입
          └ ctx.Effects.PlayOneShot(muzzleAsset, origin, rot)      → Get<VfxPlayer> + Play + 자동반납
[구동]  ProjectileEffect.Update  → 이동·명중 판정
          └ 명중 시: Spawner.PlayOneShot(hitVfxAsset, pos, rot)     → 명중 VFX
[종료]  관통 소진/사거리 초과 → ReturnToPool()(=Dispose) → OnDespawn → 풀 복귀
```

---

## 4. 테스트 전략

### 4.1 EditMode (Addressables 비의존)
- `PooledEffectSpawner.Release(fx)` → `fx.Dispose()` 호출로 풀 반납(내부 Retain seam + 런타임 Pool).
- `AbilityData.EffectAssets` 열거가 각 능력의 non-null 자산을 반환.
- 예열 중복 스킵(`WarmupAsync` 이미 있는 키 no-op)은 코어 테스트로 커버(또는 여기서 보강).

### 4.2 PlayMode (엔드투엔드 — 원래 동기 닫기)
- **명중 VFX**: 발사체 능력 스타터로 아레나 진입 → 적 명중 → **명중 VFX가 실제로 보이는지**(스크린샷) + 풀에서 나왔다가 수명 뒤 반납되는지(재사용 인스턴스 확인).
- 반복 발사로 Instantiate가 최초 1회(+예열분)만 일어나고 이후 재사용됨을 로그로 확인.
- 아레나 종료 → `PoolManager.Dispose()`로 이펙트 인스턴스 파괴·에셋 해제 확인.

---

## 5. 제거·변경 요약

| 대상 | 처리 |
|---|---|
| `SimpleEffectSpawner` | 삭제 → `PooledEffectSpawner` |
| 능력 정의의 직접 프리팹 필드 | `AssetReferenceGameObject`로 전환 |
| `ProjectileEffect.hitVfxPrefab` | 능력으로 끌어올림(`hitVfxAsset`), Activate로 주입 |
| `AbilityEffect : MonoBehaviour, IPoolable` | `: PooledBehaviour` |
| `VfxPlayer` static SpawnOneShot | 인스턴스 풀링 경로(`PlayThenReturn`) + `PlayOneShot` 스포너 API |
| `AbilityEffect.Release()` | `Dispose()`(=`ReturnToPool`) |
| `CoreAbilitySystem.Setup` | `PoolManager` 인자 추가 |
| `CardChoiceApplier` | async 예열 await 경로 |

---

## 6. 구현 순서 (이펙트 내부 단계)

1. **일회성 VFX 먼저** (명중·머즐): `VfxPlayer : PooledBehaviour` + `PlayOneShot`/`PlayThenReturn` + `ProjectileAbilityData`의 muzzle/hitVfx Addressable화 + D-5/6/7 게이트 + **PlayMode 명중 VFX 검증**. → 원래 동기를 가장 먼저 닫고 코어를 실검증.
2. **AbilityEffect 엔티티**: `Spawn<T>` 경로 + 3개 서브클래스 자가반납 전환 + 나머지 능력 데이터 Addressable화.
3. **스타터/획득 예열 배선** + `SimpleEffectSpawner` 제거.

---

## 7. 열린 항목 / 위험

- **Addressable 그룹·프리팹 준비**: 이펙트 프리팹을 Addressable로 마킹하고 루트에 poolable 컴포넌트 보장하는 에디터 작업 필요(수동 또는 스크립트). 인스펙터 재배선 포함.
- **`CardChoiceApplier` async 전환 파급**: 현재 static·sync 호출부가 async를 await하도록 흐름 조정 필요(카드 모달 닫힘 핸들러).
- **`PlayThenReturn`의 delay 수명**: 반납/씬 언로드 시 delay 취소(`OnDespawn`) 필수 — 미취소 시 재사용 인스턴스 오반납.
- **`EnsurePlay` 프리팹 보장**: 런타임 `AddComponent` 폴백 제거 대신 프리팹에 `VfxPlayer` 사전 부착 보장.
