# 설계 + 핸드오프: 공용 풀링 시스템 & Addressables 에셋 인프라

**작성일**: 2026-07-01
**상태**: 설계 확정 (구현 대기) — **메인 세션 인계용 핸드오프**
**출발점**: "피격 이펙트가 안 보인다" 코멘트 (이 흐름이 인프라 작업으로 자연스럽게 이어진 경위를 §1에 기록)

---

## 0. 이 문서의 목적

이 문서는 **브레인스토밍 세션의 설계 결정 전체를 메인 세션으로 인계**하기 위한 핸드오프다.
작업 스케일이 커서 "하나씩" 처리하기로 했고, 실제 구현은 메인 세션에서 의존 순서대로 진행한다.
이 문서 + `TASK-015`(Addressables) + `TASK-013`(공용 풀링) + `TASK-014`(피격 VFX) 4개를 함께 본다.

---

## 1. 출발점과 의존 체인 (왜 인프라까지 왔나)

작은 코멘트에서 출발했지만, 파고들수록 선행 인프라가 필요하다는 게 드러났다.

```
[증상] 피격 이펙트가 안 보임          ← 사용자 코멘트 (출발점)
   │  가설: "작아서 안 보임"
   ▼
[검증] 피격 명중 VFX 는 존재·동작함
   │  ProjectileEffect.cs:67 → VfxPlayer.SpawnOneShot(hitVfxPrefab, ...)
   │  "작아서 안 보임"은 TASK-014 B-3 에 이미 기록된 기지 이슈
   │  (적 589배 대비 파티클 scale 1 + 밝은 배경에 묻힘)
   ▼
[개선 욕구] SpawnOneShot 이 매번 Instantiate/Destroy → GC 부담
   │  → 공용 풀링 시스템 필요 (TASK-013)
   ▼
[선행 인프라] 풀이 프리팹을 강한참조/SerializeField 로 집중 보유하면 안 됨
   │  → 프리팹 약한참조(Addressables AssetReference) 필요
   ▼
[최선행] Addressables 에셋 참조 인프라 도입 (TASK-015, 신규)
```

**착수 순서는 위 화살표의 역순**: Addressables → 공용 풀링 → 피격 VFX 적용.

### 1.1 초기 진단 보정 (중요)

브레인스토밍 초기에 `SweeperEnemyVisual` 의 emission 제거(커밋 c7ecf502)를 "미완성 리팩토링"으로 의심했으나 **오진이었다**:

- `SweeperEnemyVisual` 의 emission 흰색 번쩍임 제거는 **TASK-014 A-2 "흰색 번쩍임 제거 — 사용자 요청"으로 의도된 것**이다.
- 실제 피격 명중 VFX 는 `SweeperEnemyVisual` 이 아니라 **투사체 측 `VfxPlayer.SpawnOneShot` 경로**에 살아 있다.
- 따라서 "피격 이펙트 부재"는 코드 누락이 아니라 **가시성(크기·배경) + 풀링 부재** 문제다.

---

## 2. 현재 코드 사실관계 (조사 결과)

| 항목 | 현황 | 파일 |
|---|---|---|
| 피격 명중 VFX 재생 | `VfxPlayer.SpawnOneShot` = `Instantiate` 후 수명 뒤 `Destroy` (일회성, 풀링 없음) | `Assets/Scripts/Systems/Abilities/Effects/VfxPlayer.cs:75` |
| 명중 VFX 호출처 | 투사체 명중 시 `hitVfxPrefab`(Hit_Water) 재생 | `Assets/Scripts/Systems/Abilities/Effects/ProjectileEffect.cs:67` |
| 적 풀링 | `EnemySpawner` 자체 `Dictionary<GameObject, Queue<MonsterActor>>` (프리팹 키) | `Assets/Scripts/Systems/Enemy/EnemySpawner.cs:39,247` |
| 적 프리팹 참조 | `EnemyData`(ScriptableObject)가 `prefab` 필드로 보유 → **데이터 에셋 분산 강참조** | `EnemyData` |
| 기존 풀 | `ObjectPool<T> where T : MonoBehaviour, IPoolable` (제네릭, 단순) | `Assets/Scripts/Core/ObjectPool.cs` |
| 기존 IPoolable | `{ void OnSpawn(); void OnDespawn(); }` — **14개 파일에서 사용** | `Assets/Scripts/Core/Interfaces/IPoolable.cs` |
| Addressables | **미도입** (manifest.json 에 없음) | `Packages/manifest.json` |

### 2.1 기존 IPoolable 사용처 (마이그레이션 대상 14곳)

`MonsterActor`, `TowerActor`, `AbilityEffect`(+ `OrbiterSetEffect`/`AreaZoneEffect`/`ProjectileEffect`/`SimpleEffectSpawner`), `EnemySpawner`, `ObjectPool`, `IPoolable`, 테스트 2종(`TowerBehaviorTreeTests`/`EnemyBehaviorTreeTests`), 외부 Hovl 2종(`HS_ParticleCollisionInstance`/`HS_DemoShooting` — 외부 에셋, 교체 제외 검토).

---

## 3. 확정 설계 — 공용 풀링 시스템

### 3.1 인터페이스 계층 (관심사 3분리)

```csharp
// 풀 생명주기 훅 — 기존 IPoolable 흡수(시그니처 유지로 14곳 본문 무변경)
public interface IPoolable
{
    void OnSpawn();
    void OnDespawn();
}

// 활성/비활성 추상 — MonoBehaviour 와 POCO 의 차이 흡수 (옵션 구현)
public interface IActivatable
{
    bool IsActive { get; }
    void SetActive(bool active);   // MB → gameObject.SetActive, POCO → 논리 플래그
}

// 반환 강제 — Dispose() = 풀로 반환 (파괴 아님). using 스코프 자동 반납
public interface IPooledObject : System.IDisposable { }
```

- **풀링 대상은 `IPoolable` 필수.** `OnSpawn`/`OnDespawn` 시그니처를 그대로 유지하므로 14곳 마이그레이션은 "네임스페이스·풀 호출부 교체"로 끝나고 콜백 본문은 안 건드린다.
- `IActivatable` 은 켜고/끄는 객체만 추가 구현.
- `IPooledObject.Dispose()` 구현이 곧 "내 출처 풀로 반환".

### 3.2 생성 — Creator 캡슐화 (MB/POCO 분기를 풀 밖으로)

```csharp
public interface IPoolCreator<T> where T : IPoolable { T Create(); }

// MonoBehaviour: Addressables 로 로드한 프리팹을 Instantiate
public sealed class PrefabCreator<T> : IPoolCreator<T> where T : MonoBehaviour, IPoolable { ... }

// POCO: 자체 생성자
public sealed class PocoCreator<T> : IPoolCreator<T> where T : class, IPoolable, new() { ... }
```

풀은 `creator.Create()` 만 호출 — MonoBehaviour냐 POCO냐를 **모른다**. 사용자가 상상한 "MB면 레포지토리에서 인스턴스화, 아니면 생성자 호출" 분기가 **풀 안의 if 가 아니라 어떤 Creator 를 주입하느냐**로 표현된다. ScriptableObject 풀·외부 팩토리가 추가돼도 풀 코드는 무변경(개방-폐쇄).

### 3.3 Repository + Pool + 진입점

```
PoolRepository : 구체 타입 → IPoolCreator<T> + 풀 인스턴스 매핑·보유
                 ※ 프리팹을 직접 SerializeField/강한참조로 들지 않음 (아래 3.5)
Pool<T>        : Queue<T> + IPoolCreator<T> 주입. Get()=꺼냄(없으면 Create) / Return(t)
Pull<T>()      : Repository 에서 T 의 풀을 찾아 Get → IPooledObject 로 반환
```

`Pull<T>()` 이 사용자가 말한 "`IPoolable.Pull()`" 의 실체다. 인스턴스 메서드가 아니라 **Repository/Service 의 제네릭 진입점**이어야 모순이 없다(이미 있는 인스턴스가 자길 또 만들 수 없으므로).

### 3.4 대여·반환 흐름

```csharp
// 동기 수명(스코프 기반)
using var hit = pools.Pull<HitVfx>();   // Get → OnSpawn → (IActivatable면) SetActive(true)
hit.Play(at);
// 스코프 이탈 → Dispose() → Return → OnDespawn → SetActive(false)

// 비동기 수명(파티클 등) — 수명 뒤 명시적 Dispose (UniTask 지연)
```

### 3.5 풀 보유 & 프리팹 참조 (핵심 결정)

- **풀 보유**: `PoolRepository` 를 **GameContext 가 보유**하고 소비자에 **생성자/메서드 DI 주입**. (최근 UI 재설계·자동배선의 GameContext DI 흐름과 일관. 전역 정적 상태 없음 → 테스트 모킹 용이)
- **프리팹 참조**: **Repository 가 프리팹을 직접 들지 않는다.** Addressables `AssetReferenceGameObject`(약한참조)로 보유 → 필요 시점 lazy 비동기 로드, 미사용 시 언로드. → **이 때문에 Addressables 가 선행(TASK-015)**.
  - **파급**: `Pull<T>()` 가 첫 로드 시 비동기(`UniTask<T>`)가 된다. 프리워밍으로 핫패스 동기화 가능 여부를 TASK-013 에서 결정.

### 3.6 기존 자산 전면 교체 (A 결정)

기존 `ObjectPool<T>` 제거 → 신 `Pool<T>`. 기존 `IPoolable` 흡수. `EnemySpawner` 자체 Dictionary 풀도 공용 풀로 일원화(TASK-013 §검토).

---

## 4. 피격 VFX 적용 (첫 소비자, TASK-014 B-3)

- `VfxPlayer.SpawnOneShot` 의 `Instantiate`/`Destroy` 를 공용 풀 `Pull`/`Dispose` 로 교체.
- `Hit_Water` 를 `IPoolable`/`IActivatable`/`IPooledObject` 래퍼로 풀링.
- 가시성: 적(≈589배) 대비 스폰 스케일 확대 + 배경 작업 후 재검증 (TASK-014 §4·§5).

---

## 5. 다음 액션 (메인 세션, 의존 순서)

1. **TASK-015 — Addressables 인프라** (최선행, 우선순위 상)
   - `com.unity.addressables` 도입, 그룹·라벨 정책, `AssetReferenceGameObject` 로드 래퍼(UniTask), 빌드/플레이 검증.
2. **TASK-013 — 공용 풀링 시스템** (Addressables 의존)
   - §3 인터페이스·Creator·Repository·Pool·Pull/Dispose 구현 → 단위 테스트 → 14곳 마이그레이션 → 회귀.
3. **TASK-014 B-3 — 피격 VFX 풀링·확대** (풀링 의존)
   - `VfxPlayer` 풀링 교체 + Hit_Water 스케일 + 플레이 스크린샷 검증.

---

## 6. 미결정 / 메인 세션에서 정할 것

- `Pull<T>()` 비동기화 범위 — 전부 `UniTask<T>` vs 프리워밍 후 동기 핫패스.
- `EnemySpawner` 자체 풀을 공용 풀로 흡수할지(일원화) vs 유지.
- 외부 Hovl 스크립트(`HS_*`)의 `IPoolable` 의존 — 교체 대상에서 제외(외부 에셋) 확정 여부.
- `IActivatable` 시그니처 `SetActive(bool)` vs `Activate()/Deactivate()`.
- TDD: 신 `Pool<T>` 는 EditMode 단위 테스트 선작성(superpowers:test-driven-development).

---

## 7. 규약 체크 (구현 시)

- private 필드 `camelCase`(접두어 금지), 명시적 접근제한자, `event` 는 `On*`/핸들러 `Handle*`.
- 비동기는 **UniTask 만** (Addressables 로드도 `.ToUniTask()`), Coroutine·`System.Threading.Tasks` 금지.
- 라이프사이클 함수(`OnEnable`/`OnDisable`/`OnDestroy`)에 람다(`=>`) 본문 금지 — 일반 메서드 본문.
- 임시 컬렉션은 `UnityEngine.Pool.CollectionPool`, 필드 보관 컬렉션은 `new` 허용.
- 커밋 전 `lint` 스킬 게이트.
