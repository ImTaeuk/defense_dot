using NUnit.Framework;
using UnityEngine;
using DefenseDot.Core;
using DefenseDot.Systems.Abilities;

namespace DefenseDot.Tests.EditMode
{
    public class AbilityLoadoutUpgradeTests
    {
        private sealed class StubActive : ActiveAbilityData
        {
            protected override void Fire(in AbilityContext ctx, AbilityInstance self, ITargetable target) { }
        }
        private sealed class StubCombat : ICombatState
        {
            public int Round { get; set; }
            public int AliveEnemyCount => 0;
        }
        private static StubActive NewActive()
        {
            StubActive a = ScriptableObject.CreateInstance<StubActive>();
            a.maxLevel = 5;
            return a;
        }

        [Test]
        public void TryAdd_StampsAcquiredRoundFromCombatState()
        {
            AbilityLoadout lo = new AbilityLoadout(6, 6);
            lo.Modifiers.combatState = new StubCombat { Round = 7 };
            lo.TryAdd(NewActive());
            Assert.AreEqual(7, lo.Actives[0].acquiredRound);
        }

        [Test]
        public void TryAdd_WithoutCombatState_DefaultsToOne()
        {
            AbilityLoadout lo = new AbilityLoadout(6, 6);
            lo.TryAdd(NewActive());
            Assert.AreEqual(1, lo.Actives[0].acquiredRound);
        }

        [Test]
        public void OnChanged_FiresOnAddLevelUpRemove()
        {
            AbilityLoadout lo = new AbilityLoadout(6, 6);
            int fired = 0;
            lo.OnChanged += () => fired++;
            lo.TryAdd(NewActive());
            lo.LevelUp(lo.Actives[0]);
            lo.Remove(lo.Actives[0]);
            Assert.AreEqual(3, fired);
        }
    }
}
