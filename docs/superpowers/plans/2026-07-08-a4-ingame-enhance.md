# A4 인게임 강화 Implementation Plan

> **네이밍·구조 최종 반영 (2026-07-09)** — 구현 확정본은 본문과 다름(본문은 계획 시점 이름). 최종: `EnhanceCostCalculator`→`AbilityCostExtensions`(확장메서드), `AbilityEnhancer`→`AbilityUpgradeService`(`TryUpgrade`/`Dismiss`), `EnhanceCostConfig`→`AbilityUpgradeConfig`(`refundRatio`), `ICardCommandTarget`→`IAbilityCommandTarget`, `AbilityUpgradeRow`→`UIWidget<AbilityUpgradeRowData>`(SetData). 코드·TASK-012 참조.

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** 골드로 능력을 강화(레벨업)/삭제(환불)하는 인게임 경제 루프를 추가해 원작 성장 3단 중 ②강화 축을 완성한다.

**Architecture:** 순수 함수 비용 계산기 + POCO 서비스(`AbilityEnhancer`)가 기존 `EconomyModel`·`CoreAbilitySystem`을 조율하고, 기존 UIView/UIPresenter+GameContext DI 규약 위에 최소 UI 1쌍을 얹는다. Mediator 아님(단방향 서비스 계층 + UI Facade).

**Tech Stack:** Unity 6000.2.10f1, C#, NUnit(EditMode), UniTask, ReactiveProperty, ScriptableObject.

## Global Constraints

- 네이밍/스타일: CLAUDE.md 컨벤션 — private `camelCase`(접두어 금지), 모든 메서드 `<summary>` 필수, if/else 개행+Allman, 접근제한자 명시, System 라이브러리 풀패스.
- 비동기: UniTask만. Coroutine·System.Threading.Tasks 금지.
- 예외: `try/catch` 지양(가드 절 우선).
- 폰트: 모든 TMP 텍스트는 neodgm SDF.
- **커밋: 사용자 명시 요청 시에만.** 각 Task는 lint(`lint` 스킬) 통과 후 "커밋 준비" 상태로 두고, 사용자 승인 하에 `commit` 스킬로 커밋한다.
- 테스트 실행: Unity Test Runner(EditMode) 또는 `mcp__UnityMCP__run_tests`(mode: EditMode, 클래스 필터). 스크립트 수정 후 `read_console`로 컴파일 확인.

---

## File Structure

**신규**
- `Assets/Scripts/Systems/Economy/EnhanceCostConfig.cs` — 아레나 전역 비용 곡선 SO(4필드)
- `Assets/Scripts/Systems/Economy/EnhanceCostCalculator.cs` — 비용·환불 순수 함수
- `Assets/Scripts/Systems/Economy/AbilityEnhancer.cs` — 강화/삭제 서비스
- `Assets/Scripts/UI/Views/AbilityEnhanceView.cs` — 슬롯 목록 뷰
- `Assets/Scripts/UI/Views/AbilityEnhanceRow.cs` — 슬롯 1행(리프 요소)
- `Assets/Scripts/UI/Presenters/AbilityEnhancePresenter.cs` — 뷰↔서비스 배선
- `Assets/Tests/EditMode/EnhanceCostCalculatorTests.cs`
- `Assets/Tests/EditMode/AbilityLoadoutEnhanceTests.cs`
- `Assets/Tests/EditMode/AbilityEnhancerTests.cs`
- `Assets/Data/.../EnhanceCostConfig.asset` — 설정 에셋

**수정**
- `Assets/Scripts/Systems/Abilities/AbilityData.cs` — `baseCost` 필드
- `Assets/Scripts/Systems/Abilities/AbilityInstance.cs` — `acquiredRound` 필드
- `Assets/Scripts/Systems/Abilities/AbilityLoadout.cs` — acquiredRound 박제 + `OnChanged`
- `Assets/Scripts/Systems/Abilities/ICardCommandTarget.cs` — `RemoveAbility`
- `Assets/Scripts/Systems/Abilities/CoreAbilitySystem.cs` — `RemoveAbility` 구현
- `Assets/Scripts/Domain/GameContext.cs` — `Enhancer` 프로퍼티
- `Assets/Scripts/Systems/Management/GameManager.cs` — 배선(config·enhancer)
- `Assets/Tests/EditMode/CardChoiceApplierTests.cs` — StubCore에 `RemoveAbility`(인터페이스 확장 대응)
- `Assets/Data/Abilities/*.asset (7)` — baseCost 값

---

## Task 1: 비용 기반 + 계산기 (EditMode)

**Files:**
- Create: `Assets/Scripts/Systems/Economy/EnhanceCostConfig.cs`
- Create: `Assets/Scripts/Systems/Economy/EnhanceCostCalculator.cs`
- Modify: `Assets/Scripts/Systems/Abilities/AbilityData.cs` (maxLevel 아래)
- Modify: `Assets/Scripts/Systems/Abilities/AbilityInstance.cs` (level 아래)
- Test: `Assets/Tests/EditMode/EnhanceCostCalculatorTests.cs`

**Interfaces:**
- Produces: `EnhanceCostConfig`(float levelSlope, roundInflation, maxDiscountRate, deleteRefundRatio), `EnhanceCostCalculator.Cost(AbilityInstance, EnhanceCostConfig) → int`, `.Refund(AbilityInstance, EnhanceCostConfig) → int`, `AbilityData.baseCost`, `AbilityInstance.acquiredRound`.

- [ ] **Step 1: 실패 테스트 작성**

```csharp
using NUnit.Framework;
using UnityEngine;
using DefenseDot.Systems.Abilities;
using DefenseDot.Systems.Economy;

namespace DefenseDot.Tests.EditMode
{
    public class EnhanceCostCalculatorTests
    {
        private sealed class StubData : AbilityData { }

        private static AbilityInstance Ability(int baseCost, int level, int acquiredRound)
        {
            StubData d = ScriptableObject.CreateInstance<StubData>();
            d.baseCost = baseCost;
            d.maxLevel = 99;
            AbilityInstance inst = new AbilityInstance(d, level);
            inst.acquiredRound = acquiredRound;
            return inst;
        }

        private static EnhanceCostConfig Config()
        {
            EnhanceCostConfig c = ScriptableObject.CreateInstance<EnhanceCostConfig>();
            c.levelSlope = 0.10f;
            c.roundInflation = 0.05f;
            c.maxDiscountRate = 0.55f;
            c.deleteRefundRatio = 0.40f;
            return c;
        }

        [Test]
        public void Cost_BaselineLevel1Round1_Is63()
        {
            // lvScale=(1+1)+1*0.1=2.1, roundMul=1, costMul=1 → ceil(30*2.1)=63
            Assert.AreEqual(63, EnhanceCostCalculator.Cost(Ability(30, 1, 1), Config()));
        }

        [Test]
        public void Cost_HigherLevel_CostsMore()
        {
            Assert.Greater(
                EnhanceCostCalculator.Cost(Ability(30, 3, 1), Config()),
                EnhanceCostCalculator.Cost(Ability(30, 1, 1), Config()));
        }

        [Test]
        public void Cost_LaterAcquiredRound_CostsMore()
        {
            Assert.Greater(
                EnhanceCostCalculator.Cost(Ability(30, 1, 11), Config()),
                EnhanceCostCalculator.Cost(Ability(30, 1, 1), Config()));
        }

        [Test]
        public void Refund_Level3_Is65()
        {
            // lv1: ceil(63*0.4)=26, lv2: ceil(96*0.4)=39 → 65
            Assert.AreEqual(65, EnhanceCostCalculator.Refund(Ability(30, 3, 1), Config()));
        }
    }
}
```

- [ ] **Step 2: 실패 확인**

Run: Unity Test Runner(EditMode) 또는 `run_tests(mode:"EditMode", filter:"EnhanceCostCalculatorTests")`
Expected: FAIL — `EnhanceCostConfig`/`EnhanceCostCalculator`/`baseCost`/`acquiredRound` 미정의 컴파일 에러.

- [ ] **Step 3: AbilityData.baseCost 추가**

`AbilityData.cs`의 `public int maxLevel = 5;` 바로 아래:

```csharp
        /// <summary> 강화 기본 비용(능력별). </summary>
        public int baseCost = 30;
```

- [ ] **Step 4: AbilityInstance.acquiredRound 추가**

`AbilityInstance.cs`의 `public int level;` 바로 아래:

```csharp
        /// <summary> 획득 라운드(강화비 스케일 기준). 스타터=1. </summary>
        public int acquiredRound = 1;
```

- [ ] **Step 5: EnhanceCostConfig 작성**

```csharp
using UnityEngine;

namespace DefenseDot.Systems.Economy
{
    /// <summary> 아레나 모드 전역 강화 비용 곡선 파라미터입니다. (능력 무관) </summary>
    [CreateAssetMenu(fileName = "EnhanceCostConfig", menuName = "DefenseDot/Enhance Cost Config")]
    public sealed class EnhanceCostConfig : ScriptableObject
    {
        /// <summary> 레벨당 가격 배율 가산. </summary>
        public float levelSlope = 0.10f;
        /// <summary> 획득 라운드당 가격 배율 가산. </summary>
        public float roundInflation = 0.05f;
        /// <summary> 누적 최대 할인 상한(0.55 = 최대 55% 할인). 할인원 도입 전 비활성. </summary>
        public float maxDiscountRate = 0.55f;
        /// <summary> 삭제 시 강화비 환급률. </summary>
        public float deleteRefundRatio = 0.40f;
    }
}
```

- [ ] **Step 6: EnhanceCostCalculator 작성**

```csharp
using UnityEngine;
using DefenseDot.Systems.Abilities;

namespace DefenseDot.Systems.Economy
{
    /// <summary> 강화·환불 비용을 계산하는 순수 함수 모음입니다. (원작 enhanceCost 이식) </summary>
    public static class EnhanceCostCalculator
    {
        /// <summary> 현재 레벨에서 다음 레벨로의 강화 비용을 계산합니다. </summary>
        public static int Cost(AbilityInstance ability, EnhanceCostConfig config)
        {
            return CostAtLevel(ability.data.baseCost, ability.level, ability.acquiredRound, config);
        }

        /// <summary> 지정 레벨 기준 강화 비용을 계산합니다(환불 합산용). </summary>
        public static int CostAtLevel(int baseCost, int level, int acquiredRound, EnhanceCostConfig config)
        {
            float lvScale = (level + 1) + level * config.levelSlope;
            float roundMul = 1f + (acquiredRound - 1) * config.roundInflation;
            float discountStack = 1f;   // 할인원(A7) 없음
            float costMul = Mathf.Max(1f - config.maxDiscountRate, discountStack);
            return Mathf.CeilToInt(baseCost * lvScale * roundMul * costMul);
        }

        /// <summary> 삭제 시 환급액(레벨1~직전 강화비 합 × 환급률, 레벨별 올림). </summary>
        public static int Refund(AbilityInstance ability, EnhanceCostConfig config)
        {
            int sum = 0;
            for (int lv = 1; lv < ability.level; lv++)
                sum += Mathf.CeilToInt(CostAtLevel(ability.data.baseCost, lv, ability.acquiredRound, config) * config.deleteRefundRatio);
            return sum;
        }
    }
}
```

- [ ] **Step 7: 통과 확인**

Run: `run_tests(mode:"EditMode", filter:"EnhanceCostCalculatorTests")`
Expected: PASS (4/4).

- [ ] **Step 8: lint + 커밋 준비**

`lint` 스킬로 변경 .cs 검증 → 사용자 승인 시 커밋:
```
feat: 강화 비용 SO·계산기 추가 (baseCost/acquiredRound)
```

---

## Task 2: 로드아웃 — acquiredRound 박제 + OnChanged (EditMode)

**Files:**
- Modify: `Assets/Scripts/Systems/Abilities/AbilityLoadout.cs`
- Test: `Assets/Tests/EditMode/AbilityLoadoutEnhanceTests.cs`

**Interfaces:**
- Consumes: `ICombatState.Round`(기존), `AbilityInstance.acquiredRound`(Task 1).
- Produces: `AbilityLoadout.OnChanged`(event Action), `TryAdd`가 `acquiredRound`를 `combatState.Round`로 박제(없으면 1).

- [ ] **Step 1: 실패 테스트 작성**

```csharp
using NUnit.Framework;
using UnityEngine;
using DefenseDot.Core;
using DefenseDot.Systems.Abilities;

namespace DefenseDot.Tests.EditMode
{
    public class AbilityLoadoutEnhanceTests
    {
        private sealed class StubActive : ActiveAbilityData
        {
            public override void Tick(in AbilityContext ctx, AbilityInstance self, float deltaTime) { }
            protected override void Fire(in AbilityContext ctx, AbilityInstance self, ITargetable target) { }
        }
        private sealed class StubCombat : ICombatState
        {
            public int Round { get; set; }
            public int AliveEnemyCount => 0;
        }
        private static StubActive NewActive()
        {
            StubActive a = ScriptableObject.CreateInstance<StubActive>();
            a.maxLevel = 5;
            return a;
        }

        [Test]
        public void TryAdd_StampsAcquiredRoundFromCombatState()
        {
            AbilityLoadout lo = new AbilityLoadout(6, 6);
            lo.Modifiers.combatState = new StubCombat { Round = 7 };
            lo.TryAdd(NewActive());
            Assert.AreEqual(7, lo.Actives[0].acquiredRound);
        }

        [Test]
        public void TryAdd_WithoutCombatState_DefaultsToOne()
        {
            AbilityLoadout lo = new AbilityLoadout(6, 6);
            lo.TryAdd(NewActive());
            Assert.AreEqual(1, lo.Actives[0].acquiredRound);
        }

        [Test]
        public void OnChanged_FiresOnAddLevelUpRemove()
        {
            AbilityLoadout lo = new AbilityLoadout(6, 6);
            int fired = 0;
            lo.OnChanged += () => fired++;
            lo.TryAdd(NewActive());
            lo.LevelUp(lo.Actives[0]);
            lo.Remove(lo.Actives[0]);
            Assert.AreEqual(3, fired);
        }
    }
}
```

- [ ] **Step 2: 실패 확인**

Run: `run_tests(mode:"EditMode", filter:"AbilityLoadoutEnhanceTests")`
Expected: FAIL — `OnChanged` 미정의, acquiredRound 미박제(기본 1이라 첫 테스트 FAIL).

- [ ] **Step 3: OnChanged 이벤트 필드 추가**

`AbilityLoadout.cs`의 `modifiers` 필드 선언 아래(프로퍼티 위):

```csharp
        /// <summary> 로드아웃 구조 변경(추가/레벨업/제거) 후 발화합니다. </summary>
        public event System.Action OnChanged;
```

- [ ] **Step 4: TryAdd — acquiredRound 박제 + 통지**

`TryAdd` 전체를 교체:

```csharp
        /// <summary> 새 능력을 해당 슬롯에 추가합니다. 획득 라운드를 박제하고 통지합니다. 불가 시 false. </summary>
        public bool TryAdd(AbilityData data)
        {
            if (!CanAdd(data)) return false;

            var inst = new AbilityInstance(data, 1);
            inst.acquiredRound = System.Math.Max(1, modifiers.combatState != null ? modifiers.combatState.Round : 1);

            if (data is PassiveAbilityData)
            {
                passives.Add(inst);
                RecalculateModifiers();
            }
            else
            {
                actives.Add(inst);
            }

            OnChanged?.Invoke();
            return true;
        }
```

- [ ] **Step 5: LevelUp — 통지**

`LevelUp` 전체를 교체:

```csharp
        /// <summary> 레벨업(maxLevel 클램프). 패시브면 보정 재계산 후 통지. </summary>
        public void LevelUp(AbilityInstance inst)
        {
            if (inst == null || inst.level >= inst.data.maxLevel) return;
            inst.level++;
            if (inst.data is PassiveAbilityData) RecalculateModifiers();
            OnChanged?.Invoke();
        }
```

- [ ] **Step 6: Remove — 통지**

`Remove` 전체를 교체:

```csharp
        /// <summary> 제거. 패시브면 보정 재계산. 실제 제거 시 통지. </summary>
        public void Remove(AbilityInstance inst)
        {
            if (inst == null) return;

            bool removed;
            if (passives.Remove(inst))
            {
                RecalculateModifiers();
                removed = true;
            }
            else
            {
                removed = actives.Remove(inst);
            }

            if (removed) OnChanged?.Invoke();
        }
```

- [ ] **Step 7: 통과 확인**

Run: `run_tests(mode:"EditMode", filter:"AbilityLoadoutEnhanceTests")` + 회귀로 `AbilityLoadoutTests`
Expected: PASS (신규 3/3 + 기존 회귀 유지).

- [ ] **Step 8: lint + 커밋 준비**

```
feat: 로드아웃 획득 라운드 박제 + 변경 통지(OnChanged)
```

---

## Task 3: AbilityEnhancer 서비스 + RemoveAbility 배선 (EditMode)

**Files:**
- Modify: `Assets/Scripts/Systems/Abilities/ICardCommandTarget.cs`
- Modify: `Assets/Scripts/Systems/Abilities/CoreAbilitySystem.cs`
- Modify: `Assets/Tests/EditMode/CardChoiceApplierTests.cs` (StubCore 컴파일 대응)
- Create: `Assets/Scripts/Systems/Economy/AbilityEnhancer.cs`
- Test: `Assets/Tests/EditMode/AbilityEnhancerTests.cs`

**Interfaces:**
- Consumes: `EnhanceCostCalculator`(Task 1), `EconomyModel.CanAfford/TrySpend/AddGold/Gold`(기존), `ICardCommandTarget.LevelUpAbility`(기존).
- Produces: `ICardCommandTarget.RemoveAbility(AbilityInstance)`, `AbilityEnhancer` (ctor `(ICardCommandTarget, EconomyModel, EnhanceCostConfig)`; `GetEnhanceCost/IsMaxLevel/CanEnhance/GetRefund/TryEnhance/Delete`).

- [ ] **Step 1: 실패 테스트 작성**

```csharp
using NUnit.Framework;
using UnityEngine;
using DefenseDot.Domain.Models;
using DefenseDot.Systems.Abilities;
using DefenseDot.Systems.Economy;

namespace DefenseDot.Tests.EditMode
{
    public class AbilityEnhancerTests
    {
        private sealed class StubData : AbilityData { }
        private sealed class StubCore : ICardCommandTarget
        {
            public AbilityLoadout Loadout { get; } = new AbilityLoadout(6, 6);
            public int leveled;
            public int removed;
            public AbilityInstance AddAbility(AbilityData d)
            {
                if (!Loadout.TryAdd(d)) return null;
                return Loadout.Actives.Count > 0 ? Loadout.Actives[Loadout.Actives.Count - 1] : null;
            }
            public void LevelUpAbility(AbilityInstance i) { leveled++; Loadout.LevelUp(i); }
            public void RemoveAbility(AbilityInstance i) { removed++; Loadout.Remove(i); }
        }

        private static AbilityInstance Ability(int baseCost, int level, int maxLevel = 5)
        {
            StubData d = ScriptableObject.CreateInstance<StubData>();
            d.baseCost = baseCost;
            d.maxLevel = maxLevel;
            AbilityInstance inst = new AbilityInstance(d, level);
            inst.acquiredRound = 1;
            return inst;
        }
        private static EnhanceCostConfig Config()
        {
            EnhanceCostConfig c = ScriptableObject.CreateInstance<EnhanceCostConfig>();
            c.levelSlope = 0.10f;
            c.roundInflation = 0.05f;
            c.maxDiscountRate = 0.55f;
            c.deleteRefundRatio = 0.40f;
            return c;
        }

        [Test]
        public void TryEnhance_WithEnoughGold_SpendsAndLevelsUp()
        {
            EconomyModel economy = new EconomyModel();
            economy.Initialize(1000);
            StubCore core = new StubCore();
            AbilityInstance a = Ability(30, 1);
            AbilityEnhancer enhancer = new AbilityEnhancer(core, economy, Config());
            int cost = enhancer.GetEnhanceCost(a);

            bool ok = enhancer.TryEnhance(a);

            Assert.IsTrue(ok);
            Assert.AreEqual(1, core.leveled);
            Assert.AreEqual(1000 - cost, economy.Gold.Value);
        }

        [Test]
        public void TryEnhance_WithoutEnoughGold_NoChange()
        {
            EconomyModel economy = new EconomyModel();
            economy.Initialize(10);
            StubCore core = new StubCore();
            AbilityEnhancer enhancer = new AbilityEnhancer(core, economy, Config());

            bool ok = enhancer.TryEnhance(Ability(30, 1));

            Assert.IsFalse(ok);
            Assert.AreEqual(0, core.leveled);
            Assert.AreEqual(10, economy.Gold.Value);
        }

        [Test]
        public void TryEnhance_AtMax_BlocksWithoutSpending()
        {
            EconomyModel economy = new EconomyModel();
            economy.Initialize(1000);
            StubCore core = new StubCore();
            AbilityEnhancer enhancer = new AbilityEnhancer(core, economy, Config());

            bool ok = enhancer.TryEnhance(Ability(30, level: 5, maxLevel: 5));

            Assert.IsFalse(ok);
            Assert.AreEqual(0, core.leveled);
            Assert.AreEqual(1000, economy.Gold.Value, "MAX면 헛돈 차감 없음");
        }

        [Test]
        public void Delete_RefundsAndRemoves()
        {
            EconomyModel economy = new EconomyModel();
            economy.Initialize(0);
            StubCore core = new StubCore();
            AbilityInstance a = Ability(30, level: 3);
            AbilityEnhancer enhancer = new AbilityEnhancer(core, economy, Config());
            int refund = enhancer.GetRefund(a);

            enhancer.Delete(a);

            Assert.AreEqual(1, core.removed);
            Assert.AreEqual(refund, economy.Gold.Value);
        }
    }
}
```

- [ ] **Step 2: 실패 확인**

Run: `run_tests(mode:"EditMode", filter:"AbilityEnhancerTests")`
Expected: FAIL — `AbilityEnhancer`/`RemoveAbility` 미정의 컴파일 에러.

- [ ] **Step 3: ICardCommandTarget에 RemoveAbility 추가**

`ICardCommandTarget.cs`의 `LevelUpAbility` 아래:

```csharp
        /// <summary> 능력 삭제(액티브는 언장착 동반). </summary>
        void RemoveAbility(AbilityInstance instance);
```

- [ ] **Step 4: CoreAbilitySystem에 RemoveAbility 구현**

`CoreAbilitySystem.cs`의 `LevelUpAbility` 아래(ICardCommandTarget region 안):

```csharp
        /// <summary> 능력 삭제. 액티브면 러너에서 언장착 후 로드아웃에서 제거합니다. </summary>
        public void RemoveAbility(AbilityInstance instance)
        {
            if (instance?.data is ActiveAbilityData) runner?.Unequip(instance);
            loadout?.Remove(instance);
        }
```

- [ ] **Step 5: 기존 StubCore 컴파일 대응**

`CardChoiceApplierTests.cs`의 `StubCore`에 `LevelUpAbility` 아래:

```csharp
            public void RemoveAbility(AbilityInstance i) => Loadout.Remove(i);
```

- [ ] **Step 6: AbilityEnhancer 작성**

```csharp
using DefenseDot.Domain.Models;
using DefenseDot.Systems.Abilities;

namespace DefenseDot.Systems.Economy
{
    /// <summary> 골드로 능력을 강화/삭제하는 유스케이스 서비스입니다. (경제·능력·비용 조율 + UI Facade) </summary>
    public sealed class AbilityEnhancer
    {
        private readonly ICardCommandTarget core;      // 능력 레벨업/삭제 명령 대상
        private readonly EconomyModel economy;         // 골드 차감/가산
        private readonly EnhanceCostConfig config;     // 비용 곡선 파라미터

        public AbilityEnhancer(ICardCommandTarget core, EconomyModel economy, EnhanceCostConfig config)
        {
            this.core = core;
            this.economy = economy;
            this.config = config;
        }

        /// <summary> 다음 레벨 강화 비용입니다. </summary>
        public int GetEnhanceCost(AbilityInstance ability) => EnhanceCostCalculator.Cost(ability, config);

        /// <summary> 최대 레벨 도달 여부입니다. </summary>
        public bool IsMaxLevel(AbilityInstance ability) => ability.level >= ability.data.maxLevel;

        /// <summary> 강화 가능 여부(비최대 + 골드 충분)입니다. </summary>
        public bool CanEnhance(AbilityInstance ability)
        {
            return !IsMaxLevel(ability) && economy.CanAfford(GetEnhanceCost(ability));
        }

        /// <summary> 삭제 시 환급액입니다. </summary>
        public int GetRefund(AbilityInstance ability) => EnhanceCostCalculator.Refund(ability, config);

        /// <summary> 강화를 시도합니다. MAX·골드부족이면 아무 변화 없이 false. </summary>
        public bool TryEnhance(AbilityInstance ability)
        {
            if (IsMaxLevel(ability)) return false;
            if (!economy.TrySpend(GetEnhanceCost(ability))) return false;
            core.LevelUpAbility(ability);
            return true;
        }

        /// <summary> 능력을 삭제하고 강화비 일부를 환급합니다. </summary>
        public void Delete(AbilityInstance ability)
        {
            economy.AddGold(GetRefund(ability));
            core.RemoveAbility(ability);
        }
    }
}
```

- [ ] **Step 7: 통과 확인**

Run: `run_tests(mode:"EditMode", filter:"AbilityEnhancerTests")` + 회귀 `CardChoiceApplierTests`
Expected: PASS (신규 4/4 + 기존 회귀 유지).

- [ ] **Step 8: lint + 커밋 준비**

```
feat: AbilityEnhancer 서비스 + 능력 삭제(RemoveAbility) 배선
```

---

## Task 4: 최소 UI + 합성 루트 배선 + 에셋 (PlayMode/수동)

> EditMode 자동 테스트 대상 아님. 코드 작성 후 PlayMode 수동 검증.

**Files:**
- Create: `Assets/Scripts/UI/Views/AbilityEnhanceRow.cs`
- Create: `Assets/Scripts/UI/Views/AbilityEnhanceView.cs`
- Create: `Assets/Scripts/UI/Presenters/AbilityEnhancePresenter.cs`
- Modify: `Assets/Scripts/Domain/GameContext.cs`
- Modify: `Assets/Scripts/Systems/Management/GameManager.cs`
- Create(에셋): `Assets/Data/.../EnhanceCostConfig.asset`
- Modify(에셋): `Assets/Data/Abilities/*.asset (7)` — baseCost

**Interfaces:**
- Consumes: `AbilityEnhancer`(Task 3), `AbilityLoadout.OnChanged`(Task 2), `EconomyModel.Gold`(기존), `GameContext`, `UIPresenter<TView>`(기존), `UIView`(기존).
- Produces: `GameContext.Enhancer`, `AbilityEnhanceView`, `AbilityEnhancePresenter`.

- [ ] **Step 1: AbilityEnhanceRow 작성 (슬롯 1행, 리프 요소)**

```csharp
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using DefenseDot.Systems.Abilities;

namespace DefenseDot.UI.Views
{
    /// <summary> 능력 강화 패널의 한 행입니다. 아이콘·이름·레벨과 강화/삭제 버튼을 표시하고 클릭을 통지합니다. </summary>
    public sealed class AbilityEnhanceRow : MonoBehaviour
    {
        [SerializeField] private Image icon;
        [SerializeField] private TextMeshProUGUI nameText;
        [SerializeField] private TextMeshProUGUI levelText;
        [SerializeField] private Button enhanceButton;
        [SerializeField] private TextMeshProUGUI enhanceLabel;
        [SerializeField] private Button deleteButton;

        /// <summary> 강화 버튼 클릭 시 대상 능력을 통지합니다. </summary>
        public event System.Action<AbilityInstance> OnEnhance;
        /// <summary> 삭제 버튼 클릭 시 대상 능력을 통지합니다. </summary>
        public event System.Action<AbilityInstance> OnDelete;

        private AbilityInstance bound;

        private void Awake()
        {
            if (enhanceButton != null) enhanceButton.onClick.AddListener(() => OnEnhance?.Invoke(bound));
            if (deleteButton != null) deleteButton.onClick.AddListener(() => OnDelete?.Invoke(bound));
        }

        /// <summary> 능력 정보와 강화 상태(비용/MAX/구매가능)를 반영합니다. </summary>
        public void Bind(AbilityInstance ability, bool isMax, int cost, bool canAfford)
        {
            bound = ability;
            if (icon != null) icon.sprite = ability.data.icon;
            if (nameText != null) nameText.text = ability.data.displayName;
            if (levelText != null) levelText.text = $"Lv{ability.level}";

            if (isMax)
            {
                if (enhanceLabel != null) enhanceLabel.text = "MAX";
                if (enhanceButton != null) enhanceButton.interactable = false;
            }
            else
            {
                if (enhanceLabel != null) enhanceLabel.text = $"강화 Lv{ability.level + 1} ({cost}G)";
                if (enhanceButton != null) enhanceButton.interactable = canAfford;
            }
        }
    }
}
```

- [ ] **Step 2: AbilityEnhanceView 작성**

```csharp
using System.Collections.Generic;
using UnityEngine;
using DefenseDot.UI.Base;
using DefenseDot.Systems.Abilities;

namespace DefenseDot.UI.Views
{
    /// <summary> 보유 능력 목록과 강화/삭제 버튼을 표시하는 최소 패널입니다. Presenter를 모릅니다. </summary>
    public sealed class AbilityEnhanceView : UIView
    {
        [SerializeField] private RectTransform rowContainer;
        [SerializeField] private AbilityEnhanceRow rowPrefab;

        private readonly List<AbilityEnhanceRow> rows = new List<AbilityEnhanceRow>();

        /// <summary> 강화 요청을 중계합니다. </summary>
        public event System.Action<AbilityInstance> OnEnhance;
        /// <summary> 삭제 요청을 중계합니다. </summary>
        public event System.Action<AbilityInstance> OnDelete;

        /// <summary> 능력 행들을 (재)생성하고 강화 상태를 반영합니다. </summary>
        public void Render(IReadOnlyList<AbilityInstance> abilities, System.Func<AbilityInstance, (bool isMax, int cost, bool canAfford)> query)
        {
            EnsureRowCount(abilities.Count);
            for (int i = 0; i < rows.Count; i++)
            {
                bool used = i < abilities.Count;
                rows[i].gameObject.SetActive(used);
                if (!used) continue;
                (bool isMax, int cost, bool canAfford) s = query(abilities[i]);
                rows[i].Bind(abilities[i], s.isMax, s.cost, s.canAfford);
            }
        }

        /// <summary> 필요한 만큼 행을 확보합니다(부족분만 생성). </summary>
        private void EnsureRowCount(int count)
        {
            while (rows.Count < count)
            {
                AbilityEnhanceRow row = Instantiate(rowPrefab, rowContainer);
                row.OnEnhance += a => OnEnhance?.Invoke(a);
                row.OnDelete += a => OnDelete?.Invoke(a);
                rows.Add(row);
            }
        }
    }
}
```

- [ ] **Step 3: AbilityEnhancePresenter 작성**

```csharp
using System.Collections.Generic;
using DefenseDot.Domain;
using DefenseDot.Domain.Models;
using DefenseDot.Systems.Abilities;
using DefenseDot.Systems.Economy;
using DefenseDot.UI.Base;
using DefenseDot.UI.Views;

namespace DefenseDot.UI.Presenters
{
    /// <summary> 능력 강화 패널 프레젠터입니다. 로드아웃·골드 변화를 구독해 행을 갱신하고, 강화/삭제를 서비스에 위임합니다. </summary>
    public sealed class AbilityEnhancePresenter : UIPresenter<AbilityEnhanceView>
    {
        private readonly EconomyModel economy;
        private readonly ICardCommandTarget core;
        private readonly AbilityEnhancer enhancer;
        private readonly List<AbilityInstance> buffer = new List<AbilityInstance>();

        public AbilityEnhancePresenter(AbilityEnhanceView view, GameContext ctx) : base(view)
        {
            economy = ctx.Economy;
            core = ctx.CoreTarget;
            enhancer = ctx.Enhancer;
        }

        protected override void OnInitialize()
        {
            view.OnEnhance += HandleEnhance;
            view.OnDelete += HandleDelete;
            core.Loadout.OnChanged += Refresh;
            economy.Gold.Subscribe(_ => Refresh());   // 즉시 1회 + 이후 변화
        }

        protected override void OnDispose()
        {
            view.OnEnhance -= HandleEnhance;
            view.OnDelete -= HandleDelete;
            core.Loadout.OnChanged -= Refresh;
        }

        private void HandleEnhance(AbilityInstance ability)
        {
            enhancer.TryEnhance(ability);   // 성공 시 Loadout.OnChanged로 자동 Refresh
        }

        private void HandleDelete(AbilityInstance ability)
        {
            enhancer.Delete(ability);       // 삭제 시 Loadout.OnChanged로 자동 Refresh
        }

        /// <summary> 현재 로드아웃(액티브+패시브)을 뷰에 반영합니다. </summary>
        private void Refresh()
        {
            buffer.Clear();
            buffer.AddRange(core.Loadout.Actives);
            buffer.AddRange(core.Loadout.Passives);
            view.Render(buffer, Query);
        }

        /// <summary> 한 능력의 강화 상태(MAX/비용/구매가능)를 질의합니다. </summary>
        private (bool isMax, int cost, bool canAfford) Query(AbilityInstance ability)
        {
            bool isMax = enhancer.IsMaxLevel(ability);
            int cost = isMax ? 0 : enhancer.GetEnhanceCost(ability);
            bool canAfford = !isMax && economy.CanAfford(cost);
            return (isMax, cost, canAfford);
        }
    }
}
```

> 참고: `economy.Gold.Subscribe`는 프로젝트 `ReactiveProperty` 규약(구독 즉시 현재값 1회 통지)을 따른다. 구독 API 명이 다르면(`Subscribe`/`OnGoldChanged`) 기존 `HUDPresenter`/`ArenaHudPresenter` 패턴에 맞춘다.

- [ ] **Step 4: GameContext에 Enhancer 추가**

`GameContext.cs` — 프로퍼티 추가(`CoreTarget` 아래):

```csharp
        /// <summary> 능력 강화/삭제 서비스입니다. </summary>
        public DefenseDot.Systems.Economy.AbilityEnhancer Enhancer { get; }
```

생성자 시그니처 끝에 파라미터 추가 + 대입:

```csharp
        public GameContext(EconomyModel economy, CoreModel core, WaveModel wave, ScoreModel score,
            RoundTimerModel timer, GameFlowModel flow, LevelModel level, int enemyCapacity,
            TowerRoster roster, TowerPlacementController placement, ArenaCardConfig cardConfig,
            AbilityPool abilityPool, ICardCommandTarget coreTarget, PoolManager pooling,
            DefenseDot.Systems.Economy.AbilityEnhancer enhancer)
        {
            Economy = economy; Core = core; Wave = wave; Score = score; Timer = timer;
            Flow = flow; Level = level; EnemyCapacity = enemyCapacity; Roster = roster;
            Placement = placement; CardConfig = cardConfig; AbilityPool = abilityPool;
            CoreTarget = coreTarget; Pooling = pooling; Enhancer = enhancer;
        }
```

- [ ] **Step 5: GameManager 배선**

`GameManager.cs` — 직렬화 필드 추가(다른 [SerializeField] 근처):

```csharp
        [SerializeField] private DefenseDot.Systems.Economy.EnhanceCostConfig enhanceCostConfig;
```

`GameContext` 생성(현재 L125 부근, `coreTarget` 확보 지점) 직전에 enhancer 생성 후 인자 추가:

```csharp
            var enhancer = new DefenseDot.Systems.Economy.AbilityEnhancer(coreTarget, Economy, enhanceCostConfig);
            var ctx = new GameContext(
                Economy, Core, Wave, Score, RoundTimer, Flow, Level,
                enemyCapacity, roster, placement, cardConfig, abilityPool, coreTarget, Pooling,
                enhancer);
            uiRoot.Inject(ctx);
```

> `coreTarget`/`abilityPool`/`cardConfig`/`enemyCapacity` 등 기존 지역 변수명은 GameManager L120~128 실제 코드에 맞춰 사용(현재 `Economy, Core, ...`가 이미 인자로 넘어감 — enhancer만 말미에 추가).

- [ ] **Step 6: 컴파일 확인**

Run: `read_console`(Error 필터)
Expected: 컴파일 에러 없음. (에러 시 지역 변수명 정합 수정)

- [ ] **Step 7: 에셋 생성·설정 (에디터)**

1. `Assets/Data/`에 우클릭 → Create → DefenseDot → Enhance Cost Config → `EnhanceCostConfig.asset` 생성(기본값 유지: 0.10/0.05/0.55/0.40).
2. GameManager 컴포넌트의 `enhanceCostConfig` 필드에 위 에셋 할당.
3. 능력 에셋 baseCost 설정: Ability_Shot=30, Ability_Orbital=60, Ability_AreaWave=55, Passive_Onslaught/Press/Cull/Awaken=60.

- [ ] **Step 8: 씬 UI 배선 (에디터)**

1. `AbilityEnhanceRow` 프리팹 제작 — 아이콘(Image)·이름/레벨(TMP neodgm)·강화 버튼(+라벨 TMP)·삭제 버튼. 6개 참조 할당.
2. `AbilityEnhanceView` 패널 오브젝트 생성 — `rowContainer`(세로 Layout Group)·`rowPrefab` 할당. `UIView.initType` 적절히(상시 표시면 ActiveOnStart).
3. `UIRoot`의 `views` 리스트에 `AbilityEnhanceView` 추가(팩토리가 Presenter 자동 배선).
4. 폰트: 모든 TMP는 neodgm SDF.

- [ ] **Step 9: PlayMode 수동 검증**

| 확인 | 기대 |
|---|---|
| 카드로 능력 획득 후 패널에 행 등장 | Loadout.OnChanged로 즉시 반영 |
| 골드 충분 시 강화 버튼 활성·클릭 | 골드 차감 + Lv 증가 + 라벨 갱신 |
| 골드 부족 | 버튼 비활성(회색) |
| 삭제 클릭 | 행 제거 + 환불 골드 가산 |
| MAX 도달 | 버튼 "MAX"·비활성 |
| 늦게 얻은 능력 | 강화비 더 비쌈 |

- [ ] **Step 10: lint + 커밋 준비**

```
feat: 능력 강화/삭제 최소 UI + 합성 루트 배선
```

---

## Self-Review

- **Spec coverage**: §3 SO/데이터→Task1, §4 공식→Task1, §5 서비스→Task3, §6 RemoveAbility→Task3, §7 acquiredRound·OnChanged→Task2, §8 UI→Task4, §9 baseCost→Task4 Step7, §10 테스트 8종→Task1(4)+Task2(3)+Task3(4)로 커버(스펙 #7 액티브 언장착은 Task3 StubCore/실제 impl + Task4 수동으로 커버). 누락 없음.
- **Placeholder scan**: 코드 스텝은 전부 실제 코드. 에디터 스텝(Step 7~8)은 프리팹/에셋 수작업이라 코드 아님(정상).
- **Type consistency**: `AbilityEnhancer` ctor `(ICardCommandTarget, EconomyModel, EnhanceCostConfig)` — Task3 정의 = Task4(GameManager) 호출 일치. `RemoveAbility`·`OnChanged`·`baseCost`·`acquiredRound` 명칭 전 태스크 일관.
