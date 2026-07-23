using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using DefenseDot.Systems.Abilities;
using DefenseDot.Systems.Cards;

namespace DefenseDot.Tests.EditMode
{
    public class FusionSystemMultiSetTests
    {
        private sealed class StubActive : ActiveAbilityData
        {
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

        private static AbilityInstance AddMaxed(AbilityLoadout lo, AbilityData d)
        {
            lo.TryAdd(d);
            AbilityInstance inst = lo.Actives[lo.Actives.Count - 1];
            while (inst.level < d.maxLevel)
                lo.LevelUp(inst);
            return inst;
        }

        [Test]
        public void CollectOffers_UnionOfAllSets()
        {
            StubActive a = Ability(), b = Ability(), r1 = Ability();
            StubActive c = Ability(), d = Ability(), r2 = Ability();
            var lo = new AbilityLoadout(8, 6);
            AddMaxed(lo, a); AddMaxed(lo, b); AddMaxed(lo, c); AddMaxed(lo, d);
            var svc = new FusionSystem(new[] { Lineage(a, b, r1), Lineage(c, d, r2) });

            var into = new List<Card>();
            svc.CollectOffers(lo, into, 5);

            Assert.AreEqual(2, into.Count, "두 세트의 레시피가 합쳐져 제시");
        }

        [Test]
        public void CollectOffers_DuplicateResult_OfferedOnce()
        {
            StubActive a = Ability(), b = Ability(), r = Ability();
            var lo = new AbilityLoadout(8, 6);
            AddMaxed(lo, a); AddMaxed(lo, b);
            // 공통·캐릭터 세트가 같은 레시피를 중복 정의
            var svc = new FusionSystem(new[] { Lineage(a, b, r), Lineage(a, b, r) });

            var into = new List<Card>();
            svc.CollectOffers(lo, into, 5);

            Assert.AreEqual(1, into.Count, "같은 result 는 1회만 제시");
        }

        [Test]
        public void IsResult_ChecksAllSets()
        {
            StubActive a = Ability(), b = Ability(), r1 = Ability();
            StubActive c = Ability(), d = Ability(), r2 = Ability();
            var svc = new FusionSystem(new[] { Lineage(a, b, r1), Lineage(c, d, r2) });

            Assert.IsTrue(svc.IsResult(r1));
            Assert.IsTrue(svc.IsResult(r2), "둘째 세트의 result 도 인식");
            Assert.IsFalse(svc.IsResult(a));
        }

        [Test]
        public void NullSets_AreSkipped()
        {
            StubActive a = Ability(), b = Ability(), r = Ability();
            var svc = new FusionSystem(new FusionRecipeSet[] { null, Lineage(a, b, r) });
            Assert.IsTrue(svc.IsResult(r), "null 세트는 건너뛰고 나머지 정상");
        }
    }
}
