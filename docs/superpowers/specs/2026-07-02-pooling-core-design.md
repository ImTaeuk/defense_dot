# 설계: 공용 풀링 코어 인프라 (TASK-013)

**작성일**: 2026-07-02
**상태**: 설계 확정 (사용자 검토 대기)
**선행**: TASK-015(Addressables `AssetLoader`) 완료 / **후속 소비자**: TASK-014 B-3(피격 VFX)
**관련**: `2026-07-01-pooling-addressables-design.md`(상위 핸드오프) · `2026-07-01-addressables-asset-loader-design.md`

---

## 1. 목적 / 범위

MonoBehaviour·POCO 를 모두 포괄하는 공용 풀링 시스템을 신설한다. 매 스폰마다 `Instantiate`/`Destroy`(GC) 하던 것을 재사용으로 바꾸고, 프리팹은 `AssetLoader`(Addressables, TASK-015)로 약한참조 로드한다.

**이번 범위 = 코어 인프라만**: 인터페이스 계층 · `Pool<T>` · Factory · `PoolManager` · 편의 베이스 · 데이터 선언. **마이그레이션**(기존 `ObjectPool<T>` 제거 · `IPoolable` 14곳 전환 · `EnemySpawner` 흡수 판단 · `VfxPlayer` 연결)은 본 코어 위에서 **후속 구현 계획**으로 순차 처리한다.

## 2. 설계 결정 (확정)

| # | 항목 | 결정 | 근거 |
|---|---|---|---|
| D1 | 관심사 3분리 | `IPoolable`(리셋 훅) · `IActivatable`(켜기/끄기+알림) · `IPooledObject`(반환) | 각자 한 가지 책임 |
| D2 | 활성 추상 | `Activate()`/`Deactivate()` + `OnActivated`/`OnDeactivated` 이벤트 | 외부(스폰 이펙트 등)가 객체를 안 건드리고 확장(관찰자) |
| D3 | 반환 API | `IPooledObject : IDisposable`, `Dispose()` = 풀 반환. 반환 동작은 PoolManager 가 `Get` 시 주입 | 외부 호출 가능(`using` 포함), 객체는 풀을 모름(얇은 결합) |
| D4 | 반환 타이밍 | **도메인이 결정** — 구체 타입의 개별 이벤트(`OnDied`/`OnFinished`)를 `Dispose` 에 연결 | "완료"의 의미가 타입마다 다름 → 제네릭 이벤트 강제는 과한 추상화 |
| D5 | `Pool<T>` 제약 | `where T : class, IPoolable, IActivatable` | `as`/널체크 제거(컴파일 보장). `class` = 값 타입 풀링(복사·박싱)의 실수 차단 |
| D6 | 생성 | `IPoolFactory<T>` + `PrefabFactory`(MB, AssetLoader)/`PocoFactory`(POCO) | MB/POCO 분기를 풀 밖 Factory 주입으로 캡슐화(개방-폐쇄) |
| D7 | 예열 진입점 | `PoolManager.PoolAsync(data)` | 이름의 본질을 "풀링"에 둠(사전/미리 아님) |
| D8 | 반환 안전망 | `PoolManager` 가 OUT(빌려나간) 장부 + 소유(부모→자식) 보유. `Return` 은 자식 연쇄 회수. `PoolManager.Dispose()` = 뿌리 절단(전량 회수) | 외부 타이밍·계층 반환·누수(Missing) 구조적 차단 |
| D9 | 편의 베이스 | `PooledBehaviour`(MB)/`PooledObject`(POCO) 제공 | `IActivatable`/`IPooledObject` 상속만으로 획득 → 14곳 마이그레이션 부담 최소 |
| D10 | 보유/DI | `PoolManager`(및 그 안 `AssetLoader`)를 `GameContext` 가 보유·주입 | 전역 static 없음, Arena 스코프 수명과 일치 |

## 3. 인터페이스 계층

```csharp
namespace DefenseDot.Core.Pooling
{
    /// <summary> 풀 재사용 시 자기 상태를 리셋하는 훅입니다. </summary>
    public interface IPoolable
    {
        void OnSpawn();
        void OnDespawn();
    }

    /// <summary> 켜기/끄기 추상 + 활성 전이 외부 알림입니다. </summary>
    public interface IActivatable
    {
        bool IsActive { get; }
        void Activate();
        void Deactivate();
        event System.Action OnActivated;    // Activate() 끝에 발화
        event System.Action OnDeactivated;  // Deactivate() 끝에 발화
    }

    /// <summary> Dispose() = 풀 반환(파괴 아님). 반환 동작은 PoolManager 가 Get 시 주입합니다. </summary>
    public interface IPooledObject : System.IDisposable { }
}
```

- `IPoolable.OnSpawn/OnDespawn` = **자기 상태 리셋**(내부용). `IActivatable.OnActivated/OnDeactivated` = **외부 관찰자 알림**(스폰 이펙트 등). 대상이 달라 둘 다 유지.
- 기존 `DefenseDot.Core.IPoolable`(OnSpawn/OnDespawn) 은 시그니처 동일 → **흡수**(네임스페이스만 `Core.Pooling` 으로 정리). 14곳 콜백 본문 무변경.

## 4. Pool<T> + Factory

```csharp
public interface IPoolFactory<T> where T : class { T Create(); }

public sealed class Pool<T> where T : class, IPoolable, IActivatable
{
    private readonly Queue<T> idle = new Queue<T>();
    private readonly IPoolFactory<T> factory;

    public Pool(IPoolFactory<T> factory) { this.factory = factory; }

    public T Get()
    {
        // 놀고 있으면 꺼내고, 없으면 팩토리로 새로 만든다
        T item = idle.Count > 0 ? idle.Dequeue() : factory.Create();
        item.OnSpawn();      // 자기 상태 리셋
        item.Activate();     // 켜기 + 외부 알림 (as 없이 직접 — 제약이 보장)
        return item;
    }

    public void Return(T item)
    {
        item.Deactivate();   // 끄기 + 외부 알림
        item.OnDespawn();    // 자기 정리
        idle.Enqueue(item);
    }
}
```

- `Get()` 의 "없으면 Create" = 큐가 비면 `factory.Create()` 로 즉석 생성 → 호출자는 항상 객체를 받음.
- 활성/훅을 `IActivatable`/`IPoolable` 로 **위임** → `Pool<T>` 는 MB냐 POCO냐를 모름.
- `PrefabFactory<T>`(MB): `AssetLoader.LoadAsync<GameObject>` 로 로드한 프리팹을 `Instantiate` → 컴포넌트 `T` 반환. `PocoFactory<T>`(POCO): `new T()`.

## 5. PoolManager (장부 · 연쇄 · 뿌리 절단)

`PoolManager` 는 `GameContext` 가 보유하며 `System.IDisposable`. **책임**:

- `UniTask PoolAsync(data)` — `data.effects`(EffectEntry[]) 열거 → 각 `AssetReference` 를 `AssetLoader.LoadAsync` → 에셋별 `Pool` 생성·예열. **레벨 진입 시 async 1회.**
- `T Get<T>(AssetReference reference, object owner = null)` — 프리팹 풀에서 꺼냄 + **반환 동작 주입** + **OUT 장부 등록** + `owner` 소유 등록. **동기**(이미 예열).
- `T Get<T>(object owner = null)` — POCO 풀(타입 키).
- `void Return(object obj)` — obj 소유 자식부터 **연쇄 회수** 후 obj 회수 + 장부 제거.
- `void Dispose()` — 장부의 **모든 객체 연쇄 Dispose** + 풀 정리 + `AssetLoader.ReleaseAll` (**뿌리 절단 = 누수 안전망**, Arena 스코프 종료와 일치).

**장부 구조(개념)**: `Dictionary<object,Pool>`(에셋키/타입키) + `HashSet<IPooledObject>`(OUT) + `Dictionary<object,List<IPooledObject>>`(소유 부모→자식).

```csharp
// 사용 그림
var m = poolManager.Get<Monster>(monsterRef);   // 반환 동작 주입 + OUT 등록
m.OnDied += m.Dispose;                            // 구체 타입이 자기 이벤트를 반환에 연결
// 사망 연출 끝 → OnDied 발화 → m.Dispose() → PoolManager 회수(+자식 연쇄)
// 레벨 끝 → poolManager.Dispose() → 장부의 남은 것 전량 회수
```

- **타이밍은 도메인이** 정한다(구체 타입이 `OnDied`/`OnFinished` 를 `Dispose` 에 연결). **메커니즘·안전망은 PoolManager 가**(주입·장부·연쇄·뿌리 절단).
- 재사용 간 이벤트 이중 구독 방지(`Return`/`OnDespawn` 시 해제)는 편의 베이스(§6)에서 처리.

## 6. 편의 베이스 (마이그레이션 경감)

풀 대상이 매번 `IActivatable`/`IPooledObject` 를 손으로 구현하지 않도록 베이스를 제공한다.

```csharp
public abstract class PooledBehaviour : MonoBehaviour, IPoolable, IActivatable, IPooledObject
{
    private System.Action returnToPool;   // PoolManager 가 Get 시 주입

    public bool IsActive => gameObject.activeSelf;
    public event System.Action OnActivated;
    public event System.Action OnDeactivated;

    public void Activate()   { gameObject.SetActive(true);  OnActivated?.Invoke(); }
    public void Deactivate() { gameObject.SetActive(false); OnDeactivated?.Invoke(); }

    public virtual void OnSpawn() { }
    public virtual void OnDespawn() { }

    internal void BindReturn(System.Action action) => returnToPool = action;  // PoolManager 전용
    public void Dispose() => returnToPool?.Invoke();
}
```

- MonoBehaviour 풀 대상은 `PooledBehaviour` 상속 후 `OnSpawn`/`OnDespawn` 만 오버라이드 → `IActivatable`·`IPooledObject` 는 공짜.
- POCO 용 `PooledObject`(논리 `isActive` 플래그 + 동일 계약)도 대칭 제공.
- `Dispose()` 의 `=>` 는 Unity 라이프사이클 함수가 아니므로 규약 위반 아님.

## 7. 데이터 선언 (EffectType) — TASK-015 확정본

```csharp
public enum EffectType { Hit, Muzzle, Cast, Death }

[System.Serializable]
public struct EffectEntry { public EffectType type; public AssetReferenceGameObject asset; }

// 스포너 데이터 SO
[SerializeField] private EffectEntry[] effects;
```

`PoolManager.PoolAsync(data)` 가 `effects` 를 열거해 에셋별 풀을 예열한다.

## 8. 검증 시나리오 (EditMode 단위테스트 우선)

`Pool<T>`·`PocoFactory` 는 Addressables 없이 순수 테스트 가능.

| # | 시나리오 | 기대 |
|---|---|---|
| 1 | `Get` → `Return` → `Get` | 같은 인스턴스 재사용(신규 생성 0) |
| 2 | 빈 풀에서 `Get` | 팩토리로 즉석 생성 |
| 3 | `Get`/`Return` 순서 | `OnSpawn`→`Activate` / `Deactivate`→`OnDespawn` 순서, `OnActivated`/`OnDeactivated` 발화 |
| 4 | `Dispose()` | 해당 객체가 풀로 반환(파괴 아님) |
| 5 | 소유 연쇄 | 부모 `Return` 시 자식 먼저 회수 |
| 6 | `PoolManager.Dispose()` | OUT 장부의 모든 객체 회수(누수 0) |
| 7 | `PrefabFactory`(Play) | AssetLoader 로드 → 컴포넌트 T 반환 |

## 9. 후속 (TASK-013 구현 계획으로)

- 기존 `ObjectPool<T>`(MonoBehaviour·SerializeField 프리팹) 제거 → 신 `Pool<T>`.
- `IPoolable` 14곳 전환(콜백 본문 유지 + `PooledBehaviour` 상속으로 `IActivatable`/`IPooledObject` 획득).
- `EnemySpawner` 자체 Dictionary 풀 → 공용 풀 일원화 여부 판단.
- `VfxPlayer.SpawnOneShot` 을 `PoolManager.Get`/`Dispose` 로 교체(TASK-014 B-3) + Hit_Water 스케일.
- 회귀: 기존 EditMode 스위트 그린 유지.
