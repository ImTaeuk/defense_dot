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
