using NUnit.Framework;
using DefenseDot.Systems.Combat;

namespace DefenseDot.Tests.EditMode
{
    public sealed class CombatStatsTests
    {
        [Test]
        public void Defaults_AreNeutral()
        {
            CombatStats stats = new CombatStats();
            Assert.AreEqual(1f, stats.attackSpeed, 1e-4f);
            Assert.AreEqual(1f, stats.cooldownRate, 1e-4f);
        }
    }
}
