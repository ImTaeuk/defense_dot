using NUnit.Framework;
using DefenseDot.Core;
using DefenseDot.Systems.Abilities;

namespace DefenseDot.Tests.EditMode
{
    public class AbilityModifiersTests
    {
        private sealed class Tgt : ICombatTargetInfo
        {
            public bool IsBoss { get; set; }
            public float HealthRatio { get; set; } = 1f;
        }

        private sealed class St : ICombatState
        {
            public int Round { get; set; }
            public int AliveEnemyCount { get; set; }
        }

        [Test]
        public void NoPassives_MultiplierIsOne()
        {
            var m = new AbilityModifiers();
            Assert.AreEqual(1f, m.ConditionalMultiplier(new Tgt()), 1e-4f);
        }

        [Test]
        public void Onslaught_OnlyWhenHpAboveHalf()
        {
            var m = new AbilityModifiers { onslaughtLevel = 2 }; // +24%
            Assert.AreEqual(1.24f, m.ConditionalMultiplier(new Tgt { HealthRatio = 0.8f }), 1e-4f);
            Assert.AreEqual(1f, m.ConditionalMultiplier(new Tgt { HealthRatio = 0.4f }), 1e-4f);
        }

        [Test]
        public void Cull_AppliesToNonBossOnly()
        {
            var m = new AbilityModifiers { cullLevel = 3 }; // +30%
            Assert.AreEqual(1.30f, m.ConditionalMultiplier(new Tgt { IsBoss = false }), 1e-4f);
            Assert.AreEqual(1f, m.ConditionalMultiplier(new Tgt { IsBoss = true }), 1e-4f);
        }

        [Test]
        public void Press_ScalesWithAliveCountCapped()
        {
            var m = new AbilityModifiers { pressLevel = 4, combatState = new St { AliveEnemyCount = 3 } };
            Assert.AreEqual(1.12f, m.ConditionalMultiplier(new Tgt()), 1e-4f);   // min(0.6, 0.12)
            m.combatState = new St { AliveEnemyCount = 100 };
            Assert.AreEqual(1.60f, m.ConditionalMultiplier(new Tgt()), 1e-4f);   // min(0.6, 4.0)
        }

        [Test]
        public void Awaken_ScalesWithRoundCapped()
        {
            var m = new AbilityModifiers { awakenLevel = 2, combatState = new St { Round = 3 } };
            Assert.AreEqual(1.06f, m.ConditionalMultiplier(new Tgt()), 1e-4f);   // min(0.4, 0.06)
        }

        [Test]
        public void Stacks_Multiplicatively()
        {
            var m = new AbilityModifiers { cullLevel = 3, awakenLevel = 2, combatState = new St { Round = 3 } };
            Assert.AreEqual(1.30f * 1.06f, m.ConditionalMultiplier(new Tgt { IsBoss = false }), 1e-4f);
        }
    }
}
