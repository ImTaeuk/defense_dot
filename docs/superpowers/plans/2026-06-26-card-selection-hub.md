# 카드 선택 허브 (Arena A3 코어) Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** kills 누적 → 레벨업 → 카드 3장(신규/레벨업) 선택 → AbilityLoadout 반영하는 Arena 카드 선택 허브(코어)를 구현한다.

**Architecture:** 로직(LevelModel·CardChoiceGenerator·CardSelectionPresenter)과 표현(CardSelectionView 프리팹·CardBackground 셰이더·파티클)을 분리. 설정/콘텐츠/연출은 SO(ArenaCardConfig·AbilityPool·CardTierSet)로 데이터 주도. 프레젠터는 `ICardSelectionView`·`ICardCommandTarget` 인터페이스에 의존해 EditMode 단위 테스트 가능.

**Tech Stack:** Unity 6000.2.10f1, C#, uGUI(TextMeshPro/neodgm SDF), URP 셰이더(ShaderLab/HLSL), Shuriken ParticleSystem, NUnit(EditMode), Unity MCP.

## Global Constraints

- 네이밍: C# CamelCase, private 필드 순수 `camelCase`(접두어 금지). 접근 제한자 항상 명시(IDE0040).
- System 라이브러리: using 없이 풀패스(`System.Action` 등). `System.Collections.Generic`은 using 허용.
- 비동기 필요 시 UniTask만 (본 작업엔 비동기 없음).
- event 네이밍 `On*`, 구독 핸들러 `Handle*`.
- 폰트: 모든 TMP 컴포넌트 `Assets/Font/neodgm SDF.asset`.
- 능력 슬롯 추가/변경은 반드시 `CoreAbilitySystem` façade 경유(러너 동기화 보장).
- 정지는 `Time.timeScale` 방식(`GamePhase.Paused` 추가 금지). 게임이 `Playing`일 때만 1.0 복구.
- 테스트: 로직=EditMode NUnit, 비주얼=MCP 플레이+스크린샷. 네임스페이스 `DefenseDot.*`.
- 커밋: 사용자 명시 요청 시에만 (각 Task 끝 "Commit" 스텝은 사용자 승인 후 일괄/개별 수행).

---

### Task 1: 카드 데이터 기초 (enums·CardChoice·SO 3종·AbilityData 설명 필드)

**Files:**
- Create: `Assets/Scripts/Systems/Cards/CardEnums.cs`
- Create: `Assets/Scripts/Systems/Cards/CardChoice.cs`
- Create: `Assets/Scripts/Systems/Cards/ArenaCardConfig.cs`
- Create: `Assets/Scripts/Systems/Cards/AbilityPool.cs`
- Create: `Assets/Scripts/Systems/Cards/CardTierSet.cs`
- Modify: `Assets/Scripts/Systems/Abilities/AbilityData.cs` (설명 필드 추가)
- Test: `Assets/Tests/EditMode/ArenaCardConfigTests.cs`

**Interfaces:**
- Produces: `CardAction{New,Level}`, `CardTier{New,Upgrade}`, `struct CardChoice`(+`NewCard`/`LevelCard` 팩토리), `ArenaCardConfig`(+`int KillsToNextLevel(int level)`), `AbilityPool.abilities`, `CardTierSet.Get(CardTier)`, `AbilityData.description`.

- [ ] **Step 1: 실패 테스트 작성 (곡선)**

```csharp
using NUnit.Framework;
using UnityEngine;
using DefenseDot.Systems.Cards;

namespace DefenseDot.Tests.EditMode
{
    public class ArenaCardConfigTests
    {
        private static ArenaCardConfig NewConfig()
        {
            var c = ScriptableObject.CreateInstance<ArenaCardConfig>();
            c.curveBase = 8; c.curvePerLevel = 4;
            return c;
        }

        [Test]
        public void KillsToNextLevel_FollowsCurve()
        {
            var c = NewConfig();
            Assert.AreEqual(12, c.KillsToNextLevel(1)); // 8 + 1*4
            Assert.AreEqual(28, c.KillsToNextLevel(5)); // 8 + 5*4
        }

        [Test]
        public void KillsToNextLevel_FloorsAtThree()
        {
            var c = NewConfig(); c.curveBase = 0; c.curvePerLevel = 0;
            Assert.AreEqual(3, c.KillsToNextLevel(1));
        }
    }
}
```

- [ ] **Step 2: 테스트 실패 확인**

Run: Unity MCP `run_tests` (EditMode, 필터 `ArenaCardConfigTests`).
Expected: FAIL — `ArenaCardConfig` 형식 없음(컴파일 에러).

- [ ] **Step 3: enums + CardChoice 작성**

`CardEnums.cs`:
```csharp
namespace DefenseDot.Systems.Cards
{
    /// <summary> 카드가 적용하는 동작. (향후 Combo·Bonus 확장) </summary>
    public enum CardAction { New, Level }

    /// <summary> 카드 희귀도/연출 티어. (향후 Lucky·Combo 확장) </summary>
    public enum CardTier { New, Upgrade }
}
```

`CardChoice.cs`:
```csharp
using DefenseDot.Systems.Abilities;

namespace DefenseDot.Systems.Cards
{
    /// <summary> 레벨업 시 제시되는 카드 1장의 데이터. </summary>
    public readonly struct CardChoice
    {
        public readonly CardAction action;
        public readonly AbilityData data;
        public readonly AbilityInstance instance; // Level 카드일 때 대상
        public readonly int fromLevel;
        public readonly int toLevel;
        public readonly CardTier tier;

        public CardChoice(CardAction action, AbilityData data, AbilityInstance instance,
            int fromLevel, int toLevel, CardTier tier)
        {
            this.action = action; this.data = data; this.instance = instance;
            this.fromLevel = fromLevel; this.toLevel = toLevel; this.tier = tier;
        }

        public static CardChoice NewCard(AbilityData data)
            => new CardChoice(CardAction.New, data, null, 0, 1, CardTier.New);

        public static CardChoice LevelCard(AbilityInstance inst)
            => new CardChoice(CardAction.Level, inst.data, inst, inst.level, inst.level + 1, CardTier.Upgrade);
    }
}
```

- [ ] **Step 4: SO 3종 작성**

`ArenaCardConfig.cs`:
```csharp
using UnityEngine;

namespace DefenseDot.Systems.Cards
{
    /// <summary> 카드 선택 허브 설정(토글·곡선·비율·향후 플래그). </summary>
    [CreateAssetMenu(menuName = "DefenseDot/Cards/Arena Card Config", fileName = "ArenaCardConfig")]
    public sealed class ArenaCardConfig : ScriptableObject
    {
        [Header("정지")]
        public bool pauseOnCardSelect = true;

        [Header("카드 생성")]
        public int choiceCount = 3;

        [Header("레벨 곡선  kills = max(3, curveBase + level*curvePerLevel)")]
        public int curveBase = 8;
        public int curvePerLevel = 4;

        [Header("신규 vs 레벨업 비율")]
        [Range(0f, 1f)] public float newCardChanceEarly = 0.75f;
        [Range(0f, 1f)] public float newCardChanceLate = 0.45f;
        public int earlyLevelThreshold = 4;

        [Header("향후 겹 (기본 off)")]
        public bool enableLucky = false;
        public bool enableCombo = false;
        public bool enableBonus = false;
        [Range(0f, 1f)] public float luckyChance = 0.12f;
        [Range(0f, 1f)] public float superLuckyChance = 0.03f;

        [Header("연출")]
        public CardTierSet tierSet;

        /// <summary> 해당 레벨에서 다음 레벨까지 필요한 처치 수. </summary>
        public int KillsToNextLevel(int level)
            => Mathf.Max(3, curveBase + level * curvePerLevel);
    }
}
```

`AbilityPool.cs`:
```csharp
using System.Collections.Generic;
using UnityEngine;
using DefenseDot.Systems.Abilities;

namespace DefenseDot.Systems.Cards
{
    /// <summary> "신규 능력" 카드 후보 풀(콘텐츠). </summary>
    [CreateAssetMenu(menuName = "DefenseDot/Cards/Ability Pool", fileName = "AbilityPool")]
    public sealed class AbilityPool : ScriptableObject
    {
        public List<AbilityData> abilities = new List<AbilityData>();
    }
}
```

`CardTierSet.cs`:
```csharp
using System;
using System.Collections.Generic;
using UnityEngine;

namespace DefenseDot.Systems.Cards
{
    /// <summary> 티어별 색/연출 스타일(원작 CARD_TIERS 이관). </summary>
    [CreateAssetMenu(menuName = "DefenseDot/Cards/Card Tier Set", fileName = "CardTierSet")]
    public sealed class CardTierSet : ScriptableObject
    {
        [Serializable]
        public struct TierStyle
        {
            public CardTier tier;
            public Color borderColor;
            public Color bgTop;
            public Color bgBottom;
            public Color glowColor;
            public float glowIntensity;
            public bool useParticle;
        }

        public List<TierStyle> styles = new List<TierStyle>();

        /// <summary> 티어 스타일 조회(없으면 첫 항목/기본값). </summary>
        public TierStyle Get(CardTier tier)
        {
            for (int i = 0; i < styles.Count; i++)
                if (styles[i].tier == tier) return styles[i];
            return styles.Count > 0 ? styles[0] : default;
        }
    }
}
```

- [ ] **Step 5: AbilityData 에 설명 필드 추가**

Modify `Assets/Scripts/Systems/Abilities/AbilityData.cs` — 클래스 본문에 추가:
```csharp
        [TextArea] public string description;   // 카드 표시용 설명(선택)
```

- [ ] **Step 6: 테스트 통과 확인**

Run: Unity MCP `run_tests` (EditMode, `ArenaCardConfigTests`). 먼저 `read_console`로 컴파일 0 확인.
Expected: PASS (2/2).

- [ ] **Step 7: Commit (사용자 승인 후)**

```bash
git add Assets/Scripts/Systems/Cards/ Assets/Scripts/Systems/Abilities/AbilityData.cs Assets/Tests/EditMode/ArenaCardConfigTests.cs
git commit -m "feat: 카드 데이터 기초(설정·풀·티어 SO, CardChoice) 추가"
```

---

### Task 2: LevelModel (kills → 레벨업 → pending)

**Files:**
- Create: `Assets/Scripts/Domain/Models/LevelModel.cs`
- Test: `Assets/Tests/EditMode/LevelModelTests.cs`

**Interfaces:**
- Consumes: 없음 (곡선은 `System.Func<int,int>`로 주입 — config 비의존).
- Produces: `LevelModel(System.Func<int,int> curve)`, `int Level/Kills/KillsToNextLevel/PendingLevelUps`, `event System.Action OnLevelUp`, `void RegisterKill()`, `bool TryConsumePending()`.

- [ ] **Step 1: 실패 테스트 작성**

```csharp
using NUnit.Framework;
using DefenseDot.Domain.Models;

namespace DefenseDot.Tests.EditMode
{
    public class LevelModelTests
    {
        // 곡선: level*2 (테스트 단순화). level1→2칸, level2→4칸 ...
        private static LevelModel New() => new LevelModel(lv => lv * 2);

        [Test]
        public void RegisterKill_LevelsUpAtThreshold()
        {
            var m = New();                 // KillsToNextLevel=2 (level1)
            int fired = 0; m.OnLevelUp += () => fired++;
            m.RegisterKill();              // kills 1
            Assert.AreEqual(1, m.Level);
            m.RegisterKill();              // kills 2 → 레벨업
            Assert.AreEqual(2, m.Level);
            Assert.AreEqual(0, m.Kills);
            Assert.AreEqual(1, fired);
            Assert.AreEqual(1, m.PendingLevelUps);
        }

        [Test]
        public void RegisterKill_HandlesMultiLevelInOneKill()
        {
            var m = new LevelModel(lv => 1); // 매 처치마다 레벨업
            m.RegisterKill();
            Assert.AreEqual(2, m.Level);
            Assert.AreEqual(1, m.PendingLevelUps);
        }

        [Test]
        public void TryConsumePending_DecrementsAndReportsEmpty()
        {
            var m = new LevelModel(lv => 1);
            m.RegisterKill();              // pending 1
            Assert.IsTrue(m.TryConsumePending());
            Assert.IsFalse(m.TryConsumePending());
        }
    }
}
```

- [ ] **Step 2: 실패 확인** — Run `run_tests`(EditMode, `LevelModelTests`). Expected: FAIL(형식 없음).

- [ ] **Step 3: 구현**

```csharp
namespace DefenseDot.Domain.Models
{
    /// <summary> 플레이어 레벨·처치 누적·레벨업 통지를 소유하는 모델. </summary>
    public sealed class LevelModel : BaseModel
    {
        private readonly System.Func<int, int> curve;

        public int Level { get; private set; } = 1;
        public int Kills { get; private set; }
        public int KillsToNextLevel { get; private set; }
        public int PendingLevelUps { get; private set; }

        public event System.Action OnLevelUp;

        public LevelModel(System.Func<int, int> curve)
        {
            this.curve = curve;
            KillsToNextLevel = curve(Level);
        }

        /// <summary> 처치 1회 집계. 곡선 도달 시 레벨업(다중 가능). </summary>
        public void RegisterKill()
        {
            Kills++;
            bool leveled = false;
            while (Kills >= KillsToNextLevel)
            {
                Kills -= KillsToNextLevel;
                Level++;
                KillsToNextLevel = curve(Level);
                PendingLevelUps++;
                leveled = true;
            }
            if (leveled) OnLevelUp?.Invoke();
        }

        /// <summary> 대기 레벨업 1건 소비. 없으면 false. </summary>
        public bool TryConsumePending()
        {
            if (PendingLevelUps <= 0) return false;
            PendingLevelUps--;
            return true;
        }
    }
}
```

> 참고: `BaseModel`이 abstract이고 멤버 요구가 없으면 위 그대로 컴파일. 만약 `BaseModel`에 추상 멤버가 있으면 다른 모델(`ScoreModel`)과 동일 패턴으로 충족할 것.

- [ ] **Step 4: 통과 확인** — Run `run_tests`(EditMode, `LevelModelTests`). Expected: PASS(3/3).

- [ ] **Step 5: Commit (승인 후)**

```bash
git add Assets/Scripts/Domain/Models/LevelModel.cs Assets/Tests/EditMode/LevelModelTests.cs
git commit -m "feat: 레벨·처치 누적 LevelModel 추가"
```

---

### Task 3: CardChoiceGenerator (3장 생성 로직)

**Files:**
- Create: `Assets/Scripts/Systems/Cards/CardChoiceGenerator.cs`
- Test: `Assets/Tests/EditMode/CardChoiceGeneratorTests.cs`

**Interfaces:**
- Consumes: `AbilityLoadout`(`CanAdd`/`Actives`/`Passives`), `AbilityPool`, `ArenaCardConfig`, `CardChoice`.
- Produces: `CardChoiceGenerator(System.Func<float> rng = null)`, `List<CardChoice> Generate(AbilityLoadout loadout, AbilityPool pool, ArenaCardConfig config, int level)`.

- [ ] **Step 1: 실패 테스트 작성**

```csharp
using NUnit.Framework;
using System.Collections.Generic;
using UnityEngine;
using DefenseDot.Systems.Abilities;
using DefenseDot.Systems.Cards;
using DefenseDot.Core;

namespace DefenseDot.Tests.EditMode
{
    public class CardChoiceGeneratorTests
    {
        private sealed class StubActive : ActiveAbilityData
        {
            public override void Tick(in AbilityContext ctx, AbilityInstance self, float dt) { }
            protected override void Fire(in AbilityContext ctx, AbilityInstance self, ITargetable target) { }
        }
        private static StubActive Active(int max = 5)
        { var a = ScriptableObject.CreateInstance<StubActive>(); a.maxLevel = max; return a; }

        private static ArenaCardConfig Config(int count = 3)
        { var c = ScriptableObject.CreateInstance<ArenaCardConfig>(); c.choiceCount = count;
          c.newCardChanceEarly = 1f; c.newCardChanceLate = 1f; return c; }

        private static AbilityPool Pool(params AbilityData[] abs)
        { var p = ScriptableObject.CreateInstance<AbilityPool>(); p.abilities.AddRange(abs); return p; }

        [Test]
        public void Generate_NoDuplicates_AndRespectsCount()
        {
            var lo = new AbilityLoadout(6, 6);
            var gen = new CardChoiceGenerator(() => 0f); // 항상 첫 인덱스/New
            var pool = Pool(Active(), Active(), Active());
            var choices = gen.Generate(lo, pool, Config(3), level: 1);
            Assert.AreEqual(3, choices.Count);
            CollectionAssert.AllItemsAreUnique(new[] { choices[0].data, choices[1].data, choices[2].data });
        }

        [Test]
        public void Generate_WhenSlotsFull_OnlyLevelCards()
        {
            var lo = new AbilityLoadout(1, 0);   // 액티브 1칸, 패시브 0칸
            var owned = Active();
            lo.TryAdd(owned);                    // 슬롯 가득
            var pool = Pool(Active(), Active());  // 신규 후보 있지만 슬롯 없음
            var gen = new CardChoiceGenerator(() => 0f);
            var choices = gen.Generate(lo, pool, Config(3), level: 1);
            foreach (var c in choices) Assert.AreEqual(CardAction.Level, c.action);
            Assert.AreEqual(1, choices.Count, "레벨업 가능한 1종만");
        }

        [Test]
        public void Generate_WhenExhausted_ReturnsEmpty()
        {
            var lo = new AbilityLoadout(1, 0);
            var maxed = Active(max: 1);
            lo.TryAdd(maxed);                    // 이미 max, 레벨업 불가
            var gen = new CardChoiceGenerator(() => 0f);
            var choices = gen.Generate(lo, Pool(), Config(3), level: 1); // 풀 비움
            Assert.AreEqual(0, choices.Count);
        }
    }
}
```

- [ ] **Step 2: 실패 확인** — Run `run_tests`(EditMode, `CardChoiceGeneratorTests`). Expected: FAIL.

- [ ] **Step 3: 구현**

```csharp
using System;
using System.Collections.Generic;
using DefenseDot.Systems.Abilities;

namespace DefenseDot.Systems.Cards
{
    /// <summary> 레벨업 시 보유/슬롯/풀을 보고 카드 N장을 생성. </summary>
    public sealed class CardChoiceGenerator
    {
        private readonly Func<float> rng;

        public CardChoiceGenerator(Func<float> rng = null)
        {
            this.rng = rng ?? (() => UnityEngine.Random.value);
        }

        public List<CardChoice> Generate(AbilityLoadout loadout, AbilityPool pool, ArenaCardConfig config, int level)
        {
            var result = new List<CardChoice>();
            if (loadout == null || config == null) return result;

            var newPool = new List<AbilityData>();
            if (pool != null)
                for (int i = 0; i < pool.abilities.Count; i++)
                {
                    var d = pool.abilities[i];
                    if (d != null && loadout.CanAdd(d)) newPool.Add(d); // 슬롯+미보유 동시 검사
                }

            var levelPool = new List<AbilityInstance>();
            CollectLevelable(loadout.Actives, levelPool);
            CollectLevelable(loadout.Passives, levelPool);

            float newChance = level < config.earlyLevelThreshold
                ? config.newCardChanceEarly : config.newCardChanceLate;

            for (int n = 0; n < config.choiceCount; n++)
            {
                bool canNew = newPool.Count > 0;
                bool canLv = levelPool.Count > 0;
                if (!canNew && !canLv) break;

                bool pickNew = canNew && (!canLv || rng() < newChance);
                if (pickNew)
                {
                    int idx = Index(newPool.Count);
                    result.Add(CardChoice.NewCard(newPool[idx]));
                    newPool.RemoveAt(idx);
                }
                else
                {
                    int idx = Index(levelPool.Count);
                    result.Add(CardChoice.LevelCard(levelPool[idx]));
                    levelPool.RemoveAt(idx);
                }
            }
            return result;
        }

        private int Index(int count)
        {
            int idx = (int)(rng() * count);
            return idx >= count ? count - 1 : idx;
        }

        private static void CollectLevelable(IReadOnlyList<AbilityInstance> src, List<AbilityInstance> dst)
        {
            for (int i = 0; i < src.Count; i++)
                if (src[i].level < src[i].data.maxLevel) dst.Add(src[i]);
        }
    }
}
```

- [ ] **Step 4: 통과 확인** — Run `run_tests`(EditMode, `CardChoiceGeneratorTests`). Expected: PASS(3/3).

- [ ] **Step 5: Commit (승인 후)**

```bash
git add Assets/Scripts/Systems/Cards/CardChoiceGenerator.cs Assets/Tests/EditMode/CardChoiceGeneratorTests.cs
git commit -m "feat: 레벨업 카드 3장 생성기 CardChoiceGenerator 추가"
```

---

### Task 4: CoreAbilitySystem 카드 façade + ICardCommandTarget

**Files:**
- Create: `Assets/Scripts/Systems/Abilities/ICardCommandTarget.cs`
- Modify: `Assets/Scripts/Systems/Abilities/CoreAbilitySystem.cs` (인터페이스 구현 + 메서드)

**Interfaces:**
- Consumes: `AbilityLoadout`, `AbilityRunner.Equip`.
- Produces: `interface ICardCommandTarget { AbilityLoadout Loadout {get;} bool AddAbility(AbilityData); void LevelUpAbility(AbilityInstance); }`. `CoreAbilitySystem : ICardCommandTarget`.

> **검증 방식:** 이 Task는 MonoBehaviour 배선이라 격리 단위 테스트 대신 **Task 11 플레이 통합**(신규 액티브 카드가 실제 발사되는지)으로 검증한다. 컴파일 0 + 인터페이스 시그니처 일치가 본 Task의 게이트.

- [ ] **Step 1: 인터페이스 작성**

```csharp
namespace DefenseDot.Systems.Abilities
{
    /// <summary> 카드 선택이 능력을 추가/레벨업하는 명령 대상. </summary>
    public interface ICardCommandTarget
    {
        AbilityLoadout Loadout { get; }
        bool AddAbility(AbilityData data);
        void LevelUpAbility(AbilityInstance instance);
    }
}
```

- [ ] **Step 2: CoreAbilitySystem 에 구현 추가**

클래스 선언에 인터페이스 추가: `public sealed class CoreAbilitySystem : MonoBehaviour, ICastHost, ICardCommandTarget`

본문에 추가(기존 `loadout`·`runner` 필드 사용):
```csharp
        /// <summary> 읽기 전용 로드아웃(카드 생성기 질의용). </summary>
        public AbilityLoadout Loadout => loadout;

        /// <summary> 신규 능력 추가. 액티브면 러너에 즉시 장착(틱 동기화). </summary>
        public bool AddAbility(AbilityData data)
        {
            if (loadout == null || !loadout.TryAdd(data)) return false;
            if (data is ActiveAbilityData)
            {
                var inst = loadout.Actives[loadout.Actives.Count - 1];
                runner?.Equip(inst);
            }
            return true;
        }

        /// <summary> 기존 능력 레벨업. </summary>
        public void LevelUpAbility(AbilityInstance instance)
        {
            loadout?.LevelUp(instance);
        }
```

> `runner.Equip(AbilityInstance)` 시그니처는 `AbilityRunner` 의 기존 `Equip`(개별 능력 장착 훅)을 사용. `loadout`/`runner` 필드명이 다르면 실제 필드명에 맞춰 조정.

- [ ] **Step 3: 컴파일 확인** — Unity MCP `read_console`로 에러 0 확인(도메인 리로드 후).

- [ ] **Step 4: Commit (승인 후)**

```bash
git add Assets/Scripts/Systems/Abilities/ICardCommandTarget.cs Assets/Scripts/Systems/Abilities/CoreAbilitySystem.cs
git commit -m "feat: CoreAbilitySystem 카드 명령 façade(ICardCommandTarget) 추가"
```

---

### Task 5: CardPresentation + ICardSelectionView + CardSelectionView(스크립트)

**Files:**
- Create: `Assets/Scripts/Systems/Cards/CardPresentation.cs`
- Create: `Assets/Scripts/UI/Views/ICardSelectionView.cs`
- Create: `Assets/Scripts/UI/Views/CardSelectionView.cs`
- Test: `Assets/Tests/EditMode/CardPresentationTests.cs`

**Interfaces:**
- Consumes: `CardChoice`, `PassiveAbilityData`, `AbilityData(displayName/icon/description)`.
- Produces: `struct CardDisplay{string title,kindTag,desc; CardTier tier; Sprite icon;}`, `CardPresentation.Build(in CardChoice)`, `interface ICardSelectionView{ void Show(IReadOnlyList<CardChoice>); void Hide(); event System.Action<int> OnCardSelected; }`, `CardSelectionView : MonoBehaviour, ICardSelectionView`.

- [ ] **Step 1: 실패 테스트 작성 (순수 매핑)**

```csharp
using NUnit.Framework;
using UnityEngine;
using DefenseDot.Systems.Abilities;
using DefenseDot.Systems.Cards;
using DefenseDot.Core;

namespace DefenseDot.Tests.EditMode
{
    public class CardPresentationTests
    {
        private sealed class StubActive : ActiveAbilityData
        {
            public override void Tick(in AbilityContext ctx, AbilityInstance self, float dt) { }
            protected override void Fire(in AbilityContext ctx, AbilityInstance self, ITargetable target) { }
        }

        [Test]
        public void Build_NewCard_UsesNameAndActiveTag()
        {
            var a = ScriptableObject.CreateInstance<StubActive>();
            a.displayName = "샷"; a.description = "기본 발사";
            var disp = CardPresentation.Build(CardChoice.NewCard(a));
            Assert.AreEqual("샷", disp.title);
            StringAssert.Contains("액티브", disp.kindTag);
            Assert.AreEqual("기본 발사", disp.desc);
            Assert.AreEqual(CardTier.New, disp.tier);
        }

        [Test]
        public void Build_LevelCard_ShowsLevelTransition()
        {
            var a = ScriptableObject.CreateInstance<StubActive>();
            a.displayName = "샷"; a.maxLevel = 5;
            var inst = new AbilityInstance(a, 2);
            var disp = CardPresentation.Build(CardChoice.LevelCard(inst));
            StringAssert.Contains("Lv2", disp.desc);
            StringAssert.Contains("Lv3", disp.desc);
            Assert.AreEqual(CardTier.Upgrade, disp.tier);
        }
    }
}
```

- [ ] **Step 2: 실패 확인** — Run `run_tests`(EditMode, `CardPresentationTests`). Expected: FAIL.

- [ ] **Step 3: CardPresentation 구현**

```csharp
using UnityEngine;
using DefenseDot.Systems.Abilities;

namespace DefenseDot.Systems.Cards
{
    public struct CardDisplay
    {
        public string title;
        public string kindTag;
        public string desc;
        public CardTier tier;
        public Sprite icon;
    }

    /// <summary> CardChoice → 표시용 데이터 변환(순수). </summary>
    public static class CardPresentation
    {
        public static CardDisplay Build(in CardChoice c)
        {
            var d = c.data;
            bool passive = d is PassiveAbilityData;
            string kind = passive ? "⚙ 패시브" : "⚡ 액티브";
            string desc = c.action == CardAction.Level
                ? $"Lv{c.fromLevel} → Lv{c.toLevel}"
                : (string.IsNullOrEmpty(d.description) ? d.displayName : d.description);
            return new CardDisplay
            {
                title = d.displayName,
                kindTag = kind,
                desc = desc,
                tier = c.tier,
                icon = d.icon,
            };
        }
    }
}
```

- [ ] **Step 4: 통과 확인** — Run `run_tests`(EditMode, `CardPresentationTests`). Expected: PASS(2/2).

- [ ] **Step 5: ICardSelectionView 작성**

```csharp
using System.Collections.Generic;
using DefenseDot.Systems.Cards;

namespace DefenseDot.UI.Views
{
    /// <summary> 카드 선택 모달 뷰 계약(프레젠터 테스트용 추상화). </summary>
    public interface ICardSelectionView
    {
        void Show(IReadOnlyList<CardChoice> choices);
        void Hide();
        event System.Action<int> OnCardSelected;
    }
}
```

- [ ] **Step 6: CardSelectionView(MonoBehaviour) 작성**

```csharp
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using DefenseDot.Systems.Cards;

namespace DefenseDot.UI.Views
{
    /// <summary> 레벨업 카드 모달 프리팹 바인딩. 3개 카드 아이템에 데이터/색/연출 반영. </summary>
    public sealed class CardSelectionView : MonoBehaviour, ICardSelectionView
    {
        [System.Serializable]
        public struct CardItem
        {
            public Button button;
            public Image background;     // CardBackground 머티리얼 인스턴스
            public Image border;
            public Image icon;
            public TextMeshProUGUI nameText;
            public TextMeshProUGUI kindText;
            public TextMeshProUGUI descText;
            public ParticleSystem glowParticle;
        }

        [SerializeField] private CanvasGroup canvasGroup;
        [SerializeField] private GameObject root;
        [SerializeField] private TextMeshProUGUI titleText;
        [SerializeField] private CardItem[] items;
        [SerializeField] private ArenaCardConfig config; // tierSet 접근

        public event System.Action<int> OnCardSelected;

        private void Awake()
        {
            for (int i = 0; i < items.Length; i++)
            {
                int idx = i;
                if (items[i].button != null)
                    items[i].button.onClick.AddListener(() => OnCardSelected?.Invoke(idx));
            }
            Hide();
        }

        public void Show(IReadOnlyList<CardChoice> choices)
        {
            if (root != null) root.SetActive(true);
            if (titleText != null) titleText.text = "LEVEL UP";
            for (int i = 0; i < items.Length; i++)
            {
                bool used = i < choices.Count;
                if (items[i].button != null) items[i].button.gameObject.SetActive(used);
                if (!used) continue;
                Bind(items[i], choices[i]);
            }
            StartFade();
        }

        private void Bind(in CardItem item, in CardChoice choice)
        {
            var disp = CardPresentation.Build(choice);
            if (item.nameText != null) item.nameText.text = disp.title;
            if (item.kindText != null) item.kindText.text = disp.kindTag;
            if (item.descText != null) item.descText.text = disp.desc;
            if (item.icon != null) { item.icon.sprite = disp.icon; item.icon.enabled = disp.icon != null; }

            if (config != null && config.tierSet != null && item.background != null)
            {
                var s = config.tierSet.Get(disp.tier);
                if (item.border != null) item.border.color = s.borderColor;
                var mpb = new MaterialPropertyBlock();
                item.background.material.SetColor("_ColorTop", s.bgTop);
                item.background.material.SetColor("_ColorBottom", s.bgBottom);
                item.background.material.SetColor("_GlowColor", s.glowColor);
                item.background.material.SetFloat("_GlowIntensity", s.glowIntensity);
                if (item.glowParticle != null)
                {
                    if (s.useParticle) item.glowParticle.Play(); else item.glowParticle.Stop();
                }
            }
        }

        private void StartFade()
        {
            if (canvasGroup == null) return;
            canvasGroup.alpha = 0f;
            // 간단 fade-in: 코루틴 대신 Update 트윈은 생략, 초기 alpha=1 즉시 (정지 중 코루틴 미동작 고려)
            canvasGroup.alpha = 1f;
        }

        public void Hide()
        {
            if (root != null) root.SetActive(false);
        }
    }
}
```

> 정지(timeScale=0) 중에는 코루틴/`Time.deltaTime` 트윈이 멈추므로, fade는 `unscaledDeltaTime` 기반이 필요. 본 코어 단계는 즉시 표시(alpha=1)로 단순화하고, fade 연출은 Task 10에서 `unscaledDeltaTime` 트윈으로 보강(스크린샷 확인).

- [ ] **Step 7: 컴파일 확인** — `read_console` 에러 0.

- [ ] **Step 8: Commit (승인 후)**

```bash
git add Assets/Scripts/Systems/Cards/CardPresentation.cs Assets/Scripts/UI/Views/ICardSelectionView.cs Assets/Scripts/UI/Views/CardSelectionView.cs Assets/Tests/EditMode/CardPresentationTests.cs
git commit -m "feat: 카드 표시 매핑·뷰(CardSelectionView) 추가"
```

---

### Task 6: CardSelectionPresenter (오케스트레이션 + 정지)

**Files:**
- Create: `Assets/Scripts/UI/Presenters/CardSelectionPresenter.cs`
- Test: `Assets/Tests/EditMode/CardSelectionPresenterTests.cs`

**Interfaces:**
- Consumes: `ICardSelectionView`, `LevelModel`, `CardChoiceGenerator`, `ICardCommandTarget`, `ArenaCardConfig`, `AbilityPool`, `GameFlowModel`, `IPresenter`.
- Produces: `CardSelectionPresenter : IPresenter`.

- [ ] **Step 1: 실패 테스트 작성**

```csharp
using NUnit.Framework;
using System;
using System.Collections.Generic;
using UnityEngine;
using DefenseDot.Domain.Models;
using DefenseDot.Systems.Abilities;
using DefenseDot.Systems.Cards;
using DefenseDot.UI.Views;
using DefenseDot.UI.Presenters;
using DefenseDot.Core;

namespace DefenseDot.Tests.EditMode
{
    public class CardSelectionPresenterTests
    {
        private sealed class StubView : ICardSelectionView
        {
            public int showCount; public int hideCount; public IReadOnlyList<CardChoice> last;
            public event Action<int> OnCardSelected;
            public void Show(IReadOnlyList<CardChoice> c) { showCount++; last = c; }
            public void Hide() { hideCount++; }
            public void Click(int i) { OnCardSelected?.Invoke(i); }
        }
        private sealed class StubCore : ICardCommandTarget
        {
            public AbilityLoadout Loadout { get; } = new AbilityLoadout(6, 6);
            public int added; public int leveled;
            public bool AddAbility(AbilityData d) { added++; return Loadout.TryAdd(d); }
            public void LevelUpAbility(AbilityInstance i) { leveled++; Loadout.LevelUp(i); }
        }
        private sealed class StubActive : ActiveAbilityData
        {
            public override void Tick(in AbilityContext ctx, AbilityInstance self, float dt) { }
            protected override void Fire(in AbilityContext ctx, AbilityInstance self, ITargetable target) { }
        }
        private static StubActive Active() { var a = ScriptableObject.CreateInstance<StubActive>(); a.maxLevel = 5; return a; }
        private static ArenaCardConfig Config(bool pause)
        { var c = ScriptableObject.CreateInstance<ArenaCardConfig>(); c.choiceCount = 3; c.pauseOnCardSelect = pause;
          c.newCardChanceEarly = 1f; c.newCardChanceLate = 1f; c.curveBase = 0; c.curvePerLevel = 0; return c; } // kills=3
        private static AbilityPool Pool(params AbilityData[] a)
        { var p = ScriptableObject.CreateInstance<AbilityPool>(); p.abilities.AddRange(a); return p; }

        [TearDown] public void Reset() => Time.timeScale = 1f;

        private static (CardSelectionPresenter p, StubView v, StubCore core, LevelModel lvl) Make(ArenaCardConfig cfg, AbilityPool pool)
        {
            var v = new StubView(); var core = new StubCore();
            var lvl = new LevelModel(cfg.KillsToNextLevel);
            var flow = new GameFlowModel(); flow.SetPhase(GamePhase.Playing);
            var gen = new CardChoiceGenerator(() => 0f);
            var p = new CardSelectionPresenter(v, lvl, gen, core, cfg, pool, flow);
            p.Initialize();
            return (p, v, core, lvl);
        }

        [Test]
        public void OnLevelUp_ShowsModalAndPauses()
        {
            var cfg = Config(pause: true);
            var (p, v, core, lvl) = Make(cfg, Pool(Active(), Active(), Active()));
            for (int i = 0; i < 3; i++) lvl.RegisterKill(); // kills=3 → 레벨업
            Assert.AreEqual(1, v.showCount);
            Assert.AreEqual(3, v.last.Count);
            Assert.AreEqual(0f, Time.timeScale);
        }

        [Test]
        public void SelectNewCard_AddsAbility_HidesAndResumes()
        {
            var cfg = Config(pause: true);
            var (p, v, core, lvl) = Make(cfg, Pool(Active(), Active(), Active()));
            for (int i = 0; i < 3; i++) lvl.RegisterKill();
            v.Click(0);
            Assert.AreEqual(1, core.added);
            Assert.AreEqual(1, v.hideCount);
            Assert.AreEqual(1f, Time.timeScale);
        }

        [Test]
        public void EmptyChoices_DoesNotShow()
        {
            var cfg = Config(pause: true);
            var (p, v, core, lvl) = Make(cfg, Pool()); // 빈 풀 + 보유 없음
            for (int i = 0; i < 3; i++) lvl.RegisterKill();
            Assert.AreEqual(0, v.showCount);
        }
    }
}
```

- [ ] **Step 2: 실패 확인** — Run `run_tests`(EditMode, `CardSelectionPresenterTests`). Expected: FAIL.

- [ ] **Step 3: 구현**

```csharp
using System.Collections.Generic;
using UnityEngine;
using DefenseDot.Domain.Models;
using DefenseDot.Systems.Abilities;
using DefenseDot.Systems.Cards;
using DefenseDot.UI.Views;

namespace DefenseDot.UI.Presenters
{
    /// <summary> 레벨업 → 카드 생성 → 표시/정지 → 선택 적용 → 복귀 오케스트레이션. </summary>
    public sealed class CardSelectionPresenter : IPresenter
    {
        private readonly ICardSelectionView view;
        private readonly LevelModel level;
        private readonly CardChoiceGenerator generator;
        private readonly ICardCommandTarget core;
        private readonly ArenaCardConfig config;
        private readonly AbilityPool pool;
        private readonly GameFlowModel flow;
        private List<CardChoice> current;

        public CardSelectionPresenter(ICardSelectionView view, LevelModel level, CardChoiceGenerator generator,
            ICardCommandTarget core, ArenaCardConfig config, AbilityPool pool, GameFlowModel flow)
        {
            this.view = view; this.level = level; this.generator = generator;
            this.core = core; this.config = config; this.pool = pool; this.flow = flow;
        }

        public void Initialize()
        {
            level.OnLevelUp += HandleLevelUp;
            view.OnCardSelected += HandleSelected;
            view.Hide();
        }

        private void HandleLevelUp()
        {
            if (current == null) ShowNext();   // 모달 표시 중이면 선택 후 드레인
        }

        private void ShowNext()
        {
            if (!level.TryConsumePending()) return;
            current = generator.Generate(core.Loadout, pool, config, level.Level);
            if (current == null || current.Count == 0) { current = null; ShowNext(); return; }
            view.Show(current);
            if (config.pauseOnCardSelect) Time.timeScale = 0f;
        }

        private void HandleSelected(int idx)
        {
            if (current == null || idx < 0 || idx >= current.Count) return;
            var c = current[idx];
            if (c.action == CardAction.New) core.AddAbility(c.data);
            else core.LevelUpAbility(c.instance);
            current = null;
            view.Hide();
            if (flow.Phase == GamePhase.Playing) Time.timeScale = 1f;
            ShowNext();
        }

        public void Dispose()
        {
            level.OnLevelUp -= HandleLevelUp;
            view.OnCardSelected -= HandleSelected;
            Time.timeScale = 1f;
        }
    }
}
```

- [ ] **Step 4: 통과 확인** — Run `run_tests`(EditMode, `CardSelectionPresenterTests`). Expected: PASS(3/3).

- [ ] **Step 5: Commit (승인 후)**

```bash
git add Assets/Scripts/UI/Presenters/CardSelectionPresenter.cs Assets/Tests/EditMode/CardSelectionPresenterTests.cs
git commit -m "feat: 카드 선택 오케스트레이션 CardSelectionPresenter 추가"
```

---

### Task 7: 배선 (CardContext·UIRoot·GameManager·ArenaModeBootstrap)

**Files:**
- Create: `Assets/Scripts/UI/CardContext.cs`
- Modify: `Assets/Scripts/UI/InGame/UIRoot.cs`
- Modify: `Assets/Scripts/Systems/Management/GameManager.cs`
- Modify: `Assets/Scripts/Systems/Mode/ArenaModeBootstrap.cs`

**Interfaces:**
- Consumes: 모든 선행 Task 산출물.
- Produces: `struct CardContext`, `UIRoot.Inject(...)` 확장, `ArenaModeBootstrap.CardConfig/AbilityPool/CoreAbility` 노출.

> **검증:** 컴파일 0 + Task 11 플레이 통합. (배선이라 격리 단위 테스트 없음.)

- [ ] **Step 1: CardContext 작성**

```csharp
using DefenseDot.Domain.Models;
using DefenseDot.Systems.Abilities;
using DefenseDot.Systems.Cards;

namespace DefenseDot.UI
{
    /// <summary> 카드 선택 프레젠터 조립 파라미터. </summary>
    public readonly struct CardContext
    {
        public readonly LevelModel Level;
        public readonly ArenaCardConfig Config;
        public readonly AbilityPool Pool;
        public readonly ICardCommandTarget Core;
        public readonly GameFlowModel Flow;

        public CardContext(LevelModel level, ArenaCardConfig config, AbilityPool pool,
            ICardCommandTarget core, GameFlowModel flow)
        {
            Level = level; Config = config; Pool = pool; Core = core; Flow = flow;
        }
    }
}
```

- [ ] **Step 2: UIRoot 확장**

`UIRoot`에 필드 추가:
```csharp
        [SerializeField] private DefenseDot.UI.Views.CardSelectionView cardSelectionView;
```
`Inject` 시그니처에 `in CardContext card` 추가, 프레젠터 등록(다른 프레젠터 등록부 옆):
```csharp
            if (cardSelectionView != null && card.Level != null && card.Config != null)
            {
                presenters.Add(new DefenseDot.UI.Presenters.CardSelectionPresenter(
                    cardSelectionView, card.Level, new DefenseDot.Systems.Cards.CardChoiceGenerator(),
                    card.Core, card.Config, card.Pool, card.Flow));
            }
```

- [ ] **Step 3: ArenaModeBootstrap 노출**

필드 추가 + getter:
```csharp
        [SerializeField] private DefenseDot.Systems.Cards.ArenaCardConfig cardConfig;
        [SerializeField] private DefenseDot.Systems.Cards.AbilityPool abilityPool;
        public DefenseDot.Systems.Cards.ArenaCardConfig CardConfig => cardConfig;
        public DefenseDot.Systems.Cards.AbilityPool AbilityPool => abilityPool;
```
`CreateMode`에서 생성한 `CoreAbilitySystem` 인스턴스를 `public CoreAbilitySystem CoreAbility { get; private set; }` 로 노출(생성 시 대입).

- [ ] **Step 4: GameManager 배선**

`Awake`/모델 생성부에 추가:
```csharp
        public LevelModel Level { get; private set; }
```
`Start`(혹은 모델 생성 직후), `Combat` 연결 + `Inject` 전:
```csharp
            var cfg = modeBootstrap is ArenaModeBootstrap arena ? arena.CardConfig : null;
            var pool = modeBootstrap is ArenaModeBootstrap arena2 ? arena2.AbilityPool : null;
            Level = new LevelModel(cfg != null ? cfg.KillsToNextLevel : (lv => Mathf.Max(3, 8 + lv * 4)));
            Combat.OnEnemyKilled += HandleEnemyKilledForLevel;
            ...
            var coreTarget = (modeBootstrap as ArenaModeBootstrap)?.CoreAbility as ICardCommandTarget;
            var cardCtx = new CardContext(Level, cfg, pool, coreTarget, Flow);
            uiRoot.Inject(hudContext, Flow, modeBootstrap.PlacementController, cardCtx);
```
핸들러:
```csharp
        private void HandleEnemyKilledForLevel(int reward) => Level.RegisterKill();
```
(필요 시 `OnDestroy`에서 `Combat.OnEnemyKilled -= HandleEnemyKilledForLevel`.)

> `using DefenseDot.Systems.Abilities;`(ICardCommandTarget)·`DefenseDot.UI;`(CardContext) 추가. `Inject` 호출부 모두 새 인자 반영.

- [ ] **Step 5: 컴파일 확인** — `read_console` 에러 0(도메인 리로드 후).

- [ ] **Step 6: Commit (승인 후)**

```bash
git add Assets/Scripts/UI/CardContext.cs Assets/Scripts/UI/InGame/UIRoot.cs Assets/Scripts/Systems/Management/GameManager.cs Assets/Scripts/Systems/Mode/ArenaModeBootstrap.cs
git commit -m "feat: 카드 선택 허브 배선(CardContext·UIRoot·GameManager·Bootstrap)"
```

---

### Task 8: CardBackground 셰이더 + 티어 머티리얼 (MCP)

**Files:**
- Create: `Assets/Shaders/UI/CardBackground.shader`
- Create: `Assets/Materials/UI/Card_New.mat`, `Assets/Materials/UI/Card_Upgrade.mat` (MCP)

**검증:** MCP 스크린샷 — 머티리얼 적용 쿼드/이미지가 그라데이션+글로우 표시.

- [ ] **Step 1: 셰이더 작성**

`CardBackground.shader` (URP/UI 호환, 세로 그라데이션 + 외곽 글로우):
```shaderlab
Shader "DefenseDot/UI/CardBackground"
{
    Properties
    {
        _ColorTop ("Color Top", Color) = (0.11,0.12,0.15,1)
        _ColorBottom ("Color Bottom", Color) = (0.04,0.05,0.08,1)
        _GlowColor ("Glow Color", Color) = (0.5,0.8,1,1)
        _GlowIntensity ("Glow Intensity", Range(0,3)) = 0.6
    }
    SubShader
    {
        Tags { "Queue"="Transparent" "RenderType"="Transparent" "RenderPipeline"="UniversalPipeline" }
        Blend SrcAlpha OneMinusSrcAlpha
        Cull Off ZWrite Off
        Pass
        {
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            struct A { float4 pos:POSITION; float2 uv:TEXCOORD0; };
            struct V { float4 pos:SV_POSITION; float2 uv:TEXCOORD0; };
            float4 _ColorTop; float4 _ColorBottom; float4 _GlowColor; float _GlowIntensity;
            V vert(A i){ V o; o.pos=TransformObjectToHClip(i.pos.xyz); o.uv=i.uv; return o; }
            half4 frag(V i):SV_Target
            {
                half4 col = lerp(_ColorBottom, _ColorTop, i.uv.y);
                float2 d = abs(i.uv - 0.5) * 2.0;          // 0(중앙)~1(가장자리)
                float edge = max(d.x, d.y);
                float glow = smoothstep(0.7, 1.0, edge) * _GlowIntensity;
                col.rgb += _GlowColor.rgb * glow;
                return col;
            }
            ENDHLSL
        }
    }
}
```

- [ ] **Step 2: 셰이더 컴파일 확인** — MCP `manage_asset`(refresh) 후 `read_console` 에러 0.

- [ ] **Step 3: 머티리얼 2종 생성** — MCP `manage_material`:
  - `Card_New.mat`: shader=`DefenseDot/UI/CardBackground`, _ColorTop/_ColorBottom=중성 그레이, _GlowColor=흰빛
  - `Card_Upgrade.mat`: _ColorTop/_Bottom=청색 계열, _GlowColor=하늘색
  (원작 CARD_TIERS의 new/upgrade 색 참조)

- [ ] **Step 4: 시각 검증** — 임시 UI Image에 머티리얼 적용 후 MCP `manage_camera`/게임뷰 스크린샷으로 그라데이션+글로우 확인. 임시 오브젝트 제거.

- [ ] **Step 5: Commit (승인 후)**

```bash
git add Assets/Shaders/UI/CardBackground.shader Assets/Materials/UI/
git commit -m "feat: 카드 배경 셰이더·티어 머티리얼 추가"
```

---

### Task 9: SO 인스턴스 생성 (MCP)

**Files (생성):**
- `Assets/Data/Cards/ArenaCardConfig.asset`
- `Assets/Data/Cards/AbilityPool.asset`
- `Assets/Data/Cards/CardTierSet.asset`

- [ ] **Step 1: CardTierSet.asset 생성** — MCP `manage_scriptable_object`: New/Upgrade 두 TierStyle(보더·bgTop/Bottom·glow 색). 원작 색 참조.
- [ ] **Step 2: AbilityPool.asset 생성** — `abilities` = [Ability_Shot, Ability_Orbital, Ability_AreaWave] 참조 할당.
- [ ] **Step 3: ArenaCardConfig.asset 생성** — 기본값(pause=on, choiceCount=3, curveBase=8, perLevel=4) + `tierSet`=위 CardTierSet 할당.
- [ ] **Step 4: 검증** — MCP로 세 에셋 inspector 값 읽어 참조 채워졌는지 확인.
- [ ] **Step 5: Commit (승인 후)**

```bash
git add Assets/Data/Cards/
git commit -m "feat: 카드 설정·풀·티어 SO 인스턴스 추가"
```

---

### Task 10: CardSelection_Panel 프리팹 (MCP)

**Files (생성):**
- `Assets/Prefabs/UI/CardSelection_Panel.prefab`

**검증:** MCP 스크린샷 — 암전 + 3카드 레이아웃·neodgm·색 확인.

- [ ] **Step 1: 계층 구성** — MCP `manage_gameobject`/`manage_ui`:
  - Root(CanvasGroup) > Dim(Image, 검정 알파 .8, 전체 stretch) > Title(TMP, neodgm "LEVEL UP") > Cards(HorizontalLayoutGroup) > Card0..2
  - 각 Card: Button + Background(Image, material=Card_New) + Border(Image) + Icon(Image) + Name(TMP) + Kind(TMP) + Desc(TMP), 모두 neodgm SDF
- [ ] **Step 2: 파티클 추가** — MCP `execute_code`로 각 Card에 ParticleSystem(글로우 스파클) 구성: main(작은 수명·additive), emission(rate 낮음), shape(카드 영역). 기본 Stop 상태.
- [ ] **Step 3: CardSelectionView 부착·배선** — `CardSelectionView` 컴포넌트 추가, `canvasGroup/root/titleText/items[3]/config` 직렬화 필드에 위 오브젝트·`ArenaCardConfig.asset` 할당.
- [ ] **Step 4: 프리팹화** — `manage_prefabs`로 `CardSelection_Panel.prefab` 저장.
- [ ] **Step 5: fade 연출 보강** — `CardSelectionView.StartFade`를 `unscaledDeltaTime` 트윈(0→1, ~0.3s)으로 교체(정지 중 동작). `read_console` 에러 0.
- [ ] **Step 6: 시각 검증** — 프리팹을 씬에 임시 배치, MCP 스크린샷으로 레이아웃·색·폰트 확인. 더미 데이터로 Show 호출(`execute_code`).
- [ ] **Step 7: Commit (승인 후)**

```bash
git add Assets/Prefabs/UI/CardSelection_Panel.prefab Assets/Scripts/UI/Views/CardSelectionView.cs
git commit -m "feat: 카드 선택 모달 프리팹·fade 연출 추가"
```

---

### Task 11: 씬 통합 + 플레이 검증 (MCP)

**Files (수정):**
- `Assets/Scenes/ArenaScene.unity` (프리팹 배치·참조 할당)

- [ ] **Step 1: 모달 배치** — ArenaScene UIRoot Canvas 하위에 `CardSelection_Panel` 배치, `UIRoot.cardSelectionView`에 할당.
- [ ] **Step 2: 부트스트랩 참조** — `ArenaModeBootstrap`의 `cardConfig`=ArenaCardConfig.asset, `abilityPool`=AbilityPool.asset 할당. `CoreAbility` 노출 확인.
- [ ] **Step 3: 플레이 진입** — MCP `manage_editor`(play). `read_console` 런타임 에러 0 확인.
- [ ] **Step 4: 레벨업 유발** — 정상 플레이로 적 처치 누적(또는 `execute_code`로 `Combat.RegisterKill` 반복 호출). 모달 등장 + `Time.timeScale==0` 확인(스크린샷).
- [ ] **Step 5: 카드 선택 검증** — 카드 클릭(`execute_code`로 `OnCardSelected` 또는 버튼). 신규=Loadout.Actives 증가 & 실제 발사(스크린샷), 레벨업=level 증가. `timeScale==1` 복귀 확인.
- [ ] **Step 6: 엣지 확인** — 모두 보유+max 상태에서 레벨업 시 모달 미표시(레벨만) 확인(로그).
- [ ] **Step 7: 회귀** — `run_tests`(EditMode 전체) 통과. 플레이 종료.
- [ ] **Step 8: Commit (승인 후)**

```bash
git add Assets/Scenes/ArenaScene.unity
git commit -m "feat: 카드 선택 허브 씬 통합 및 플레이 검증"
```

---

## Self-Review (작성자 점검)

**1. 스펙 커버리지:**
- 레벨업 곡선 → Task 1(config)·2(LevelModel) ✓
- 카드 3장 생성/슬롯/폴백 → Task 3 ✓
- 선택 적용+러너 동기화 → Task 4·6 ✓
- 정지 토글+소유권 → Task 6(테스트)·Task 11(플레이) ✓
- 모달 UI(셰이더/파티클/희귀도색) → Task 8·10 ✓
- SO 분리(Config/Pool/TierSet) → Task 1·9 ✓
- 배선(Inject/GameManager/Bootstrap) → Task 7 ✓
- 엣지(0장/다중 pending) → Task 6·11 ✓
- 테스트(EditMode 단위 + 플레이) → 전 Task ✓

**2. 플레이스홀더:** 없음 — 모든 코드 스텝에 실제 코드 포함. MCP 스텝은 구체 오브젝트/속성 명시.

**3. 타입 일관성:** `CardChoice`(action/data/instance/from/toLevel/tier), `ICardCommandTarget`(Loadout/AddAbility/LevelUpAbility), `ICardSelectionView`(Show/Hide/OnCardSelected), `LevelModel`(RegisterKill/TryConsumePending/OnLevelUp), `CardChoiceGenerator.Generate(loadout,pool,config,level)` — 전 Task에서 동일 사용 확인.

**주의(구현 중 확인 필요):** `AbilityRunner.Equip` 실제 시그니처, `CoreAbilitySystem`의 `loadout`/`runner` 필드명, `GameFlowModel`/`GamePhase` 위치, `UIRoot.Inject` 기존 시그니처 — 모두 매핑 단계서 실제 코드에 맞춰 조정(스펙 §5.9 근거).
