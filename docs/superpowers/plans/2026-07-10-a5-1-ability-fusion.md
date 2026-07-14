# A5-1 능력 합성(Fusion) Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** MAX된 능력 2개를 소진해 상위 능력 1개로 합치는 인런 합성(Fusion)을 추가하고, 합성 루트를 타워(캐릭터)별 계보 데이터로 둔다.

**Architecture:** 데이터(`TowerData.fusionLineage : FusionRecipeSet`)와 로직(`FusionResolver` POCO + 카드 시스템 확장)을 분리. 합성 판정은 로드아웃+계보를 읽는 `FusionResolver`가, 카드 제시/적용은 기존 카드 시스템(Generator/Applier)이 담당하며 재료 소진은 A4 `IAbilityCommandTarget.RemoveAbility`를 재사용한다.

**Tech Stack:** Unity 6000.2.10f1, C#, NUnit(EditMode), UniTask, ScriptableObject.

## Global Constraints

- 용어: 코드 어휘 = **Fusion**, 플레이어 노출 텍스트 = **"합성"**.
- 네이밍/스타일: CLAUDE.md — private `camelCase`, 모든 메서드 `<summary>`, if/else 개행+Allman, 접근제한자 명시, `System.*` 풀패스.
- 비동기 UniTask만. 예외 `try/catch` 지양.
- **커밋: 사용자 명시 요청 시에만.** 각 Task는 `lint` 통과 후 커밋 준비 상태로 두고, 승인 하에 `commit` 스킬로 커밋.
- 테스트: Unity Test Runner(EditMode) 또는 `run_tests(mode:"EditMode")`. 신규 `.cs` 생성 후 `refresh_unity(scope:"all", mode:"force")` → `idle` 대기 → `read_console` 에러 확인.

---

## File Structure

**신규**
- `Assets/Scripts/Systems/Cards/FusionRecipeSet.cs` — `FusionRecipeSet` SO + `FusionRecipe` struct (데이터)
- `Assets/Scripts/Systems/Cards/FusionResolver.cs` — 가용 합성 판정 POCO + `AvailableFusion` struct
- `Assets/Scripts/Systems/Cards/Editor/FusionRecipeSetDrawer.cs` — 커스텀 드로어(에디터)
- `Assets/Tests/EditMode/FusionResolverTests.cs`
- `Assets/Tests/EditMode/FusionCardTests.cs` (Applier + Generator 합성)
- `Assets/Data/.../*_FusionResult.asset` (결과 능력 2), `Aris_FusionLineage.asset`

**수정**
- `Assets/Scripts/Systems/Cards/CardEnums.cs` — `CardAction.Fuse`, `CardTier.Combo`→`Fusion`
- `Assets/Scripts/Systems/Cards/CardChoice.cs` — 합성 필드 + `FusionCard` 팩토리
- `Assets/Scripts/Systems/Cards/CardChoiceApplier.cs` — `Fuse` 분기
- `Assets/Scripts/Systems/Cards/CardChoiceGenerator.cs` — 합성 카드 생성(lineage 옵션 인자)
- `Assets/Scripts/Data/TowerData.cs` — `fusionLineage` + `starterAbilities`
- `Assets/Scripts/Systems/Mode/ArenaModeBootstrap.cs` — 타워에서 스타터·계보 주입
- `Assets/Scripts/Domain/GameContext.cs` — `FusionLineage`
- `Assets/Scripts/Systems/Management/GameManager.cs` — 계보 전달
- `Assets/Scripts/UI/Presenters/CardSelectionPresenter.cs` — 생성기에 계보 전달

---

## Task 1: FusionRecipeSet(데이터) + FusionResolver(로직) — EditMode

**Files:**
- Create: `Assets/Scripts/Systems/Cards/FusionRecipeSet.cs`
- Create: `Assets/Scripts/Systems/Cards/FusionResolver.cs`
- Test: `Assets/Tests/EditMode/FusionResolverTests.cs`

**Interfaces:**
- Produces: `FusionRecipe`(struct: `AbilityData materialA, materialB, result`), `FusionRecipeSet`(SO: `List<FusionRecipe> recipes`), `AvailableFusion`(struct: `FusionRecipe recipe; AbilityInstance materialA, materialB`), `FusionResolver.Available(AbilityLoadout, FusionRecipeSet) → List<AvailableFusion>`.

- [ ] **Step 1: 실패 테스트 작성**

```csharp
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using DefenseDot.Systems.Abilities;
using DefenseDot.Systems.Cards;

namespace DefenseDot.Tests.EditMode
{
    public class FusionResolverTests
    {
        private sealed class StubActive : ActiveAbilityData
        {
            public override void Tick(in AbilityContext ctx, AbilityInstance self, float deltaTime) { }
            protected override void Fire(in AbilityContext ctx, AbilityInstance self, DefenseDot.Core.ITargetable target) { }
        }
        private static StubActive Ability(int maxLevel = 3)
        {
            StubActive a = ScriptableObject.CreateInstance<StubActive>();
            a.maxLevel = maxLevel;
            return a;
        }
        private static FusionRecipeSet Lineage(AbilityData a, AbilityData b, AbilityData r)
        {
            FusionRecipeSet s = ScriptableObject.CreateInstance<FusionRecipeSet>();
            s.recipes = new List<FusionRecipe> { new FusionRecipe { materialA = a, materialB = b, result = r } };
            return s;
        }
        private static void AddMaxed(AbilityLoadout lo, AbilityData d)
        {
            lo.TryAdd(d);
            AbilityInstance inst = lo.Actives[lo.Actives.Count - 1];
            while (inst.level < d.maxLevel) lo.LevelUp(inst);
        }

        [Test]
        public void Available_BothMaxedResultUnowned_Included()
        {
            StubActive a = Ability(), b = Ability(), r = Ability();
            AbilityLoadout lo = new AbilityLoadout(6, 6);
            AddMaxed(lo, a); AddMaxed(lo, b);
            var res = new FusionResolver().Available(lo, Lineage(a, b, r));
            Assert.AreEqual(1, res.Count);
            Assert.AreEqual(r, res[0].recipe.result);
        }

        [Test]
        public void Available_MaterialNotMaxed_Excluded()
        {
            StubActive a = Ability(), b = Ability(), r = Ability();
            AbilityLoadout lo = new AbilityLoadout(6, 6);
            AddMaxed(lo, a); lo.TryAdd(b);   // b는 Lv1(비MAX)
            Assert.AreEqual(0, new FusionResolver().Available(lo, Lineage(a, b, r)).Count);
        }

        [Test]
        public void Available_ResultAlreadyOwned_Excluded()
        {
            StubActive a = Ability(), b = Ability(), r = Ability();
            AbilityLoadout lo = new AbilityLoadout(6, 6);
            AddMaxed(lo, a); AddMaxed(lo, b); lo.TryAdd(r);
            Assert.AreEqual(0, new FusionResolver().Available(lo, Lineage(a, b, r)).Count);
        }

        [Test]
        public void Available_MaterialMissing_Excluded()
        {
            StubActive a = Ability(), b = Ability(), r = Ability();
            AbilityLoadout lo = new AbilityLoadout(6, 6);
            AddMaxed(lo, a);   // b 미보유
            Assert.AreEqual(0, new FusionResolver().Available(lo, Lineage(a, b, r)).Count);
        }
    }
}
```

- [ ] **Step 2: 실패 확인** — `run_tests(mode:"EditMode", assembly_names:["DefenseDot.Tests.EditMode"])` → FAIL(`FusionRecipeSet`/`FusionResolver` 미정의).

- [ ] **Step 3: FusionRecipeSet 작성**

```csharp
using System.Collections.Generic;
using UnityEngine;
using DefenseDot.Systems.Abilities;

namespace DefenseDot.Systems.Cards
{
    /// <summary> 한 타워(캐릭터)의 합성 계보 — 재료 2개→결과 레시피 목록입니다. (데이터만) </summary>
    [CreateAssetMenu(fileName = "FusionRecipeSet", menuName = "DefenseDot/Fusion Recipe Set")]
    public sealed class FusionRecipeSet : ScriptableObject
    {
        /// <summary> 합성 레시피 목록. </summary>
        public List<FusionRecipe> recipes = new List<FusionRecipe>();

        /// <summary> 디자이너 실수(null·자기합성·결과=재료·중복)를 콘솔 경고로 알립니다. </summary>
        private void OnValidate()
        {
            if (recipes == null) return;
            for (int i = 0; i < recipes.Count; i++)
            {
                FusionRecipe r = recipes[i];
                if (r.materialA == null || r.materialB == null || r.result == null)
                    Debug.LogWarning($"[FusionRecipeSet] {name} 레시피 {i}: 참조 누락", this);
                else if (r.materialA == r.materialB)
                    Debug.LogWarning($"[FusionRecipeSet] {name} 레시피 {i}: 재료 A==B", this);
                else if (r.result == r.materialA || r.result == r.materialB)
                    Debug.LogWarning($"[FusionRecipeSet] {name} 레시피 {i}: 결과가 재료와 같음", this);
            }
        }
    }

    /// <summary> 합성 레시피 1건 — 재료 2개 소진 → 결과 1개. </summary>
    [System.Serializable]
    public struct FusionRecipe
    {
        /// <summary> 재료 A. </summary>
        public AbilityData materialA;
        /// <summary> 재료 B. </summary>
        public AbilityData materialB;
        /// <summary> 결과 능력(일반 카드 풀 제외). </summary>
        public AbilityData result;
    }
}
```

- [ ] **Step 4: FusionResolver 작성**

```csharp
using System.Collections.Generic;
using DefenseDot.Systems.Abilities;

namespace DefenseDot.Systems.Cards
{
    /// <summary> 가용 합성(재료 2개 보유+둘 다 MAX+결과 미보유)을 판정하는 순수 로직입니다. </summary>
    public sealed class FusionResolver
    {
        /// <summary> 계보에서 지금 만들 수 있는 합성 목록을 반환합니다. </summary>
        public List<AvailableFusion> Available(AbilityLoadout loadout, FusionRecipeSet lineage)
        {
            var result = new List<AvailableFusion>();
            if (loadout == null || lineage == null || lineage.recipes == null) return result;

            for (int i = 0; i < lineage.recipes.Count; i++)
            {
                FusionRecipe r = lineage.recipes[i];
                if (r.materialA == null || r.materialB == null || r.result == null) continue;
                if (loadout.Contains(r.result)) continue;

                AbilityInstance a = FindMaxed(loadout, r.materialA);
                AbilityInstance b = FindMaxed(loadout, r.materialB);
                if (a == null || b == null) continue;

                result.Add(new AvailableFusion(r, a, b));
            }
            return result;
        }

        /// <summary> 로드아웃에서 해당 능력의 MAX 인스턴스를 찾습니다(없거나 비MAX면 null). </summary>
        private static AbilityInstance FindMaxed(AbilityLoadout loadout, AbilityData data)
        {
            AbilityInstance inst = Find(loadout.Actives, data);
            if (inst == null) inst = Find(loadout.Passives, data);
            if (inst == null || inst.level < inst.data.maxLevel) return null;
            return inst;
        }

        /// <summary> 목록에서 설계도 일치 인스턴스를 찾습니다. </summary>
        private static AbilityInstance Find(IReadOnlyList<AbilityInstance> list, AbilityData data)
        {
            for (int i = 0; i < list.Count; i++)
                if (list[i].data == data) return list[i];
            return null;
        }
    }

    /// <summary> 지금 만들 수 있는 합성 1건(레시피 + 소진할 재료 인스턴스 2개). </summary>
    public readonly struct AvailableFusion
    {
        /// <summary> 대상 레시피. </summary>
        public readonly FusionRecipe recipe;
        /// <summary> 소진할 재료 A 인스턴스. </summary>
        public readonly AbilityInstance materialA;
        /// <summary> 소진할 재료 B 인스턴스. </summary>
        public readonly AbilityInstance materialB;

        /// <summary> 레시피와 재료 인스턴스로 구성합니다. </summary>
        public AvailableFusion(FusionRecipe recipe, AbilityInstance materialA, AbilityInstance materialB)
        {
            this.recipe = recipe;
            this.materialA = materialA;
            this.materialB = materialB;
        }
    }
}
```

- [ ] **Step 5: 통과 확인** — `run_tests` → `FusionResolverTests` 4/4 PASS.
- [ ] **Step 6: lint + 커밋 준비** — `feat: 합성 레시피 데이터와 가용 판정(FusionResolver) 추가`

---

## Task 2: 카드 시스템 확장(Fuse) — EditMode

**Files:**
- Modify: `Assets/Scripts/Systems/Cards/CardEnums.cs`
- Modify: `Assets/Scripts/Systems/Cards/CardChoice.cs`
- Modify: `Assets/Scripts/Systems/Cards/CardChoiceApplier.cs`
- Test: `Assets/Tests/EditMode/FusionCardTests.cs`

**Interfaces:**
- Consumes: `AvailableFusion`(Task 1), `IAbilityCommandTarget`(A4).
- Produces: `CardAction.Fuse`, `CardTier.Fusion`, `CardChoice.materialA/materialB`, `CardChoice.FusionCard(AbilityData result, AbilityInstance a, AbilityInstance b, CardTier tier)`, `CardChoiceApplier` Fuse 처리.

- [ ] **Step 1: 실패 테스트 작성**

```csharp
using NUnit.Framework;
using UnityEngine;
using DefenseDot.Core.Pooling;
using DefenseDot.Systems.Abilities;
using DefenseDot.Systems.Cards;

namespace DefenseDot.Tests.EditMode
{
    public class FusionCardTests
    {
        private sealed class StubActive : ActiveAbilityData
        {
            public override void Tick(in AbilityContext ctx, AbilityInstance self, float deltaTime) { }
            protected override void Fire(in AbilityContext ctx, AbilityInstance self, DefenseDot.Core.ITargetable target) { }
        }
        private sealed class StubCore : IAbilityCommandTarget
        {
            public AbilityLoadout Loadout { get; } = new AbilityLoadout(6, 6);
            public int added, removed;
            public AbilityInstance AddAbility(AbilityData d)
            {
                added++;
                if (!Loadout.TryAdd(d)) return null;
                return Loadout.Actives[Loadout.Actives.Count - 1];
            }
            public void LevelUpAbility(AbilityInstance i) => Loadout.LevelUp(i);
            public void RemoveAbility(AbilityInstance i) { removed++; Loadout.Remove(i); }
        }
        private static StubActive Ability()
        {
            StubActive a = ScriptableObject.CreateInstance<StubActive>();
            a.maxLevel = 3;
            return a;
        }

        [Test]
        public void Apply_FusionCard_ConsumesTwoAndAddsResult()
        {
            StubCore core = new StubCore();
            StubActive a = Ability(), b = Ability(), r = Ability();
            AbilityInstance ia = core.AddAbility(a);
            AbilityInstance ib = core.AddAbility(b);
            CardChoice card = CardChoice.FusionCard(r, ia, ib, CardTier.Fusion);

            CardChoiceApplier.ApplyAsync(core, card, null).GetAwaiter().GetResult();

            Assert.AreEqual(2, core.removed, "재료 2개 소진");
            Assert.IsFalse(core.Loadout.Contains(a));
            Assert.IsFalse(core.Loadout.Contains(b));
            Assert.IsTrue(core.Loadout.Contains(r), "결과 추가");
        }
    }
}
```

- [ ] **Step 2: 실패 확인** — FAIL(`CardTier.Fusion`/`FusionCard` 미정의).

- [ ] **Step 3: CardEnums 수정**

```csharp
namespace DefenseDot.Systems.Cards
{
    /// <summary> 카드가 적용하는 동작. </summary>
    public enum CardAction { New, Level, Fuse }

    /// <summary> 카드 희귀도/연출 티어. New·Upgrade 는 코어, 나머지는 합성·럭키용. </summary>
    public enum CardTier { New, Upgrade, Fusion, Lucky, SuperLucky }
}
```

- [ ] **Step 4: CardChoice에 합성 필드/팩토리 추가**

`materialA`/`materialB` 필드 추가(기존 필드 아래), 생성자에 기본값 파라미터 추가(기존 `NewCard`/`LevelCard` 호출 불변), `FusionCard` 팩토리 추가:

```csharp
        /// <summary> 합성 카드일 때 소진할 재료 A. </summary>
        public readonly AbilityInstance materialA;
        /// <summary> 합성 카드일 때 소진할 재료 B. </summary>
        public readonly AbilityInstance materialB;

        public CardChoice(CardAction action, AbilityData data, AbilityInstance instance,
            int fromLevel, int toLevel, CardTier tier,
            AbilityInstance materialA = null, AbilityInstance materialB = null)
        {
            this.action = action;
            this.data = data;
            this.instance = instance;
            this.fromLevel = fromLevel;
            this.toLevel = toLevel;
            this.tier = tier;
            this.materialA = materialA;
            this.materialB = materialB;
        }

        /// <summary> 합성 카드(재료 2개 소진 → 결과 Lv1). </summary>
        public static CardChoice FusionCard(AbilityData result, AbilityInstance materialA, AbilityInstance materialB, CardTier tier)
            => new CardChoice(CardAction.Fuse, result, null, 0, 1, tier, materialA, materialB);
```

- [ ] **Step 5: CardChoiceApplier에 Fuse 분기 추가**

`ApplyAsync` 의 `if (New) … else …` 를 `New / Fuse / else(Level)` 로 확장:

```csharp
            if (choice.action == CardAction.New)
            {
                if (pool != null && choice.data != null) await pool.WarmupAsync(choice.data.EffectAssets);
                AbilityInstance added = core.AddAbility(choice.data);
                if (added != null)
                    for (int lv = added.level; lv < choice.toLevel; lv++) core.LevelUpAbility(added);
            }
            else if (choice.action == CardAction.Fuse)
            {
                if (pool != null && choice.data != null) await pool.WarmupAsync(choice.data.EffectAssets);
                core.RemoveAbility(choice.materialA);
                core.RemoveAbility(choice.materialB);
                core.AddAbility(choice.data);
            }
            else
            {
                for (int lv = choice.fromLevel; lv < choice.toLevel; lv++) core.LevelUpAbility(choice.instance);
            }
```

- [ ] **Step 6: 통과 확인** — `FusionCardTests` 1/1 + 기존 `CardChoiceApplierTests`·`CardPresentationTests` 회귀 유지.
- [ ] **Step 7: lint + 커밋 준비** — `feat: 합성 카드 액션·티어·적용(Fuse) 추가`

---

## Task 3: CardChoiceGenerator — 합성 카드 생성 — EditMode

**Files:**
- Modify: `Assets/Scripts/Systems/Cards/CardChoiceGenerator.cs`
- Test: `Assets/Tests/EditMode/FusionCardTests.cs` (Generator 테스트 추가)

**Interfaces:**
- Consumes: `FusionResolver`, `FusionRecipeSet`(Task 1), `CardChoice.FusionCard`(Task 2).
- Produces: `CardChoiceGenerator.Generate(loadout, pool, config, level, FusionRecipeSet lineage = null)` — 가용 합성이 있으면 합성 카드를 슬롯에 포함.

- [ ] **Step 1: 실패 테스트 작성** (FusionCardTests.cs에 추가)

```csharp
        [Test]
        public void Generate_AvailableFusion_IncludesFusionCard()
        {
            StubActive a = Ability(), b = Ability(), r = Ability();
            AbilityLoadout lo = new AbilityLoadout(6, 6);
            lo.TryAdd(a); lo.TryAdd(b);
            for (int i = 0; i < 2; i++) { lo.LevelUp(lo.Actives[i]); lo.LevelUp(lo.Actives[i]); } // Lv3=MAX

            FusionRecipeSet lineage = ScriptableObject.CreateInstance<FusionRecipeSet>();
            lineage.recipes = new System.Collections.Generic.List<FusionRecipe> {
                new FusionRecipe { materialA = a, materialB = b, result = r } };

            ArenaCardConfig config = ScriptableObject.CreateInstance<ArenaCardConfig>();
            config.choiceCount = 3;

            var choices = new CardChoiceGenerator(() => 0.5f).Generate(lo, null, config, 1, lineage);

            bool hasFusion = false;
            foreach (var c in choices) if (c.action == CardAction.Fuse && c.data == r) hasFusion = true;
            Assert.IsTrue(hasFusion, "가용 합성이 있으면 합성 카드 제시");
        }
```

- [ ] **Step 2: 실패 확인** — FAIL(`Generate` 5번째 인자 없음 / 합성 카드 미포함).

- [ ] **Step 3: Generate에 lineage 인자 + 합성 카드 선행 추가**

`Generate` 시그니처에 `FusionRecipeSet lineage = null` 추가. 본문 초입(결과 리스트 생성 직후)에 가용 합성을 먼저 채운다:

```csharp
        private readonly FusionResolver fusionResolver = new FusionResolver();

        public List<CardChoice> Generate(AbilityLoadout loadout, AbilityPool pool, ArenaCardConfig config, int level, FusionRecipeSet lineage = null)
        {
            var result = new List<CardChoice>();
            if (loadout == null || config == null) return result;

            // 1. 가용 합성 우선 제시 (MAX 게이트라 드묾·의도적)
            if (lineage != null)
            {
                var fusions = fusionResolver.Available(loadout, lineage);
                for (int i = 0; i < fusions.Count && result.Count < config.choiceCount; i++)
                {
                    AvailableFusion f = fusions[i];
                    result.Add(CardChoice.FusionCard(f.recipe.result, f.materialA, f.materialB, CardTier.Fusion));
                }
            }

            // 2. 기존 신규/레벨업 카드로 남은 슬롯 채움
            // (기존 newPool/levelPool 로직: for 루프를 result.Count 시작으로)
```

기존 `for (int n = 0; n < config.choiceCount; n++)` 를 `for (int n = result.Count; n < config.choiceCount; n++)` 로 바꾸고, 루프 내부의 `result.Add(...)` 로직은 그대로 둔다.

- [ ] **Step 4: 통과 확인** — `FusionCardTests.Generate_AvailableFusion_IncludesFusionCard` PASS + 기존 `CardChoiceGeneratorTests` 회귀 유지(옵션 인자라 기존 호출 불변).
- [ ] **Step 5: lint + 커밋 준비** — `feat: 레벨업 카드 풀에 합성 카드 생성 추가`

---

## Task 4: 계보 배선 + 데이터 자산 (PlayMode/에디터)

> EditMode 자동 테스트 대상 아님. 배선 후 PlayMode 수동 검증.

**Files:**
- Modify: `Data/TowerData.cs`, `Systems/Mode/ArenaModeBootstrap.cs`, `Domain/GameContext.cs`, `Systems/Management/GameManager.cs`, `UI/Presenters/CardSelectionPresenter.cs`
- Create: `Systems/Cards/Editor/FusionRecipeSetDrawer.cs`
- Create(에셋): 결과 능력 2 + `Aris_FusionLineage.asset`

- [ ] **Step 1: TowerData 확장(데이터만)**

`TowerData.cs` — 필드 추가(메서드 없음):
```csharp
        [Tooltip("이 타워(캐릭터)의 시작 능력")]
        public System.Collections.Generic.List<AbilityData> starterAbilities = new System.Collections.Generic.List<AbilityData>();
        [Tooltip("이 타워(캐릭터)의 합성 계보")]
        public DefenseDot.Systems.Cards.FusionRecipeSet fusionLineage;
```
(`using DefenseDot.Systems.Abilities;` 필요 시 추가)

- [ ] **Step 2: ArenaModeBootstrap — 타워에서 스타터·계보 노출/주입**

기존 `[SerializeField] private List<AbilityData> starterAbilities;` 를 제거하고, 타워 데이터에서 읽는다:
```csharp
        /// <summary> 선택된 타워의 합성 계보(카드 생성용). </summary>
        public DefenseDot.Systems.Cards.FusionRecipeSet FusionLineage => centerTowerData != null ? centerTowerData.fusionLineage : null;
```
`SpawnCenterTower` 의 `coreAbility.Setup(..., starterAbilities, ...)` 를 `data.starterAbilities`(런타임 복제본) 로 교체:
```csharp
            coreAbility.Setup(ctx.TargetFinder, ctx.CoreCenter, ctx.Flow, ctx.CombatState, data.starterAbilities, ctx.Pooling, fireOrigin);
```

- [ ] **Step 3: GameContext — FusionLineage 추가**

프로퍼티 + 생성자 인자 추가(`CardConfig` 근처):
```csharp
        /// <summary> 선택된 타워의 합성 계보입니다. </summary>
        public DefenseDot.Systems.Cards.FusionRecipeSet FusionLineage { get; }
```
생성자 끝에 파라미터 `DefenseDot.Systems.Cards.FusionRecipeSet fusionLineage` 추가 + `FusionLineage = fusionLineage;`.

- [ ] **Step 4: GameManager — 계보 전달**

`GameContext` 생성 인자에 `arenaBoot != null ? arenaBoot.FusionLineage : null` 추가.

- [ ] **Step 5: CardSelectionPresenter — 생성기에 계보 전달**

`lineage = ctx.FusionLineage` 필드 추가, `generator.Generate(core.Loadout, pool, config, level.Level)` → `generator.Generate(core.Loadout, pool, config, level.Level, lineage)`.

- [ ] **Step 6: 컴파일 확인** — `read_console`(Error) 0. (지역 변수명 정합 수정)

- [ ] **Step 7: FusionRecipeSetDrawer 작성(에디터)**

```csharp
using UnityEditor;
using UnityEngine;
using DefenseDot.Systems.Cards;

namespace DefenseDot.Systems.Cards.EditorTools
{
    /// <summary> FusionRecipe 를 "A + B → C"(아이콘·이름) 한 줄로 표시하는 드로어입니다. </summary>
    [CustomPropertyDrawer(typeof(FusionRecipe))]
    public sealed class FusionRecipeDrawer : PropertyDrawer
    {
        /// <summary> 재료A·재료B·결과 오브젝트 필드를 한 줄에 배치합니다. </summary>
        public override void OnGUI(Rect pos, SerializedProperty prop, GUIContent label)
        {
            SerializedProperty a = prop.FindPropertyRelative("materialA");
            SerializedProperty b = prop.FindPropertyRelative("materialB");
            SerializedProperty r = prop.FindPropertyRelative("result");
            float w = (pos.width - 40f) / 3f;
            float x = pos.x;
            EditorGUI.PropertyField(new Rect(x, pos.y, w, pos.height), a, GUIContent.none); x += w;
            EditorGUI.LabelField(new Rect(x, pos.y, 20f, pos.height), "+"); x += 20f;
            EditorGUI.PropertyField(new Rect(x, pos.y, w, pos.height), b, GUIContent.none); x += w;
            EditorGUI.LabelField(new Rect(x, pos.y, 20f, pos.height), "→"); x += 20f;
            EditorGUI.PropertyField(new Rect(x, pos.y, w, pos.height), r, GUIContent.none);
        }
    }
}
```

- [ ] **Step 8: 데이터 자산 (에디터)**

1. 결과 능력 2종 신규: 기존 `ProjectileAbilityData`(정밀사격)·`OrbitalAbilityData`(폭풍궤도) 타입으로 강한 수치·`baseCost` 설정. `displayName` "정밀사격"/"폭풍궤도". (`AbilityPool`에는 넣지 않음)
2. `Create → DefenseDot → Fusion Recipe Set` → `Aris_FusionLineage.asset`. 레시피 2: (샷+맹공→정밀사격), (오비탈+에어리어웨이브→폭풍궤도).
3. Aris 중앙 타워 `TowerData` 자산에 `fusionLineage`=위 셋, `starterAbilities`=기존 스타터 목록(부트스트랩에서 이관) 설정.

- [ ] **Step 9: PlayMode 수동 검증**

| 확인 | 기대 |
|---|---|
| 재료(샷·맹공) Lv1 | 합성 카드 미등장 |
| 재료 둘 다 MAX | 레벨업 카드 모달에 "합성" 카드(퍼플) 등장 |
| 합성 카드 선택 | 재료 2개 사라지고 결과 능력 등장(Lv1) |
| 결과 보유 후 | 같은 합성 재등장 안 함 |

- [ ] **Step 10: lint + 커밋 준비** — `feat: 타워별 합성 계보 배선과 데모 레시피 자산`

---

## Self-Review

- **Spec coverage**: §3 데이터→T1, §4.1 Resolver→T1, §4.2 카드 확장→T2/T3, §4.3 주입→T4, §5 데모→T4, §6 테스트→T1(4)+T2(1)+T3(1). 커버 완료.
- **Placeholder scan**: 코드 스텝 전부 실제 코드. T4 에디터 스텝은 자산 수작업(정상).
- **Type consistency**: `FusionRecipe{materialA,materialB,result}` · `AvailableFusion{recipe,materialA,materialB}` · `CardChoice.FusionCard(result,a,b,tier)` · `Generate(...,lineage=null)` — 전 태스크 일관. `CardTier.Fusion`/`CardAction.Fuse` 통일. `RemoveAbility`(A4) 재사용.
