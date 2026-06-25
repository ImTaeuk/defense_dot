# Arena A2 코어 자동전투 Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Arena 코어가 능력으로 자동 전투하도록 능력 실행 시스템(러너·합성 추상화·효과 엔티티)과 스타터 능력 2종(샷·오비탈)을 구축한다.

**Architecture:** 능력은 단일 `Tick` 진입점 + 쿨다운 헬퍼/`IAbilityLifecycle` 합성. 시간축 거동은 자가구동 `AbilityEffect`(Hovl VFX 비주얼)에 캡슐화. `AbilityRunner`(순수 C#)가 매 프레임 액티브를 Tick하고 `CoreAbilitySystem`이 코어에서 구동. 풀링은 `IEffectSpawner` 심으로 분리(A2는 Instantiate/Destroy, 풀은 TASK-013).

**Tech Stack:** Unity 6000.4, C#, NUnit EditMode, UniTask(기존), Hovl Studio VFX(ExternalResources).

## Global Constraints

- private 필드 순수 `camelCase`(접두어 금지). 모든 멤버 접근제한자 명시(IDE0040).
- `System.*` 풀패스(예: `System.Action`), `System.Collections.Generic`만 using 허용.
- 비동기는 UniTask만(본 A2는 비동기 없음). 이벤트 `On`/핸들러 `Handle` 접두.
- 주석 한국어 `<summary>`, 인라인 20자 이내.
- 능력계는 **Arena 코어 한정** — Grid 타워(디버그 단일공격) 무수정.
- 풀링은 본 계획 제외(TASK-013). `IEffectSpawner` 교체 심만 둠.
- 커밋은 **사용자 명시 요청 시에만** `commit` 스킬로. 커밋 전 변경 `.cs`는 `lint` 스킬.

## 핵심 타입 사실(확정)

- `DefenseDot.Core`: `IPoolable{OnSpawn();OnDespawn()}`, `IDamageable:IActor{TakeDamage(float)}`, `ITargetable:IActor{bool IsActive}`. `IActor`에 `Position`.
- `TargetFinder`(DefenseDot.Systems.Tower): `ITargetable FindNearest(Vector3 origin,float range)`, `void FindAllInRange(Vector3,float,List<ITargetable>)`.
- A1(DefenseDot.Systems.Abilities): `AbilityData:ScriptableObject{string id;displayName;Sprite icon;int rarity;int maxLevel}`, `ActiveAbilityData:AbilityData{float baseCooldown;ValueAtLevel;CooldownAtLevel;abstract Execute}`(→Tick로 교체), `AbilityInstance{readonly AbilityData data;int level;float cooldownRemaining}`, `AbilityContext(readonly struct){Host,Origin,Finder,Modifiers}`, `AbilityModifiers{float damageBonus;float cooldownReduction}`, `AbilityLoadout{IReadOnlyList<AbilityInstance> Actives/Passives;AbilityModifiers Modifiers;TryAdd;LevelUp;Remove}`.
- 적 사망→처치 집계는 기존 경로(`MonsterActor.TakeDamage`→사망→`EnemySpawner.HandleEnemyKilled`→Combat/Score). **A2 변경 없음.**
- 중앙 타워 디버그 전투는 `TowerBehaviorTree`(MonoBehaviour)가 `UpdateCombat`로 구동 → 코어에선 이 컴포넌트 제거.
- EditMode 어셈블리는 단일 `DefenseDot` 참조(asmdef 수정 불필요). 기존 `AbilityLoadoutTests.StubActive`가 `ActiveAbilityData` 상속 → Execute→Tick 교체 시 함께 수정.

---

## File Structure

| 파일 | 책임 | 신규/수정 |
|---|---|---|
| `Assets/Scripts/Systems/Abilities/Effects/IEffectSpawner.cs` | 효과 스폰/반납 계약(풀링 심) | 신규 |
| `Assets/Scripts/Systems/Abilities/Effects/AbilityEffect.cs` | 자가구동 효과 베이스(Release) | 신규 |
| `Assets/Scripts/Systems/Abilities/Effects/SimpleEffectSpawner.cs` | A2 Instantiate/Destroy 구현 | 신규 |
| `Assets/Scripts/Systems/Abilities/Effects/ProjectileEffect.cs` | 유도·이동·관통·데미지 | 신규 |
| `Assets/Scripts/Systems/Abilities/Effects/OrbiterSetEffect.cs` | 회전 위성·접촉 데미지 | 신규 |
| `Assets/Scripts/Systems/Abilities/ActiveAbilityData.cs` | Execute→Tick + 쿨다운 헬퍼 | 수정 |
| `Assets/Scripts/Systems/Abilities/IAbilityLifecycle.cs` | 상시 수명 인터페이스 | 신규 |
| `Assets/Scripts/Systems/Abilities/AbilityInstance.cs` | runtimeState 추가 | 수정 |
| `Assets/Scripts/Systems/Abilities/AbilityContext.cs` | Effects 추가 | 수정 |
| `Assets/Scripts/Systems/Abilities/AbilityRunner.cs` | 매 프레임 러너 | 신규 |
| `Assets/Scripts/Systems/Abilities/Definitions/ProjectileAbilityData.cs` | 샷 템플릿(이산) | 신규 |
| `Assets/Scripts/Systems/Abilities/Definitions/OrbitalAbilityData.cs` | 오비탈 템플릿(상시) | 신규 |
| `Assets/Scripts/Systems/Abilities/CoreAbilitySystem.cs` | 코어 능력 구동 MB | 신규 |
| `Assets/Scripts/Systems/Mode/ArenaModeBootstrap.cs` | starter·CoreAbilitySystem 배선 | 수정 |
| `Assets/Tests/EditMode/CooldownHelperTests.cs` | 쿨다운 헬퍼 | 신규 |
| `Assets/Tests/EditMode/AbilityRunnerTests.cs` | 러너·라이프사이클 | 신규 |
| `Assets/Tests/EditMode/AbilityLoadoutTests.cs` | StubActive Execute→Tick | 수정 |

---

## Task 1: 효과 레이어 베이스 (IEffectSpawner · AbilityEffect · SimpleEffectSpawner)

**Files:**
- Create: `Assets/Scripts/Systems/Abilities/Effects/IEffectSpawner.cs`
- Create: `Assets/Scripts/Systems/Abilities/Effects/AbilityEffect.cs`
- Create: `Assets/Scripts/Systems/Abilities/Effects/SimpleEffectSpawner.cs`

**Interfaces:**
- Produces: `IEffectSpawner{ T Spawn<T>(T prefab) where T:AbilityEffect; void Release(AbilityEffect) }`, `abstract AbilityEffect:MonoBehaviour,IPoolable{ void Bind(IEffectSpawner); protected void Release() }`, `SimpleEffectSpawner(Transform container=null)`.

- [ ] **Step 1: IEffectSpawner 작성**

`Effects/IEffectSpawner.cs`:
```csharp
namespace DefenseDot.Systems.Abilities.Effects
{
    /// <summary> 효과 엔티티 스폰·반납 계약입니다. (A2 단순 구현, 풀링은 TASK-013) </summary>
    public interface IEffectSpawner
    {
        /// <summary> 효과 프리팹을 스폰해 반환합니다. </summary>
        T Spawn<T>(T prefab) where T : AbilityEffect;

        /// <summary> 효과를 반납(또는 파괴)합니다. </summary>
        void Release(AbilityEffect fx);
    }
}
```

- [ ] **Step 2: AbilityEffect 베이스 작성**

`Effects/AbilityEffect.cs`:
```csharp
// 자가구동 효과 엔티티 베이스 — 반납은 스포너에 위임(풀링 심)
using UnityEngine;
using DefenseDot.Core;

namespace DefenseDot.Systems.Abilities.Effects
{
    /// <summary>
    /// 능력이 스폰하는 자가구동 효과의 베이스입니다.
    /// 시간축 거동·데미지는 서브클래스 Update가 수행하고, 종료 시 Release로 반납합니다.
    /// </summary>
    public abstract class AbilityEffect : MonoBehaviour, IPoolable
    {
        private IEffectSpawner spawner;

        /// <summary> 반납 대상 스포너를 주입합니다. </summary>
        public void Bind(IEffectSpawner effectSpawner) { spawner = effectSpawner; }

        /// <summary> 효과를 스포너로 반납합니다. (스포너 없으면 파괴) </summary>
        protected void Release()
        {
            if (spawner != null) spawner.Release(this);
            else Destroy(gameObject);
        }

        /// <summary> 풀 재사용 시 초기화 훅입니다. </summary>
        public virtual void OnSpawn() { }

        /// <summary> 반납 시 정리 훅입니다. </summary>
        public virtual void OnDespawn() { }
    }
}
```

- [ ] **Step 3: SimpleEffectSpawner 작성**

`Effects/SimpleEffectSpawner.cs`:
```csharp
// A2 임시 스포너 — Instantiate/Destroy. 풀링은 TASK-013에서 교체.
using UnityEngine;

namespace DefenseDot.Systems.Abilities.Effects
{
    /// <summary> 풀링 없이 Instantiate/Destroy로 동작하는 임시 스포너입니다. </summary>
    public sealed class SimpleEffectSpawner : IEffectSpawner
    {
        private readonly Transform container;

        public SimpleEffectSpawner(Transform container = null) { this.container = container; }

        public T Spawn<T>(T prefab) where T : AbilityEffect
        {
            T fx = Object.Instantiate(prefab, container);
            fx.Bind(this);
            fx.OnSpawn();
            return fx;
        }

        public void Release(AbilityEffect fx)
        {
            if (fx == null) return;
            fx.OnDespawn();
            Object.Destroy(fx.gameObject);
        }
    }
}
```

- [ ] **Step 4: 컴파일 확인**

Run: `mcp__UnityMCP__refresh_unity`(scope=all, mode=force) → `mcp__UnityMCP__read_console`(error).
Expected: 에러 없음.

- [ ] **Step 5: 커밋 (사용자 요청 시)** — `lint` 후 `commit`. 예: `feat: 능력 효과 엔티티 베이스·스포너 추가`

---

## Task 2: 능력 레이어 계약 정비 (Tick + 헬퍼 + runtimeState + Effects + IAbilityLifecycle)

**Files:**
- Modify: `Assets/Scripts/Systems/Abilities/ActiveAbilityData.cs`
- Modify: `Assets/Scripts/Systems/Abilities/AbilityInstance.cs`
- Modify: `Assets/Scripts/Systems/Abilities/AbilityContext.cs`
- Create: `Assets/Scripts/Systems/Abilities/IAbilityLifecycle.cs`
- Modify: `Assets/Tests/EditMode/AbilityLoadoutTests.cs` (StubActive: Execute→Tick)
- Test: `Assets/Tests/EditMode/CooldownHelperTests.cs`

**Interfaces:**
- Consumes: `IEffectSpawner`(Task 1).
- Produces: `ActiveAbilityData.Tick(in AbilityContext,AbilityInstance,float)`, `protected bool TickCooldown(AbilityInstance,float)`, `protected void ResetCooldown(AbilityInstance,in AbilityContext)`; `AbilityInstance.runtimeState(object)`; `AbilityContext(host,origin,finder,modifiers,effects)`; `IAbilityLifecycle{OnEquip;OnUnequip}`.

- [ ] **Step 1: 실패 테스트 작성**

`Assets/Tests/EditMode/CooldownHelperTests.cs`:
```csharp
using NUnit.Framework;
using UnityEngine;
using DefenseDot.Systems.Abilities;

namespace DefenseDot.Tests.EditMode
{
    public class CooldownHelperTests
    {
        // 헬퍼(protected) 검증용 테스트 능력 — Tick에서 헬퍼 호출, Fire 횟수 집계
        private sealed class CdAbility : ActiveAbilityData
        {
            public int fireCount;
            public override void Tick(in AbilityContext ctx, AbilityInstance self, float dt)
            {
                if (!TickCooldown(self, dt)) return;
                fireCount++;
                ResetCooldown(self, ctx);
            }
        }

        private static AbilityContext Ctx(float cdr = 0f)
        {
            var mods = new AbilityModifiers { cooldownReduction = cdr };
            return new AbilityContext(null, Vector3.zero, null, mods, null);
        }

        [Test]
        public void TickCooldown_FiresWhenElapsed()
        {
            var a = ScriptableObject.CreateInstance<CdAbility>();
            a.baseCooldown = 1f;
            var inst = new AbilityInstance(a, 1) { cooldownRemaining = 1f };
            a.Tick(Ctx(), inst, 0.4f);   // 0.6
            a.Tick(Ctx(), inst, 0.4f);   // 0.2
            Assert.AreEqual(0, a.fireCount);
            a.Tick(Ctx(), inst, 0.4f);   // -0.2 → fire
            Assert.AreEqual(1, a.fireCount);
        }

        [Test]
        public void ResetCooldown_AppliesReductionClamped()
        {
            var a = ScriptableObject.CreateInstance<CdAbility>();
            a.baseCooldown = 1f;
            var inst = new AbilityInstance(a, 1) { cooldownRemaining = 0f };
            a.Tick(Ctx(cdr: 0.3f), inst, 0.1f);   // 발동 후 reset = 1 - 0.3 = 0.7
            Assert.AreEqual(0.7f, inst.cooldownRemaining, 0.0001f);
        }

        [Test]
        public void ResetCooldown_ClampsToFloor()
        {
            var a = ScriptableObject.CreateInstance<CdAbility>();
            a.baseCooldown = 0.1f;
            var inst = new AbilityInstance(a, 1) { cooldownRemaining = 0f };
            a.Tick(Ctx(cdr: 5f), inst, 0.1f);   // 0.1 - 5 = 음수 → 0.05 클램프
            Assert.AreEqual(0.05f, inst.cooldownRemaining, 0.0001f);
        }
    }
}
```

- [ ] **Step 2: 테스트 실패 확인**

Run: `mcp__UnityMCP__run_tests` mode=EditMode filter `CooldownHelperTests`.
Expected: 컴파일 실패(Tick/TickCooldown 미정의) 또는 FAIL.

- [ ] **Step 3: ActiveAbilityData 교체**

`ActiveAbilityData.cs` 전체:
```csharp
using UnityEngine;

namespace DefenseDot.Systems.Abilities
{
    /// <summary> 발동형 능력(추상). 매 프레임 Tick으로 구동합니다. </summary>
    public abstract class ActiveAbilityData : AbilityData
    {
        /// <summary> 기본 쿨다운(초). </summary>
        public float baseCooldown = 1f;

        /// <summary> 레벨별 효과값(데미지 등). 서브클래스가 재정의. </summary>
        public virtual float ValueAtLevel(int level) { return level; }

        /// <summary> 레벨별 쿨다운(보정 미적용). </summary>
        public virtual float CooldownAtLevel(int level) { return baseCooldown; }

        /// <summary> 매 프레임 구동. 이산/지속 모두 여기서 처리. </summary>
        public abstract void Tick(in AbilityContext ctx, AbilityInstance self, float deltaTime);

        /// <summary> 쿨다운을 감소시키고 준비 여부를 반환합니다. (리셋 안 함) </summary>
        protected bool TickCooldown(AbilityInstance self, float deltaTime)
        {
            self.cooldownRemaining -= deltaTime;
            return self.cooldownRemaining <= 0f;
        }

        /// <summary> 발동 성공 후 쿨다운을 리셋합니다. (보정·하한 적용) </summary>
        protected void ResetCooldown(AbilityInstance self, in AbilityContext ctx)
        {
            self.cooldownRemaining = Mathf.Max(0.05f, CooldownAtLevel(self.level) - ctx.Modifiers.cooldownReduction);
        }
    }
}
```

- [ ] **Step 4: IAbilityLifecycle 작성**

`IAbilityLifecycle.cs`:
```csharp
namespace DefenseDot.Systems.Abilities
{
    /// <summary> 상시 수명 능력(오비탈 등)이 구현하는 장착/해제 훅입니다. </summary>
    public interface IAbilityLifecycle
    {
        /// <summary> 로드아웃 장착 시 1회 호출(상시 효과 스폰 등). </summary>
        void OnEquip(in AbilityContext ctx, AbilityInstance self);

        /// <summary> 로드아웃 해제 시 1회 호출(상시 효과 반납 등). </summary>
        void OnUnequip(in AbilityContext ctx, AbilityInstance self);
    }
}
```

- [ ] **Step 5: AbilityInstance에 runtimeState 추가**

`AbilityInstance.cs`의 `cooldownRemaining` 아래에 추가:
```csharp
        /// <summary> 효과 핸들·커스텀 런타임 상태(상시형 사용). </summary>
        public object runtimeState;
```

- [ ] **Step 6: AbilityContext에 Effects 추가**

`AbilityContext.cs` 전체:
```csharp
using UnityEngine;
using DefenseDot.Systems.Tower;
using DefenseDot.Systems.Abilities.Effects;

namespace DefenseDot.Systems.Abilities
{
    /// <summary> 능력 구동 입력 묶음(Context Object)입니다. </summary>
    public readonly struct AbilityContext
    {
        /// <summary> 투사체 생성 등에 쓰는 호스트 MonoBehaviour. </summary>
        public readonly MonoBehaviour Host;
        /// <summary> 발동 원점(코어 위치). </summary>
        public readonly Vector3 Origin;
        /// <summary> 적 질의 수단. </summary>
        public readonly TargetFinder Finder;
        /// <summary> 패시브 합산 보정. </summary>
        public readonly AbilityModifiers Modifiers;
        /// <summary> 효과 엔티티 스포너. </summary>
        public readonly IEffectSpawner Effects;

        public AbilityContext(MonoBehaviour host, Vector3 origin, TargetFinder finder,
            AbilityModifiers modifiers, IEffectSpawner effects)
        {
            Host = host;
            Origin = origin;
            Finder = finder;
            Modifiers = modifiers;
            Effects = effects;
        }
    }
}
```

- [ ] **Step 7: 기존 AbilityLoadoutTests.StubActive 갱신**

`AbilityLoadoutTests.cs`에서 `StubActive`의 `Execute` 구현을 Tick으로 교체(시그니처만):
```csharp
        // 기존: public override void Execute(in AbilityContext ctx, AbilityInstance self) { }
        public override void Tick(in AbilityContext ctx, AbilityInstance self, float deltaTime) { }
```

- [ ] **Step 8: 테스트 통과 확인**

Run: `mcp__UnityMCP__refresh_unity`(force) → `mcp__UnityMCP__run_tests` mode=EditMode (전체).
Expected: 기존 71 + CooldownHelperTests 3 = **74 PASS**. (AbilityLoadout 기존 6 유지)

- [ ] **Step 9: 커밋 (사용자 요청 시)** — `lint` 후 `commit`. 예: `feat: 능력 Tick 모델·쿨다운 헬퍼·라이프사이클·효과 컨텍스트`

---

## Task 3: AbilityRunner (순수 C#)

**Files:**
- Create: `Assets/Scripts/Systems/Abilities/AbilityRunner.cs`
- Test: `Assets/Tests/EditMode/AbilityRunnerTests.cs`

**Interfaces:**
- Consumes: `AbilityLoadout.Actives`, `ActiveAbilityData.Tick`, `IAbilityLifecycle`.
- Produces: `AbilityRunner(AbilityLoadout, in AbilityContext)`, `void EquipAll()`, `void Equip(AbilityInstance)`, `void Unequip(AbilityInstance)`, `void Tick(float dt)`.

- [ ] **Step 1: 실패 테스트 작성**

`Assets/Tests/EditMode/AbilityRunnerTests.cs`:
```csharp
using NUnit.Framework;
using UnityEngine;
using DefenseDot.Systems.Abilities;

namespace DefenseDot.Tests.EditMode
{
    public class AbilityRunnerTests
    {
        private sealed class CountTick : ActiveAbilityData
        {
            public int ticks;
            public override void Tick(in AbilityContext ctx, AbilityInstance self, float dt) { ticks++; }
        }

        private sealed class LifeAbility : ActiveAbilityData, IAbilityLifecycle
        {
            public int equips, unequips;
            public override void Tick(in AbilityContext ctx, AbilityInstance self, float dt) { }
            public void OnEquip(in AbilityContext ctx, AbilityInstance self) { equips++; }
            public void OnUnequip(in AbilityContext ctx, AbilityInstance self) { unequips++; }
        }

        private static AbilityContext Ctx()
            => new AbilityContext(null, Vector3.zero, null, new AbilityModifiers(), null);

        [Test]
        public void Tick_CallsEachActive()
        {
            var loadout = new AbilityLoadout();
            var a = ScriptableObject.CreateInstance<CountTick>();
            loadout.TryAdd(a);
            var runner = new AbilityRunner(loadout, Ctx());
            runner.Tick(0.1f);
            runner.Tick(0.1f);
            var inst = (CountTick)loadout.Actives[0].data;
            Assert.AreEqual(2, inst.ticks);
        }

        [Test]
        public void EquipAll_CallsOnEquipForLifecycle()
        {
            var loadout = new AbilityLoadout();
            var a = ScriptableObject.CreateInstance<LifeAbility>();
            loadout.TryAdd(a);
            var runner = new AbilityRunner(loadout, Ctx());
            runner.EquipAll();
            Assert.AreEqual(1, ((LifeAbility)loadout.Actives[0].data).equips);
        }

        [Test]
        public void Unequip_CallsOnUnequipForLifecycle()
        {
            var loadout = new AbilityLoadout();
            var a = ScriptableObject.CreateInstance<LifeAbility>();
            loadout.TryAdd(a);
            var inst = loadout.Actives[0];
            var runner = new AbilityRunner(loadout, Ctx());
            runner.Unequip(inst);
            Assert.AreEqual(1, ((LifeAbility)inst.data).unequips);
        }
    }
}
```

- [ ] **Step 2: 테스트 실패 확인**

Run: `mcp__UnityMCP__run_tests` mode=EditMode filter `AbilityRunnerTests`.
Expected: 컴파일 실패(AbilityRunner 미정의).

- [ ] **Step 3: AbilityRunner 작성**

`AbilityRunner.cs`:
```csharp
// 능력 러너 — 매 프레임 액티브 Tick, 장착/해제 시 라이프사이클 호출
using System.Collections.Generic;

namespace DefenseDot.Systems.Abilities
{
    /// <summary>
    /// 로드아웃의 액티브 능력을 매 프레임 구동하고, 장착/해제 시
    /// IAbilityLifecycle 훅을 호출하는 순수 C# 러너입니다.
    /// </summary>
    public sealed class AbilityRunner
    {
        private readonly AbilityLoadout loadout;
        private readonly AbilityContext ctx;

        public AbilityRunner(AbilityLoadout loadout, in AbilityContext ctx)
        {
            this.loadout = loadout;
            this.ctx = ctx;
        }

        /// <summary> 현재 장착된 모든 액티브에 OnEquip을 적용합니다. </summary>
        public void EquipAll()
        {
            IReadOnlyList<AbilityInstance> actives = loadout.Actives;
            for (int i = 0; i < actives.Count; i++) Equip(actives[i]);
        }

        /// <summary> 한 능력의 OnEquip을 호출합니다(라이프사이클 보유 시). </summary>
        public void Equip(AbilityInstance inst)
        {
            if (inst != null && inst.data is IAbilityLifecycle life) life.OnEquip(ctx, inst);
        }

        /// <summary> 한 능력의 OnUnequip을 호출합니다(라이프사이클 보유 시). </summary>
        public void Unequip(AbilityInstance inst)
        {
            if (inst != null && inst.data is IAbilityLifecycle life) life.OnUnequip(ctx, inst);
        }

        /// <summary> 매 프레임 모든 액티브를 Tick합니다. </summary>
        public void Tick(float deltaTime)
        {
            IReadOnlyList<AbilityInstance> actives = loadout.Actives;
            for (int i = 0; i < actives.Count; i++)
            {
                AbilityInstance inst = actives[i];
                if (inst.data is ActiveAbilityData active) active.Tick(ctx, inst, deltaTime);
            }
        }
    }
}
```

- [ ] **Step 4: 테스트 통과 확인**

Run: `mcp__UnityMCP__run_tests` mode=EditMode (전체).
Expected: **77 PASS** (74 + AbilityRunnerTests 3).

- [ ] **Step 5: 커밋 (사용자 요청 시)** — `lint` 후 `commit`. 예: `feat: 능력 러너(AbilityRunner) 추가`

---

## Task 4: 효과 엔티티 구현 (ProjectileEffect · OrbiterSetEffect)

**Files:**
- Create: `Assets/Scripts/Systems/Abilities/Effects/ProjectileEffect.cs`
- Create: `Assets/Scripts/Systems/Abilities/Effects/OrbiterSetEffect.cs`

> MonoBehaviour·시간 거동이라 EditMode 단위 테스트 없음 — Task 7 Play 검증.

**Interfaces:**
- Produces: `ProjectileEffect.Activate(Vector3 origin, ITargetable target, float damage, float speed, int pierce, float range, TargetFinder finder)`; `OrbiterSetEffect.Activate(Vector3 center, int count, float damage, float rotSpeed, TargetFinder finder)`.

- [ ] **Step 1: ProjectileEffect 작성 (DebugProjectile 로직 승격)**

`Effects/ProjectileEffect.cs`:
```csharp
// 유도 투사체 효과 — 명중 데미지 후 미명중 최근접으로 관통, 수명/관통 소진 시 반납
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Pool;
using DefenseDot.Core;
using DefenseDot.Systems.Tower;

namespace DefenseDot.Systems.Abilities.Effects
{
    /// <summary> 능력이 발사하는 유도 투사체 효과입니다. </summary>
    public sealed class ProjectileEffect : AbilityEffect
    {
        private TargetFinder finder;
        private ITargetable target;
        private float damage;
        private float speed;
        private float range;
        private int pierceRemaining;
        private float life;
        private readonly HashSet<ITargetable> hit = new HashSet<ITargetable>();

        /// <summary> 투사체를 활성화합니다. </summary>
        public void Activate(Vector3 origin, ITargetable target, float damage, float speed, int pierce, float range, TargetFinder finder)
        {
            transform.position = origin;
            this.target = target;
            this.damage = damage;
            this.speed = speed;
            this.pierceRemaining = Mathf.Max(1, pierce);
            this.range = range;
            this.finder = finder;
            this.life = 3f;
            hit.Clear();
            if (target != null) transform.forward = (target.Position - origin).normalized;
        }

        public override void OnDespawn() { hit.Clear(); target = null; }

        private void Update()
        {
            life -= Time.deltaTime;
            if (life <= 0f) { Release(); return; }

            if (target == null || !target.IsActive)
            {
                transform.position += transform.forward * (speed * Time.deltaTime);
                return;
            }

            transform.position = Vector3.MoveTowards(transform.position, target.Position, speed * Time.deltaTime);
            if ((transform.position - target.Position).sqrMagnitude >= 0.09f) return;

            if (target is IDamageable damageable) damageable.TakeDamage(damage);
            hit.Add(target);
            pierceRemaining--;
            if (pierceRemaining <= 0) { Release(); return; }
            target = NextNearestUnhit();
        }

        private ITargetable NextNearestUnhit()
        {
            if (finder == null) return null;
            List<ITargetable> cands = ListPool<ITargetable>.Get();
            finder.FindAllInRange(transform.position, range, cands);
            ITargetable best = null;
            float bestSqr = float.MaxValue;
            for (int i = 0; i < cands.Count; i++)
            {
                ITargetable c = cands[i];
                if (hit.Contains(c)) continue;
                float d = (c.Position - transform.position).sqrMagnitude;
                if (d < bestSqr) { bestSqr = d; best = c; }
            }
            ListPool<ITargetable>.Release(cands);
            return best;
        }
    }
}
```

- [ ] **Step 2: OrbiterSetEffect 작성 (회전 위성·접촉 데미지)**

`Effects/OrbiterSetEffect.cs`:
```csharp
// 회전 위성 효과 — count개 위성이 회전, 반경은 최근접 적 추종, 접촉 적에 재타격 쿨다운 데미지
using System.Collections.Generic;
using UnityEngine;
using DefenseDot.Core;
using DefenseDot.Systems.Tower;

namespace DefenseDot.Systems.Abilities.Effects
{
    /// <summary> 코어 주위를 도는 위성 집합 효과입니다. (상시) </summary>
    public sealed class OrbiterSetEffect : AbilityEffect
    {
        [SerializeField] private GameObject orbVisualPrefab;   // 위성 1개 비주얼(없으면 구체)
        [SerializeField] private float hitRadius = 0.6f;
        [SerializeField] private float rehitCooldown = 0.3f;
        [SerializeField] private float minRadius = 1.5f;
        [SerializeField] private float maxRadius = 12f;

        private TargetFinder finder;
        private float damage;
        private float rotSpeed;
        private Vector3 center;
        private float angle;
        private float radius = 3f;
        private float targetRadius = 3f;
        private readonly List<Transform> orbs = new List<Transform>();
        private readonly Dictionary<ITargetable, float> rehit = new Dictionary<ITargetable, float>();

        /// <summary> 위성 집합을 활성화합니다. </summary>
        public void Activate(Vector3 center, int count, float damage, float rotSpeed, TargetFinder finder)
        {
            this.center = center;
            this.damage = damage;
            this.rotSpeed = rotSpeed;
            this.finder = finder;
            transform.position = center;
            EnsureOrbs(Mathf.Max(1, count));
        }

        private void EnsureOrbs(int count)
        {
            for (int i = orbs.Count; i < count; i++)
            {
                GameObject o = orbVisualPrefab != null
                    ? Instantiate(orbVisualPrefab, transform)
                    : GameObject.CreatePrimitive(PrimitiveType.Sphere);
                o.transform.SetParent(transform, false);
                o.transform.localScale = Vector3.one * 0.4f;
                orbs.Add(o.transform);
            }
            for (int i = 0; i < orbs.Count; i++) orbs[i].gameObject.SetActive(i < count);
        }

        public override void OnDespawn() { rehit.Clear(); }

        private void Update()
        {
            float dt = Time.deltaTime;
            angle += rotSpeed * dt;

            ITargetable t = finder != null ? finder.FindNearest(center, maxRadius + 5f) : null;
            if (t != null) targetRadius = Vector3.Distance(center, t.Position);
            radius = Mathf.Lerp(radius, Mathf.Clamp(targetRadius, minRadius, maxRadius), dt * 3f);

            DecayRehit(dt);

            int active = 0;
            for (int i = 0; i < orbs.Count; i++) if (orbs[i].gameObject.activeSelf) active++;
            int idx = 0;
            for (int i = 0; i < orbs.Count; i++)
            {
                if (!orbs[i].gameObject.activeSelf) continue;
                float a = angle + (Mathf.PI * 2f / Mathf.Max(1, active)) * idx;
                Vector3 pos = center + new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a)) * radius;
                orbs[i].position = pos;
                DamageAround(pos);
                idx++;
            }
        }

        private void DamageAround(Vector3 pos)
        {
            if (finder == null) return;
            List<ITargetable> cands = UnityEngine.Pool.ListPool<ITargetable>.Get();
            finder.FindAllInRange(pos, hitRadius, cands);
            for (int i = 0; i < cands.Count; i++)
            {
                ITargetable c = cands[i];
                if (rehit.ContainsKey(c)) continue;
                if (c is IDamageable d) d.TakeDamage(damage);
                rehit[c] = rehitCooldown;
            }
            UnityEngine.Pool.ListPool<ITargetable>.Release(cands);
        }

        private void DecayRehit(float dt)
        {
            if (rehit.Count == 0) return;
            List<ITargetable> done = UnityEngine.Pool.ListPool<ITargetable>.Get();
            foreach (var kv in rehit)
            {
                float left = kv.Value - dt;
                if (left <= 0f) done.Add(kv.Key);
                else rehit[kv.Key] = left;
            }
            for (int i = 0; i < done.Count; i++) rehit.Remove(done[i]);
            UnityEngine.Pool.ListPool<ITargetable>.Release(done);
        }
    }
}
```
> 주의: foreach 중 Dictionary 수정 불가 → 만료 키를 모아 후처리.

- [ ] **Step 3: 컴파일 확인**

Run: `mcp__UnityMCP__refresh_unity`(force) → `read_console`(error). Expected: 에러 없음.

- [ ] **Step 4: 커밋 (사용자 요청 시)** — `lint` 후 `commit`. 예: `feat: 투사체·회전위성 효과 엔티티 추가`

---

## Task 5: 구체 능력 2종 (ProjectileAbilityData · OrbitalAbilityData)

**Files:**
- Create: `Assets/Scripts/Systems/Abilities/Definitions/ProjectileAbilityData.cs`
- Create: `Assets/Scripts/Systems/Abilities/Definitions/OrbitalAbilityData.cs`

**Interfaces:**
- Consumes: `ProjectileEffect`/`OrbiterSetEffect`(Task 4), `ActiveAbilityData`/`IAbilityLifecycle`/`AbilityContext`(Task 2).

- [ ] **Step 1: ProjectileAbilityData 작성**

`Definitions/ProjectileAbilityData.cs`:
```csharp
// 발사체 능력(이산) — 쿨다운마다 최근접 적에 유도 투사체
using UnityEngine;
using DefenseDot.Core;
using DefenseDot.Systems.Abilities.Effects;

namespace DefenseDot.Systems.Abilities.Definitions
{
    /// <summary> 쿨다운마다 유도 투사체를 발사하는 발사체 능력입니다. </summary>
    [CreateAssetMenu(fileName = "ProjectileAbility", menuName = "DefenseDot/Abilities/Projectile")]
    public sealed class ProjectileAbilityData : ActiveAbilityData
    {
        [SerializeField] private ProjectileEffect projectilePrefab;
        [SerializeField] private float baseDamage = 1f;
        [SerializeField] private float damagePerLevel = 1f;
        [SerializeField] private float speed = 12f;
        [SerializeField] private int pierce = 1;
        [SerializeField] private float range = 30f;

        public override float ValueAtLevel(int level) { return baseDamage + damagePerLevel * (level - 1); }

        public override void Tick(in AbilityContext ctx, AbilityInstance self, float deltaTime)
        {
            if (!TickCooldown(self, deltaTime)) return;
            if (ctx.Finder == null || projectilePrefab == null) return;
            ITargetable target = ctx.Finder.FindNearest(ctx.Origin, range);
            if (target == null) return;   // 준비상태 유지·재시도

            ProjectileEffect fx = ctx.Effects.Spawn(projectilePrefab);
            float dmg = ValueAtLevel(self.level) + ctx.Modifiers.damageBonus;
            fx.Activate(ctx.Origin, target, dmg, speed, pierce, range, ctx.Finder);
            ResetCooldown(self, ctx);
        }
    }
}
```

- [ ] **Step 2: OrbitalAbilityData 작성**

`Definitions/OrbitalAbilityData.cs`:
```csharp
// 오비탈 능력(상시) — 장착 시 회전 위성 스폰, 해제 시 반납
using UnityEngine;
using DefenseDot.Systems.Abilities.Effects;

namespace DefenseDot.Systems.Abilities.Definitions
{
    /// <summary> 장착 동안 코어 주위를 도는 위성을 유지하는 상시 능력입니다. </summary>
    [CreateAssetMenu(fileName = "OrbitalAbility", menuName = "DefenseDot/Abilities/Orbital")]
    public sealed class OrbitalAbilityData : ActiveAbilityData, IAbilityLifecycle
    {
        [SerializeField] private OrbiterSetEffect orbiterPrefab;
        [SerializeField] private float baseDamage = 3f;
        [SerializeField] private float damagePerLevel = 2f;
        [SerializeField] private float rotSpeed = 2f;

        public override float ValueAtLevel(int level) { return baseDamage + damagePerLevel * (level - 1); }

        public void OnEquip(in AbilityContext ctx, AbilityInstance self)
        {
            if (orbiterPrefab == null) return;
            OrbiterSetEffect fx = ctx.Effects.Spawn(orbiterPrefab);
            float dmg = ValueAtLevel(self.level) + ctx.Modifiers.damageBonus;
            fx.Activate(ctx.Origin, 1 + self.level, dmg, rotSpeed, ctx.Finder);
            self.runtimeState = fx;
        }

        public void OnUnequip(in AbilityContext ctx, AbilityInstance self)
        {
            if (self.runtimeState is AbilityEffect fx) ctx.Effects.Release(fx);
            self.runtimeState = null;
        }

        // 회전·데미지는 효과가 자가 수행 → Tick은 비움
        public override void Tick(in AbilityContext ctx, AbilityInstance self, float deltaTime) { }
    }
}
```

- [ ] **Step 3: 컴파일 확인**

Run: `mcp__UnityMCP__refresh_unity`(force) → `read_console`(error). Expected: 에러 없음.

- [ ] **Step 4: 커밋 (사용자 요청 시)** — `lint` 후 `commit`. 예: `feat: 샷·오비탈 능력 템플릿 추가`

---

## Task 6: 코어 배선 (CoreAbilitySystem + ArenaModeBootstrap)

**Files:**
- Create: `Assets/Scripts/Systems/Abilities/CoreAbilitySystem.cs`
- Modify: `Assets/Scripts/Systems/Mode/ArenaModeBootstrap.cs`

**Interfaces:**
- Consumes: `AbilityLoadout`, `AbilityRunner`, `AbilityContext`, `SimpleEffectSpawner`, `GameFlowModel`(DefenseDot.Domain.Models), `TargetFinder`.
- Produces: `CoreAbilitySystem.Setup(TargetFinder finder, Vector3 origin, GameFlowModel flow, IReadOnlyList<AbilityData> starters)`.

- [ ] **Step 1: CoreAbilitySystem 작성**

`CoreAbilitySystem.cs`:
```csharp
// 코어 능력 구동 — 로드아웃·러너 보유, 매 프레임 Tick(진행 중에만)
using System.Collections.Generic;
using UnityEngine;
using DefenseDot.Domain.Models;
using DefenseDot.Systems.Tower;
using DefenseDot.Systems.Abilities.Effects;

namespace DefenseDot.Systems.Abilities
{
    /// <summary> Arena 코어의 능력 로드아웃을 구동하는 컴포넌트입니다. </summary>
    public sealed class CoreAbilitySystem : MonoBehaviour
    {
        private AbilityLoadout loadout;
        private AbilityRunner runner;
        private GameFlowModel flow;

        /// <summary> 합성 루트가 의존성·스타터 능력을 주입합니다. </summary>
        public void Setup(TargetFinder finder, Vector3 origin, GameFlowModel gameFlow,
            IReadOnlyList<AbilityData> starters)
        {
            flow = gameFlow;
            loadout = new AbilityLoadout();
            if (starters != null)
                for (int i = 0; i < starters.Count; i++)
                    if (starters[i] != null) loadout.TryAdd(starters[i]);

            IEffectSpawner effects = new SimpleEffectSpawner();
            var ctx = new AbilityContext(this, origin, finder, loadout.Modifiers, effects);
            runner = new AbilityRunner(loadout, ctx);
            runner.EquipAll();
        }

        private void Update()
        {
            if (runner == null || flow == null || !flow.IsPlaying) return;
            runner.Tick(Time.deltaTime);
        }
    }
}
```

- [ ] **Step 2: ArenaModeBootstrap 수정 — starter 필드 + 코어 배선**

`ArenaModeBootstrap.cs`의 필드부에 추가:
```csharp
        [SerializeField] private System.Collections.Generic.List<DefenseDot.Systems.Abilities.AbilityData> starterAbilities
            = new System.Collections.Generic.List<DefenseDot.Systems.Abilities.AbilityData>();
```
`SpawnCenterTower`의 `tower.SetTargetFinder(ctx.TargetFinder);` 아래에 추가:
```csharp
            // 코어: 디버그 단일공격 제거 + 능력 시스템 부착
            var debugBt = go.GetComponent<DefenseDot.Systems.Tower.TowerBehaviorTree>();
            if (debugBt != null) Destroy(debugBt);
            var coreAbility = go.AddComponent<DefenseDot.Systems.Abilities.CoreAbilitySystem>();
            coreAbility.Setup(ctx.TargetFinder, ctx.CoreCenter, ctx.Flow, starterAbilities);
```
> `ModeContext`에 `Flow`(GameFlowModel)가 없으면 추가 필요. 확인: `ModeContext`(DefenseDot.Systems.Mode)에 `GameFlowModel Flow`가 없으면 본 스텝에서 `ModeContext`·`GameManager.CreateMode`(ctx 생성)·`ArenaMode`/`GridDefenseMode` 호출부에 Flow를 전달하도록 보강. (GameManager는 `Flow` 보유)

- [ ] **Step 3: ModeContext에 Flow 추가 (확정 — 현재 미보유)**

확인됨: `ModeContext` 생성자는 `(CoreModel, EconomyModel, TargetFinder, Vector3 spawnOrigin, Vector3 coreCenter)`로 Flow 없음. 추가한다.
`ModeContext.cs`: `using DefenseDot.Domain.Models;` 확인 후 `public readonly GameFlowModel Flow;` 필드 + 생성자 끝에 `GameFlowModel flow` 매개변수 추가(`Flow = flow;`).
`GameManager.cs:105` `new ModeContext(Core, Economy, targetFinder, origin, center)` → `new ModeContext(Core, Economy, targetFinder, origin, center, Flow)`. (유일 호출부)

- [ ] **Step 4: 통합 컴파일 검증**

Run: `mcp__UnityMCP__refresh_unity`(force) → `read_console`(error) → `run_tests` EditMode (전체).
Expected: 에러 없음, **77 PASS 유지**.

- [ ] **Step 5: 커밋 (사용자 요청 시)** — `lint` 후 `commit`. 예: `feat: 코어 능력 시스템 배선 및 스타터 능력 주입`

---

## Task 7: 에디터 에셋 + Play 검증

**Files (에디터 — 코드 아님):**
- 효과 프리팹 2: `ProjectileEffect` 부착(+Hovl `Projectile 14 blue rapid` 비주얼 자식), `OrbiterSetEffect` 부착(+컴팩트 오브 비주얼)
- 능력 SO 2: `ProjectileAbility`(샷), `OrbitalAbility`(오비탈) — 효과 프리팹·수치 할당
- `ArenaModeBootstrap.starterAbilities` = [샷, 오비탈]

- [ ] **Step 1: 효과 프리팹 생성**

`Assets/Prefabs/Abilities/` 생성. 
- `ProjectileEffect.prefab`: 빈 GO + `ProjectileEffect` 컴포넌트, 자식으로 `AAA Projectiles Vol 1/Prefabs/Projectiles(transform)/Projectile 14 blue rapid` 비주얼 배치(트레일은 이동 시 자연 발생).
- `OrbiterSetEffect.prefab`: 빈 GO + `OrbiterSetEffect` 컴포넌트, `orbVisualPrefab`에 컴팩트 글로우 오브(예: `MasterStylizedProjectiles/.../SmallEnergyBullet` 또는 단순 이미시브 구체) 지정.

도구: `mcp__UnityMCP__manage_asset`(create_folder), `manage_gameobject`/`manage_prefabs`(컴포넌트 부착·자식 배치), `manage_components`(SerializeField 지정).

- [ ] **Step 2: 능력 SO 에셋 생성**

`Assets/Data/Abilities/` 에 `ProjectileAbilityData`(샷)·`OrbitalAbilityData`(오비탈) 인스턴스 생성, 필드 지정:
- 샷: projectilePrefab=ProjectileEffect.prefab, baseDamage=1, damagePerLevel=1, speed=12, pierce=1, range=30, baseCooldown=0.4
- 오비탈: orbiterPrefab=OrbiterSetEffect.prefab, baseDamage=3, damagePerLevel=2, rotSpeed=2

도구: `mcp__UnityMCP__manage_scriptable_object`(create) 또는 메뉴 `DefenseDot/Abilities/*`.

- [ ] **Step 3: ArenaScene 배선**

ArenaScene의 `ArenaModeBootstrap.starterAbilities`에 [샷 SO, 오비탈 SO] 할당. 저장.

- [ ] **Step 4: Play 검증 (수동)**

`mcp__UnityMCP__manage_editor`(play). 콘솔 에러 0 + 확인:

| # | 시나리오 | 기대 |
|---|---|---|
| 1 | Arena 진입 | 코어가 자동으로 샷 발사 + 위성 회전 |
| 2 | 샷 | 쿨다운마다 최근접 적 유도 투사체 → 명중·관통 데미지 |
| 3 | 오비탈 | 위성 회전·반경 적 추종·접촉 데미지 |
| 4 | 처치 연동 | 적 사망 시 골드·점수(기존 HUD) |
| 5 | 일시정지/게임오버 | 능력 정지 |
| 6 | Grid 회귀 | Grid 타워 단일공격 정상 |

- [ ] **Step 5: 커밋 (사용자 요청 시)** — 프리팹·SO·씬. 예: `feat: 능력 효과 프리팹·SO 에셋 및 스타터 배선`

---

## Self-Review 결과

- **Spec coverage**: 추상화(Task2)·효과레이어(Task1·4)·러너(Task3)·구체능력 2종(Task5)·코어배선(Task6)·에셋/Play(Task7)·풀링분리(전체 IEffectSpawner 심) — 스펙 2~5절 매핑됨.
- **Placeholder scan**: 모든 코드 스텝 실제 코드 포함. Task6 Step3는 조건부(ModeContext.Flow 부재 시) — 확인 후 처리 명시.
- **Type consistency**: `Tick(in AbilityContext,AbilityInstance,float)` / `TickCooldown(self,dt):bool` / `ResetCooldown(self,in ctx)` / `AbilityContext(host,origin,finder,modifiers,effects)` / `IEffectSpawner.Spawn<T>(T):T`·`Release(AbilityEffect)` / `ProjectileEffect.Activate(origin,target,dmg,speed,pierce,range,finder)` / `OrbiterSetEffect.Activate(center,count,dmg,rotSpeed,finder)` / `CoreAbilitySystem.Setup(finder,origin,flow,starters)` / `AbilityRunner(loadout,in ctx)·EquipAll·Tick` — 일치 확인.
- **확인 완료**: `IActor.Position`(Vector3, ActorBase 구현) 존재 → `target.Position` 사용 가능. `ModeContext`엔 Flow 미보유 → Task6 Step3에서 추가(확정).
- **잔여 확인점**: 오비탈 오브 비주얼 최종 선택(Task7) — 배선 중 확정.
