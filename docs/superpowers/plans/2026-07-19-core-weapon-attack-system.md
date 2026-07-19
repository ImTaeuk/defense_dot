# CoreWeapon 공격 체계 Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** 아리스의 공격 주기를 `CoreWeapon` 한 곳이 단독 소유하게 하여, 시전 채널 병목을 제거하고 공격 능력을 모을수록 실제로 화력이 늘어나게 한다.

**Architecture:** 능력을 "언제 발동하는가" 한 축으로만 분류한다(`MainAbilityData` / `SubAbilityData` / `AutoAbilityData`). 순수 C# 클래스 `CoreWeapon`이 로드아웃을 읽어 주축 1개와 동반 N개를 파악하고, 발사 주기를 계산하고, 모션 속도를 지시하고, 발사 프레임에 전부 발사한다. 형태(투사체·장판)는 상속이 아니라 공용 조각(`ProjectileLauncher`) 호출로 공유한다.

**Tech Stack:** Unity 6000.2.10f1 · C# · NUnit(EditMode) · UniTask · ScriptableObject

## Global Constraints

- **스펙**: `docs/superpowers/specs/2026-07-19-core-weapon-attack-system-design.md`
- **커밋 금지** — 사용자의 명시적 요청이 있을 때만 커밋한다. 각 태스크는 커밋 대신 "변경 확인"으로 끝낸다.
- **`.cs` 편집 전 필수** — `unity-standards/references/*.md` 중 하나를 Read 한다. 훅이 하드 차단하므로 생략하면 편집이 실패한다.
- **코딩 컨벤션** — private 필드는 순수 `camelCase`(`m_`·`_` 금지). 모든 메서드 선언 위에 `<summary>` 1줄 + 각 인자 `<param>`. `if`/`else` 뒤 문장은 단일이라도 개행. 실행문 1줄이면 `{}` 생략, 2줄 이상이면 Allman `{}`. 모든 멤버에 접근 제한자 명시. `System` 라이브러리는 풀패스(`System.Action`). 비동기는 UniTask만.
- **Unity 에디터가 꺼져 있다** — Task 1~7은 코드 작성만 가능하다. 컴파일·테스트 실행·에셋 GUID 교체·Animator 편집은 **Task 8(Unity 켠 뒤)** 로 미룬다. 새 `.cs`는 Unity가 임포트해야 `.meta`(GUID)가 생기므로 에셋 마이그레이션이 그 뒤에만 가능하다.
- **테스트 위치** — `Assets/Tests/EditMode/`, 네임스페이스 `DefenseDot.Tests.EditMode`
- **네이밍 확정** — 주기 가감값 필드명은 `cycleDelta` 로 확정한다(스펙 U1).

---

### Task 1: 타이밍 축 3타입 신설

`ActiveAbilityData`에서 시전 분기와 `castAnimation`을 걷어내고, 그 아래 3개의 추상 타입을 만든다.

**Files:**
- Modify: `Assets/Scripts/Systems/Abilities/ActiveAbilityData.cs`
- Create: `Assets/Scripts/Systems/Abilities/MainAbilityData.cs`
- Create: `Assets/Scripts/Systems/Abilities/SubAbilityData.cs`
- Create: `Assets/Scripts/Systems/Abilities/AutoAbilityData.cs`

**Interfaces:**
- Consumes: 없음 (첫 태스크)
- Produces:
  - `ActiveAbilityData.TargetRange` → `float` (public, 외부 조회용 사거리)
  - `ActiveAbilityData.FireFromWeapon(in AbilityContext, AbilityInstance, ITargetable)` → `void` (internal)
  - `MainAbilityData.CastAnimation` → `AnimationClip` (public)
  - `MainAbilityData.cycleDelta` → `float` (public 필드)
  - `SubAbilityData.cycleDelta` → `float` (public 필드)
  - `AutoAbilityData.Tick(in AbilityContext, AbilityInstance, float)` → `void` (public virtual)

- [ ] **Step 1: unity-standards 가이드를 읽는다**

훅이 `.cs` 편집을 차단하므로 먼저 읽어야 한다.

Read: `C:\Users\USER\.claude\skills\unity-standards\references\` 아래 `.md` 파일 중 하나

- [ ] **Step 2: `ActiveAbilityData.cs` 를 아래 내용으로 교체한다**

`Tick`·`castAnimation`·`TickCooldown`·`ResetCooldown` 중 Tick과 castAnimation을 제거한다. 쿨다운 헬퍼는 `AutoAbilityData`가 쓰므로 `protected`로 남긴다.

```csharp
using UnityEngine;
using DefenseDot.Core;

namespace DefenseDot.Systems.Abilities
{
    /// <summary> 발동형 능력(추상). 언제 발동하는지는 파생 타입(Main·Sub·Auto)이 정합니다. </summary>
    public abstract class ActiveAbilityData : AbilityData
    {
        /// <summary> 기본 쿨다운(초). Auto 계열만 사용합니다. </summary>
        public float baseCooldown = 1f;

        /// <summary> 타겟 탐색 사거리. 서브클래스가 재정의. </summary>
        protected virtual float Range => 30f;

        /// <summary> 타겟 탐색 사거리(외부 조회용). </summary>
        public float TargetRange => Range;

        /// <summary> 레벨별 효과값(데미지 등). 서브클래스가 재정의. </summary>
        public virtual float ValueAtLevel(int level) { return level; }

        /// <summary> 레벨별 쿨다운(보정 미적용). </summary>
        public virtual float CooldownAtLevel(int level) { return baseCooldown; }

        /// <summary> 실제 발사(효과 생성)입니다. 서브클래스가 구현합니다. </summary>
        /// <param name="ctx">능력 구동 컨텍스트</param>
        /// <param name="self">이 능력의 런타임 인스턴스</param>
        /// <param name="target">발사 대상</param>
        protected abstract void Fire(in AbilityContext ctx, AbilityInstance self, ITargetable target);

        /// <summary> 무기가 발사 프레임에 호출하는 래퍼입니다. </summary>
        /// <param name="ctx">능력 구동 컨텍스트</param>
        /// <param name="self">이 능력의 런타임 인스턴스</param>
        /// <param name="target">발사 대상</param>
        internal void FireFromWeapon(in AbilityContext ctx, AbilityInstance self, ITargetable target)
        {
            Fire(ctx, self, target);
        }

        /// <summary> 쿨다운을 감소시키고 준비 여부를 반환합니다(리셋하지 않음). </summary>
        /// <param name="self">이 능력의 런타임 인스턴스</param>
        /// <param name="deltaTime">경과 시간(초)</param>
        protected bool TickCooldown(AbilityInstance self, float deltaTime)
        {
            self.cooldownRemaining -= deltaTime;
            return self.cooldownRemaining <= 0f;
        }

        /// <summary> 발동 성공 후 쿨다운을 리셋합니다(보정·하한 적용). </summary>
        /// <param name="self">이 능력의 런타임 인스턴스</param>
        /// <param name="ctx">능력 구동 컨텍스트</param>
        protected void ResetCooldown(AbilityInstance self, in AbilityContext ctx)
        {
            self.cooldownRemaining = Mathf.Max(0.05f, CooldownAtLevel(self.level) - ctx.Modifiers.cooldownReduction);
        }
    }
}
```

- [ ] **Step 3: `MainAbilityData.cs` 를 만든다**

```csharp
using UnityEngine;

namespace DefenseDot.Systems.Abilities
{
    /// <summary>
    /// 주축 공격 능력(추상). 공격 모션의 주인이며 발사 주기의 기준입니다.
    /// 스스로 발사하지 않고 CoreWeapon 이 발사 시점을 정합니다. 코어에 1개만 장착됩니다.
    /// </summary>
    public abstract class MainAbilityData : ActiveAbilityData
    {
        /// <summary> 공격 모션. 없으면 모션 없이 즉시 발사합니다. </summary>
        [SerializeField] private AnimationClip castAnimation;

        /// <summary> 타워 기본 주기에 더할 시간(초). 음수면 빨라집니다. </summary>
        public float cycleDelta;

        /// <summary> 공격 모션(외부 조회용). </summary>
        public AnimationClip CastAnimation => castAnimation;
    }
}
```

- [ ] **Step 4: `SubAbilityData.cs` 를 만든다**

```csharp
namespace DefenseDot.Systems.Abilities
{
    /// <summary>
    /// 동반 공격 능력(추상). 스스로 발사하지 않고 주축 발사에 함께 나갑니다.
    /// 보유한 만큼 발사 주기에 시간을 더하거나 뺍니다.
    /// </summary>
    public abstract class SubAbilityData : ActiveAbilityData
    {
        /// <summary> 타워 기본 주기에 더할 시간(초). 음수면 빨라집니다. </summary>
        public float cycleDelta;
    }
}
```

- [ ] **Step 5: `AutoAbilityData.cs` 를 만든다**

기존 `ActiveAbilityData.Tick`의 쿨다운 로직을 이리로 옮긴다. 시전 분기는 넣지 않는다.

```csharp
using DefenseDot.Core;

namespace DefenseDot.Systems.Abilities
{
    /// <summary>
    /// 자율 발동 능력(추상). 무기의 발사 주기와 무관하게 자기 쿨다운으로 동작합니다.
    /// </summary>
    public abstract class AutoAbilityData : ActiveAbilityData
    {
        /// <summary> 매 프레임 구동 — 쿨다운이 차고 타겟이 있으면 발사합니다. </summary>
        /// <param name="ctx">능력 구동 컨텍스트</param>
        /// <param name="self">이 능력의 런타임 인스턴스</param>
        /// <param name="deltaTime">경과 시간(초)</param>
        public virtual void Tick(in AbilityContext ctx, AbilityInstance self, float deltaTime)
        {
            if (!TickCooldown(self, deltaTime)) return;

            ITargetable target = ctx.Finder != null ? ctx.Finder.FindNearest(ctx.Origin, Range) : null;
            if (target == null) return;   // 준비 유지·재시도

            Fire(ctx, self, target);
            ResetCooldown(self, ctx);
        }
    }
}
```

- [ ] **Step 6: 변경 확인**

Run: `git status --porcelain Assets/Scripts/Systems/Abilities/`
Expected: `ActiveAbilityData.cs` 수정 1건 + 새 파일 3건(`MainAbilityData.cs`·`SubAbilityData.cs`·`AutoAbilityData.cs`)

---

### Task 2: CoreWeapon 주기 계산 (TDD)

발사 주기 계산만 먼저 만든다. 발사·모션은 다음 태스크다.

**Files:**
- Create: `Assets/Scripts/Systems/Abilities/CoreWeapon.cs`
- Test: `Assets/Tests/EditMode/CoreWeaponCycleTests.cs`

**Interfaces:**
- Consumes: `MainAbilityData.cycleDelta`, `SubAbilityData.cycleDelta` (Task 1)
- Produces:
  - `CoreWeapon(AbilityLoadout loadout, IAttackMotion motion)` 생성자
  - `CoreWeapon.SetBaseAttackSpeed(float attacksPerSecond)` → `void`
  - `CoreWeapon.Main` → `AbilityInstance` (public, 없으면 null)
  - `CoreWeapon.CalculateCycle(in AbilityContext ctx)` → `float` (public)

- [ ] **Step 1: `IAttackMotion.cs` 를 만든다**

`CoreWeapon` 생성자가 요구하므로 먼저 만든다.

Create: `Assets/Scripts/Systems/Abilities/IAttackMotion.cs`

```csharp
using UnityEngine;
using DefenseDot.Core;

namespace DefenseDot.Systems.Abilities
{
    /// <summary> 공격 모션 재생 대상(타워 비주얼)입니다. </summary>
    public interface IAttackMotion
    {
        /// <summary> 공격 모션을 지정 속도로 재생하고 대상을 향해 조준합니다. </summary>
        /// <param name="clip">재생할 공격 모션</param>
        /// <param name="target">조준 대상</param>
        /// <param name="speed">재생 속도 배수(클립 길이 ÷ 발사 주기)</param>
        void PlayAttack(AnimationClip clip, ITargetable target, float speed);
    }
}
```

- [ ] **Step 2: 실패하는 테스트를 작성한다**

Create: `Assets/Tests/EditMode/CoreWeaponCycleTests.cs`

```csharp
using NUnit.Framework;
using UnityEngine;
using DefenseDot.Core;
using DefenseDot.Systems.Abilities;

namespace DefenseDot.Tests.EditMode
{
    public class CoreWeaponCycleTests
    {
        private sealed class TestMain : MainAbilityData
        {
            protected override void Fire(in AbilityContext ctx, AbilityInstance self, ITargetable target) { }
        }

        private sealed class TestSub : SubAbilityData
        {
            protected override void Fire(in AbilityContext ctx, AbilityInstance self, ITargetable target) { }
        }

        private sealed class TestAuto : AutoAbilityData
        {
            protected override void Fire(in AbilityContext ctx, AbilityInstance self, ITargetable target) { }
        }

        private static AbilityContext Ctx(AbilityModifiers mods)
            => new AbilityContext(null, Vector3.zero, null, mods, null);

        private static CoreWeapon Weapon(AbilityLoadout loadout, float attacksPerSecond)
        {
            var weapon = new CoreWeapon(loadout, null);
            weapon.SetBaseAttackSpeed(attacksPerSecond);
            return weapon;
        }

        [Test]
        public void Cycle_MainOnly_UsesBaseCycle()
        {
            var loadout = new AbilityLoadout();
            var main = ScriptableObject.CreateInstance<TestMain>();
            main.cycleDelta = 0f;
            loadout.TryAdd(main);

            CoreWeapon weapon = Weapon(loadout, 1f);   // 기본 주기 1.0초

            Assert.AreEqual(1f, weapon.CalculateCycle(Ctx(new AbilityModifiers())), 0.0001f);
        }

        [Test]
        public void Cycle_MainDelta_IsAdded()
        {
            var loadout = new AbilityLoadout();
            var main = ScriptableObject.CreateInstance<TestMain>();
            main.cycleDelta = 0.3f;
            loadout.TryAdd(main);

            CoreWeapon weapon = Weapon(loadout, 1f);

            Assert.AreEqual(1.3f, weapon.CalculateCycle(Ctx(new AbilityModifiers())), 0.0001f);
        }

        [Test]
        public void Cycle_SubDeltas_AreSummed()
        {
            var loadout = new AbilityLoadout();
            var main = ScriptableObject.CreateInstance<TestMain>();
            var subA = ScriptableObject.CreateInstance<TestSub>();
            var subB = ScriptableObject.CreateInstance<TestSub>();
            subA.cycleDelta = 0.5f;
            subB.cycleDelta = 0.2f;
            loadout.TryAdd(main);
            loadout.TryAdd(subA);
            loadout.TryAdd(subB);

            CoreWeapon weapon = Weapon(loadout, 1f);

            Assert.AreEqual(1.7f, weapon.CalculateCycle(Ctx(new AbilityModifiers())), 0.0001f);
        }

        [Test]
        public void Cycle_ClampedToFloor()
        {
            var loadout = new AbilityLoadout();
            var main = ScriptableObject.CreateInstance<TestMain>();
            main.cycleDelta = -5f;
            loadout.TryAdd(main);

            CoreWeapon weapon = Weapon(loadout, 1f);

            Assert.AreEqual(0.05f, weapon.CalculateCycle(Ctx(new AbilityModifiers())), 0.0001f);
        }

        [Test]
        public void Cycle_CooldownReduction_IsSubtracted()
        {
            var loadout = new AbilityLoadout();
            var main = ScriptableObject.CreateInstance<TestMain>();
            loadout.TryAdd(main);

            CoreWeapon weapon = Weapon(loadout, 1f);
            var mods = new AbilityModifiers();
            mods.cooldownReduction = 0.2f;

            Assert.AreEqual(0.8f, weapon.CalculateCycle(Ctx(mods)), 0.0001f);
        }

        [Test]
        public void Cycle_AutoAbilities_DoNotAffectCycle()
        {
            var loadout = new AbilityLoadout();
            var main = ScriptableObject.CreateInstance<TestMain>();
            loadout.TryAdd(main);
            loadout.TryAdd(ScriptableObject.CreateInstance<TestAuto>());
            loadout.TryAdd(ScriptableObject.CreateInstance<TestAuto>());

            CoreWeapon weapon = Weapon(loadout, 1f);

            Assert.AreEqual(1f, weapon.CalculateCycle(Ctx(new AbilityModifiers())), 0.0001f);
        }

        [Test]
        public void Main_TracksLoadoutChanges()
        {
            var loadout = new AbilityLoadout();
            CoreWeapon weapon = Weapon(loadout, 1f);
            Assert.IsNull(weapon.Main);

            var main = ScriptableObject.CreateInstance<TestMain>();
            loadout.TryAdd(main);

            Assert.IsNotNull(weapon.Main);
            Assert.AreSame(main, weapon.Main.data);
        }
    }
}
```

- [ ] **Step 3: 테스트가 실패하는지 확인한다**

Unity가 꺼져 있으므로 이 단계는 **Task 8에서 수행**한다. 지금은 파일만 만들고 넘어간다.

- [ ] **Step 4: `CoreWeapon.cs` 를 만든다**

```csharp
using System.Collections.Generic;
using UnityEngine;
using DefenseDot.Core;

namespace DefenseDot.Systems.Abilities
{
    /// <summary>
    /// 코어의 무기입니다. 주축 1개와 동반 N개를 로드아웃에서 읽어, 발사 주기를 계산하고
    /// 모션 속도를 지시하고 발사 프레임에 전부 발사합니다. 공격 주기의 유일한 소유자입니다.
    /// </summary>
    public sealed class CoreWeapon
    {
        /// <summary> 발사 주기 하한(초). </summary>
        private const float MinCycle = 0.05f;

        /// <summary> 장착 능력의 원천. 여기서 주축·동반을 읽습니다. </summary>
        private readonly AbilityLoadout loadout;
        /// <summary> 공격 모션 재생 대상. null이면 모션 없이 즉시 발사합니다. </summary>
        private readonly IAttackMotion motion;
        /// <summary> 현재 장착된 동반 공격 능력들. </summary>
        private readonly List<AbilityInstance> subs = new List<AbilityInstance>();

        /// <summary> 현재 장착된 주축 공격 능력. 없으면 null. </summary>
        private AbilityInstance main;
        /// <summary> 타워 기본 공격 주기(초). </summary>
        private float baseCycle = 1f;
        /// <summary> 다음 발사까지 남은 시간(초). </summary>
        private float remaining;
        /// <summary> 이번 발사 묶음의 대상. </summary>
        private ITargetable pendingTarget;

        /// <summary> 현재 장착된 주축 공격 능력. 없으면 null. </summary>
        public AbilityInstance Main => main;

        /// <summary> 직전에 계산된 발사 주기(초). </summary>
        public float Cycle { get; private set; } = 1f;

        /// <summary> 로드아웃을 구독해 주축·동반을 추적합니다. </summary>
        /// <param name="loadout">장착 능력의 원천</param>
        /// <param name="motion">공격 모션 재생 대상(없으면 null)</param>
        public CoreWeapon(AbilityLoadout loadout, IAttackMotion motion)
        {
            this.loadout = loadout;
            this.motion = motion;
            if (loadout != null)
            {
                loadout.OnChanged += Rebuild;
                Rebuild();
            }
        }

        /// <summary> 타워 기본 공격 속도를 설정합니다. </summary>
        /// <param name="attacksPerSecond">초당 공격 횟수</param>
        public void SetBaseAttackSpeed(float attacksPerSecond)
        {
            baseCycle = 1f / Mathf.Max(0.01f, attacksPerSecond);
        }

        /// <summary> 로드아웃 구독을 해제합니다. </summary>
        public void Detach()
        {
            if (loadout != null) loadout.OnChanged -= Rebuild;
        }

        /// <summary> 현재 구성의 발사 주기(초)를 계산합니다. </summary>
        /// <param name="ctx">쿨다운 감소 보정을 읽을 컨텍스트</param>
        public float CalculateCycle(in AbilityContext ctx)
        {
            float cycle = baseCycle;
            if (main != null && main.data is MainAbilityData mainData) cycle += mainData.cycleDelta;

            for (int i = 0; i < subs.Count; i++)
            {
                if (subs[i].data is SubAbilityData subData) cycle += subData.cycleDelta;
            }

            if (ctx.Modifiers != null) cycle -= ctx.Modifiers.cooldownReduction;
            return Mathf.Max(MinCycle, cycle);
        }

        /// <summary> 로드아웃에서 주축·동반을 다시 읽습니다(로드아웃 변경 시 호출). </summary>
        private void Rebuild()
        {
            main = null;
            subs.Clear();
            if (loadout == null) return;

            IReadOnlyList<AbilityInstance> actives = loadout.Actives;
            for (int i = 0; i < actives.Count; i++)
            {
                AbilityInstance inst = actives[i];
                if (inst.data is MainAbilityData) main = inst;
                else if (inst.data is SubAbilityData) subs.Add(inst);
            }
        }
    }
}
```

- [ ] **Step 5: 변경 확인**

Run: `git status --porcelain Assets/Scripts/Systems/Abilities/ Assets/Tests/EditMode/`
Expected: `CoreWeapon.cs`·`IAttackMotion.cs`·`CoreWeaponCycleTests.cs` 3건 추가

---

### Task 3: CoreWeapon 발사 묶음

주기가 되면 모션을 재생하고, 발사 프레임에 주축+동반을 한 번에 발사한다.

**Files:**
- Modify: `Assets/Scripts/Systems/Abilities/CoreWeapon.cs`
- Test: `Assets/Tests/EditMode/CoreWeaponFireTests.cs`

**Interfaces:**
- Consumes: `CoreWeapon.CalculateCycle` (Task 2), `ActiveAbilityData.FireFromWeapon`·`TargetRange` (Task 1)
- Produces:
  - `CoreWeapon.AimAt(ITargetable target)` → `void`
  - `CoreWeapon.FindMainToReplace(AbilityData incoming)` → `AbilityInstance` (없으면 null)
  - `CoreWeapon.Tick(in AbilityContext ctx, float deltaTime)` → `void`
  - `CoreWeapon.FireAll(in AbilityContext ctx)` → `void`

- [ ] **Step 1: 실패하는 테스트를 작성한다**

Create: `Assets/Tests/EditMode/CoreWeaponFireTests.cs`

```csharp
using NUnit.Framework;
using UnityEngine;
using DefenseDot.Core;
using DefenseDot.Systems.Abilities;

namespace DefenseDot.Tests.EditMode
{
    public class CoreWeaponFireTests
    {
        private sealed class CountMain : MainAbilityData
        {
            public int fires;
            protected override void Fire(in AbilityContext ctx, AbilityInstance self, ITargetable target) { fires++; }
        }

        private sealed class CountSub : SubAbilityData
        {
            public int fires;
            protected override void Fire(in AbilityContext ctx, AbilityInstance self, ITargetable target) { fires++; }
        }

        private sealed class FakeTarget : ITargetable
        {
            public Vector3 Position => Vector3.zero;
            public bool IsActive => true;
            public ActorState CurrentState => ActorState.Moving;
            public void SetState(ActorState newState) { }
            public event System.Action<ActorState> StateChanged;
        }

        private static AbilityContext Ctx()
            => new AbilityContext(null, Vector3.zero, null, new AbilityModifiers(), null);

        [Test]
        public void FireAll_FiresMainAndEverySub_Once()
        {
            var loadout = new AbilityLoadout();
            var main = ScriptableObject.CreateInstance<CountMain>();
            var subA = ScriptableObject.CreateInstance<CountSub>();
            var subB = ScriptableObject.CreateInstance<CountSub>();
            loadout.TryAdd(main);
            loadout.TryAdd(subA);
            loadout.TryAdd(subB);

            var weapon = new CoreWeapon(loadout, null);
            weapon.SetBaseAttackSpeed(1f);
            weapon.AimAt(new FakeTarget());
            weapon.FireAll(Ctx());

            Assert.AreEqual(1, main.fires);
            Assert.AreEqual(1, subA.fires);
            Assert.AreEqual(1, subB.fires);
        }

        [Test]
        public void FireAll_WithoutTarget_DoesNothing()
        {
            var loadout = new AbilityLoadout();
            var main = ScriptableObject.CreateInstance<CountMain>();
            loadout.TryAdd(main);

            var weapon = new CoreWeapon(loadout, null);
            weapon.FireAll(Ctx());

            Assert.AreEqual(0, main.fires);
        }

        [Test]
        public void Tick_WithoutMain_DoesNothing()
        {
            var loadout = new AbilityLoadout();
            var sub = ScriptableObject.CreateInstance<CountSub>();
            loadout.TryAdd(sub);

            var weapon = new CoreWeapon(loadout, null);
            weapon.SetBaseAttackSpeed(1f);
            weapon.Tick(Ctx(), 5f);

            Assert.AreEqual(0, sub.fires);
        }

        [Test]
        public void FindMainToReplace_ReturnsExistingMain_WhenIncomingIsMain()
        {
            var loadout = new AbilityLoadout();
            var equipped = ScriptableObject.CreateInstance<CountMain>();
            loadout.TryAdd(equipped);
            var weapon = new CoreWeapon(loadout, null);

            var incoming = ScriptableObject.CreateInstance<CountMain>();

            Assert.AreSame(weapon.Main, weapon.FindMainToReplace(incoming));
        }

        [Test]
        public void FindMainToReplace_ReturnsNull_WhenIncomingIsSub()
        {
            var loadout = new AbilityLoadout();
            loadout.TryAdd(ScriptableObject.CreateInstance<CountMain>());
            var weapon = new CoreWeapon(loadout, null);

            var incoming = ScriptableObject.CreateInstance<CountSub>();

            Assert.IsNull(weapon.FindMainToReplace(incoming));
        }

        [Test]
        public void FindMainToReplace_ReturnsNull_WhenNoMainEquipped()
        {
            var loadout = new AbilityLoadout();
            var weapon = new CoreWeapon(loadout, null);

            var incoming = ScriptableObject.CreateInstance<CountMain>();

            Assert.IsNull(weapon.FindMainToReplace(incoming));
        }
    }
}
```

- [ ] **Step 2: `CoreWeapon.cs` 에 조준·발사·구동을 추가한다**

`Rebuild()` 메서드 **위**에 아래 세 메서드를 삽입한다.

```csharp
        /// <summary> 이번 발사 묶음의 대상을 지정합니다(모션 없는 경로·테스트용). </summary>
        /// <param name="target">발사 대상</param>
        public void AimAt(ITargetable target)
        {
            pendingTarget = target;
        }

        /// <summary> 새 능력을 받기 위해 해제해야 할 기존 주축을 반환합니다(주축은 1개만 보유). </summary>
        /// <param name="incoming">새로 장착하려는 능력 설계도</param>
        public AbilityInstance FindMainToReplace(AbilityData incoming)
        {
            if (incoming is MainAbilityData) return main;
            return null;
        }

        /// <summary> 주기를 진행시키고, 준비되면 모션을 시작하거나 즉시 발사합니다. </summary>
        /// <param name="ctx">능력 구동 컨텍스트</param>
        /// <param name="deltaTime">경과 시간(초)</param>
        public void Tick(in AbilityContext ctx, float deltaTime)
        {
            if (main == null) return;

            remaining -= deltaTime;
            if (remaining > 0f) return;

            // 타겟이 없으면 남은시간을 되돌리지 않아, 잡히는 즉시 발사된다
            MainAbilityData mainData = main.data as MainAbilityData;
            if (mainData == null) return;
            ITargetable target = ctx.Finder != null
                ? ctx.Finder.FindNearest(ctx.Origin, mainData.TargetRange)
                : null;
            if (target == null) return;

            pendingTarget = target;
            Cycle = CalculateCycle(ctx);
            remaining = Cycle;

            // 모션이 있으면 발사 프레임이 FireAll 을 호출하고, 없으면 지금 발사한다
            AnimationClip clip = mainData.CastAnimation;
            if (motion != null && clip != null)
            {
                float speed = clip.length / Mathf.Max(MinCycle, Cycle);
                motion.PlayAttack(clip, target, speed);
            }
            else
            {
                FireAll(ctx);
            }
        }

        /// <summary> 주축과 모든 동반 능력을 한 번에 발사합니다(모션의 발사 프레임이 호출). </summary>
        /// <param name="ctx">능력 구동 컨텍스트</param>
        public void FireAll(in AbilityContext ctx)
        {
            if (main == null || pendingTarget == null) return;

            if (main.data is ActiveAbilityData mainActive) mainActive.FireFromWeapon(ctx, main, pendingTarget);
            for (int i = 0; i < subs.Count; i++)
            {
                if (subs[i].data is ActiveAbilityData subActive) subActive.FireFromWeapon(ctx, subs[i], pendingTarget);
            }
        }
```

- [ ] **Step 3: 변경 확인**

Run: `git status --porcelain Assets/Tests/EditMode/CoreWeaponFireTests.cs`
Expected: 새 파일 1건

---

### Task 4: 투사체 공용 조각과 Main/Sub 구현체

기존 `ProjectileAbilityData`(sealed)를 대체할 주축·동반 구현체를 만든다. 발사 로직은 공용 조각으로 뺀다.

**직렬화 필드를 두 클래스에 같은 이름으로 중복 선언한다.** 값 보존 마이그레이션(Task 8)이 "필드명이 같으면 값이 유지된다"에 의존하기 때문이다. 로직 중복은 `ProjectileLauncher`로 없앤다.

**Files:**
- Create: `Assets/Scripts/Systems/Abilities/Effects/ProjectileLauncher.cs`
- Create: `Assets/Scripts/Systems/Abilities/Definitions/MainProjectileAbilityData.cs`
- Create: `Assets/Scripts/Systems/Abilities/Definitions/SubProjectileAbilityData.cs`
- Delete: `Assets/Scripts/Systems/Abilities/Definitions/ProjectileAbilityData.cs` (+ `.meta`) — **Task 8에서 삭제**한다. 지금 지우면 기존 에셋이 스크립트를 잃는다.

**Interfaces:**
- Consumes: `MainAbilityData`·`SubAbilityData` (Task 1)
- Produces: `ProjectileLauncher.Launch(in AbilityContext, AbilityInstance, ITargetable, ActiveAbilityData, AssetReferenceGameObject, AssetReferenceGameObject, AssetReferenceGameObject, float, int, float)` → `void`

- [ ] **Step 1: `ProjectileLauncher.cs` 를 만든다**

```csharp
using UnityEngine;
using UnityEngine.AddressableAssets;
using DefenseDot.Core;

namespace DefenseDot.Systems.Abilities.Effects
{
    /// <summary> 유도 투사체 발사를 수행하는 공용 조각입니다. 주축·동반 능력이 함께 씁니다. </summary>
    public static class ProjectileLauncher
    {
        /// <summary> 총구에서 대상으로 유도 투사체를 발사하고 머즐 VFX를 재생합니다. </summary>
        /// <param name="ctx">능력 구동 컨텍스트</param>
        /// <param name="self">발사한 능력의 런타임 인스턴스</param>
        /// <param name="target">발사 대상</param>
        /// <param name="source">데미지 산출 기준이 되는 능력</param>
        /// <param name="projectileAsset">투사체 프리팹</param>
        /// <param name="muzzleAsset">발사 머즐 VFX(없으면 null)</param>
        /// <param name="hitVfxAsset">명중 VFX(없으면 null)</param>
        /// <param name="speed">투사체 속도</param>
        /// <param name="pierce">관통 횟수</param>
        /// <param name="range">유효 사거리</param>
        public static void Launch(in AbilityContext ctx, AbilityInstance self, ITargetable target,
            ActiveAbilityData source, AssetReferenceGameObject projectileAsset,
            AssetReferenceGameObject muzzleAsset, AssetReferenceGameObject hitVfxAsset,
            float speed, int pierce, float range)
        {
            if (ctx.Effects == null || projectileAsset == null || !projectileAsset.RuntimeKeyIsValid()) return;

            if (target == null || !target.IsActive)
                target = ctx.Finder != null ? ctx.Finder.FindNearest(ctx.Origin, range) : null;
            if (target == null) return;

            Vector3 firePos = ctx.FirePosition;   // 타워 총구(없으면 코어 중심)
            ProjectileEffect fx = ctx.Effects.Spawn<ProjectileEffect>(projectileAsset);
            if (fx == null) return;   // 스폰 실패 시 이 발동만 무산

            DamageSource src = new DamageSource(source, self, ctx.Modifiers);
            fx.Activate(firePos, target, src, speed, pierce, range, ctx.Finder, hitVfxAsset);

            if (muzzleAsset != null && muzzleAsset.RuntimeKeyIsValid())
            {
                Vector3 dir = target.Position - firePos;
                Quaternion rot = dir.sqrMagnitude > 0.0001f ? Quaternion.LookRotation(dir) : Quaternion.identity;
                ctx.Effects.PlayOneShot(muzzleAsset, firePos, rot);
            }
        }
    }
}
```

- [ ] **Step 2: `MainProjectileAbilityData.cs` 를 만든다**

필드 이름은 기존 `ProjectileAbilityData`와 **글자 하나까지 동일해야 한다**(값 보존).

```csharp
// 주축 투사체 능력 — 무기 주기에 맞춰 유도 투사체를 발사
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AddressableAssets;
using DefenseDot.Core;
using DefenseDot.Systems.Abilities.Effects;

namespace DefenseDot.Systems.Abilities.Definitions
{
    /// <summary> 무기의 주축이 되는 투사체 능력입니다. 공격 모션을 소유합니다. </summary>
    [CreateAssetMenu(fileName = "MainProjectileAbility", menuName = "DefenseDot/Abilities/Main Projectile")]
    public sealed class MainProjectileAbilityData : MainAbilityData
    {
        [SerializeField] private AssetReferenceGameObject projectileAsset;
        [SerializeField] private float baseDamage = 1f;
        [SerializeField] private float damagePerLevel = 1f;
        [SerializeField] private float speed = 12f;
        [SerializeField] private int pierce = 1;
        [SerializeField] private float range = 30f;
        [SerializeField] private AssetReferenceGameObject muzzleAsset;   // 발사 머즐 VFX
        [SerializeField] private AssetReferenceGameObject hitVfxAsset;   // 명중 VFX

        /// <summary> 레벨별 데미지입니다. </summary>
        /// <param name="level">현재 레벨</param>
        public override float ValueAtLevel(int level) { return baseDamage + damagePerLevel * (level - 1); }

        protected override float Range => range;

        /// <summary> 투사체·머즐·명중 VFX 프리팹(예열 대상). </summary>
        public override IEnumerable<AssetReferenceGameObject> EffectAssets
        {
            get
            {
                if (projectileAsset != null && projectileAsset.RuntimeKeyIsValid()) yield return projectileAsset;
                if (muzzleAsset != null && muzzleAsset.RuntimeKeyIsValid()) yield return muzzleAsset;
                if (hitVfxAsset != null && hitVfxAsset.RuntimeKeyIsValid()) yield return hitVfxAsset;
            }
        }

        /// <summary> 대상에게 유도 투사체를 발사합니다. </summary>
        /// <param name="ctx">능력 구동 컨텍스트</param>
        /// <param name="self">이 능력의 런타임 인스턴스</param>
        /// <param name="target">발사 대상</param>
        protected override void Fire(in AbilityContext ctx, AbilityInstance self, ITargetable target)
        {
            ProjectileLauncher.Launch(ctx, self, target, this,
                projectileAsset, muzzleAsset, hitVfxAsset, speed, pierce, range);
        }
    }
}
```

- [ ] **Step 3: `SubProjectileAbilityData.cs` 를 만든다**

`MainProjectileAbilityData`와 필드·본문이 같고 부모만 다르다.

```csharp
// 동반 투사체 능력 — 주축 발사에 함께 나가는 유도 투사체
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AddressableAssets;
using DefenseDot.Core;
using DefenseDot.Systems.Abilities.Effects;

namespace DefenseDot.Systems.Abilities.Definitions
{
    /// <summary> 주축 발사에 동반해 함께 나가는 투사체 능력입니다. </summary>
    [CreateAssetMenu(fileName = "SubProjectileAbility", menuName = "DefenseDot/Abilities/Sub Projectile")]
    public sealed class SubProjectileAbilityData : SubAbilityData
    {
        [SerializeField] private AssetReferenceGameObject projectileAsset;
        [SerializeField] private float baseDamage = 1f;
        [SerializeField] private float damagePerLevel = 1f;
        [SerializeField] private float speed = 12f;
        [SerializeField] private int pierce = 1;
        [SerializeField] private float range = 30f;
        [SerializeField] private AssetReferenceGameObject muzzleAsset;   // 발사 머즐 VFX
        [SerializeField] private AssetReferenceGameObject hitVfxAsset;   // 명중 VFX

        /// <summary> 레벨별 데미지입니다. </summary>
        /// <param name="level">현재 레벨</param>
        public override float ValueAtLevel(int level) { return baseDamage + damagePerLevel * (level - 1); }

        protected override float Range => range;

        /// <summary> 투사체·머즐·명중 VFX 프리팹(예열 대상). </summary>
        public override IEnumerable<AssetReferenceGameObject> EffectAssets
        {
            get
            {
                if (projectileAsset != null && projectileAsset.RuntimeKeyIsValid()) yield return projectileAsset;
                if (muzzleAsset != null && muzzleAsset.RuntimeKeyIsValid()) yield return muzzleAsset;
                if (hitVfxAsset != null && hitVfxAsset.RuntimeKeyIsValid()) yield return hitVfxAsset;
            }
        }

        /// <summary> 대상에게 유도 투사체를 발사합니다. </summary>
        /// <param name="ctx">능력 구동 컨텍스트</param>
        /// <param name="self">이 능력의 런타임 인스턴스</param>
        /// <param name="target">발사 대상</param>
        protected override void Fire(in AbilityContext ctx, AbilityInstance self, ITargetable target)
        {
            ProjectileLauncher.Launch(ctx, self, target, this,
                projectileAsset, muzzleAsset, hitVfxAsset, speed, pierce, range);
        }
    }
}
```

- [ ] **Step 4: 변경 확인**

Run: `git status --porcelain Assets/Scripts/Systems/Abilities/`
Expected: 새 파일 3건. `ProjectileAbilityData.cs` 는 **아직 그대로 있어야 한다**.

---

### Task 5: Auto 계열 이전과 러너 수정

`OrbitalAbilityData`·`AreaWaveAbilityData`의 부모를 바꾸고, 러너가 Auto만 Tick하게 한다.

**Files:**
- Modify: `Assets/Scripts/Systems/Abilities/Definitions/OrbitalAbilityData.cs:11`
- Modify: `Assets/Scripts/Systems/Abilities/Definitions/AreaWaveAbilityData.cs:12`
- Modify: `Assets/Scripts/Systems/Abilities/AbilityRunner.cs`
- Modify: `Assets/Tests/EditMode/AbilityRunnerTests.cs`

**Interfaces:**
- Consumes: `AutoAbilityData` (Task 1)
- Produces: 없음 (기존 시그니처 유지)

- [ ] **Step 1: `OrbitalAbilityData` 의 부모를 바꾼다**

`Assets/Scripts/Systems/Abilities/Definitions/OrbitalAbilityData.cs:11`

```csharp
    public sealed class OrbitalAbilityData : AutoAbilityData, IAbilityLifecycle
```

(기존 `: ActiveAbilityData, IAbilityLifecycle` 에서 변경. `:50`의 빈 `Tick` 재정의는 그대로 둔다 — 상시 능력이라 Tick이 필요 없다.)

- [ ] **Step 2: `AreaWaveAbilityData` 의 부모를 바꾼다**

`Assets/Scripts/Systems/Abilities/Definitions/AreaWaveAbilityData.cs:12`

```csharp
    public sealed class AreaWaveAbilityData : AutoAbilityData
```

- [ ] **Step 3: `AbilityRunner.Tick` 이 Auto만 구동하게 한다**

`Assets/Scripts/Systems/Abilities/AbilityRunner.cs` 의 `Tick` 본문에서 캐스팅 대상을 바꾼다.

```csharp
        /// <summary> 매 프레임 자율 능력을 Tick합니다(주축·동반은 CoreWeapon이 구동). </summary>
        /// <param name="deltaTime">경과 시간(초)</param>
        public void Tick(float deltaTime)
        {
            IReadOnlyList<AbilityInstance> actives = loadout.Actives;
            for (int i = 0; i < actives.Count; i++)
            {
                AbilityInstance inst = actives[i];
                if (inst.data is AutoAbilityData auto) auto.Tick(ctx, inst, deltaTime);
            }
        }
```

- [ ] **Step 4: `AbilityRunnerTests` 의 테스트 더블 부모를 바꾼다**

`Tick`이 `AutoAbilityData`로 내려갔으므로 `CountTick`이 이를 상속해야 한다.

`Assets/Tests/EditMode/AbilityRunnerTests.cs:9` 와 `:16`

```csharp
        private sealed class CountTick : AutoAbilityData
        {
            public int ticks;
            public override void Tick(in AbilityContext ctx, AbilityInstance self, float dt) { ticks++; }
            protected override void Fire(in AbilityContext ctx, AbilityInstance self, DefenseDot.Core.ITargetable target) { }
        }

        private sealed class LifeAbility : AutoAbilityData, IAbilityLifecycle
        {
            public int equips, unequips;
            public override void Tick(in AbilityContext ctx, AbilityInstance self, float dt) { }
            protected override void Fire(in AbilityContext ctx, AbilityInstance self, DefenseDot.Core.ITargetable target) { }
            public void OnEquip(in AbilityContext ctx, AbilityInstance self) { equips++; }
            public void OnUnequip(in AbilityContext ctx, AbilityInstance self) { unequips++; }
        }
```

- [ ] **Step 5: 다른 곳에 `ActiveAbilityData`를 직접 상속한 코드가 없는지 확인한다**

Run: `grep -rn ": ActiveAbilityData" Assets/Scripts Assets/Tests --include=*.cs`
Expected: `MainAbilityData`·`SubAbilityData`·`AutoAbilityData` 3건과, 아직 지우지 않은 `ProjectileAbilityData` 1건만 나온다. 그 외가 나오면 그 클래스도 Auto 계열로 옮긴다.

---

### Task 6: 시전 채널 제거

`ICastHost`·`ICastReceiver`를 걷어내고 `CoreAbilitySystem`이 `CoreWeapon`을 소유·구동하게 한다.

**Files:**
- Delete: `Assets/Scripts/Systems/Abilities/ICastHost.cs`, `Assets/Scripts/Systems/Abilities/ICastReceiver.cs` (+ `.meta`)
- Modify: `Assets/Scripts/Systems/Abilities/AbilityContext.cs`
- Modify: `Assets/Scripts/Systems/Abilities/CoreAbilitySystem.cs`
- Modify: `Assets/Scripts/Systems/Mode/ArisTowerVisual.cs`

**Interfaces:**
- Consumes: `CoreWeapon`(Task 2·3), `IAttackMotion`(Task 2)
- Produces:
  - `CoreAbilitySystem.SetAttackMotion(IAttackMotion motion)` → `void`
  - `CoreAbilitySystem.SetBaseAttackSpeed(float attacksPerSecond)` → `void`
  - `CoreAbilitySystem.NotifyFireFrame()` → `void` (유지 — 비주얼이 호출)

- [ ] **Step 1: `AbilityContext` 에서 `Cast` 를 제거한다**

`Assets/Scripts/Systems/Abilities/AbilityContext.cs` — `Cast` 필드와 생성자 인자를 지운다.

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
        /// <summary> 발동 원점(코어 중심). 타게팅·오비탈 궤도 중심 기준. </summary>
        public readonly Vector3 Origin;
        /// <summary> 발사체·머즐 스폰용 발사점(타워 총구). null이면 Origin으로 폴백. </summary>
        public readonly Transform FireOrigin;
        /// <summary> 적 질의 수단. </summary>
        public readonly TargetFinder Finder;
        /// <summary> 패시브 합산 보정. </summary>
        public readonly AbilityModifiers Modifiers;
        /// <summary> 효과 엔티티 스포너. </summary>
        public readonly IEffectSpawner Effects;

        /// <summary> 발사 시점의 총구 월드 위치. 발사점 미배선이면 코어 중심(Origin). </summary>
        public Vector3 FirePosition => FireOrigin != null ? FireOrigin.position : Origin;

        public AbilityContext(MonoBehaviour host, Vector3 origin, TargetFinder finder,
            AbilityModifiers modifiers, IEffectSpawner effects, Transform fireOrigin = null)
        {
            Host = host;
            Origin = origin;
            FireOrigin = fireOrigin;
            Finder = finder;
            Modifiers = modifiers;
            Effects = effects;
        }
    }
}
```

- [ ] **Step 2: `CoreAbilitySystem` 을 고친다**

`ICastHost` 구현·대기 발사 필드·`RequestCast`를 지우고 `CoreWeapon`을 소유한다. 주축 교체 규칙도 여기서 적용한다.

`Assets/Scripts/Systems/Abilities/CoreAbilitySystem.cs` 전체를 아래로 교체한다.

```csharp
// 코어 능력 구동 — 로드아웃·러너·무기 보유. 자율 능력은 러너가, 주축·동반은 무기가 구동
using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.AddressableAssets;
using DefenseDot.Core.Pooling;
using DefenseDot.Domain.Models;
using DefenseDot.Systems.Tower;
using DefenseDot.Systems.Abilities.Effects;

namespace DefenseDot.Systems.Abilities
{
    /// <summary> Arena 코어의 능력 로드아웃과 무기를 구동하는 컴포넌트입니다. </summary>
    public sealed class CoreAbilitySystem : MonoBehaviour, IAbilityCommandTarget
    {
        private AbilityLoadout loadout;      // 장착 능력 슬롯(액티브/패시브)
        private AbilityRunner runner;        // 자율 능력 프레임 구동·장착 훅
        private CoreWeapon weapon;           // 주축·동반 발사 묶음(공격 주기 소유)
        private GameFlowModel flow;          // 진행 단계(발동 게이트)
        private AbilityContext ctx;          // 공용 컨텍스트(모든 능력 공유)
        private IAttackMotion motion;        // 공격 모션 재생 대상
        private PoolManager pool;            // 스타터 예열용
        private float baseAttackSpeed = 1f;  // 타워 기본 공격 속도(초당 횟수)

        /// <summary> 공격 모션 재생 대상을 연결합니다(무기 생성 전에 호출). </summary>
        /// <param name="attackMotion">타워 비주얼</param>
        public void SetAttackMotion(IAttackMotion attackMotion)
        {
            motion = attackMotion;
            weapon?.Detach();
            weapon = null;
        }

        /// <summary> 타워 기본 공격 속도를 설정합니다. </summary>
        /// <param name="attacksPerSecond">초당 공격 횟수</param>
        public void SetBaseAttackSpeed(float attacksPerSecond)
        {
            baseAttackSpeed = attacksPerSecond;
            weapon?.SetBaseAttackSpeed(attacksPerSecond);
        }

        /// <summary> 합성 루트가 의존성·스타터 능력을 주입합니다. fireOrigin은 발사체·머즐 스폰용 총구(없으면 origin 폴백). </summary>
        public void Setup(TargetFinder finder, Vector3 origin, GameFlowModel gameFlow,
            ICombatState combatState, IReadOnlyList<AbilityData> starters, PoolManager poolManager,
            Transform fireOrigin = null)
        {
            flow = gameFlow;
            pool = poolManager;
            loadout = new AbilityLoadout();
            loadout.Modifiers.combatState = combatState;
            if (starters != null)
            {
                for (int i = 0; i < starters.Count; i++)
                {
                    if (starters[i] != null) loadout.TryAdd(starters[i]);
                }
            }

            IEffectSpawner effects = new PooledEffectSpawner(poolManager);
            ctx = new AbilityContext(this, origin, finder, loadout.Modifiers, effects, fireOrigin);
            runner = new AbilityRunner(loadout, ctx);
            weapon = new CoreWeapon(loadout, motion);
            weapon.SetBaseAttackSpeed(baseAttackSpeed);
            // 장착은 예열 후로 미룸(예열 전 Spawn 방지) → WarmupStartersAsync → EquipAll
        }

        /// <summary> 스타터 이펙트를 예열합니다(장착 전. 로드 실패는 값으로 스킵되어 예외 없음). </summary>
        public async UniTask WarmupStartersAsync()
        {
            if (pool == null || loadout == null) return;
            using (UnityEngine.Pool.HashSetPool<AssetReferenceGameObject>.Get(out HashSet<AssetReferenceGameObject> set))
            {
                CollectAssets(loadout.Actives, set);
                CollectAssets(loadout.Passives, set);
                if (set.Count > 0) await pool.WarmupAsync(set);
            }
        }

        /// <summary> 장착된 액티브 능력을 러너에 장착합니다. </summary>
        public void EquipAll() => runner?.EquipAll();

        /// <summary> 능력 목록의 예열 대상 에셋을 집합에 모읍니다. </summary>
        private static void CollectAssets(IReadOnlyList<AbilityInstance> list, HashSet<AssetReferenceGameObject> set)
        {
            for (int i = 0; i < list.Count; i++)
            {
                AbilityData d = list[i].data;
                if (d == null) continue;
                foreach (AssetReferenceGameObject a in d.EffectAssets)
                {
                    if (a != null) set.Add(a);
                }
            }
        }

        #region IAbilityCommandTarget
        /// <summary> 읽기 전용 로드아웃(카드 생성기 질의용). </summary>
        public AbilityLoadout Loadout => loadout;

        /// <summary> 신규 능력 추가. 주축이면 기존 주축을 먼저 해제합니다(주축은 1개만). </summary>
        /// <param name="data">추가할 능력 설계도</param>
        public AbilityInstance AddAbility(AbilityData data)
        {
            if (loadout == null) return null;

            // 주축은 1개만 보유 — 교체 규칙은 무기가 판단한다
            AbilityInstance replaced = weapon?.FindMainToReplace(data);
            if (replaced != null) RemoveAbility(replaced);

            if (!loadout.TryAdd(data)) return null;
            bool isActive = data is ActiveAbilityData;
            AbilityInstance inst = isActive
                ? loadout.Actives[loadout.Actives.Count - 1]
                : loadout.Passives[loadout.Passives.Count - 1];
            if (isActive) runner?.Equip(inst);
            return inst;
        }

        /// <summary> 기존 능력 레벨업. </summary>
        /// <param name="instance">레벨업할 인스턴스</param>
        public void LevelUpAbility(AbilityInstance instance) => loadout?.LevelUp(instance);

        /// <summary> 능력 삭제. 액티브면 러너에서 언장착 후 로드아웃에서 제거합니다. </summary>
        /// <param name="instance">제거할 인스턴스</param>
        public void RemoveAbility(AbilityInstance instance)
        {
            if (instance?.data is ActiveAbilityData) runner?.Unequip(instance);
            loadout?.Remove(instance);
        }
        #endregion

        /// <summary> 공격 모션의 발사 프레임에서 비주얼이 호출합니다. </summary>
        public void NotifyFireFrame()
        {
            weapon?.FireAll(ctx);
        }

        private void Update()
        {
            if (flow == null || !flow.IsPlaying) return;
            float dt = Time.deltaTime;
            runner?.Tick(dt);
            weapon?.Tick(ctx, dt);
        }

        private void OnDestroy()
        {
            weapon?.Detach();
        }
    }
}
```

- [ ] **Step 3: `ArisTowerVisual` 을 `IAttackMotion` 으로 바꾼다**

`Assets/Scripts/Systems/Mode/ArisTowerVisual.cs` 에서 아래 4곳을 고친다.

3-1. 클래스 선언(`:15`)

```csharp
    public sealed class ArisTowerVisual : MonoBehaviour, IAttackMotion
```

3-2. `isCasting`·`castRemaining`·`IsCasting` 제거. `:39-45` 를 아래로 교체한다.

```csharp
        private bool locked;              // 파괴/승리 시 회전·시전 잠금
        private ITargetable castTarget;   // 발사 대상(조준 유지용)
```

3-3. `Update`(`:79-87`)에서 시전 카운트다운을 제거한다.

```csharp
        private void Update()
        {
            if (!locked) FaceTarget();
        }
```

3-4. `FaceTarget`(`:93-95`)의 조준 대상 선택에서 `isCasting` 조건을 뺀다.

```csharp
            ITargetable target = (castTarget != null && castTarget.IsActive)
                ? castTarget
                : (finder != null ? finder.FindNearest(transform.position, targetRange) : null);
```

3-5. `PlayCast`(`:106-117`)를 `PlayAttack` 으로 교체한다.

```csharp
        #region IAttackMotion
        /// <summary> Attack 슬롯을 지정 클립으로 교체하고 지정 속도로 재생합니다. </summary>
        /// <param name="clip">재생할 공격 모션</param>
        /// <param name="target">조준 대상</param>
        /// <param name="speed">재생 속도 배수</param>
        public void PlayAttack(AnimationClip clip, ITargetable target, float speed)
        {
            if (locked || animator == null) return;

            castTarget = target;
            if (clip != null && overrideController != null) overrideController[AttackClipKey] = clip;
            animator.SetFloat(AttackSpeedHash, Mathf.Max(0.01f, speed));
            animator.SetTrigger(AttackHash);
        }
        #endregion
```

3-6. 속도 파라미터 해시를 추가한다(`:27` 근처).

```csharp
        private static readonly int AttackSpeedHash = Animator.StringToHash("AttackSpeed");
```

3-7. `Setup`(`:70`)의 연결을 바꾼다.

```csharp
            if (core != null) core.SetAttackMotion(this);
```

3-8. `HandleCoreDestroyed`(`:141`)·`HandlePhaseChanged`(`:149`)의 `isCasting = false;` 줄을 지운다.

- [ ] **Step 4: `ICastHost.cs`·`ICastReceiver.cs` 를 지운다**

```bash
rm Assets/Scripts/Systems/Abilities/ICastHost.cs Assets/Scripts/Systems/Abilities/ICastHost.cs.meta
rm Assets/Scripts/Systems/Abilities/ICastReceiver.cs Assets/Scripts/Systems/Abilities/ICastReceiver.cs.meta
```

- [ ] **Step 5: 남은 참조가 없는지 확인한다**

Run: `grep -rn "ICastHost\|ICastReceiver\|RequestCast\|IsCasting\|SetCastReceiver\|PlayCast\|FireFromHost\|ctx.Cast" Assets/Scripts Assets/Tests --include=*.cs`
Expected: 출력 없음. 남으면 그 자리를 함께 고친다.

---

### Task 7: 타워 기본 공격 속도 배선

`TowerData.attackSpeed` 를 아레나 코어의 무기에 주입한다.

**Files:**
- Modify: `Assets/Scripts/Systems/Mode/ArenaModeBootstrap.cs:88-100`

**Interfaces:**
- Consumes: `CoreAbilitySystem.SetAttackMotion`·`SetBaseAttackSpeed` (Task 6)
- Produces: 없음

- [ ] **Step 1: 배선 순서를 고친다**

`SpawnCenterTower` 안에서, 비주얼을 만든 뒤 능력 시스템에 모션과 공격 속도를 주입한다. 기존의 `arisVisual.Setup(...)` 호출은 그대로 두되(코어 HP·단계 구독), 모션 연결이 `Setup` 안에서 `SetAttackMotion`으로 바뀌었으므로 **공격 속도 주입을 `coreAbility.Setup` 앞에 넣는다**.

`Assets/Scripts/Systems/Mode/ArenaModeBootstrap.cs` 의 아래 블록을 교체한다.

```csharp
            // 코어: 디버그 단일공격 제거 + 능력 시스템 부착
            TowerBehaviorTree debugBt = go.GetComponent<TowerBehaviorTree>();
            if (debugBt != null) Destroy(debugBt);
            coreAbility = go.AddComponent<CoreAbilitySystem>();

            // Aris 비주얼을 먼저 생성해 발사점(총구)을 확보 → 능력 시스템에 주입
            ArisTowerVisual arisVisual = ReplaceWithArisVisual(go, ctx);
            Transform fireOrigin = arisVisual != null ? arisVisual.FirePoint : null;

            // 모션·기본 공격 속도를 먼저 주입해야 무기가 올바른 값으로 생성된다
            if (arisVisual != null) coreAbility.SetAttackMotion(arisVisual);
            coreAbility.SetBaseAttackSpeed(data.attackSpeed);

            coreAbility.Setup(ctx.TargetFinder, ctx.CoreCenter, ctx.Flow, ctx.CombatState, starterAbilities, ctx.Pooling, fireOrigin);
            StartCoreAbilities(coreAbility).Forget();   // 예열 → 장착 순서 조율

            // 총구 확보 후 비주얼에 능력 시스템 연동
            if (arisVisual != null) arisVisual.Setup(coreAbility, ctx.TargetFinder, ctx.Flow, ctx.Core);
```

- [ ] **Step 2: `ArisTowerVisual.Setup` 안의 모션 연결이 중복되지 않는지 확인한다**

Task 6 Step 3-7에서 `Setup` 안에 `core.SetAttackMotion(this)` 를 넣었고 여기서도 호출한다. `SetAttackMotion` 은 같은 값을 두 번 받아도 무해하지만(무기를 다시 만들 뿐), **`Setup` 이후에 호출되면 무기가 날아간다**. 순서상 `arisVisual.Setup` 이 `coreAbility.Setup` 뒤에 오므로, 중복 호출이 무기를 파괴한다.

따라서 **`ArisTowerVisual.Setup` 안의 `core.SetAttackMotion(this)` 줄을 지운다**. 모션 연결은 `ArenaModeBootstrap`이 단독으로 책임진다.

`Assets/Scripts/Systems/Mode/ArisTowerVisual.cs:70` 의 아래 줄을 삭제한다.

```csharp
            if (core != null) core.SetAttackMotion(this);
```

- [ ] **Step 3: 확인**

Run: `grep -n "SetAttackMotion" Assets/Scripts --include=*.cs -r`
Expected: `CoreAbilitySystem.cs` 의 정의 1건 + `ArenaModeBootstrap.cs` 의 호출 1건 = 총 2건

---

### Task 8: Unity 켠 뒤 마무리 (에디터 필요)

여기부터는 **Unity 에디터가 켜져 있어야** 한다. 새 스크립트가 임포트되어 `.meta`(GUID)가 생겨야 에셋 마이그레이션이 가능하다.

**Files:**
- Modify: `Assets/Data/Abilities/Ability_Shot.asset`, `Ability_Railgun.asset`, `Ability_StormBrand.asset`
- Delete: `Assets/Scripts/Systems/Abilities/Definitions/ProjectileAbilityData.cs` (+ `.meta`)
- Modify: Aris Animator Controller (에디터 작업)
- Modify: `Assets/Settings/` 의 `TowerData` 에셋 (`attackSpeed` 값)

- [ ] **Step 1: Unity 를 켜고 컴파일 오류를 해소한다**

Unity 콘솔에 오류가 없어야 다음으로 간다. 오류가 나면 그 자리를 고친다.

- [ ] **Step 2: EditMode 테스트를 돌린다**

Unity: `Window > General > Test Runner > EditMode > Run All`
Expected: `CoreWeaponCycleTests` 7건 + `CoreWeaponFireTests` 6건 전부 통과. 기존 테스트도 전부 통과.

- [ ] **Step 3: 새 클래스의 GUID 를 확인한다**

```bash
grep -h "^guid:" Assets/Scripts/Systems/Abilities/Definitions/MainProjectileAbilityData.cs.meta \
                 Assets/Scripts/Systems/Abilities/Definitions/SubProjectileAbilityData.cs.meta
```

- [ ] **Step 4: 능력 에셋 3종의 `m_Script` GUID 를 교체한다**

`Ability_Shot.asset` → `MainProjectileAbilityData` 의 GUID
`Ability_Railgun.asset`·`Ability_StormBrand.asset` → `SubProjectileAbilityData` 의 GUID

각 `.asset` 파일의 `m_Script: {fileID: 11500000, guid: <구 GUID>, type: 3}` 에서 guid만 바꾼다. **필드 이름이 같으므로 값은 보존된다.**

- [ ] **Step 5: 교체 결과를 확인한다**

Unity 인스펙터에서 세 에셋을 연다.
Expected: 스크립트가 각각 `MainProjectileAbilityData`·`SubProjectileAbilityData` 로 표시되고, `projectileAsset`·`baseDamage`·`speed`·`pierce`·`range`·`muzzleAsset`·`hitVfxAsset` 값이 교체 전과 같다. `Ability_Shot` 의 `castAnimation` 도 유지된다.

- [ ] **Step 6: `cycleDelta` 초기값을 넣는다**

| 에셋 | `cycleDelta` |
|---|---|
| `Ability_Shot` | `0` |
| `Ability_Railgun` | `0.5` |
| `Ability_StormBrand` | `0.2` |

- [ ] **Step 7: 구 `ProjectileAbilityData` 를 지운다**

에셋 3종이 모두 새 클래스를 가리키는 것을 확인한 **뒤에만** 지운다.

```bash
rm Assets/Scripts/Systems/Abilities/Definitions/ProjectileAbilityData.cs \
   Assets/Scripts/Systems/Abilities/Definitions/ProjectileAbilityData.cs.meta
```

- [ ] **Step 8: Animator 에 `AttackSpeed` 파라미터를 만든다**

Aris Animator Controller 에서:
1. Parameters 에 `Float` 타입 `AttackSpeed` 추가 (기본값 `1`)
2. Attack 상태를 선택 → Inspector 의 `Speed` 옆 `Multiplier` 체크 → 드롭다운에서 `AttackSpeed` 선택

이 단계를 건너뛰어도 **발사 주기는 정상 동작한다**(모션이 원래 속도로 재생될 뿐). 모션과 주기의 동기화만 안 된다.

- [ ] **Step 9: `TowerData.attackSpeed` 를 정한다**

아레나 중앙 타워가 쓰는 `TowerData` 에셋에서 `attackSpeed` 를 설정한다. 시작값은 `1`(주기 1.0초)을 권한다.

- [ ] **Step 10: 플레이로 확인한다**

| # | 확인 | 기대 |
|---|---|---|
| 1 | 샷만 보유하고 발사 간격 관찰 | 약 1초 간격 (기존 2.67초) |
| 2 | 치트로 레일건·폭풍낙인 획득 | 한 모션에 3발이 함께 나감 |
| 3 | 위 상태에서 발사 간격 관찰 | 약 1.7초로 느려짐 |
| 4 | 오비탈 획득 | 발사 간격 변화 없음, 오비탈은 계속 회전 |
| 5 | 콘솔 | 오류·경고 없음 |

- [ ] **Step 11: 결과를 사용자에게 보고한다**

커밋하지 않는다. 사용자가 명시적으로 요청할 때만 커밋한다.

---

## 후속 (별도 계획)

라운드 이어가기 + 적 체력 스케일링. 스펙 §14 참조. **검증은 본 계획과 함께** 수행해야 한다 — 화력이 정상화되지 않으면 체력 곡선을 넣어도 몇 라운드 만에 무너지고, 체력 곡선이 없으면 패배가 불가능해 판단 기준이 없다.
