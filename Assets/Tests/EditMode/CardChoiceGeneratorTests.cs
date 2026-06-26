using NUnit.Framework;
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
            public override void Tick(in AbilityContext ctx, AbilityInstance self, float deltaTime) { }
            protected override void Fire(in AbilityContext ctx, AbilityInstance self, ITargetable target) { }
        }

        private static StubActive Active(int max = 5)
        {
            var a = ScriptableObject.CreateInstance<StubActive>();
            a.maxLevel = max;
            return a;
        }

        private static ArenaCardConfig Config(int count = 3)
        {
            var c = ScriptableObject.CreateInstance<ArenaCardConfig>();
            c.choiceCount = count;
            c.newCardChanceEarly = 1f; c.newCardChanceLate = 1f;
            return c;
        }

        private static AbilityPool Pool(params AbilityData[] abs)
        {
            var p = ScriptableObject.CreateInstance<AbilityPool>();
            p.abilities.AddRange(abs);
            return p;
        }

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
