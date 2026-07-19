using NUnit.Framework;
using UnityEngine;
using DefenseDot.Systems.Abilities;
using DefenseDot.Systems.Cards;

namespace DefenseDot.Tests.EditMode
{
    /// <summary> CardGenerator와 FusionSystem의 통합(합성 카드 제시·결과 배제) 테스트. </summary>
    public class FusionCardTests
    {
        private sealed class StubActive : ActiveAbilityData
        {
            protected override void Fire(in AbilityContext ctx, AbilityInstance self, DefenseDot.Core.ITargetable target) { }
        }
        private static StubActive Ability()
        {
            StubActive a = ScriptableObject.CreateInstance<StubActive>();
            a.maxLevel = 3;
            return a;
        }
        private static FusionSystem Fusion(AbilityData a, AbilityData b, AbilityData r)
        {
            FusionRecipeSet lineage = ScriptableObject.CreateInstance<FusionRecipeSet>();
            lineage.recipes = new System.Collections.Generic.List<FusionRecipe> {
                new FusionRecipe { materialA = a, materialB = b, result = r } };
            return new FusionSystem(lineage);
        }

        [Test]
        public void Generate_AvailableFusion_IncludesFusionCard()
        {
            StubActive a = Ability(), b = Ability(), r = Ability();
            AbilityLoadout lo = new AbilityLoadout(6, 6);
            lo.TryAdd(a); lo.TryAdd(b);
            for (int i = 0; i < 2; i++) { lo.LevelUp(lo.Actives[i]); lo.LevelUp(lo.Actives[i]); }

            ArenaCardConfig config = ScriptableObject.CreateInstance<ArenaCardConfig>();
            config.choiceCount = 3;

            var choices = new CardGenerator(() => 0.5f).Generate(lo, null, config, 1, Fusion(a, b, r));

            bool hasFusion = false;
            foreach (var c in choices)
                if (c.applyType == CardApplyType.Fuse && c.data == r)
                    hasFusion = true;
            Assert.IsTrue(hasFusion, "가용 합성이 있으면 합성 카드 제시");
        }

        [Test]
        public void Generate_FusionResult_NotOfferedAsNewCard()
        {
            StubActive a = Ability(), b = Ability(), r = Ability();
            AbilityLoadout lo = new AbilityLoadout(6, 6);
            lo.TryAdd(a); lo.TryAdd(b);
            for (int i = 0; i < 2; i++) { lo.LevelUp(lo.Actives[i]); lo.LevelUp(lo.Actives[i]); }

            // 합성 결과 r 이 일반 풀에도 들어 있는 상황
            AbilityPool pool = ScriptableObject.CreateInstance<AbilityPool>();
            pool.abilities = new System.Collections.Generic.List<AbilityData> { r };

            ArenaCardConfig config = ScriptableObject.CreateInstance<ArenaCardConfig>();
            config.choiceCount = 3;

            var choices = new CardGenerator(() => 0.0f).Generate(lo, pool, config, 1, Fusion(a, b, r));

            foreach (var c in choices)
                Assert.IsFalse(c.applyType == CardApplyType.New && c.data == r, "합성 결과는 일반 New 카드로 제시되면 안 됨");
            bool hasFuse = false;
            foreach (var c in choices)
                if (c.applyType == CardApplyType.Fuse && c.data == r)
                    hasFuse = true;
            Assert.IsTrue(hasFuse, "합성 카드 자체는 제시되어야 함");
        }
    }
}
