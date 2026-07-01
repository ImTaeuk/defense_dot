# 설계: Addressables 에셋 로더 인프라 (TASK-015)

**작성일**: 2026-07-01
**상태**: 설계 확정 (사용자 검토 대기)
**선행**: 없음 (최선행 인프라) / **후속**: TASK-013(공용 풀링) → TASK-014 B-3(피격 VFX)
**관련**: `2026-07-01-pooling-addressables-design.md`(상위 핸드오프)

---

## 1. 목적 / 배경

"피격 이펙트가 안 보인다"에서 출발한 조사가 인프라 결정으로 확장됐다: `VfxPlayer.SpawnOneShot` 의 `Instantiate`/`Destroy`(GC) → 공용 풀링 필요(TASK-013) → 풀이 프리팹을 강한참조로 상주 보유하면 안 됨 → **프리팹 약한참조 = Addressables 필요**. 착수는 역순이라 Addressables 가 최선행이다.

본 문서는 그 최선행 조각인 **에셋 로더 인프라**만 다룬다. 풀링·선언 데이터 모델의 상세는 TASK-013 소관이며, 여기서는 로더가 그것들을 떠받치도록 **경계와 방향**만 확정한다.

## 2. 범위

**포함 (TASK-015)**
- `com.unity.addressables` 패키지 도입 + 초기 그룹 구성.
- `AssetLoader` — `AssetReferenceGameObject` → `GameObject` 비동기 로드, 핸들 추적, 일괄 해제.
- 그룹·라벨 정책 확정 (로컬 빌드 기준, 원격 CDN 범위 외).
- 샘플 1종(`Hit_Water`)을 Addressable 로 마킹해 로드/해제 왕복 검증.

**제외 (TASK-013 으로)**
- `PoolManager`(선로드·풀 워밍·대여/반환), `Pool<T>`, `IPoolable` 계층.
- 스포너 데이터 SO 의 `AssetReference` 선언 필드 실제 추가 + 기존 데이터 마이그레이션.
- `EffectType`/`EffectEntry` 구현.

## 3. 설계 결정 (확정)

| # | 항목 | 결정 | 근거 |
|---|---|---|---|
| D1 | 로드 수명 | 유닛(레벨) 진입 시 선(先)로드·상주, 퇴장 시 해제 | "1회 로드·다회 인스턴스화" 풀링과 궁합. 런타임 `Get` 이 동기(핫패스 await 없음) |
| D2 | 로드 스코프 2층 | **Boot**(앱 수명·1회 로드·상주) / **Arena**(런 수명·진입 로드·퇴장 해제) | 레벨 교체 시 공용은 재로딩 없이 레벨 에셋만 교체 |
| D3 | 의존 선언 위치 | 스포너 데이터 SO 가 쓰는 프리팹을 `AssetReferenceGameObject` 로 직접 선언 | 타워가 자기 투사체를 소유(응집), Addressables 자동 의존추적 |
| D4 | 그룹 축 | **그룹 = 로드스코프**(Boot/Arena), **라벨 = 용도**(vfx/hit/enemy). 직교 2축 | Unity 권장: 함께 로드되는 것을 함께 번들. 불변식 "그룹 1개 = 로드/해제 스코프 1개" |
| D5 | 로더 이름 | `AssetLoader` (`LoadAsync`/`Release`/`ReleaseAll`) | Addressables `LoadAssetAsync` 결, 단순·직관 |
| D6 | Provisioner 폐기 | 별도 클래스 없이 `PoolManager.PreloadAsync(data)` 가 겸함 | 이름·개념 최소화. 풀 주인이 선로드+워밍 담당 (TASK-013) |
| D7 | 오너십 | `AssetLoader`(및 `PoolManager`)를 `GameContext` 가 보유·DI 주입 | 기존 UI 자동배선 DI 흐름과 일관, 전역 static 없음 → 테스트 용이 |
| D8 | `EffectType` 선언 | plain `enum EffectType` + `EffectEntry[]` (용도별 짝) | 용도 vocabulary 는 소수·안정적(YAGNI). 확장 필요 시 SO 방식으로 승격 |

## 4. AssetLoader API

```csharp
using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.AsyncOperations;

namespace DefenseDot.Systems.Assets
{
    /// <summary> Addressables 에셋을 로드·해제하고 핸들을 추적하는 로더. </summary>
    public sealed class AssetLoader
    {
        // GUID 기준 중복 로드 방지
        private readonly Dictionary<object, AsyncOperationHandle> handles
            = new Dictionary<object, AsyncOperationHandle>();

        /// <summary> 참조를 로드해 에셋을 반환. 이미 로드됐으면 캐시 반환. </summary>
        public async UniTask<T> LoadAsync<T>(AssetReference reference) where T : UnityEngine.Object
        {
            object key = reference.RuntimeKey;
            if (handles.TryGetValue(key, out AsyncOperationHandle cached))
                return await cached.Convert<T>().ToUniTask();

            AsyncOperationHandle<T> handle = Addressables.LoadAssetAsync<T>(reference);
            handles[key] = handle;
            return await handle.ToUniTask();
        }

        /// <summary> 특정 참조의 핸들을 해제. </summary>
        public void Release(AssetReference reference)
        {
            object key = reference.RuntimeKey;
            if (!handles.TryGetValue(key, out AsyncOperationHandle handle)) return;
            Addressables.Release(handle);
            handles.Remove(key);
        }

        /// <summary> 추적 중인 모든 핸들을 해제. (런/씬 종료) </summary>
        public void ReleaseAll()
        {
            foreach (AsyncOperationHandle handle in handles.Values)
                Addressables.Release(handle);
            handles.Clear();
        }
    }
}
```

- **중복 로드 방지**: 필드가 다른 여러 `AssetReferenceGameObject` 가 같은 에셋을 가리켜도 `RuntimeKey`(GUID)로 묶어 핸들 1개만 유지.
- **누수 방지 (D 목표)**: 모든 핸들을 `handles` 에 추적 → `ReleaseAll` 로 스코프 종료 시 일괄 해제.
- **UniTask**: `AsyncOperationHandle.ToUniTask()` 사용 (UniTask.Addressables 통합). Coroutine·`System.Threading.Tasks` 금지.

## 5. 그룹·라벨 정책

- **그룹 (로드스코프)**
  - `Boot` — 앱 전체 공용·인프라. 앱 시작 시 로드, 상주(해제 안 함).
  - `Arena` — 레벨(런) 에셋. 진입 시 로드, 퇴장 시 `ReleaseAll`.
  - 로컬 빌드만(원격 CDN 미사용). 각 그룹 기본 Pack Together.
- **라벨 (용도 교차태그)**: `vfx`, `hit`, `enemy` 등. 한 에셋에 복수 부여. 부분 로드·조회에 사용.
- **불변식**: "그룹 1개 = 로드/해제 스코프 1개". 새 콘텐츠는 스코프 맞는 그룹에 합류. 그룹이 커지고 하위 스코프가 생기면 스코프 축으로만 분할 → 눈덩이 방지.
- 레벨이 여러 개로 늘면 각 레벨을 자기 Arena-스코프 그룹으로 분리 가능(실제로 따로 로드될 때만).

## 6. 데이터 선언 방향 (상세는 TASK-013)

```csharp
public enum EffectType { Hit, Muzzle, Cast, Death }

[System.Serializable]
public struct EffectEntry
{
    public EffectType type;                 // 용도
    public AssetReferenceGameObject asset;  // 그 용도의 프리팹
}

// 스포너 데이터 SO (예: TowerData)
[SerializeField] private EffectEntry[] effects;
```

- 에디터에서 "Hit → Hit_Water" 식으로 용도별 짝 등록.
- `PoolManager.PreloadAsync(data)` 가 `effects` 를 열거 → `AssetLoader.LoadAsync` → 풀 워밍.
- 런타임 조회: `pool.Get(data.GetAsset(EffectType.Hit))` (동기, 이미 로드·워밍됨).

## 7. 로드/해제 흐름

```
앱 시작        → Boot 그룹 로드 (부트스트랩, 상주)
레벨 시작      → PoolManager.PreloadAsync(레벨 데이터)
                  └ 선언된 AssetReference 마다 AssetLoader.LoadAsync → 풀 워밍
레벨 진행 중   → pool.Get / Release (동기 핫패스, 로드 없음)
레벨 종료      → PoolManager 풀 정리 + AssetLoader.ReleaseAll (Arena 스코프 해제, Boot 유지)
```

## 8. 검증 시나리오

| # | 시나리오 | 기대 |
|---|---|---|
| 1 | `Hit_Water` 를 Addressable(Arena, 라벨 vfx/hit) 로 마킹 | 그룹/라벨 반영 |
| 2 | `AssetLoader.LoadAsync(hitWaterRef)` | non-null `GameObject` 반환, Instantiate 가능 |
| 3 | 같은 참조 2회 로드 | 핸들 1개(중복 로드 없음), 동일 프리팹 |
| 4 | `ReleaseAll` 후 상태 | 핸들 0, 재로드 시 정상 |
| 5 | Addressables Build 포함 빌드 | 산출물에 그룹 번들 포함 |

> Addressables 로드는 런타임 초기화가 필요하므로 로드/해제 검증은 PlayMode(또는 초기화된 Addressables) 기준.

## 9. 컨벤션 체크

- private 필드 `camelCase`(접두어 금지), 명시적 접근제한자.
- 비동기는 **UniTask 만** (`ToUniTask()`), Coroutine·`System.Threading.Tasks` 금지.
- 임시 컬렉션은 `UnityEngine.Pool.CollectionPool`, 필드 보관 컬렉션은 `new` 허용.
- `event` 는 `On*`/핸들러 `Handle*`(현재 로더엔 이벤트 없음).
- 커밋 전 `lint` 게이트.

## 10. TASK-013 으로 넘기는 것

- `PoolManager.PreloadAsync/Get/Release`, `Pool<T>`, `IPoolable`/`IActivatable`/`IPooledObject`, `PrefabFactory`/`PocoFactory`.
- `EffectType`/`EffectEntry` 구현 + 스포너 데이터 SO 에 선언 필드 추가 + 기존 데이터 마이그레이션.
- `VfxPlayer` 풀링 교체(TASK-014 B-3).
