using NUnit.Framework;
using UnityEngine;
using DefenseDot.Systems.Abilities;
using DefenseDot.Systems.Economy;

namespace DefenseDot.Tests.EditMode
{
    public class AbilityCostExtensionsTests
    {
        private sealed class StubData : AbilityData { }

        private static AbilityInstance Ability(int baseCost, int level, int acquiredRound)
        {
            StubData d = ScriptableObject.CreateInstance<StubData>();
            d.baseCost = baseCost;
            d.maxLevel = 99;
            AbilityInstance inst = new AbilityInstance(d, level);
            inst.acquiredRound = acquiredRound;
            return inst;
        }

        private static AbilityUpgradeConfig Config()
        {
            AbilityUpgradeConfig c = ScriptableObject.CreateInstance<AbilityUpgradeConfig>();
            c.levelSlope = 0.10f;
            c.roundInflation = 0.05f;
            c.maxDiscountRate = 0.55f;
            c.refundRatio = 0.40f;
            return c;
        }

        [Test]
        public void UpgradeCost_BaselineLevel1Round1_Is63()
        {
            // lvScale=(1+1)+1*0.1=2.1, roundMul=1, costMul=1 → ceil(30*2.1)=63
            Assert.AreEqual(63, Ability(30, 1, 1).UpgradeCost(Config()));
        }

        [Test]
        public void UpgradeCost_HigherLevel_CostsMore()
        {
            Assert.Greater(Ability(30, 3, 1).UpgradeCost(Config()), Ability(30, 1, 1).UpgradeCost(Config()));
        }

        [Test]
        public void UpgradeCost_LaterAcquiredRound_CostsMore()
        {
            Assert.Greater(Ability(30, 1, 11).UpgradeCost(Config()), Ability(30, 1, 1).UpgradeCost(Config()));
        }

        [Test]
        public void RefundValue_Level3_Is65()
        {
            // lv1: ceil(63*0.4)=26, lv2: ceil(96*0.4)=39 → 65
            Assert.AreEqual(65, Ability(30, 3, 1).RefundValue(Config()));
        }
    }
}
