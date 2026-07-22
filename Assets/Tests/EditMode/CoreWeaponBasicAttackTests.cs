using NUnit.Framework;
using UnityEngine;
using DefenseDot.Core;
using DefenseDot.Systems.Abilities;
using DefenseDot.Systems.Combat;

namespace DefenseDot.Tests.EditMode
{
    public sealed class CoreWeaponBasicAttackTests
    {
        private sealed class BasicActive : AutoAbilityData
        {
            public int fireCount;
            protected override void Fire(in AbilityContext ctx, AbilityInstance self, ITargetable target) { fireCount++; }
        }
        private sealed class OtherActive : AutoAbilityData
        {
            public int fireCount;
            protected override void Fire(in AbilityContext ctx, AbilityInstance self, ITargetable target) { fireCount++; }
        }
        private sealed class StubTarget : ITargetable
        {
            public Vector3 Position => Vector3.zero;
            public bool IsActive => true;
            public ActorState CurrentState => ActorState.Moving;
            public void SetState(ActorState newState) { }
            public event System.Action<ActorState> StateChanged;
        }

        private static BasicActive MakeBasic()
        {
            BasicActive a = ScriptableObject.CreateInstance<BasicActive>();
            a.tier = AbilityTier.Basic;
            return a;
        }
        private static AbilityContext Ctx()
        {
            return new AbilityContext(null, Vector3.zero, null, new AbilityModifiers(), null, new CombatStats());
        }

        [Test]
        public void FireAll_FiresOnlyBasicAttack()
        {
            var loadout = new AbilityLoadout();
            BasicActive basic = MakeBasic();
            OtherActive other = ScriptableObject.CreateInstance<OtherActive>();
            other.tier = AbilityTier.Signature;
            loadout.TryAdd(basic);
            loadout.TryAdd(other);
            var weapon = new CoreWeapon(loadout, null);

            weapon.AimAt(new StubTarget());
            weapon.FireAll(Ctx());

            Assert.AreEqual(1, basic.fireCount, "기본 공격은 발사");
            Assert.AreEqual(0, other.fireCount, "그 외 능력은 CoreWeapon이 발사하지 않음");
        }

        [Test]
        public void BasicAttack_IsTheBasicTier()
        {
            var loadout = new AbilityLoadout();
            BasicActive basic = MakeBasic();
            loadout.TryAdd(basic);
            var weapon = new CoreWeapon(loadout, null);
            Assert.AreSame(basic, weapon.BasicAttack.data);
        }

        [Test]
        public void FireAll_WithoutAimedTarget_DoesNothing()
        {
            var loadout = new AbilityLoadout();
            BasicActive basic = MakeBasic();
            loadout.TryAdd(basic);
            var weapon = new CoreWeapon(loadout, null);

            weapon.FireAll(Ctx());

            Assert.AreEqual(0, basic.fireCount, "AimAt 없이는 발사하지 않음");
        }
    }
}
