# 이펙트 풀링 마이그레이션 구현 계획 (서브 마이그레이션 ①)

> **For agentic workers:** REQUIRED SUB-SKILL: `superpowers:executing-plans`(인라인, Unity MCP 필요)로 태스크별 구현. 체크박스(`- [ ]`)로 추적.

**Goal:** 이펙트(AbilityEffect 엔티티 + 일회성 VFX)를 풀링 코어에 연결해 Instantiate/Destroy를 제거하고, 명중 VFX를 PlayMode로 실검증한다.

**Architecture:** `IEffectSpawner`를 `PoolManager` 어댑터(`PooledEffectSpawner`)로 교체하고, `AbilityEffect`·`VfxPlayer`가 `PooledBehaviour`를 상속해 자가반납(`Dispose`)한다. 능력 정의는 이펙트 프리팹을 `AssetReferenceGameObject`로 들고, 획득/스타터 시 `WarmupAsync`로 예열한다.

**Tech Stack:** Unity 6000.2.10f1, C#, UniTask 2.5.11, Addressables 3.1.0, NUnit EditMode, Unity MCP(에디터/PlayMode).

## Global Constraints

- private 필드 순수 camelCase, 모든 멤버 명시적 접근제한자(IDE0040).
- `System.*` 풀패스(`using System;` 금지), `System.Collections.Generic`만 using 허용.
- 비동기 UniTask만(`Async` 접미사). 인라인 `//` 주석 ≤20자·의미 우선, 한국어 `<summary>`.
- 코어(`Pool`/`PoolManager`/`PooledBehaviour`/계약)는 **변경 없음** — 소비자만 연결.
- 커밋 전 `lint` 스킬, 커밋은 commit 스킬, push는 사용자 승인 시.

## 코드 결합 주의 (스펙 순서 조정 근거)

`AbilityEffect : PooledBehaviour`와 `IEffectSpawner` 시그니처 변경은 3개 효과·3개 능력 데이터에 동시 파급(컴파일 원자성). 스펙의 "일회성 VFX 먼저"를 문자대로 분리하면 스포너가 둘로 갈린다. 따라서 순서를 **코드 계약 전환(원자, 컴파일 그린) → 에디터 Addressable화 → 예열 배선 → PlayMode 검증**으로 조정한다. 명중 VFX는 여전히 최종 검증 1순위.

---

## File Structure

| 파일 | 책임 | 처리 |
|---|---|---|
| `Effects/VfxPlayer.cs` | 일회성/지속 연출 래퍼 | `: PooledBehaviour`, `PlayThenReturn`, 정적 SpawnOneShot 제거 |
| `Effects/IEffectSpawner.cs` | 이펙트 스폰 시임 | `Spawn<T>(AssetReference)`·`PlayOneShot`·`Release` |
| `Effects/PooledEffectSpawner.cs` | PoolManager 어댑터 | **신규** |
| `Effects/SimpleEffectSpawner.cs` | 임시 Instantiate 스포너 | **삭제** |
| `Effects/AbilityEffect.cs` | 효과 베이스 | `: PooledBehaviour`, `ReturnToPool` |
| `Effects/ProjectileEffect.cs` | 유도 투사체 | hitVfx 필드 제거·Activate(hitVfxAsset)·Spawner.PlayOneShot·ReturnToPool |
| `Effects/OrbiterSetEffect.cs`·`AreaZoneEffect.cs` | 오비터·장판 | Release→ReturnToPool |
| `Abilities/AbilityData.cs` | 능력 베이스 | `EffectAssets` virtual |
| `Definitions/ProjectileAbilityData.cs` 외 2 | 능력 정의 | 프리팹 필드 → AssetReference, EffectAssets, Fire 갱신 |
| `Abilities/CoreAbilitySystem.cs` | 능력 구동 | Setup에 PoolManager, PooledEffectSpawner 생성, 스타터 예열 |
| `Cards/CardChoiceApplier.cs` | 카드 적용 | async 예열 await |
| `Mode/ArenaModeBootstrap.cs` | 합성 루트 | Setup에 `ctx` PoolManager 전달 |
| `Tests/EditMode/PooledEffectSpawnerTests.cs`, `AbilityEffectAssetsTests.cs` | 테스트 | **신규** |

---

## Task 1: 계약·스포너 코드 전환 (컴파일 그린)

원자 단위 — 계약/스포너/효과/능력데이터를 함께 바꿔 컴파일을 맞춘다. 실제 동작(예열·Addressable)은 Task 3~4 후 성립하지만, 여기서 **컴파일 + 기존 EditMode 그린**을 확보한다.

**Files:**
- Modify: `Assets/Scripts/Systems/Abilities/Effects/VfxPlayer.cs`
- Modify: `Assets/Scripts/Systems/Abilities/Effects/IEffectSpawner.cs`
- Create: `Assets/Scripts/Systems/Abilities/Effects/PooledEffectSpawner.cs`
- Delete: `Assets/Scripts/Systems/Abilities/Effects/SimpleEffectSpawner.cs` (+meta)
- Modify: `AbilityEffect.cs`, `ProjectileEffect.cs`, `OrbiterSetEffect.cs`, `AreaZoneEffect.cs`
- Modify: `AbilityData.cs`, `ProjectileAbilityData.cs`, `OrbitalAbilityData.cs`, `AreaWaveAbilityData.cs`
- Modify: `CoreAbilitySystem.cs`, `ArenaModeBootstrap.cs`

**Interfaces (Produces):**
- `IEffectSpawner.Spawn<T>(AssetReferenceGameObject) where T : AbilityEffect`
- `IEffectSpawner.PlayOneShot(AssetReferenceGameObject, Vector3, Quaternion)`
- `IEffectSpawner.Release(AbilityEffect)`
- `AbilityData.EffectAssets : IEnumerable<AssetReferenceGameObject>` (virtual)
- `AbilityEffect.ReturnToPool()` (protected), `AbilityEffect.Spawner` (protected)
- `VfxPlayer.PlayThenReturn()`
- `CoreAbilitySystem.Setup(TargetFinder, Vector3, GameFlowModel, ICombatState, IReadOnlyList<AbilityData>, PoolManager)`

- [ ] **Step 1: `IEffectSpawner` 시그니처 교체**

```csharp
using UnityEngine;
using UnityEngine.AddressableAssets;

namespace DefenseDot.Systems.Abilities.Effects
{
    /// <summary> 이펙트 스폰 시임 — 엔티티 풀 대여와 일회성 VFX 재생을 제공합니다. </summary>
    public interface IEffectSpawner
    {
        /// <summary> 풀에서 효과 엔티티를 꺼내 스포너를 배선해 돌려줍니다. </summary>
        T Spawn<T>(AssetReferenceGameObject asset) where T : AbilityEffect;

        /// <summary> 일회성 VFX를 꺼내 위치잡고 재생 — 수명 뒤 자동 반납합니다. </summary>
        void PlayOneShot(AssetReferenceGameObject asset, Vector3 pos, Quaternion rot);

        /// <summary> 효과를 풀로 반납합니다. </summary>
        void Release(AbilityEffect fx);
    }
}
```

- [ ] **Step 2: `VfxPlayer : PooledBehaviour` + `PlayThenReturn`, 정적 SpawnOneShot 제거**

`VfxPlayer.cs` — 클래스 선언을 `public sealed class VfxPlayer : PooledBehaviour`로. 정적 `SpawnOneShot` 삭제. `EnsurePlay`는 유지(단 인스턴스 대상). 인스턴스 메서드 추가:

```csharp
using DefenseDot.Core.Pooling;
using Cysharp.Threading.Tasks;
// ...
private System.Threading.CancellationTokenSource lifeCts;

/// <summary> 재생 후 실제 수명만큼 뒤 풀로 반납합니다. </summary>
public void PlayThenReturn()
{
    Play();
    lifeCts?.Cancel();
    lifeCts = new System.Threading.CancellationTokenSource();
    ReturnAfterAsync(ResolveDuration(), lifeCts.Token).Forget();
}

private async UniTask ReturnAfterAsync(float seconds, System.Threading.CancellationToken token)
{
    bool canceled = await UniTask.Delay(System.TimeSpan.FromSeconds(seconds),
        cancellationToken: token).SuppressCancellationThrow();
    if (!canceled) Dispose();
}

/// <summary> 반납 시 진행 중 수명 타이머 취소(재사용 인스턴스 오반납 방지). </summary>
public override void OnDespawn() { lifeCts?.Cancel(); }
```

- [ ] **Step 3: `PooledEffectSpawner` 신규**

```csharp
using UnityEngine;
using UnityEngine.AddressableAssets;
using DefenseDot.Core.Pooling;

namespace DefenseDot.Systems.Abilities.Effects
{
    /// <summary> PoolManager를 감싸 효과 엔티티·일회성 VFX를 풀링으로 제공합니다. </summary>
    public sealed class PooledEffectSpawner : IEffectSpawner
    {
        private readonly PoolManager pool;

        public PooledEffectSpawner(PoolManager poolManager) { pool = poolManager; }

        public T Spawn<T>(AssetReferenceGameObject asset) where T : AbilityEffect
        {
            T fx = pool.Get<T>(asset);
            fx.Bind(this);
            return fx;
        }

        public void PlayOneShot(AssetReferenceGameObject asset, Vector3 pos, Quaternion rot)
        {
            VfxPlayer vp = pool.Get<VfxPlayer>(asset);
            vp.transform.SetPositionAndRotation(pos, rot);
            vp.PlayThenReturn();
        }

        public void Release(AbilityEffect fx) { fx?.Dispose(); }
    }
}
```

- [ ] **Step 4: `AbilityEffect : PooledBehaviour`**

`AbilityEffect.cs` 전체를 교체:

```csharp
using DefenseDot.Core.Pooling;

namespace DefenseDot.Systems.Abilities.Effects
{
    /// <summary>
    /// 능력이 스폰하는 자가구동 효과의 베이스입니다.
    /// 시간축 거동·데미지는 서브클래스 Update가 수행하고, 종료 시 ReturnToPool로 반납합니다.
    /// </summary>
    public abstract class AbilityEffect : PooledBehaviour
    {
        private IEffectSpawner spawner;

        /// <summary> 중첩 VFX 스폰용 스포너를 주입합니다(반납 아님). </summary>
        public void Bind(IEffectSpawner effectSpawner) => spawner = effectSpawner;

        /// <summary> 중첩 일회성 VFX 스폰에 쓰는 스포너입니다. </summary>
        protected IEffectSpawner Spawner => spawner;

        /// <summary> 효과를 풀로 반납합니다. </summary>
        protected void ReturnToPool() => Dispose();
    }
}
```

- [ ] **Step 5: `ProjectileEffect` — hitVfx 필드 제거·Activate(hitVfxAsset)·Spawner.PlayOneShot·Release→ReturnToPool**

`hitVfxPrefab` 필드 삭제. `hitVfxAsset` 필드 추가(런타임 주입용, non-serialize). `Activate` 시그니처에 `AssetReferenceGameObject hitVfxAsset` 추가·저장. 명중 지점·수명/관통 소진부 교체:

```csharp
using UnityEngine.AddressableAssets;
// 필드
private AssetReferenceGameObject hitVfxAsset;
// Activate(...) 끝에 파라미터 추가:
public void Activate(Vector3 origin, ITargetable target, DamageSource source,
    float speed, int pierce, float range, TargetFinder finder, AssetReferenceGameObject hitVfx)
{
    // ... 기존 대입 ...
    this.hitVfxAsset = hitVfx;
    // ...
}
// Update 내부 교체:
//  life <= 0f     : Release();  → ReturnToPool();
//  명중 VFX       : if (hitVfxPrefab != null) VfxPlayer.SpawnOneShot(hitVfxPrefab, pos, id);
//                   → if (hitVfxAsset != null && Spawner != null) Spawner.PlayOneShot(hitVfxAsset, transform.position, Quaternion.identity);
//  pierce 소진    : Release(); → ReturnToPool();
```

- [ ] **Step 6: `OrbiterSetEffect`·`AreaZoneEffect` — Release→ReturnToPool**

두 파일에서 종료 반납 호출 `Release()`를 `ReturnToPool()`로 교체(현재 소스 확인 후 해당 라인만). `EnsurePlay` 사용부는 유지.

- [ ] **Step 7: `AbilityData.EffectAssets` + 능력 정의 필드 전환**

`AbilityData.cs` 베이스에 추가:
```csharp
using System.Collections.Generic;
using UnityEngine.AddressableAssets;
// ...
/// <summary> 이 능력이 사용하는 모든 풀링 프리팹(예열 대상). </summary>
public virtual IEnumerable<AssetReferenceGameObject> EffectAssets
    => System.Array.Empty<AssetReferenceGameObject>();
```

`ProjectileAbilityData.cs`:
```csharp
[SerializeField] private AssetReferenceGameObject projectileAsset;
[SerializeField] private AssetReferenceGameObject muzzleAsset;
[SerializeField] private AssetReferenceGameObject hitVfxAsset;
// projectilePrefab·muzzlePrefab 삭제

public override IEnumerable<AssetReferenceGameObject> EffectAssets
{
    get
    {
        if (projectileAsset != null && projectileAsset.RuntimeKeyIsValid()) yield return projectileAsset;
        if (muzzleAsset != null && muzzleAsset.RuntimeKeyIsValid()) yield return muzzleAsset;
        if (hitVfxAsset != null && hitVfxAsset.RuntimeKeyIsValid()) yield return hitVfxAsset;
    }
}

// Fire(...) 갱신:
ProjectileEffect fx = ctx.Effects.Spawn<ProjectileEffect>(projectileAsset);
DamageSource src = new DamageSource(this, self, ctx.Modifiers);
fx.Activate(ctx.Origin, target, src, speed, pierce, range, ctx.Finder, hitVfxAsset);
if (muzzleAsset != null && muzzleAsset.RuntimeKeyIsValid())
    ctx.Effects.PlayOneShot(muzzleAsset, ctx.Origin, rot);
```
`OrbitalAbilityData`·`AreaWaveAbilityData`도 동일 패턴(각자 엔티티 asset + EffectAssets + Spawn<T> 갱신, Release 호출부는 `ctx.Effects.Release(fx)` 유지).

- [ ] **Step 8: `CoreAbilitySystem.Setup`에 PoolManager + 스포너 교체**

```csharp
// using DefenseDot.Core.Pooling;
public void Setup(TargetFinder finder, Vector3 origin, GameFlowModel gameFlow,
    ICombatState combatState, IReadOnlyList<AbilityData> starters, PoolManager poolManager)
{
    // ... 기존 ...
    IEffectSpawner effects = new PooledEffectSpawner(poolManager);
    ctx = new AbilityContext(this, origin, finder, loadout.Modifiers, effects, this);
    // ... runner ...
}
```
`ArenaModeBootstrap.cs:90` 호출에 PoolManager 인자 추가: `coreAbility.Setup(ctx.TargetFinder, ctx.CoreCenter, ctx.Flow, ctx.CombatState, starterAbilities, ctx.Pooling)` (ModeContext에 Pooling 노출 필요 시 GameContext.Pooling 경유 — 실행 시 ModeContext 확인).

- [ ] **Step 9: Unity 컴파일 확인 + 기존 EditMode 그린**

Unity MCP `refresh_unity`(compile) → `read_console`(에러 0) → `run_tests`(EditMode). 컴파일 에러 잔여분(누락 using·시그니처 불일치) 수정.
Expected: 컴파일 성공, 기존 136 테스트 그린 유지(신규 테스트 전).

- [ ] **Step 10: 커밋** (lint → commit 스킬, push 안 함)

---

## Task 2: EditMode 테스트 (스포너 어댑터 · EffectAssets)

**Files:**
- Create: `Assets/Tests/EditMode/PooledEffectSpawnerTests.cs`, `AbilityEffectAssetsTests.cs`

- [ ] **Step 1: `PooledEffectSpawner.Release(fx)` → 풀 반납 테스트**

nested `TestEffect : AbilityEffect`(DespawnCount)로, PoolManager 런타임 Pool + 내부 Retain seam으로 대여 후 `spawner.Release(fx)` → `live` 제거·`DespawnCount==1` 확인. (PoolManagerTests 패턴 재사용, Addressables 비의존.)

- [ ] **Step 2: `EffectAssets` 열거 테스트**

`ProjectileAbilityData` ScriptableObject 인스턴스에 SerializedObject로 asset 필드 3개 세팅 → `EffectAssets`가 non-null 3개 반환. (또는 null 자산 시 빈 열거.)

- [ ] **Step 3: 실행·그린 확인 + 커밋**

`run_tests`(EditMode). Expected: 신규 테스트 PASS. 커밋.

---

## Task 3: 에디터 — 프리팹 Addressable화 + 루트 컴포넌트 보장 + 능력 데이터 재배선 (Unity MCP)

**Files:** (에셋/프리팹/SO — 코드 아님)

- [ ] **Step 1: 대상 프리팹 식별**

기존 능력 SO의 직접 프리팹 참조(projectile/orbiter/zone·muzzle·hit VFX)를 조사(Unity MCP `manage_asset` 검색). 각 프리팹 경로 목록화.

- [ ] **Step 2: 루트 poolable 컴포넌트 보장**

각 이펙트 엔티티 프리팹 루트에 해당 `AbilityEffect` 서브클래스 컴포넌트가 1개인지 확인. VFX 프리팹(muzzle·hit) 루트에 `VfxPlayer` 부착(없으면 `manage_gameobject`/prefab 편집으로 추가). D-6(루트 poolable 1개) 규약 충족.

- [ ] **Step 3: Addressable 마킹**

각 프리팹을 Addressable 그룹에 등록(주소 = 안정 키). Unity MCP로 Addressable 엔트리 생성.

- [ ] **Step 4: 능력 SO 재배선**

각 능력 SO의 새 `AssetReferenceGameObject` 필드에 대응 프리팹 할당(SerializedObject). 구 직접 필드는 코드에서 이미 제거됨.

- [ ] **Step 5: 콘솔 확인**

`read_console` 에러 0. Addressables 빌드/설정 경고 확인.

---

## Task 4: 예열 배선 (스타터 + 카드 획득)

**Files:**
- Modify: `CoreAbilitySystem.cs`(스타터 예열), `CardChoiceApplier.cs`(async), 카드 적용 호출부.

- [ ] **Step 1: 스타터 예열 — `CoreAbilitySystem`에 예열 진입점**

```csharp
/// <summary> 스타터 능력들의 이펙트를 아레나 진입 시 예열합니다. </summary>
public async UniTask WarmupStartersAsync()
{
    if (loadout == null) return;
    using (UnityEngine.Pool.HashSetPool<AssetReferenceGameObject>.Get(out var set))
    {
        foreach (AbilityInstance a in loadout.Actives) CollectAssets(a.Data, set);
        foreach (AbilityInstance a in loadout.Passives) CollectAssets(a.Data, set);
        await pool.WarmupAsync(set);   // Setup에서 PoolManager 필드 보관
    }
}
```
(`CollectAssets` = data.EffectAssets를 set에 추가. `pool` 필드는 Step 8/Task1에서 Setup이 보관.)
`ArenaModeBootstrap`에서 Setup 직후(첫 웨이브 전) `await coreAbility.WarmupStartersAsync()`.

- [ ] **Step 2: 카드 획득 예열 — `CardChoiceApplier.Apply` async 전환**

```csharp
public static async UniTask ApplyAsync(ICardCommandTarget core, CardChoice choice, PoolManager pool)
{
    if (choice.action == CardAction.New)
    {
        if (choice.data != null) await pool.WarmupAsync(choice.data.EffectAssets);
        AbilityInstance added = core.AddAbility(choice.data);
        if (added != null)
            for (int lv = added.level; lv < choice.toLevel; lv++) core.LevelUpAbility(added);
    }
    else { for (int lv = choice.fromLevel; lv < choice.toLevel; lv++) core.LevelUpAbility(choice.instance); }
}
```
카드 적용 호출부(실행 시 grep으로 확정)를 `await ...ApplyAsync(...)`로. 모달 닫힘 → 예열 완료 후 재개.

- [ ] **Step 3: 컴파일·기존 테스트 그린 + 커밋**

---

## Task 5: PlayMode 검증 (원래 동기 닫기)

- [ ] **Step 1: 발사체 스타터로 아레나 진입**

발사체 능력을 스타터로 둔 아레나 씬 진입(PlayMode). `WarmupStartersAsync`로 projectile·muzzle·hitVfx 예열됨 확인(콘솔).

- [ ] **Step 2: 명중 VFX 가시 확인**

적 스폰 → 발사 → 명중. Unity MCP 스크린샷으로 **명중 VFX가 실제로 보이는지** 확인(원래 "피격 이펙트 안 보임" 동기 닫기).

- [ ] **Step 3: 풀 재사용·반납 확인**

반복 발사 로그로 Instantiate가 최초(+예열)만, 이후 재사용됨 확인. 투사체 수명 소진 시 풀 복귀.

- [ ] **Step 4: 아레나 종료 정리**

종료 → `GameManager`의 `poolManager.Dispose()`로 인스턴스 파괴·에셋 해제 확인(콘솔 에러 0).

- [ ] **Step 5: 최종 커밋 + TASK-013/014 문서 D-4~D-7 상태 갱신**

---

## Self-Review

- **스펙 커버리지**: (2.1 데이터→Task1 S7·Task3) (2.2 스포너→Task1 S1·S3) (2.3 AbilityEffect→S4) (2.4 VfxPlayer→S2) (2.5 예열→Task4) (2.6 게이트 D-5/6/7→아래 주). 데이터흐름/테스트/제거/순서 모두 태스크 존재.
- **D-5/6/7 게이트**: 코어(`Pool`/`PoolManager`)는 변경 없음 규칙과 상충 — 게이트 가드는 **코어 파일에 최소 추가**(D-5 TryGetValue 메시지, D-6 GetComponent null 가드)가 필요. Task 1에 코어 가드 스텝을 포함하되, 계약/로직 불변(방어 메시지·null 가드만)으로 한정. → Task 1 Step 5.5로 코어 가드 추가(실행 시 `PoolManager.Get`/`Pool.Get`에 방어).
- **타입 일관성**: `Spawn<T>`·`PlayOneShot`·`Release`·`EffectAssets`·`ReturnToPool`·`PlayThenReturn`·`Setup(...,PoolManager)` 태스크 간 일치 확인.
- **미탐색 파일**: `OrbiterSetEffect`·`AreaZoneEffect`·카드 적용 호출부·`ModeContext.Pooling`은 실행 시 현재 소스 확인 후 해당 라인만 변경(패턴 동일).

---

## Execution Handoff

이 계획은 Unity MCP(에디터·PlayMode)와 반복 검증이 필요하므로 **인라인 실행**(`superpowers:executing-plans`)으로 이 세션에서 태스크별 진행한다(서브에이전트 부적합 — PlayMode·에디터 상태 필요). 각 태스크 종료 시 체크포인트 보고(텔레그램).
