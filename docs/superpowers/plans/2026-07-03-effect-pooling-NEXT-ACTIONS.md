# 이펙트 풀링 마이그레이션 — 다음 실행 지침 (복붙 실행용)

**작성**: 2026-07-03
**전제**: Task 1 완료·커밋 `24d67711`(EditMode 136/136). 이 문서는 Task 3→4→5를 **바로 실행**하기 위한 구체 스크립트·코드. 상태·배경은 `2026-07-03-effect-pooling-migration-HANDOFF.md`.

> 재개: `superpowers:executing-plans` 인라인, Unity MCP 필요. 각 태스크 후 lint→commit + 텔레그램 보고. push는 사용자 승인 시.

---

## Task 3 — 에디터 Addressable화 (Unity MCP `execute_code` 1회 실행)

**하는 일**: (1) 머즐·명중 VFX 프리팹 루트에 `VfxPlayer` 부착, (2) 5개 프리팹 Addressable 등록, (3) 3개 SO의 AssetReference 필드 배선.

> 실행 전: `AddressableAssetSettingsDefaultObject.Settings`가 non-null인지 확인(TASK-015로 이미 구성됨). null이면 Addressables 그룹을 먼저 초기화.

```csharp
using UnityEngine;
using UnityEditor;
using UnityEditor.AddressableAssets;
using UnityEditor.AddressableAssets.Settings;
using DefenseDot.Systems.Abilities.Effects;

internal class CommandScript : IRunCommand
{
    public void Execute(ExecutionResult result)
    {
        string projectile = "6a69eecf8d11bfc43a543e8ac90745bc"; // Projectile_Water
        string muzzle     = "aa7f91c6190ce6540a0b18ab3483498d"; // Par_YellowSwordBeam_Muzzle
        string hit        = "3428211ee08e9f443a44710e9689fe26"; // 명중 VFX
        string orbiter    = "c41eb85840eeb8a45aeda2d220e6c31b"; // OrbiterSetEffect
        string zone       = "6c364e6e955529f42bf87330ff72afd5"; // AreaZoneEffect

        // 1) VFX 프리팹 루트에 VfxPlayer 부착
        EnsureVfxPlayer(muzzle, result);
        EnsureVfxPlayer(hit, result);

        // 2) Addressable 등록
        AddressableAssetSettings settings = AddressableAssetSettingsDefaultObject.Settings;
        if (settings == null) { result.LogError("Addressable Settings 없음"); return; }
        AddressableAssetGroup group = settings.DefaultGroup;
        string[] all = { projectile, muzzle, hit, orbiter, zone };
        foreach (string g in all)
        {
            AddressableAssetEntry e = settings.CreateOrMoveEntry(g, group);
            string path = AssetDatabase.GUIDToAssetPath(g);
            e.address = System.IO.Path.GetFileNameWithoutExtension(path);
            result.Log("Addressable {0} -> {1}", e.address, path);
        }
        settings.SetDirty(AddressableAssetSettings.ModificationEvent.EntryMoved, null, true);

        // 3) SO AssetReference 배선
        SetRef("Assets/Data/Abilities/Ability_Shot.asset", "projectileAsset", projectile, result);
        SetRef("Assets/Data/Abilities/Ability_Shot.asset", "muzzleAsset", muzzle, result);
        SetRef("Assets/Data/Abilities/Ability_Shot.asset", "hitVfxAsset", hit, result);
        SetRef("Assets/Data/Abilities/Ability_Orbital.asset", "orbiterAsset", orbiter, result);
        SetRef("Assets/Data/Abilities/Ability_AreaWave.asset", "zoneAsset", zone, result);

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        result.Log("Task 3 완료");
    }

    private void EnsureVfxPlayer(string guid, ExecutionResult result)
    {
        string path = AssetDatabase.GUIDToAssetPath(guid);
        GameObject root = PrefabUtility.LoadPrefabContents(path);
        if (root.GetComponent<VfxPlayer>() == null)
        {
            root.AddComponent<VfxPlayer>();
            PrefabUtility.SaveAsPrefabAsset(root, path);
            result.Log("VfxPlayer 부착 {0}", path);
        }
        PrefabUtility.UnloadPrefabContents(root);
    }

    private void SetRef(string soPath, string field, string guid, ExecutionResult result)
    {
        Object so = AssetDatabase.LoadAssetAtPath<Object>(soPath);
        SerializedObject sob = new SerializedObject(so);
        SerializedProperty prop = sob.FindProperty(field);
        if (prop == null) { result.LogError("필드 없음 {0}.{1}", soPath, field); return; }
        prop.FindPropertyRelative("m_AssetGUID").stringValue = guid;
        sob.ApplyModifiedProperties();
        EditorUtility.SetDirty(so);
        result.Log("SO {0}.{1} = {2}", soPath, field, guid);
    }
}
```

**검증**: `read_console`(에러 0). 3개 SO의 AssetReference가 프리팹을 가리키는지 인스펙터/재열기로 확인. 엔티티 3개(Projectile_Water/Orbiter/AreaZone)는 루트에 각 `AbilityEffect` 서브클래스가 있어야 함(없으면 부착 — 보통 이미 있음).
**커밋**: 프리팹 2개(.prefab)·SO 3개(.asset)·Addressable 설정 파일. `chore: 이펙트 프리팹 Addressable 등록 및 능력 SO 배선`.

---

## Task 4 — 예열 배선 (코드)

### 4-A. ⚠️ 선행: `PoolManager.WarmupAsync` AssetReference 오버로드 추가

코어의 기존 `WarmupAsync(IEnumerable<EffectEntry>)`는 EffectType을 요구하나, 능력은 순수 `AssetReferenceGameObject`를 쓴다. `Assets/Scripts/Core/Pooling/PoolManager.cs`에 오버로드 추가(기존 예열 로직과 동일, 키는 `asset.RuntimeKey`):

```csharp
using UnityEngine.AddressableAssets;   // 이미 있으면 생략
// ...
/// <summary> AssetReference 목록을 예열합니다(EffectType 불요). </summary>
public async UniTask WarmupAsync(IEnumerable<AssetReferenceGameObject> assets, int count = 3)
{
    foreach (AssetReferenceGameObject asset in assets)
    {
        object key = asset.RuntimeKey;
        if (pools.ContainsKey(key)) continue;
        GameObject prefab = await assetLoader.LoadAsync<GameObject>(asset);
        var pool = new Pool(prefab);
        pools[key] = pool;
        Prewarm(pool, count);
    }
}
```
> 기존 `EffectEntry` 오버로드에 다른 호출자가 없으면 이 오버로드로 대체 가능(확인 후 정리). `Get<T>`가 `reference.RuntimeKey`로 조회하므로 키 일관됨.

### 4-B. Setup의 EquipAll 선행 문제 해결 (warmup-후-equip)

`CoreAbilitySystem.cs` — `Setup` 끝의 `runner.EquipAll();` **제거**하고, 예열 후 equip하는 async 진입점 신설:

```csharp
using Cysharp.Threading.Tasks;
using UnityEngine.AddressableAssets;
using System.Collections.Generic;   // 이미 있음
// ...
// Setup(): runner = new AbilityRunner(loadout, ctx);  // EquipAll 호출 제거

/// <summary> 스타터 이펙트 예열 후 장착합니다(예열 전 Spawn 방지). </summary>
public async UniTask WarmupAndEquipAsync()
{
    if (pool != null && loadout != null)
    {
        using (UnityEngine.Pool.HashSetPool<AssetReferenceGameObject>.Get(out HashSet<AssetReferenceGameObject> set))
        {
            CollectAssets(loadout.Actives, set);
            CollectAssets(loadout.Passives, set);
            if (set.Count > 0) await pool.WarmupAsync(set);
        }
    }
    runner?.EquipAll();
}

private static void CollectAssets(IReadOnlyList<AbilityInstance> list, HashSet<AssetReferenceGameObject> set)
{
    for (int i = 0; i < list.Count; i++)
    {
        AbilityData d = list[i].Data;
        if (d == null) continue;
        foreach (AssetReferenceGameObject a in d.EffectAssets) if (a != null) set.Add(a);
    }
}
```
> `loadout.Actives`/`Passives`의 실제 타입 확인(IReadOnlyList<AbilityInstance> 가정). `AbilityInstance.Data` 접근자 확인.

`ArenaModeBootstrap.SpawnCenterTower` — Setup 직후 예열·장착을 띄운다(fire-and-forget 허용: 예열 몇 프레임 후 장착, 발동은 쿨다운 뒤라 안전):

```csharp
coreAbility.Setup(ctx.TargetFinder, ctx.CoreCenter, ctx.Flow, ctx.CombatState, starterAbilities, ctx.Pooling);
coreAbility.WarmupAndEquipAsync().Forget();
```
> 더 엄밀히 하려면 `GameManager.Start`에서 `Flow.SetPhase(Playing)`을 예열 완료 뒤로 게이트. 우선 fire-and-forget로 시작해 PlayMode에서 확인.

### 4-C. 카드 획득 예열

`CardChoiceApplier.cs` — `Apply`를 async로:

```csharp
using Cysharp.Threading.Tasks;
using DefenseDot.Core.Pooling;
// ...
public static async UniTask ApplyAsync(ICardCommandTarget core, CardChoice choice, PoolManager pool)
{
    if (choice.action == CardAction.New)
    {
        if (pool != null && choice.data != null) await pool.WarmupAsync(choice.data.EffectAssets);
        AbilityInstance added = core.AddAbility(choice.data);
        if (added != null)
            for (int lv = added.level; lv < choice.toLevel; lv++) core.LevelUpAbility(added);
    }
    else { for (int lv = choice.fromLevel; lv < choice.toLevel; lv++) core.LevelUpAbility(choice.instance); }
}
```
**호출부 찾기**: `Grep "CardChoiceApplier.Apply"` → 카드 모달 확정 핸들러. 그 메서드를 async로 만들고 `await CardChoiceApplier.ApplyAsync(core, choice, pooling)`. `pooling`은 UI에 주입된 `GameContext.Pooling`에서 획득(핸들러가 GameContext/PoolManager 접근 가능한지 확인 — 없으면 주입 경로 추가).
> 구 `Apply`는 호출부 전환 후 제거. 레벨업 경로는 예열 불필요(코어 ContainsKey no-op).

**검증**: `refresh_unity`→`read_console`(에러 0)→`run_tests`(EditMode 그린 유지). **커밋**: `feat: 이펙트 예열을 능력 획득·스타터 시점에 배선`.

---

## Task 5 — 코어 가드(D-5/6/7) + PlayMode 검증

### 5-A. 코어 가드 (최소, 계약·로직 불변)

`Assets/Scripts/Core/Pooling/PoolManager.cs` `Get<T>`:
```csharp
// pools[reference.RuntimeKey] 인덱서 → TryGetValue 로
if (!pools.TryGetValue(reference.RuntimeKey, out Pool p))
    throw new System.InvalidOperationException($"WarmupAsync 예열 안 됨: {reference.RuntimeKey}");
GameObject instance = p.Get();
T item = instance.GetComponent<T>();
if (item == null) { p.Return(instance); throw new System.InvalidOperationException($"프리팹에 {typeof(T).Name} 없음"); }
// ... Retain(item, p, owner) ...
```
`Assets/Scripts/Core/Pooling/Pool.cs` `Get`:
```csharp
IPoolableObject poolable = instance.GetComponent<IPoolableObject>();
if (poolable == null) throw new System.InvalidOperationException($"프리팹 루트에 IPoolableObject 없음: {prefab.name}");
```
> 실제 현재 `Get<T>` 본문 확인 후 최소 삽입. EditMode 136 그린 유지 확인.

### 5-B. PlayMode 검증 (원래 "피격 이펙트" 동기 닫기)

1. `Ability_Shot`(발사체)가 스타터인 아레나 씬 확인(`ArenaModeBootstrap.starterAbilities`에 포함). 없으면 임시 포함.
2. `manage_editor`로 PlayMode 진입 → 콘솔에 예열 로그·에러 0 확인.
3. 적 스폰까지 대기 → 발사·명중 시점 `SceneView.Capture2DScene`/스크린샷으로 **명중 VFX 가시 확인**.
4. 반복 발사: Instantiate가 최초(+예열분)만 발생, 이후 재사용(로그/프로파일). 투사체 수명 소진 시 풀 복귀.
5. 아레나 종료(패배/승리) → `GameManager`의 `poolManager.Dispose()` 정리, 콘솔 에러 0.
6. PlayMode 종료.

**검증 실패 시**: D-5/6/7 예외 메시지로 원인 특정(미예열 키·프리팹 컴포넌트 누락 등).
**커밋**: `feat: 풀 미예열·프리팹 미구성 방어 가드 추가` + 검증 결과. TASK-013 문서 D-5/6/7 상태·마이그레이션 완료 반영. 메모리 `pooling-addressables-architecture` 갱신.

---

## 완료 후

- 서브 마이그레이션 ②(액터: Monster/Tower·EnemySpawner·ObjectPool<T> 제거·소유 연쇄)는 별도 스펙→계획으로.
- 미push 커밋 push 여부 사용자 확인.
