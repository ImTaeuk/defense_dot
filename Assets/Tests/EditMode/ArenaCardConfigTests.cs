using NUnit.Framework;
using UnityEngine;
using DefenseDot.Systems.Cards;

namespace DefenseDot.Tests.EditMode
{
    public class ArenaCardConfigTests
    {
        private static ArenaCardConfig NewConfig()
        {
            var c = ScriptableObject.CreateInstance<ArenaCardConfig>();
            c.curveBase = 8; c.curvePerLevel = 4;
            return c;
        }

        [Test]
        public void KillsToNextLevel_FollowsCurve()
        {
            var c = NewConfig();
            Assert.AreEqual(12, c.KillsToNextLevel(1)); // 8 + 1*4
            Assert.AreEqual(28, c.KillsToNextLevel(5)); // 8 + 5*4
        }

        [Test]
        public void KillsToNextLevel_FloorsAtThree()
        {
            var c = NewConfig(); c.curveBase = 0; c.curvePerLevel = 0;
            Assert.AreEqual(3, c.KillsToNextLevel(1));
        }
    }
}
