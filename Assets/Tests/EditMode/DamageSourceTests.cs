using NUnit.Framework;
using UnityEngine;
using DefenseDot.Systems.Abilities;
using DefenseDot.Core;

namespace DefenseDot.Tests.EditMode
{
    public class DamageSourceTests
    {
        private sealed class StubActive : ActiveAbilityData
        {
            public override float ValueAtLevel(int level) { return level * 10f; }
            public override void Tick(in AbilityContext ctx, AbilityInstance self, float deltaTime) { }
            protected override void Fire(in AbilityContext ctx, AbilityInstance self, ITargetable target) { }
        }

        [Test]
        public void Resolve_UsesLiveLevelAndBonus()
        {
            var a = ScriptableObject.CreateInstance<StubActive>();
            var inst = new AbilityInstance(a, 1);
            var mods = new AbilityModifiers();
            var src = new DamageSource(a, inst, mods);

            Assert.AreEqual(10f, src.Resolve(null), 1e-4f); // lv1*10 + 0

            // 명중 전 레벨업 + 패시브 보정 → 명중 시 라이브 반영
            inst.level = 3;
            mods.damageBonus = 5f;
            Assert.AreEqual(35f, src.Resolve(null), 1e-4f); // lv3*10 + 5
        }

        [Test]
        public void Resolve_NullSafe()
        {
            var src = new DamageSource(null, null, null);
            Assert.AreEqual(0f, src.Resolve(null), 1e-4f);
        }
    }
}
