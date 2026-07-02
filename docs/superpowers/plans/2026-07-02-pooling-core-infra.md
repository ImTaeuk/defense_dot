# 공용 풀링 코어 인프라 Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** MonoBehaviour·POCO 를 모두 포괄하는 재사용 풀링 코어 인프라(인터페이스 계층 · `Pool<T>` · Factory · `PoolManager` · 편의 베이스 · 데이터 선언)를 신설하고 EditMode 단위테스트로 방어한다.

**Architecture:** 관심사 3분리 인터페이스(`IPoolable` 리셋 / `IActivatable` 켜기·알림 / `IPooledObject` 반환) 위에 순수 `Pool<T>`(큐+Factory)를 두고, 그 위를 `PoolManager`가 감싸 반환 동작 주입·OUT 장부·소유 연쇄·뿌리 절단을 담당한다. MB/POCO 분기는 `IPoolFactory<T>` 주입으로 캡슐화하고, 프리팹은 `AssetLoader`(Addressables)로 약한참조 로드한다.

**Tech Stack:** Unity 6000.2.10f1, C#, UniTask 2.5.11, Addressables 3.1.0, NUnit EditMode

**선행:** TASK-015(Addressables `AssetLoader`) 완료 — `Assets/Scripts/Systems/Assets/AssetLoader.cs`, `LoadAsync<T>(AssetReference)`
**스펙:** `docs/superpowers/specs/2026-07-02-pooling-core-design.md`
**범위 밖(후속 마이그레이션 계획):** 기존 `ObjectPool<T>` 제거 · `DefenseDot.Core.IPoolable` 14곳 전환 · `EnemySpawner` 흡수 판단 · `VfxPlayer` 연결. 본 계획은 **코어 인프라 + DI 배선까지만**.

## Global Constraints

- 네이밍: C# PascalCase(타입/메서드/프로퍼티/이벤트), private 필드는 순수 `camelCase`(접두어 `m_`/`_` 금지)
- 접근 제한자: 모든 멤버에 명시적 선언(IDE0040)
- `System` 라이브러리는 using 없이 풀패스(`System.Action`, `System.IDisposable`). `System.Collections.Generic`(Dictionary/Queue/List/HashSet)만 using 허용
- 비동기: UniTask(async/await)만. Coroutine · `System.Threading.Tasks` 금지
- event 네이밍: `On` 접두사(`OnActivated`), 구독 핸들러는 `Handle` 접두사
- 주석: 한국어 `<summary>`, 인라인 주석은 필요 시 20자 이내·최대 2줄
- 신규 풀링 코드 네임스페이스: `DefenseDot.Core.Pooling` (기존 `DefenseDot.Core.IPoolable` 는 마이그레이션 전까지 무변경 — 두 `IPoolable` 은 네임스페이스로 공존, 풀링 파일은 `using DefenseDot.Core` 하지 않음)
- 파일명 = 파일 첫 타입명(PostToolUse `sync_cs_filename` 훅이 강제) → 각 파일 첫 타입을 파일명과 일치시킴
- 풀링 코드는 메인 asmdef `DefenseDot`(`Assets/Scripts/` 하위)에 위치 → 테스트 asmdef 가 접근 가능
- 커밋 메시지: `feat:`/`test:` 등 Conventional Commits, 한국어 본문 허용
- **린트 게이트**: 각 태스크 커밋 전 `lint` 스킬로 컨벤션 검증
- **unity-standards 게이트**: `.cs` 편집 전 세션에서 `unity-standards/references/*.md` 를 최소 1회 Read(마커 필요)

---

### Task 1: 인터페이스 계층 + Pool<T> + Factory 골격

**Files:**
- Create: `Assets/Scripts/Core/Pooling/IPoolable.cs` (첫 타입 `IPoolable`)
- Create: `Assets/Scripts/Core/Pooling/IPoolFactory.cs` (첫 타입 `IPoolFactory`)
- Create: `Assets/Scripts/Core/Pooling/Pool.cs` (첫 타입 `Pool`)
- Test: `Assets/Tests/EditMode/PoolTests.cs`

**Interfaces:**
- Consumes: (없음 — 코어 시작점)
- Produces:
  - `DefenseDot.Core.Pooling.IPoolable { void OnSpawn(); void OnDespawn(); }`
  - `DefenseDot.Core.Pooling.IActivatable { bool IsActive { get; } void Activate(); void Deactivate(); event System.Action OnActivated; event System.Action OnDeactivated; }`
  - `DefenseDot.Core.Pooling.IPooledObject : System.IDisposable {}`
  - `DefenseDot.Core.Pooling.IPoolFactory<T> where T : class { T Create(); }`
  - `DefenseDot.Core.Pooling.PocoFactory<T> : IPoolFactory<T> where T : class, new() { T Create(); }`
  - `internal DefenseDot.Core.Pooling.IPool { void ReturnObject(object item); void Clear(); }` — Task 3 이 소비
  - `DefenseDot.Core.Pooling.Pool<T> : IPool where T : class, IPoolable, IActivatable`
    - `Pool(IPoolFactory<T> factory)`
    - `T Get()` — 큐가 비면 `factory.Create()`, `OnSpawn()` 후 `Activate()`
    - `void Return(T item)` — `Deactivate()` 후 `OnDespawn()` 후 큐에 넣음

- [ ] **Step 1: 인터페이스 파일 작성**

`Assets/Scripts/Core/Pooling/IPoolable.cs`:

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
        event System.Action OnActivated;
        event System.Action OnDeactivated;
    }

    /// <summary> Dispose() = 풀 반환(파괴 아님). 반환 동작은 PoolManager 가 주입합니다. </summary>
    public interface IPooledObject : System.IDisposable
    {
    }
}
```

- [ ] **Step 2: Factory 인터페이스 + POCO 팩토리 작성**

`Assets/Scripts/Core/Pooling/IPoolFactory.cs`:

```csharp
namespace DefenseDot.Core.Pooling
{
    /// <summary> 풀이 새 인스턴스를 만들 때 호출하는 생성 창구입니다. </summary>
    public interface IPoolFactory<T> where T : class
    {
        T Create();
    }

    /// <summary> 매개변수 없는 생성자로 POCO 를 만드는 팩토리입니다. </summary>
    public sealed class PocoFactory<T> : IPoolFactory<T> where T : class, new()
    {
        public T Create() => new T();
    }
}
```

- [ ] **Step 3: Pool<T> + 비제네릭 IPool 작성**

`Assets/Scripts/Core/Pooling/Pool.cs`:

```csharp
using System.Collections.Generic;

namespace DefenseDot.Core.Pooling
{
    /// <summary>
    /// 큐 기반 재사용 풀입니다. 놀고 있으면 꺼내고, 없으면 팩토리로 새로 만듭니다.
    /// 활성/리셋은 IActivatable/IPoolable 로 위임하므로 MB·POCO 를 모릅니다.
    /// </summary>
    public sealed class Pool<T> : IPool where T : class, IPoolable, IActivatable
    {
        private readonly Queue<T> idle = new Queue<T>();
        private readonly IPoolFactory<T> factory;

        public Pool(IPoolFactory<T> factory)
        {
            this.factory = factory;
        }

        public T Get()
        {
            T item = idle.Count > 0 ? idle.Dequeue() : factory.Create();
            item.OnSpawn();
            item.Activate();
            return item;
        }

        public void Return(T item)
        {
            item.Deactivate();
            item.OnDespawn();
            idle.Enqueue(item);
        }

        void IPool.ReturnObject(object item) => Return((T)item);
        void IPool.Clear() => idle.Clear();
    }

    /// <summary> 서로 다른 T 의 Pool 을 한 레지스트리에 담기 위한 비제네릭 창구입니다. </summary>
    internal interface IPool
    {
        void ReturnObject(object item);
        void Clear();
    }
}
```

- [ ] **Step 4: 실패 테스트 작성**

`Assets/Tests/EditMode/PoolTests.cs`:

```csharp
using NUnit.Framework;
using DefenseDot.Core.Pooling;

namespace DefenseDot.Tests.EditMode
{
    /// <summary>
    /// Pool&lt;T&gt; 단위 테스트. 재사용·즉석생성·호출순서·이벤트를 방어한다.
    /// </summary>
    public class PoolTests
    {
        private sealed class Dummy : IPoolable, IActivatable
        {
            public int Spawns;
            public int Despawns;
            public int Activations;
            public int Deactivations;
            public System.Collections.Generic.List<string> Trace = new System.Collections.Generic.List<string>();

            public bool IsActive { get; private set; }
            public event System.Action OnActivated;
            public event System.Action OnDeactivated;

            public void OnSpawn() { Spawns++; Trace.Add("spawn"); }
            public void OnDespawn() { Despawns++; Trace.Add("despawn"); }
            public void Activate() { IsActive = true; Activations++; Trace.Add("activate"); OnActivated?.Invoke(); }
            public void Deactivate() { IsActive = false; Deactivations++; Trace.Add("deactivate"); OnDeactivated?.Invoke(); }
        }

        private sealed class DummyFactory : IPoolFactory<Dummy>
        {
            public int Created;
            public Dummy Create() { Created++; return new Dummy(); }
        }

        [Test]
        public void Get_ThenReturn_ThenGet_ReusesSameInstance()
        {
            var factory = new DummyFactory();
            var pool = new Pool<Dummy>(factory);

            Dummy first = pool.Get();
            pool.Return(first);
            Dummy second = pool.Get();

            Assert.AreSame(first, second, "반환 후 다시 꺼내면 같은 인스턴스");
            Assert.AreEqual(1, factory.Created, "재사용 시 신규 생성 0");
        }

        [Test]
        public void Get_EmptyPool_CreatesViaFactory()
        {
            var factory = new DummyFactory();
            var pool = new Pool<Dummy>(factory);

            pool.Get();
            pool.Get();

            Assert.AreEqual(2, factory.Created, "빈 풀에서 꺼낼 때마다 즉석 생성");
        }

        [Test]
        public void Get_RunsSpawnBeforeActivate_AndReturnRunsDeactivateBeforeDespawn()
        {
            var pool = new Pool<Dummy>(new DummyFactory());

            Dummy item = pool.Get();
            pool.Return(item);

            Assert.AreEqual(
                new[] { "spawn", "activate", "deactivate", "despawn" },
                item.Trace.ToArray(),
                "Get=리셋→켜기, Return=끄기→정리 순서");
            Assert.AreEqual(1, item.Activations);
            Assert.AreEqual(1, item.Deactivations);
        }
    }
}
```

- [ ] **Step 5: 컴파일 실패 확인**

Unity 로 컴파일 후 `read_console` 확인.
Expected: `PoolTests` 는 `Pool`/`IPoolable` 타입이 없으면 컴파일 에러 → Step 1~3 파일이 존재하면 컴파일 통과. (TDD RED 은 여기선 "타입 미존재 컴파일 에러"가 아니라, Step 1~3 을 먼저 작성했으므로 곧바로 GREEN 을 기대. 순수 로직이라 별도 최소구현 단계 불필요.)

- [ ] **Step 6: 테스트 실행 — 통과 확인**

`run_tests` (EditMode, 필터 `PoolTests`).
Expected: 3/3 PASS.

- [ ] **Step 7: 커밋**

```bash
git add Assets/Scripts/Core/Pooling/IPoolable.cs Assets/Scripts/Core/Pooling/IPoolable.cs.meta \
        Assets/Scripts/Core/Pooling/IPoolFactory.cs Assets/Scripts/Core/Pooling/IPoolFactory.cs.meta \
        Assets/Scripts/Core/Pooling/Pool.cs Assets/Scripts/Core/Pooling/Pool.cs.meta \
        Assets/Tests/EditMode/PoolTests.cs Assets/Tests/EditMode/PoolTests.cs.meta
git commit -m "feat: 풀링 인터페이스 계층 + Pool<T> + PocoFactory (TASK-013)"
```

---

### Task 2: 편의 베이스 (PooledObject POCO · PooledBehaviour MB)

**Files:**
- Create: `Assets/Scripts/Core/Pooling/PooledObject.cs` (첫 타입 `PooledObject`)
- Create: `Assets/Scripts/Core/Pooling/PooledBehaviour.cs` (첫 타입 `PooledBehaviour`)
- Test: `Assets/Tests/EditMode/PooledBaseTests.cs`

**Interfaces:**
- Consumes: `IPoolable`, `IActivatable`, `IPooledObject` (Task 1)
- Produces:
  - `internal DefenseDot.Core.Pooling.IReturnBindable { void BindReturn(System.Action action); }` — Task 3(PoolManager)이 소비
  - `DefenseDot.Core.Pooling.PooledObject : IPoolable, IActivatable, IPooledObject, IReturnBindable` — POCO 베이스. `bool IsActive`(논리 플래그), `Activate/Deactivate`+이벤트, `virtual OnSpawn/OnDespawn`, `Dispose()`=주입된 반환 호출
  - `DefenseDot.Core.Pooling.PooledBehaviour : MonoBehaviour, IPoolable, IActivatable, IPooledObject, IReturnBindable` — MB 베이스. `IsActive => gameObject.activeSelf`, `Activate/Deactivate`=`SetActive`+이벤트, `Dispose()`=반환

- [ ] **Step 1: PooledObject + IReturnBindable 작성**

`Assets/Scripts/Core/Pooling/PooledObject.cs`:

```csharp
namespace DefenseDot.Core.Pooling
{
    /// <summary> POCO 풀 대상 베이스입니다. 상속만으로 활성·반환 계약을 획득합니다. </summary>
    public abstract class PooledObject : IPoolable, IActivatable, IPooledObject, IReturnBindable
    {
        private System.Action returnToPool;

        public bool IsActive { get; private set; }
        public event System.Action OnActivated;
        public event System.Action OnDeactivated;

        public void Activate()
        {
            IsActive = true;
            OnActivated?.Invoke();
        }

        public void Deactivate()
        {
            IsActive = false;
            OnDeactivated?.Invoke();
        }

        public virtual void OnSpawn() { }
        public virtual void OnDespawn() { }

        void IReturnBindable.BindReturn(System.Action action) => returnToPool = action;
        public void Dispose() => returnToPool?.Invoke();
    }

    /// <summary> PoolManager 가 Get 시 반환 동작을 심기 위한 내부 계약입니다. </summary>
    internal interface IReturnBindable
    {
        void BindReturn(System.Action action);
    }
}
```

- [ ] **Step 2: PooledBehaviour 작성**

`Assets/Scripts/Core/Pooling/PooledBehaviour.cs`:

```csharp
using UnityEngine;

namespace DefenseDot.Core.Pooling
{
    /// <summary> MonoBehaviour 풀 대상 베이스입니다. 상속만으로 활성·반환 계약을 획득합니다. </summary>
    public abstract class PooledBehaviour : MonoBehaviour, IPoolable, IActivatable, IPooledObject, IReturnBindable
    {
        private System.Action returnToPool;

        public bool IsActive => gameObject.activeSelf;
        public event System.Action OnActivated;
        public event System.Action OnDeactivated;

        public void Activate()
        {
            gameObject.SetActive(true);
            OnActivated?.Invoke();
        }

        public void Deactivate()
        {
            gameObject.SetActive(false);
            OnDeactivated?.Invoke();
        }

        public virtual void OnSpawn() { }
        public virtual void OnDespawn() { }

        void IReturnBindable.BindReturn(System.Action action) => returnToPool = action;
        public void Dispose() => returnToPool?.Invoke();
    }
}
```

- [ ] **Step 3: 실패 테스트 작성**

`Assets/Tests/EditMode/PooledBaseTests.cs`:

```csharp
using NUnit.Framework;
using UnityEngine;
using DefenseDot.Core.Pooling;

namespace DefenseDot.Tests.EditMode
{
    /// <summary>
    /// 편의 베이스(PooledObject·PooledBehaviour) 테스트.
    /// 활성 플래그·이벤트·Dispose 반환 위임을 방어한다.
    /// </summary>
    public class PooledBaseTests
    {
        private sealed class Node : PooledObject { }

        private sealed class Behaviour : PooledBehaviour { }

        [Test]
        public void PooledObject_Activate_SetsFlagAndRaisesEvent()
        {
            var node = new Node();
            int activated = 0;
            int deactivated = 0;
            node.OnActivated += () => activated++;
            node.OnDeactivated += () => deactivated++;

            node.Activate();
            Assert.IsTrue(node.IsActive);
            node.Deactivate();
            Assert.IsFalse(node.IsActive);

            Assert.AreEqual(1, activated);
            Assert.AreEqual(1, deactivated);
        }

        [Test]
        public void PooledObject_Dispose_InvokesBoundReturn()
        {
            var node = new Node();
            int returned = 0;
            ((IReturnBindable)node).BindReturn(() => returned++);

            node.Dispose();

            Assert.AreEqual(1, returned, "Dispose 는 주입된 반환 동작을 호출");
        }

        [Test]
        public void PooledObject_Dispose_WithoutBinding_DoesNotThrow()
        {
            var node = new Node();
            Assert.DoesNotThrow(() => node.Dispose());
        }

        [Test]
        public void PooledBehaviour_ActivateDeactivate_TogglesGameObject()
        {
            var go = new GameObject("pooled");
            var behaviour = go.AddComponent<Behaviour>();

            behaviour.Deactivate();
            Assert.IsFalse(behaviour.IsActive, "Deactivate 는 gameObject 비활성");
            behaviour.Activate();
            Assert.IsTrue(behaviour.IsActive, "Activate 는 gameObject 활성");

            Object.DestroyImmediate(go);
        }

        [Test]
        public void PooledBehaviour_Dispose_InvokesBoundReturn()
        {
            var go = new GameObject("pooled");
            var behaviour = go.AddComponent<Behaviour>();
            int returned = 0;
            ((IReturnBindable)behaviour).BindReturn(() => returned++);

            behaviour.Dispose();

            Assert.AreEqual(1, returned);
            Object.DestroyImmediate(go);
        }
    }
}
```

- [ ] **Step 4: 컴파일 + 테스트 실행**

`run_tests` (EditMode, 필터 `PooledBaseTests`).
Expected: 5/5 PASS.

- [ ] **Step 5: 커밋**

```bash
git add Assets/Scripts/Core/Pooling/PooledObject.cs Assets/Scripts/Core/Pooling/PooledObject.cs.meta \
        Assets/Scripts/Core/Pooling/PooledBehaviour.cs Assets/Scripts/Core/Pooling/PooledBehaviour.cs.meta \
        Assets/Tests/EditMode/PooledBaseTests.cs Assets/Tests/EditMode/PooledBaseTests.cs.meta
git commit -m "feat: 풀링 편의 베이스 PooledObject/PooledBehaviour (TASK-013)"
```

---

### Task 3: PoolManager (장부 · 소유 연쇄 · 뿌리 절단) — POCO 경로

**Files:**
- Create: `Assets/Scripts/Core/Pooling/PoolManager.cs` (첫 타입 `PoolManager`)
- Test: `Assets/Tests/EditMode/PoolManagerTests.cs`

**Interfaces:**
- Consumes: `Pool<T>`(Task 1), `IPool`/`IReturnBindable`(Task 1·2), `PooledObject`(Task 2), `AssetLoader`(`DefenseDot.Systems.Assets.AssetLoader`, `LoadAsync<T>`/`ReleaseAll`)
- Produces:
  - `DefenseDot.Core.Pooling.PoolManager : System.IDisposable`
    - `PoolManager(DefenseDot.Systems.Assets.AssetLoader assetLoader)`
    - `T Get<T>(object owner = null) where T : class, IPoolable, IActivatable, IPooledObject, new()` — POCO 풀(키=typeof(T)). 반환 동작 주입 + OUT 장부 등록 + owner 소유 등록
    - `void Return(object obj)` — 소유 자식부터 연쇄 회수 후 자신 회수(이중 반환 가드)
    - `void Dispose()` — OUT 장부 전량 연쇄 회수 + 풀 정리 + `assetLoader.ReleaseAll()`
    - (프리팹 경로 `Get<T>(AssetReference,...)`·`PoolAsync` 는 Task 4 에서 추가)

- [ ] **Step 1: PoolManager 작성 (POCO 경로 + 장부/연쇄/Dispose)**

`Assets/Scripts/Core/Pooling/PoolManager.cs`:

```csharp
using System.Collections.Generic;
using DefenseDot.Systems.Assets;

namespace DefenseDot.Core.Pooling
{
    /// <summary>
    /// 풀들을 감싸 반환 동작 주입·OUT 장부·소유 연쇄·뿌리 절단을 담당합니다.
    /// 타이밍은 도메인이(구체 타입 이벤트→Dispose), 메커니즘·안전망은 이 매니저가 맡습니다.
    /// </summary>
    public sealed class PoolManager : System.IDisposable
    {
        private readonly AssetLoader assetLoader;
        // 키: POCO=typeof(T), 프리팹=RuntimeKey
        private readonly Dictionary<object, IPool> pools = new Dictionary<object, IPool>();
        // 빌려나간 객체(OUT) → 소속 풀
        private readonly Dictionary<IPooledObject, IPool> origin = new Dictionary<IPooledObject, IPool>();
        // OUT 집합(이중 반환 가드)
        private readonly HashSet<IPooledObject> live = new HashSet<IPooledObject>();
        // 소유 부모 → 자식들
        private readonly Dictionary<IPooledObject, List<IPooledObject>> owned
            = new Dictionary<IPooledObject, List<IPooledObject>>();

        public PoolManager(AssetLoader assetLoader)
        {
            this.assetLoader = assetLoader;
        }

        /// <summary> POCO 풀에서 꺼냅니다. 타입을 키로 쓰고 없으면 즉석 생성합니다. </summary>
        public T Get<T>(object owner = null) where T : class, IPoolable, IActivatable, IPooledObject, new()
        {
            Pool<T> pool = ResolvePocoPool<T>();
            T item = pool.Get();
            Track(item, pool, owner);
            return item;
        }

        private Pool<T> ResolvePocoPool<T>() where T : class, IPoolable, IActivatable, new()
        {
            object key = typeof(T);
            if (pools.TryGetValue(key, out IPool existing)) return (Pool<T>)existing;
            var pool = new Pool<T>(new PocoFactory<T>());
            pools[key] = pool;
            return pool;
        }

        // 반환 동작 주입 + 장부/소유 등록 (프리팹 경로도 공용)
        internal void Track(IPooledObject item, IPool pool, object owner)
        {
            live.Add(item);
            origin[item] = pool;
            if (item is IReturnBindable bindable)
                bindable.BindReturn(() => Return(item));
            if (owner is IPooledObject parent)
            {
                if (!owned.TryGetValue(parent, out List<IPooledObject> list))
                {
                    list = new List<IPooledObject>();
                    owned[parent] = list;
                }
                list.Add(item);
            }
        }

        /// <summary> 객체를 풀로 되돌립니다. 소유 자식이 있으면 먼저 연쇄 회수합니다. </summary>
        public void Return(object obj)
        {
            if (obj is not IPooledObject pooled) return;
            if (!live.Contains(pooled)) return;   // 이미 반환됨 — 이중 반환 가드

            if (owned.TryGetValue(pooled, out List<IPooledObject> children))
            {
                for (int i = children.Count - 1; i >= 0; i--) Return(children[i]);
                owned.Remove(pooled);
            }

            live.Remove(pooled);
            if (origin.TryGetValue(pooled, out IPool pool))
                pool.ReturnObject(pooled);
        }

        /// <summary> 남은 OUT 을 전량 연쇄 회수하고 풀·에셋을 정리합니다(뿌리 절단). </summary>
        public void Dispose()
        {
            var snapshot = new List<IPooledObject>(live);
            foreach (IPooledObject o in snapshot)
                if (live.Contains(o)) Return(o);

            foreach (IPool p in pools.Values) p.Clear();
            pools.Clear();
            origin.Clear();
            live.Clear();
            owned.Clear();
            assetLoader?.ReleaseAll();
        }
    }
}
```

- [ ] **Step 2: 실패 테스트 작성**

`Assets/Tests/EditMode/PoolManagerTests.cs`:

```csharp
using System.Collections.Generic;
using NUnit.Framework;
using DefenseDot.Core.Pooling;
using DefenseDot.Systems.Assets;

namespace DefenseDot.Tests.EditMode
{
    /// <summary>
    /// PoolManager 단위 테스트(POCO 경로).
    /// 재사용·자기 Dispose 반환·소유 연쇄·뿌리 절단(누수 0)을 방어한다.
    /// </summary>
    public class PoolManagerTests
    {
        private sealed class Node : PooledObject
        {
            public int DespawnCount;
            public override void OnDespawn() => DespawnCount++;
        }

        private static PoolManager NewManager() => new PoolManager(new AssetLoader());

        [Test]
        public void Get_ThenReturn_ThenGet_ReusesInstance()
        {
            PoolManager m = NewManager();

            Node a = m.Get<Node>();
            m.Return(a);
            Node b = m.Get<Node>();

            Assert.AreSame(a, b, "반환 후 재요청은 같은 인스턴스");
        }

        [Test]
        public void Dispose_OnPooledObject_ReturnsItToPool()
        {
            PoolManager m = NewManager();

            Node a = m.Get<Node>();
            a.Dispose();               // 자기 반환 API
            Node b = m.Get<Node>();

            Assert.AreSame(a, b, "Dispose 가 곧 풀 반환");
            Assert.AreEqual(1, a.DespawnCount, "반환 시 OnDespawn 1회");
        }

        [Test]
        public void Return_Parent_CascadesToChildrenFirst()
        {
            PoolManager m = NewManager();

            Node parent = m.Get<Node>();
            Node child = m.Get<Node>(owner: parent);

            m.Return(parent);

            Assert.AreEqual(1, child.DespawnCount, "부모 반환 시 자식이 먼저 회수");
            // 자식이 풀로 돌아갔는지: 다음 요청이 자식 재사용
            Assert.AreSame(child, m.Get<Node>(), "회수된 자식이 재사용됨");
        }

        [Test]
        public void Dispose_Manager_ReclaimsAllLiveObjects()
        {
            PoolManager m = NewManager();

            Node a = m.Get<Node>();
            Node b = m.Get<Node>();
            Node c = m.Get<Node>(owner: a);

            m.Dispose();               // 뿌리 절단

            Assert.AreEqual(1, a.DespawnCount, "a 회수");
            Assert.AreEqual(1, b.DespawnCount, "b 회수");
            Assert.AreEqual(1, c.DespawnCount, "소유 자식 c 회수 — 누수 0");
        }

        [Test]
        public void Return_Twice_DoesNotDoubleDespawn()
        {
            PoolManager m = NewManager();

            Node a = m.Get<Node>();
            m.Return(a);
            m.Return(a);               // 이중 반환

            Assert.AreEqual(1, a.DespawnCount, "이중 반환은 무시");
        }
    }
}
```

- [ ] **Step 3: 컴파일 + 테스트 실행**

`run_tests` (EditMode, 필터 `PoolManagerTests`).
Expected: 5/5 PASS.

- [ ] **Step 4: 커밋**

```bash
git add Assets/Scripts/Core/Pooling/PoolManager.cs Assets/Scripts/Core/Pooling/PoolManager.cs.meta \
        Assets/Tests/EditMode/PoolManagerTests.cs Assets/Tests/EditMode/PoolManagerTests.cs.meta
git commit -m "feat: PoolManager 장부·소유 연쇄·뿌리 절단 (POCO 경로) (TASK-013)"
```

---

### Task 4: 프리팹 경로 (EffectType 데이터 · PrefabFactory · PoolAsync · Get(AssetReference))

**Files:**
- Create: `Assets/Scripts/Core/Pooling/EffectType.cs` (첫 타입 `EffectType`)
- Modify: `Assets/Scripts/Core/Pooling/IPoolFactory.cs` (`PrefabFactory` 추가)
- Modify: `Assets/Scripts/Core/Pooling/PoolManager.cs` (프리팹 풀 필드 + `PoolAsync` + `Get<T>(AssetReference,...)`)
- Verify: Unity `execute_code` (Play 아님, 에디터 인메모리 — Addressables `WaitForCompletion`)

**Interfaces:**
- Consumes: `PoolManager`(Task 3, `Track` 내부 메서드), `PooledBehaviour`(Task 2), `AssetLoader.LoadAsync<GameObject>`
- Produces:
  - `DefenseDot.Core.Pooling.EffectType : enum { Hit, Muzzle, Cast, Death }`
  - `DefenseDot.Core.Pooling.EffectEntry : struct { EffectType type; AssetReferenceGameObject asset; }`
  - `DefenseDot.Core.Pooling.PrefabFactory : IPoolFactory<PooledBehaviour>` — `PrefabFactory(GameObject prefab)`, `Create()` = `Instantiate` 후 `GetComponent<PooledBehaviour>()`
  - `PoolManager.PoolAsync(IEnumerable<EffectEntry> entries) : UniTask` — 에셋별 `Pool<PooledBehaviour>` 예열(RuntimeKey 키, 중복 스킵)
  - `PoolManager.Get<T>(AssetReference reference, object owner = null) where T : PooledBehaviour` — 예열된 프리팹 풀에서 꺼내 `T` 로 캐스팅

- [ ] **Step 1: EffectType/EffectEntry 데이터 작성**

`Assets/Scripts/Core/Pooling/EffectType.cs`:

```csharp
using UnityEngine.AddressableAssets;

namespace DefenseDot.Core.Pooling
{
    /// <summary> 이펙트 용도 분류입니다. </summary>
    public enum EffectType
    {
        Hit,
        Muzzle,
        Cast,
        Death
    }

    /// <summary> 이펙트 용도 → Addressables 프리팹 약한참조 매핑입니다. </summary>
    [System.Serializable]
    public struct EffectEntry
    {
        public EffectType type;
        public AssetReferenceGameObject asset;
    }
}
```

- [ ] **Step 2: PrefabFactory 추가**

`Assets/Scripts/Core/Pooling/IPoolFactory.cs` 에 아래 클래스를 `PocoFactory` 뒤에 추가:

```csharp
    /// <summary> 로드된 프리팹을 Instantiate 해 PooledBehaviour 컴포넌트를 반환하는 팩토리입니다. </summary>
    public sealed class PrefabFactory : IPoolFactory<PooledBehaviour>
    {
        private readonly UnityEngine.GameObject prefab;

        public PrefabFactory(UnityEngine.GameObject prefab)
        {
            this.prefab = prefab;
        }

        public PooledBehaviour Create()
        {
            UnityEngine.GameObject go = UnityEngine.Object.Instantiate(prefab);
            return go.GetComponent<PooledBehaviour>();
        }
    }
```

- [ ] **Step 3: PoolManager 에 프리팹 경로 추가**

`Assets/Scripts/Core/Pooling/PoolManager.cs` 상단 using 에 추가:

```csharp
using Cysharp.Threading.Tasks;
using UnityEngine.AddressableAssets;
```

`PoolManager` 클래스 안, `Get<T>(object owner = null)` 아래에 추가:

```csharp
        /// <summary> 스포너 데이터의 이펙트 목록을 에셋별로 로드·예열합니다(레벨 진입 시 1회). </summary>
        public async UniTask PoolAsync(IEnumerable<EffectEntry> entries)
        {
            foreach (EffectEntry entry in entries)
            {
                object key = entry.asset.RuntimeKey;
                if (pools.ContainsKey(key)) continue;
                UnityEngine.GameObject prefab = await assetLoader.LoadAsync<UnityEngine.GameObject>(entry.asset);
                pools[key] = new Pool<PooledBehaviour>(new PrefabFactory(prefab));
            }
        }

        /// <summary> 예열된 프리팹 풀에서 꺼냅니다. 동기(이미 로드됨). </summary>
        public T Get<T>(AssetReference reference, object owner = null) where T : PooledBehaviour
        {
            object key = reference.RuntimeKey;
            var pool = (Pool<PooledBehaviour>)pools[key];
            PooledBehaviour item = pool.Get();
            Track(item, pool, owner);
            return (T)item;
        }
```

- [ ] **Step 4: 컴파일 확인**

Unity 컴파일 후 `read_console` — 에러 0 확인.
Expected: 컴파일 통과, 경고/에러 없음.

- [ ] **Step 5: 인에디터 검증 (Addressables 프리팹 경로)**

Unity `execute_code` 로 아래 스크립트 실행(기존 Addressable `Hit_Water` 프리팹 사용 — TASK-015 에서 Arena/vfx,hit 로 마킹됨). PlayMode 없이 `WaitForCompletion` 으로 동기 확인:

```csharp
using UnityEngine;
using UnityEditor;
using DefenseDot.Core.Pooling;
using DefenseDot.Systems.Assets;
using UnityEngine.AddressableAssets;
using Cysharp.Threading.Tasks;

internal class CommandScript : IRunCommand
{
    public void Execute(ExecutionResult result)
    {
        // Hit_Water 의 AssetReferenceGameObject 를 GUID 로 구성
        string path = "Assets/.../Hit_Water.prefab"; // 실제 경로로 교체
        string guid = AssetDatabase.AssetPathToGUID(path);
        var reference = new AssetReferenceGameObject(guid);

        var loader = new AssetLoader();
        var manager = new PoolManager(loader);

        var entries = new System.Collections.Generic.List<EffectEntry>
        {
            new EffectEntry { type = EffectType.Hit, asset = reference }
        };
        // 예열 — WaitForCompletion 으로 동기화
        manager.PoolAsync(entries).ToUniTask().Forget();
        UnityEngine.AddressableAssets.Addressables.LoadAssetAsync<GameObject>(reference)
            .WaitForCompletion();

        var fx = manager.Get<PooledBehaviour>(reference);
        result.Log("Get 결과: {0}, IsActive={1}", fx, fx != null && fx.IsActive);

        fx.Dispose();
        var fx2 = manager.Get<PooledBehaviour>(reference);
        result.Log("재사용 동일 인스턴스: {0}", fx == fx2);

        manager.Dispose();
    }
}
```

주의: 대상 프리팹 루트에 `PooledBehaviour` 파생 컴포넌트가 없으면 `Create()` 의 `GetComponent` 가 null → 이 검증은 마이그레이션(후속)에서 실제 이펙트가 `PooledBehaviour` 를 상속한 뒤 완결된다. **본 태스크의 프리팹 경로는 "컴파일·API 형태 검증"까지가 완료 기준**이며, 실효 검증은 마이그레이션 계획의 첫 소비자(VfxPlayer)에서 수행한다.
Expected: 컴파일 성공 + `PoolAsync`/`Get(AssetReference)` 호출이 예외 없이 실행(프리팹에 PooledBehaviour 있으면 재사용 true).

- [ ] **Step 6: 커밋**

```bash
git add Assets/Scripts/Core/Pooling/EffectType.cs Assets/Scripts/Core/Pooling/EffectType.cs.meta \
        Assets/Scripts/Core/Pooling/IPoolFactory.cs \
        Assets/Scripts/Core/Pooling/PoolManager.cs
git commit -m "feat: 풀링 프리팹 경로 PrefabFactory/PoolAsync/Get(AssetReference) (TASK-013)"
```

---

### Task 5: GameContext DI 배선 (D10 capstone)

**Files:**
- Modify: `Assets/Scripts/Domain/GameContext.cs` (필드 + 생성자 인자 추가)
- Modify: `Assets/Scripts/Systems/Management/GameManager.cs` (`AssetLoader`·`PoolManager` 생성·주입·해제)

**Interfaces:**
- Consumes: `PoolManager`(Task 3·4), `AssetLoader`
- Produces: `GameContext.Pooling { get; }` (`DefenseDot.Core.Pooling.PoolManager`)

**Note:** `GameContext` 생성 지점은 `GameManager.cs:119` 단 하나(확인 완료). `PoolManager` 인자를 **생성자 맨 끝**에 추가한다. 아직 소비자는 없지만(마이그레이션에서 소비), 이 태스크로 D10(GameContext 소유) 결정이 컴파일·주입·해제까지 성립함을 검증한다.

- [ ] **Step 1: GameContext 에 Pooling 추가**

`Assets/Scripts/Domain/GameContext.cs`:
- 파일 상단 using 에 `using DefenseDot.Core.Pooling;` 추가
- `CoreTarget` 프로퍼티 아래에 추가:

```csharp
        /// <summary> 공용 풀링 매니저입니다. </summary>
        public PoolManager Pooling { get; }
```

- 생성자 시그니처 맨 끝 인자 추가 + 대입:

```csharp
        public GameContext(EconomyModel economy, CoreModel core, WaveModel wave, ScoreModel score,
            RoundTimerModel timer, GameFlowModel flow, LevelModel level, int enemyCapacity,
            TowerRoster roster, TowerPlacementController placement, ArenaCardConfig cardConfig,
            AbilityPool abilityPool, ICardCommandTarget coreTarget, PoolManager pooling)
        {
            Economy = economy; Core = core; Wave = wave; Score = score; Timer = timer;
            Flow = flow; Level = level; EnemyCapacity = enemyCapacity; Roster = roster;
            Placement = placement; CardConfig = cardConfig; AbilityPool = abilityPool;
            CoreTarget = coreTarget; Pooling = pooling;
        }
```

- [ ] **Step 2: GameManager 에서 생성·주입**

`Assets/Scripts/Systems/Management/GameManager.cs`:
- 클래스 필드에 추가(다른 private 필드 근처):

```csharp
        private DefenseDot.Core.Pooling.PoolManager poolManager;
```

- `GameContext` 생성 직전(현 118 라인 부근)에 생성:

```csharp
                var assetLoader = new DefenseDot.Systems.Assets.AssetLoader();
                poolManager = new DefenseDot.Core.Pooling.PoolManager(assetLoader);
```

- `new DefenseDot.Domain.GameContext(...)` 호출 마지막 인자에 `poolManager` 추가:

```csharp
                var ctx = new DefenseDot.Domain.GameContext(
                    Economy, Core, Wave, Score, RoundTimer, Flow, Level,
                    modeBootstrap.EnemyDisplayCapacity, towerRoster,
                    modeBootstrap.PlacementController, cardConfig, abilityPool, coreTarget, poolManager);
```

- [ ] **Step 3: GameManager 해제 배선**

`GameManager` 의 `OnDestroy` 를 찾아(없으면 신설) `poolManager` 를 해제한다. 신설 시:

```csharp
        private void OnDestroy()
        {
            poolManager?.Dispose();
        }
```

기존 `OnDestroy` 가 있으면 그 안에 `poolManager?.Dispose();` 한 줄만 추가. (`OnDestroy` 는 Unity 라이프사이클 함수이므로 `=>` 표현식 본문 금지 — 반드시 블록 본문)

- [ ] **Step 4: 컴파일 + 전체 회귀 테스트**

`run_tests` (EditMode 전체).
Expected: 기존 스위트 그린 유지 + 신규 풀링 테스트 포함 전량 PASS(회귀 0). `read_console` 에러 0.

- [ ] **Step 5: 린트 게이트**

`lint` 스킬로 변경 `.cs`(GameContext.cs, GameManager.cs) 컨벤션 검증. 위반 시 참조 추적 수정.

- [ ] **Step 6: 커밋**

```bash
git add Assets/Scripts/Domain/GameContext.cs Assets/Scripts/Systems/Management/GameManager.cs
git commit -m "feat: PoolManager 를 GameContext DI 로 배선 + GameManager 생성/해제 (TASK-013)"
```

---

## 완료 기준 (Definition of Done)

- 신규 파일 7개(`IPoolable`, `IPoolFactory`, `Pool`, `PooledObject`, `PooledBehaviour`, `PoolManager`, `EffectType`) + 테스트 3개(`PoolTests`, `PooledBaseTests`, `PoolManagerTests`)
- EditMode 전량 그린(신규 13개 케이스 + 기존 회귀 0)
- `PoolManager` 가 `GameContext.Pooling` 으로 DI 주입·`GameManager` 에서 생성·해제
- 프리팹 경로는 API·컴파일 검증 완료(실효 검증은 후속 마이그레이션의 첫 소비자에서)
- 기존 `DefenseDot.Core.IPoolable`·`ObjectPool<T>`·14 사용처 **무변경**(마이그레이션 계획으로 이월)

## 후속 (별도 마이그레이션 계획)

- `ObjectPool<T>`(SerializeField 프리팹) 제거 → 신 `Pool<T>`/`PoolManager`
- `DefenseDot.Core.IPoolable` 14곳 → `PooledBehaviour` 상속 전환(콜백 본문 유지)
- `EnemySpawner` 자체 Dictionary 풀 일원화 여부 판단
- `VfxPlayer.SpawnOneShot` → `PoolManager.Get`/`Dispose`(TASK-014 B-3) + Hit_Water 스케일, 프리팹 경로 실효 검증
- 회귀: `EnemyBehaviorTreeTests`·`TowerBehaviorTreeTests` 그린 유지
