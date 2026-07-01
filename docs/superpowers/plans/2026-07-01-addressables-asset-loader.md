# Addressables 에셋 로더 인프라 (TASK-015) Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Addressables 를 도입하고, `AssetReferenceGameObject` 를 비동기 로드·핸들 추적·일괄 해제하는 `AssetLoader` 를 구현해 이후 풀링(TASK-013)의 약한참조 로딩 토대를 만든다.

**Architecture:** 그룹=로드스코프(Boot 앱수명 / Arena 런수명), 라벨=용도(vfx/hit). `AssetLoader` 는 RuntimeKey 로 핸들을 1개만 유지(중복 로드 방지)하고 `ReleaseAll` 로 스코프 종료 시 누수를 차단한다. Addressables 런타임 의존이라 검증은 PlayMode 통합으로 한다.

**Tech Stack:** Unity 6000.2.10f1, com.unity.addressables(신규), UniTask 2.5.11(+UniTask.Addressables), URP.

## Global Constraints

- Unity 버전: 6000.2.10f1.
- 비동기는 **UniTask 만** — Addressables 는 `AsyncOperationHandle.ToUniTask()`. Coroutine·`System.Threading.Tasks` 금지.
- private 필드 `camelCase`(접두어 `m_`/`_` 금지), 모든 멤버 명시적 접근제한자(IDE0040).
- `System.*` 풀패스(예: `System.IDisposable`), `System.Collections.Generic` 만 using 허용.
- 라이프사이클 함수에 `=>` 본문 금지.
- Addressables: **로컬 빌드만** (원격 CDN 범위 외).
- 범위: `AssetLoader` + 패키지 도입 + 샘플 검증. `PoolManager`/`EffectType`/데이터 선언 필드/`GameContext` 배치는 **TASK-013**.
- 커밋 전 `lint` 게이트 (변경 `.cs`).

---

### Task 1: Addressables 패키지 도입 + 초기 그룹

**Files:**
- Modify: `Packages/manifest.json` (com.unity.addressables 추가)
- Create: `Assets/AddressableAssetsData/**` (Addressables 초기화가 생성)

**Interfaces:**
- Consumes: 없음
- Produces: `AddressableAssetSettings` 자산 + `Boot`/`Arena` 그룹 (Task 3 이 사용)

- [ ] **Step 1: Addressables 패키지 추가**

MCP `manage_packages` 로 `com.unity.addressables` 추가(Unity 6000.2 호환 버전 자동 해석, 예상 2.x). 또는 `Packages/manifest.json` 의 dependencies 에 한 줄 추가:
```json
"com.unity.addressables": "2.6.0"
```

- [ ] **Step 2: 임포트·컴파일 확인**

`refresh_unity`(force/all) 후 `read_console` error 0 확인. Addressables 도입은 대규모 임포트+도메인 리로드를 유발하므로 완료까지 대기.
Expected: 컴파일 에러 0.

- [ ] **Step 3: Addressables 설정·그룹 생성 (Editor API)**

MCP `execute_code` 로 실행:
```csharp
var settings = UnityEditor.AddressableAssets.AddressableAssetSettingsDefaultObject.GetSettings(true);
System.Func<string, UnityEditor.AddressableAssets.Settings.AddressableAssetGroup> ensure = name =>
{
    var g = settings.FindGroup(name);
    if (g == null)
        g = settings.CreateGroup(name, false, false, false, null,
            typeof(UnityEditor.AddressableAssets.Settings.GroupSchemas.BundledAssetGroupSchema),
            typeof(UnityEditor.AddressableAssets.Settings.GroupSchemas.ContentUpdateGroupSchema));
    return g;
};
ensure("Boot");
ensure("Arena");
UnityEditor.AssetDatabase.SaveAssets();
return "groups: " + string.Join(",", System.Array.ConvertAll(settings.groups.ToArray(), g => g.Name));
```
Expected: 반환에 `Boot`, `Arena` 포함.

- [ ] **Step 4: Commit**

메시지 파일로 커밋(한국어 인코딩):
```
chore: Addressables 패키지 도입 및 Boot/Arena 그룹 생성

공용 풀링의 프리팹 약한참조 로딩을 위해 com.unity.addressables 를 도입.
로드 스코프 축(Boot=앱수명 / Arena=런수명)에 맞춰 초기 그룹 2개를 생성.
```
`git add Packages/manifest.json Packages/packages-lock.json Assets/AddressableAssetsData` → `git commit -F <msg>`

---

### Task 2: asmdef 참조 추가 + AssetLoader 구현

**Files:**
- Modify: `Assets/Scripts/DefenseDot.asmdef` (references 추가)
- Create: `Assets/Scripts/Systems/Assets/AssetLoader.cs`

**Interfaces:**
- Consumes: Addressables 런타임(`Addressables`, `AsyncOperationHandle<GameObject>`, `AssetReferenceGameObject`), UniTask(`.ToUniTask()`)
- Produces:
  - `DefenseDot.Systems.Assets.AssetLoader`
  - `UniTask<GameObject> LoadAsync(AssetReferenceGameObject reference)`
  - `void Release(AssetReferenceGameObject reference)`
  - `void ReleaseAll()`

- [ ] **Step 1: DefenseDot.asmdef 에 참조 추가**

`references` 배열에 3개 추가:
```json
"references": [
    "UniTask",
    "UniTask.Addressables",
    "Unity.Addressables",
    "Unity.ResourceManager",
    "Unity.Collections",
    "Unity.Burst",
    "Unity.TextMeshPro",
    "Unity.InputSystem",
    "UnityEngine.UI",
    "Unity.RenderPipelines.Core.Runtime",
    "Unity.RenderPipelines.Universal.Runtime"
]
```

- [ ] **Step 2: AssetLoader.cs 작성**

Create `Assets/Scripts/Systems/Assets/AssetLoader.cs`:
```csharp
using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.AsyncOperations;

namespace DefenseDot.Systems.Assets
{
    /// <summary> Addressables 프리팹을 로드·해제하고 핸들을 추적하는 로더입니다. </summary>
    public sealed class AssetLoader
    {
        // RuntimeKey(에셋 GUID) 기준 핸들 1개만 유지 → 중복 로드 방지
        private readonly Dictionary<object, AsyncOperationHandle<GameObject>> handles
            = new Dictionary<object, AsyncOperationHandle<GameObject>>();

        /// <summary> 참조를 로드해 프리팹을 반환합니다. 이미 로드됐으면 캐시 반환. </summary>
        public async UniTask<GameObject> LoadAsync(AssetReferenceGameObject reference)
        {
            object key = reference.RuntimeKey;
            if (handles.TryGetValue(key, out AsyncOperationHandle<GameObject> cached))
                return await cached.ToUniTask();

            AsyncOperationHandle<GameObject> handle = Addressables.LoadAssetAsync<GameObject>(reference);
            handles[key] = handle;   // await 전에 등록 → 동시 로드 레이스 방지
            return await handle.ToUniTask();
        }

        /// <summary> 특정 참조의 핸들을 해제합니다. </summary>
        public void Release(AssetReferenceGameObject reference)
        {
            object key = reference.RuntimeKey;
            if (!handles.TryGetValue(key, out AsyncOperationHandle<GameObject> handle)) return;
            Addressables.Release(handle);
            handles.Remove(key);
        }

        /// <summary> 추적 중인 모든 핸들을 해제합니다. (런/씬 종료) </summary>
        public void ReleaseAll()
        {
            foreach (AsyncOperationHandle<GameObject> handle in handles.Values)
                Addressables.Release(handle);
            handles.Clear();
        }
    }
}
```

- [ ] **Step 3: 컴파일 검증**

`refresh_unity`(scripts) 후 `read_console` error 0. (Addressables 래퍼라 순수 단위테스트는 부적합 — 실제 동작은 Task 3 PlayMode 통합에서 검증.)
Expected: 컴파일 에러 0.

- [ ] **Step 4: lint + Commit**

`lint` 스킬로 `AssetLoader.cs` 검증(통과 예상: camelCase·명시 접근자·UniTask). 이어서:
```
feat: Addressables 프리팹 로더(AssetLoader) 추가

AssetReferenceGameObject 를 UniTask 로 로드하고 RuntimeKey 기준으로 핸들을 중복 없이 추적.
ReleaseAll 로 로드 스코프 종료 시 모든 핸들을 일괄 해제해 누수를 방지.
```
`git add Assets/Scripts/DefenseDot.asmdef Assets/Scripts/Systems/Assets/AssetLoader.cs*` → commit.

---

### Task 3: Hit_Water Addressable 마킹 + PlayMode 왕복 검증

**Files:**
- Modify: `Assets/Prefabs/Abilities/Hit_Water.prefab` (Addressable entry — AddressableAssetSettings 에 기록)

**Interfaces:**
- Consumes: `AssetLoader`(Task 2), `Arena` 그룹(Task 1), `Hit_Water.prefab`
- Produces: 검증된 로드/해제 왕복 (동작 근거)

- [ ] **Step 1: Hit_Water 를 Addressable 로 마킹 (Editor API)**

MCP `execute_code`:
```csharp
var settings = UnityEditor.AddressableAssets.AddressableAssetSettingsDefaultObject.GetSettings(true);
string guid = UnityEditor.AssetDatabase.AssetPathToGUID("Assets/Prefabs/Abilities/Hit_Water.prefab");
var group = settings.FindGroup("Arena");
var entry = settings.CreateOrMoveEntry(guid, group);
entry.address = "Hit_Water";
entry.SetLabel("vfx", true, true);
entry.SetLabel("hit", true, true);
UnityEditor.AssetDatabase.SaveAssets();
return "addr=" + entry.address + " group=" + entry.parentGroup.Name + " guid=" + guid;
```
Expected: `addr=Hit_Water group=Arena guid=<GUID>` — GUID 를 Step 2 에서 사용.

- [ ] **Step 2: PlayMode 왕복 검증 (실패 먼저 관찰 불가 → 통합 검증)**

`manage_editor` play → `execute_code` (GUID 는 Step 1 값으로 치환):
```csharp
var loader = new DefenseDot.Systems.Assets.AssetLoader();
var refA = new UnityEngine.AddressableAssets.AssetReferenceGameObject("<GUID>");
var refB = new UnityEngine.AddressableAssets.AssetReferenceGameObject("<GUID>");
var go1 = await loader.LoadAsync(refA);
var go2 = await loader.LoadAsync(refB);   // 같은 에셋 → dedup
var f = typeof(DefenseDot.Systems.Assets.AssetLoader).GetField("handles",
    System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
int afterLoad = ((System.Collections.ICollection)f.GetValue(loader)).Count;
loader.ReleaseAll();
int afterRelease = ((System.Collections.ICollection)f.GetValue(loader)).Count;
return string.Format("go1={0} go2={1} sameAsset={2} handlesAfterLoad={3} afterRelease={4}",
    go1 != null, go2 != null, go1 == go2, afterLoad, afterRelease);
```
> `execute_code` 가 top-level `await` 를 지원하지 않으면 `loader.LoadAsync(refA).GetAwaiter().GetResult()` 로 대체.
Expected: `go1=True go2=True sameAsset=True handlesAfterLoad=1 afterRelease=0`.

- [ ] **Step 3: 결과 확인 + Play 종료**

`handlesAfterLoad=1`(dedup), `afterRelease=0`(누수 없음), `go1/go2` non-null 확인. `manage_editor` stop.
Expected: 위 값 일치.

- [ ] **Step 4: Commit**

```
chore: Hit_Water 를 Addressable(Arena/vfx,hit)로 마킹

AssetLoader 로드/해제 왕복 검증용 샘플로 Hit_Water 를 Arena 그룹에 등록하고 vfx·hit 라벨을 부여.
```
`git add Assets/AddressableAssetsData Assets/Prefabs/Abilities/Hit_Water.prefab*` → commit.

---

## 후속 (TASK-013, 본 계획 범위 외)

- `AssetLoader` 를 `GameContext` 가 보유·DI 주입 (소비자 `PoolManager` 등장 시점).
- `PoolManager.PreloadAsync/Get/Release`, `Pool<T>`, `IPoolable`/`IActivatable`/`IPooledObject`, `PrefabFactory`/`PocoFactory`.
- `EffectType` enum + `EffectEntry[]` + 스포너 데이터 SO 선언 필드 + 기존 데이터 마이그레이션.
- `VfxPlayer` 풀링 교체 (TASK-014 B-3).

## Self-Review

- **Spec coverage**: 패키지 도입(§2)=Task1 / AssetLoader API(§4)=Task2 / 그룹·라벨(§5)=Task1·Task3 / 검증(§8)=Task3. EffectType·PoolManager 는 스펙에서 명시적으로 TASK-013 이관 → 본 계획 제외 정합.
- **Placeholder scan**: `<GUID>` 는 Task3 Step1 산출을 Step2 에 주입하는 실제 값 자리(플레이스홀더 아님, 런타임 획득값).
- **Type consistency**: `AssetLoader.LoadAsync/Release/ReleaseAll`·`handles` 필드명이 Task2 정의와 Task3 검증에서 일치.
