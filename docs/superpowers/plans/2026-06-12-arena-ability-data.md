# Arena 능력 데이터 아키텍처 (A1) Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** 능력을 데이터로 표현하는 `AbilityData` SO 계층과 런타임 `AbilityInstance`/`AbilityLoadout`/`AbilityModifiers`/`AbilityContext`를 구축한다. (능력 콘텐츠·tick 루프·카드 UI는 비범위)

**Architecture:** 추상 ScriptableObject(`AbilityData`→`ActiveAbilityData`/`PassiveAbilityData`)가 정적 설계도, 런타임 상태는 POCO(`AbilityInstance`), 보유·슬롯은 POCO `AbilityLoadout`(active6+passive6). 패시브는 `AbilityModifiers`에 합산 캐시. 실행 입력은 `AbilityContext`(struct) 계약. 로드아웃은 순수 POCO라 EditMode로 완전 검증.

**Tech Stack:** C# (Unity 6000.2), ScriptableObject, NUnit EditMode, 어셈블리 `DefenseDot`.

**Commits:** 각 Task commit 단계는 **`commit` 스킬**로 수행(직접 git commit 금지).

**Spec:** `docs/superpowers/specs/2026-06-12-arena-ability-data-design.md`. **상위:** TASK-012 Arena 로드맵 A1.

---

## File Structure

| 파일 | 책임 |
|---|---|
| `Assets/Scripts/Systems/Abilities/AbilityData.cs` | 추상 SO — 공통 메타 |
| `Assets/Scripts/Systems/Abilities/ActiveAbilityData.cs` | 추상 SO — 발동형(쿨다운+Execute) |
| `Assets/Scripts/Systems/Abilities/PassiveAbilityData.cs` | 추상 SO — 보정형(ApplyModifiers) |
| `Assets/Scripts/Systems/Abilities/AbilityModifiers.cs` | POCO — 패시브 합산 보정 |
| `Assets/Scripts/Systems/Abilities/AbilityContext.cs` | readonly struct — 실행 입력 계약 |
| `Assets/Scripts/Systems/Abilities/AbilityInstance.cs` | POCO — 런타임 상태(level·cooldown) |
| `Assets/Scripts/Systems/Abilities/AbilityLoadout.cs` | POCO — active6+passive6 슬롯 + API |
| `Assets/Tests/EditMode/AbilityLoadoutTests.cs` | 로드아웃 동작 검증 |

> 네임스페이스 `DefenseDot.Systems.Abilities`. 파일명=첫 타입명(훅 동기화). 신규 타입은 `DefenseDot` 어셈블리 → 기존 EditMode 테스트 asmdef가 참조.

---

## Task 1: 타입 계약 (SO 계층 + POCO + struct)

**Files:**
- Create: `Assets/Scripts/Systems/Abilities/AbilityData.cs`
- Create: `Assets/Scripts/Systems/Abilities/ActiveAbilityData.cs`
- Create: `Assets/Scripts/Systems/Abilities/PassiveAbilityData.cs`
- Create: `Assets/Scripts/Systems/Abilities/AbilityModifiers.cs`
- Create: `Assets/Scripts/Systems/Abilities/AbilityInstance.cs`
- Create: `Assets/Scripts/Systems/Abilities/AbilityContext.cs`

- [ ] **Step 1: AbilityData.cs**

```csharp
using UnityEngine;

namespace DefenseDot.Systems.Abilities
{
    /// <summary> 능력의 정적 설계도(추상). 능력 1종 = 이 파생형의 에셋 1개. </summary>
    public abstract class AbilityData : ScriptableObject
    {
        /// <summary> 고유 식별자. </summary>
        public string id;
        /// <summary> 표시 이름. </summary>
        public string displayName;
        /// <summary> 카드/슬롯 아이콘. </summary>
        public Sprite icon;
        /// <summary> 등급/티어. </summary>
        public int rarity;
        /// <summary> 최대 레벨. </summary>
        public int maxLevel = 5;
    }
}
```

- [ ] **Step 2: AbilityModifiers.cs**

```csharp
namespace DefenseDot.Systems.Abilities
{
    /// <summary> 패시브들이 누적하는 합산 보정값입니다. (필드는 패시브 추가에 따라 확장) </summary>
    public sealed class AbilityModifiers
    {
        /// <summary> 가산 공격력 보너스. </summary>
        public float damageBonus;
        /// <summary> 쿨다운 감소(초). </summary>
        public float cooldownReduction;
    }
}
```

- [ ] **Step 3: AbilityContext.cs**

```csharp
using UnityEngine;
using DefenseDot.Systems.Tower;

namespace DefenseDot.Systems.Abilities
{
    /// <summary> 능력 1회 발동에 필요한 입력 묶음(Context Object)입니다. </summary>
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

        public AbilityContext(MonoBehaviour host, Vector3 origin, TargetFinder finder, AbilityModifiers modifiers)
        {
            Host = host;
            Origin = origin;
            Finder = finder;
            Modifiers = modifiers;
        }
    }
}
```

- [ ] **Step 4: AbilityInstance.cs**

```csharp
namespace DefenseDot.Systems.Abilities
{
    /// <summary> 한 능력의 런타임 상태입니다. (설계도=AbilityData, 상태=레벨·쿨다운) </summary>
    public sealed class AbilityInstance
    {
        /// <summary> 참조하는 정적 설계도. </summary>
        public readonly AbilityData data;
        /// <summary> 현재 레벨. </summary>
        public int level;
        /// <summary> 남은 쿨다운(초, 액티브용). </summary>
        public float cooldownRemaining;

        public AbilityInstance(AbilityData data, int level = 1)
        {
            this.data = data;
            this.level = level;
        }
    }
}
```

- [ ] **Step 5: ActiveAbilityData.cs**

```csharp
namespace DefenseDot.Systems.Abilities
{
    /// <summary> 발동형 능력(추상). 쿨다운마다 Execute로 1회 발동합니다. </summary>
    public abstract class ActiveAbilityData : AbilityData
    {
        /// <summary> 기본 쿨다운(초). </summary>
        public float baseCooldown = 1f;

        /// <summary> 레벨별 효과값(데미지 등). 서브클래스가 재정의. </summary>
        public virtual float ValueAtLevel(int level) { return level; }

        /// <summary> 레벨별 쿨다운(보정 적용은 호출부). </summary>
        public virtual float CooldownAtLevel(int level) { return baseCooldown; }

        /// <summary> 1회 발동(투사체·데미지 등). 가동 루프는 A2. </summary>
        public abstract void Execute(in AbilityContext ctx, AbilityInstance self);
    }
}
```

- [ ] **Step 6: PassiveAbilityData.cs**

```csharp
namespace DefenseDot.Systems.Abilities
{
    /// <summary> 보정형 능력(추상). 보유/레벨에 따라 합산 보정에 기여합니다. </summary>
    public abstract class PassiveAbilityData : AbilityData
    {
        /// <summary> 자신의 보정을 mods에 누적합니다. </summary>
        public abstract void ApplyModifiers(AbilityModifiers mods, int level);
    }
}
```

- [ ] **Step 7: 컴파일 확인** — `refresh_unity`(compile) → `read_console`(error 0). (추상/struct/POCO만 — 동작 테스트는 Task 2)

- [ ] **Step 8: 커밋** — `commit` 스킬. 예: `feat: 능력 데이터 계약(AbilityData 계층·Instance·Context·Modifiers) 추가`

---

## Task 2: AbilityLoadout + EditMode 테스트 (TDD)

**Files:**
- Test: `Assets/Tests/EditMode/AbilityLoadoutTests.cs`
- Create: `Assets/Scripts/Systems/Abilities/AbilityLoadout.cs`

- [ ] **Step 1: 실패 테스트 작성**

`Assets/Tests/EditMode/AbilityLoadoutTests.cs`:
```csharp
using NUnit.Framework;
using UnityEngine;
using DefenseDot.Systems.Abilities;

namespace DefenseDot.Tests.EditMode
{
    public class AbilityLoadoutTests
    {
        private sealed class StubActive : ActiveAbilityData
        {
            public override void Execute(in AbilityContext ctx, AbilityInstance self) { }
        }
        private sealed class StubPassive : PassiveAbilityData
        {
            public float perLevelBonus = 2f;
            public override void ApplyModifiers(AbilityModifiers mods, int level) { mods.damageBonus += perLevelBonus * level; }
        }

        private static StubActive NewActive(int maxLevel = 5)
        {
            var a = ScriptableObject.CreateInstance<StubActive>();
            a.maxLevel = maxLevel;
            return a;
        }
        private static StubPassive NewPassive()
        {
            var p = ScriptableObject.CreateInstance<StubPassive>();
            p.maxLevel = 5;
            return p;
        }

        [Test]
        public void TryAdd_RoutesActiveAndPassiveBySubclass()
        {
            var lo = new AbilityLoadout(6, 6);
            Assert.IsTrue(lo.TryAdd(NewActive()));
            Assert.IsTrue(lo.TryAdd(NewPassive()));
            Assert.AreEqual(1, lo.Actives.Count);
            Assert.AreEqual(1, lo.Passives.Count);
        }

        [Test]
        public void TryAdd_WhenActiveFull_ReturnsFalse()
        {
            var lo = new AbilityLoadout(2, 6);
            Assert.IsTrue(lo.TryAdd(NewActive()));
            Assert.IsTrue(lo.TryAdd(NewActive()));
            Assert.IsFalse(lo.TryAdd(NewActive()), "액티브 슬롯 한계 초과는 거부");
        }

        [Test]
        public void TryAdd_Duplicate_ReturnsFalse()
        {
            var lo = new AbilityLoadout(6, 6);
            var a = NewActive();
            Assert.IsTrue(lo.TryAdd(a));
            Assert.IsFalse(lo.TryAdd(a), "이미 보유한 능력은 추가 대신 LevelUp 대상");
        }

        [Test]
        public void LevelUp_IncrementsClampedToMax()
        {
            var lo = new AbilityLoadout(6, 6);
            var a = NewActive(maxLevel: 2);
            lo.TryAdd(a);
            var inst = lo.Actives[0];
            lo.LevelUp(inst);
            Assert.AreEqual(2, inst.level);
            lo.LevelUp(inst);
            Assert.AreEqual(2, inst.level, "maxLevel에서 클램프");
        }

        [Test]
        public void Remove_RemovesInstance()
        {
            var lo = new AbilityLoadout(6, 6);
            lo.TryAdd(NewActive());
            lo.Remove(lo.Actives[0]);
            Assert.AreEqual(0, lo.Actives.Count);
        }

        [Test]
        public void Modifiers_SumsPassivesAndRecalcsOnChange()
        {
            var lo = new AbilityLoadout(6, 6);
            lo.TryAdd(NewPassive());           // level1 → +2
            Assert.AreEqual(2f, lo.Modifiers.damageBonus, 1e-4f);
            lo.TryAdd(NewPassive());           // +2 → 합 4
            Assert.AreEqual(4f, lo.Modifiers.damageBonus, 1e-4f);
            lo.LevelUp(lo.Passives[0]);        // 첫째 level2 → +4, 둘째 +2 → 합 6
            Assert.AreEqual(6f, lo.Modifiers.damageBonus, 1e-4f);
            lo.Remove(lo.Passives[0]);         // 둘째만 → +2
            Assert.AreEqual(2f, lo.Modifiers.damageBonus, 1e-4f);
        }
    }
}
```

- [ ] **Step 2: 테스트 실패 확인** — `run_tests` EditMode(`AbilityLoadoutTests`). Expected: `AbilityLoadout` 미정의 RED.

- [ ] **Step 3: AbilityLoadout.cs 구현**

```csharp
using System.Collections.Generic;

namespace DefenseDot.Systems.Abilities
{
    /// <summary> 코어가 보유하는 능력 슬롯(액티브/패시브)과 그 관리 API입니다. </summary>
    public sealed class AbilityLoadout
    {
        private readonly int activeCapacity;
        private readonly int passiveCapacity;
        private readonly List<AbilityInstance> actives = new List<AbilityInstance>();
        private readonly List<AbilityInstance> passives = new List<AbilityInstance>();
        private readonly AbilityModifiers modifiers = new AbilityModifiers();

        /// <summary> 장착된 액티브 능력들. </summary>
        public IReadOnlyList<AbilityInstance> Actives => actives;
        /// <summary> 장착된 패시브 능력들. </summary>
        public IReadOnlyList<AbilityInstance> Passives => passives;
        /// <summary> 패시브 합산 보정(캐시). </summary>
        public AbilityModifiers Modifiers => modifiers;

        public AbilityLoadout(int activeCapacity = 6, int passiveCapacity = 6)
        {
            this.activeCapacity = activeCapacity;
            this.passiveCapacity = passiveCapacity;
        }

        /// <summary> 보유 여부(액티브·패시브 통합). </summary>
        public bool Contains(AbilityData data)
        {
            for (int i = 0; i < actives.Count; i++) if (actives[i].data == data) return true;
            for (int i = 0; i < passives.Count; i++) if (passives[i].data == data) return true;
            return false;
        }

        /// <summary> 추가 가능 여부(타입별 슬롯 여유 + 미보유). </summary>
        public bool CanAdd(AbilityData data)
        {
            if (data == null || Contains(data)) return false;
            if (data is PassiveAbilityData) return passives.Count < passiveCapacity;
            return actives.Count < activeCapacity;   // 그 외(ActiveAbilityData)
        }

        /// <summary> 새 능력을 해당 슬롯에 추가합니다. 불가 시 false. </summary>
        public bool TryAdd(AbilityData data)
        {
            if (!CanAdd(data)) return false;
            var inst = new AbilityInstance(data, 1);
            if (data is PassiveAbilityData)
            {
                passives.Add(inst);
                RecalculateModifiers();
            }
            else
            {
                actives.Add(inst);
            }
            return true;
        }

        /// <summary> 레벨업(maxLevel 클램프). 패시브면 보정 재계산. </summary>
        public void LevelUp(AbilityInstance inst)
        {
            if (inst == null || inst.level >= inst.data.maxLevel) return;
            inst.level++;
            if (inst.data is PassiveAbilityData) RecalculateModifiers();
        }

        /// <summary> 제거. 패시브면 보정 재계산. </summary>
        public void Remove(AbilityInstance inst)
        {
            if (inst == null) return;
            if (passives.Remove(inst)) RecalculateModifiers();
            else actives.Remove(inst);
        }

        private void RecalculateModifiers()
        {
            modifiers.damageBonus = 0f;
            modifiers.cooldownReduction = 0f;
            for (int i = 0; i < passives.Count; i++)
            {
                var p = passives[i].data as PassiveAbilityData;
                if (p != null) p.ApplyModifiers(modifiers, passives[i].level);
            }
        }
    }
}
```

- [ ] **Step 4: 테스트 통과 확인** — `run_tests` EditMode. Expected: `AbilityLoadoutTests` 6개 PASS, 회귀 0.

- [ ] **Step 5: 린트 + 커밋** — `commit` 스킬. 예: `feat: AbilityLoadout(슬롯·추가·레벨업·패시브 보정) + 테스트`

---

## Self-Review (작성자 체크)

- **Spec 커버리지**: AbilityData 계층(§2)=Task1, AbilityInstance(§3.1)=Task1, AbilityLoadout API(§3.2)=Task2, AbilityModifiers(§3.3)=Task1+RecalculateModifiers, AbilityContext(§4)=Task1, 서브클래스 라우팅(enum 제거)=`CanAdd`/`TryAdd`의 `is PassiveAbilityData`. ✅
- **Placeholder 스캔**: 없음 — 전 코드 실값. (`ValueAtLevel` 기본 `return level`은 A2에서 서브클래스 재정의되는 virtual 훅, gap 아님.)
- **타입 일관성**: `AbilityData.maxLevel`, `AbilityInstance.data/level`, `AbilityLoadout.Actives/Passives/Modifiers/TryAdd/CanAdd/LevelUp/Remove/Contains`, `AbilityModifiers.damageBonus`, `PassiveAbilityData.ApplyModifiers(AbilityModifiers,int)`가 테스트·구현에서 동일. ✅
- **범위**: A1 데이터 아키텍처 단일 plan. 콘텐츠·tick·UI는 비범위(A2~). ✅

## 후속 (A2~)
- A2: 구체 `ActiveAbilityData` 서브클래스(디버그 공격 이식) + 코어 tick 루프(cooldown→Execute) + `AbilityLoadout`을 중앙 `TowerActor`에 부착.
- A3: `AbilityData` 풀 → 카드 3장 → `TryAdd`/`LevelUp`.
