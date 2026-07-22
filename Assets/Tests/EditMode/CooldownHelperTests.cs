using NUnit.Framework;
using UnityEngine;
using DefenseDot.Systems.Abilities;
using DefenseDot.Systems.Combat;

namespace DefenseDot.Tests.EditMode
{
    public class CooldownHelperTests
    {
        // 헬퍼(protected) 검증용 테스트 능력 — Tick에서 헬퍼 호출, Fire 횟수 집계
        private sealed class CdAbility : ActiveAbilityData
        {
            public int fireCount;
            public void Tick(in AbilityContext ctx, AbilityInstance self, float dt)
            {
                if (!TickCooldown(self, dt)) return;
                fireCount++;
                ResetCooldown(self, ctx);
            }
            protected override void Fire(in AbilityContext ctx, AbilityInstance self, DefenseDot.Core.ITargetable target) { }
            public void ResetForTest(AbilityInstance self, in AbilityContext ctx) { ResetCooldown(self, ctx); }
        }

        private static AbilityContext Ctx()
        {
            return new AbilityContext(null, Vector3.zero, null, new AbilityModifiers(), null, new CombatStats());
        }

        [Test]
        public void TickCooldown_FiresWhenElapsed()
        {
            var a = ScriptableObject.CreateInstance<CdAbility>();
            a.baseCooldown = 1f;
            var inst = new AbilityInstance(a, 1) { cooldownRemaining = 1f };
            a.Tick(Ctx(), inst, 0.4f);   // 0.6
            a.Tick(Ctx(), inst, 0.4f);   // 0.2
            Assert.AreEqual(0, a.fireCount);
            a.Tick(Ctx(), inst, 0.4f);   // -0.2 → fire
            Assert.AreEqual(1, a.fireCount);
        }

        [Test]
        public void ResetCooldown_ClampsToFloor()
        {
            var a = ScriptableObject.CreateInstance<CdAbility>();
            a.baseCooldown = 1f;
            var inst = new AbilityInstance(a, 1) { cooldownRemaining = 0f };
            var stats = new CombatStats { cooldownRate = 0.01f };
            var ctx = new AbilityContext(null, Vector3.zero, null, new AbilityModifiers(), null, stats);
            a.ResetForTest(inst, ctx);   // 1 * 0.01 = 0.01 → 0.05 클램프
            Assert.AreEqual(0.05f, inst.cooldownRemaining, 0.0001f);
        }
    }
}
