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
            protected override void Fire(in AbilityContext ctx, AbilityInstance self, DefenseDot.Core.ITargetable target) { }
        }

        private sealed class LifeAbility : ActiveAbilityData, IAbilityLifecycle
        {
            public int equips, unequips;
            public override void Tick(in AbilityContext ctx, AbilityInstance self, float dt) { }
            protected override void Fire(in AbilityContext ctx, AbilityInstance self, DefenseDot.Core.ITargetable target) { }
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
