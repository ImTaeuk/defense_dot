using NUnit.Framework;
using UnityEngine;
using DefenseDot.Core;
using DefenseDot.Systems.Abilities;
using DefenseDot.Systems.Combat;

namespace DefenseDot.Tests.EditMode
{
    public sealed class CooldownCarryTests
    {
        private sealed class CarryAbility : ActiveAbilityData
        {
            public float fixedCooldown = 1f;
            public override float CooldownAtLevel(int level) { return fixedCooldown; }
            protected override void Fire(in AbilityContext ctx, AbilityInstance self, ITargetable target) { }
            public void ResetForTest(AbilityInstance self, in AbilityContext ctx) { ResetCooldown(self, ctx); }
        }

        private static AbilityContext MakeCtx(CombatStats stats)
        {
            return new AbilityContext(Vector3.zero, null, new AbilityModifiers(), null, stats);
        }

        [Test]
        public void ResetCooldown_CarriesOvershoot()
        {
            CarryAbility a = ScriptableObject.CreateInstance<CarryAbility>();
            a.fixedCooldown = 1f;
            AbilityInstance inst = new AbilityInstance(a, 1);
            inst.cooldownRemaining = -0.2f;
            a.ResetForTest(inst, MakeCtx(new CombatStats()));
            Assert.AreEqual(0.8f, inst.cooldownRemaining, 1e-4f);
        }

        [Test]
        public void ResetCooldown_AppliesCooldownRate()
        {
            CarryAbility a = ScriptableObject.CreateInstance<CarryAbility>();
            a.fixedCooldown = 1f;
            AbilityInstance inst = new AbilityInstance(a, 1);
            inst.cooldownRemaining = 0f;
            CombatStats stats = new CombatStats();
            stats.cooldownRate = 0.5f;
            a.ResetForTest(inst, MakeCtx(stats));
            Assert.AreEqual(0.5f, inst.cooldownRemaining, 1e-4f);
        }
    }
}
