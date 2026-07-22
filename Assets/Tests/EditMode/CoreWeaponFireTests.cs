using NUnit.Framework;
using UnityEngine;
using DefenseDot.Core;
using DefenseDot.Systems.Abilities;
using DefenseDot.Systems.Combat;

namespace DefenseDot.Tests.EditMode
{
    public class CoreWeaponFireTests
    {
        private sealed class CountMain : MainAbilityData
        {
            public int fires;
            protected override void Fire(in AbilityContext ctx, AbilityInstance self, ITargetable target) { fires++; }
        }

        private sealed class CountSub : SubAbilityData
        {
            public int fires;
            protected override void Fire(in AbilityContext ctx, AbilityInstance self, ITargetable target) { fires++; }
        }

        private sealed class FakeTarget : ITargetable
        {
            public Vector3 Position => Vector3.zero;
            public bool IsActive => true;
            public ActorState CurrentState => ActorState.Moving;
            public void SetState(ActorState newState) { }
            public event System.Action<ActorState> StateChanged;
        }

        private static AbilityContext Ctx()
            => new AbilityContext(null, Vector3.zero, null, new AbilityModifiers(), null, new CombatStats());

        [Test]
        public void FireAll_FiresMainAndEverySub_Once()
        {
            var loadout = new AbilityLoadout();
            var main = ScriptableObject.CreateInstance<CountMain>();
            var subA = ScriptableObject.CreateInstance<CountSub>();
            var subB = ScriptableObject.CreateInstance<CountSub>();
            loadout.TryAdd(main);
            loadout.TryAdd(subA);
            loadout.TryAdd(subB);

            var weapon = new CoreWeapon(loadout, null);
            weapon.SetBaseAttackSpeed(1f);
            weapon.AimAt(new FakeTarget());
            weapon.FireAll(Ctx());

            Assert.AreEqual(1, main.fires);
            Assert.AreEqual(1, subA.fires);
            Assert.AreEqual(1, subB.fires);
        }

        [Test]
        public void FireAll_WithoutTarget_DoesNothing()
        {
            var loadout = new AbilityLoadout();
            var main = ScriptableObject.CreateInstance<CountMain>();
            loadout.TryAdd(main);

            var weapon = new CoreWeapon(loadout, null);
            weapon.FireAll(Ctx());

            Assert.AreEqual(0, main.fires);
        }

        [Test]
        public void Tick_WithoutMain_DoesNothing()
        {
            var loadout = new AbilityLoadout();
            var sub = ScriptableObject.CreateInstance<CountSub>();
            loadout.TryAdd(sub);

            var weapon = new CoreWeapon(loadout, null);
            weapon.SetBaseAttackSpeed(1f);
            weapon.Tick(Ctx(), 5f);

            Assert.AreEqual(0, sub.fires);
        }

        [Test]
        public void FindMainToReplace_ReturnsExistingMain_WhenIncomingIsMain()
        {
            var loadout = new AbilityLoadout();
            var equipped = ScriptableObject.CreateInstance<CountMain>();
            loadout.TryAdd(equipped);
            var weapon = new CoreWeapon(loadout, null);

            var incoming = ScriptableObject.CreateInstance<CountMain>();

            Assert.AreSame(weapon.Main, weapon.FindMainToReplace(incoming));
        }

        [Test]
        public void FindMainToReplace_ReturnsNull_WhenIncomingIsSub()
        {
            var loadout = new AbilityLoadout();
            loadout.TryAdd(ScriptableObject.CreateInstance<CountMain>());
            var weapon = new CoreWeapon(loadout, null);

            var incoming = ScriptableObject.CreateInstance<CountSub>();

            Assert.IsNull(weapon.FindMainToReplace(incoming));
        }

        [Test]
        public void FindMainToReplace_ReturnsNull_WhenNoMainEquipped()
        {
            var loadout = new AbilityLoadout();
            var weapon = new CoreWeapon(loadout, null);

            var incoming = ScriptableObject.CreateInstance<CountMain>();

            Assert.IsNull(weapon.FindMainToReplace(incoming));
        }
    }
}
