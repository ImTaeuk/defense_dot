using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using DefenseDot.Systems.Abilities;
using DefenseDot.Systems.Cards;

namespace DefenseDot.Tests.EditMode
{
    public class FusionSystemTests
    {
        private sealed class StubActive : ActiveAbilityData
        {
            public override void Tick(in AbilityContext ctx, AbilityInstance self, float deltaTime) { }
            protected override void Fire(in AbilityContext ctx, AbilityInstance self, DefenseDot.Core.ITargetable target) { }
        }

        private sealed class StubCore : IAbilityCommandTarget
        {
            public AbilityLoadout Loadout { get; } = new AbilityLoadout(6, 6);
            public int removed;
            public AbilityInstance AddAbility(AbilityData d)
            {
                if (!Loadout.TryAdd(d))
                    return null;
                return Loadout.Actives[Loadout.Actives.Count - 1];
            }
            public void LevelUpAbility(AbilityInstance i) => Loadout.LevelUp(i);
            public void RemoveAbility(AbilityInstance i) { removed++; Loadout.Remove(i); }
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

        private static List<Card> Offers(FusionSystem svc, AbilityLoadout lo)
        {
            var into = new List<Card>();
            svc.CollectOffers(lo, into, 3);
            return into;
        }

        [Test]
        public void CollectOffers_BothMaxedResultUnowned_Offered()
        {
            StubActive a = Ability(), b = Ability(), r = Ability();
            AbilityLoadout lo = new AbilityLoadout(6, 6);
            AddMaxed(lo, a); AddMaxed(lo, b);
            var offers = Offers(new FusionSystem(Lineage(a, b, r)), lo);
            Assert.AreEqual(1, offers.Count);
            Assert.AreEqual(CardApplyType.Fuse, offers[0].applyType);
            Assert.AreEqual(r, offers[0].data);
        }

        [Test]
        public void CollectOffers_MaterialNotMaxed_None()
        {
            StubActive a = Ability(), b = Ability(), r = Ability();
            AbilityLoadout lo = new AbilityLoadout(6, 6);
            AddMaxed(lo, a); lo.TryAdd(b);
            Assert.AreEqual(0, Offers(new FusionSystem(Lineage(a, b, r)), lo).Count);
        }

        [Test]
        public void CollectOffers_ResultAlreadyOwned_None()
        {
            StubActive a = Ability(), b = Ability(), r = Ability();
            AbilityLoadout lo = new AbilityLoadout(6, 6);
            AddMaxed(lo, a); AddMaxed(lo, b); lo.TryAdd(r);
            Assert.AreEqual(0, Offers(new FusionSystem(Lineage(a, b, r)), lo).Count);
        }

        [Test]
        public void CollectOffers_MaterialMissing_None()
        {
            StubActive a = Ability(), b = Ability(), r = Ability();
            AbilityLoadout lo = new AbilityLoadout(6, 6);
            AddMaxed(lo, a);
            Assert.AreEqual(0, Offers(new FusionSystem(Lineage(a, b, r)), lo).Count);
        }

        [Test]
        public void Apply_ConsumesTwoMaxedAndAddsResult()
        {
            StubActive a = Ability(), b = Ability(), r = Ability();
            StubCore core = new StubCore();
            AddMaxed(core.Loadout, a); AddMaxed(core.Loadout, b);
            FusionSystem svc = new FusionSystem(Lineage(a, b, r));
            Card card = Card.FusionCard(r, a, b, CardTier.Fusion);

            svc.Apply(core, card);

            Assert.AreEqual(2, core.removed, "재료 2개 소진");
            Assert.IsFalse(core.Loadout.Contains(a));
            Assert.IsFalse(core.Loadout.Contains(b));
            Assert.IsTrue(core.Loadout.Contains(r), "결과 추가");
        }

        [Test]
        public void Apply_MaterialNotMaxed_NoMutation()
        {
            StubActive a = Ability(), b = Ability(), r = Ability();
            StubCore core = new StubCore();
            AddMaxed(core.Loadout, a); core.Loadout.TryAdd(b);   // b는 비MAX
            FusionSystem svc = new FusionSystem(Lineage(a, b, r));
            Card card = Card.FusionCard(r, a, b, CardTier.Fusion);

            svc.Apply(core, card);

            Assert.AreEqual(0, core.removed, "재검증 실패 시 소진 없음");
            Assert.IsTrue(core.Loadout.Contains(a));
            Assert.IsTrue(core.Loadout.Contains(b));
            Assert.IsFalse(core.Loadout.Contains(r), "결과 미부여");
        }

        [Test]
        public void IsResult_DistinguishesResultFromMaterial()
        {
            StubActive a = Ability(), b = Ability(), r = Ability();
            FusionSystem svc = new FusionSystem(Lineage(a, b, r));
            Assert.IsTrue(svc.IsResult(r));
            Assert.IsFalse(svc.IsResult(a));
        }
    }
}
