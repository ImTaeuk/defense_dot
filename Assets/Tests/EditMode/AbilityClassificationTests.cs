using NUnit.Framework;
using UnityEngine;
using DefenseDot.Systems.Abilities;

namespace DefenseDot.Tests.EditMode
{
    public sealed class AbilityClassificationTests
    {
        private sealed class Dummy : AbilityData { }

        [Test]
        public void Tier_DefaultsToUnset()
        {
            Dummy d = ScriptableObject.CreateInstance<Dummy>();
            Assert.AreEqual(AbilityTier.Unset, d.tier, "미저작 tier는 Unset(0)이어야 Basic과 구분됨");
        }

        [Test]
        public void UnsetIsZero_BasicIsOne()
        {
            Assert.AreEqual(0, (int)AbilityTier.Unset);
            Assert.AreEqual(1, (int)AbilityTier.Basic);
        }

        [Test]
        public void Kind_ProjectileIsZero()
        {
            Assert.AreEqual(0, (int)AbilityKind.Projectile);
            Assert.AreEqual(5, (int)AbilityKind.Buff);
        }
    }
}
