using NUnit.Framework;
using UnityEngine;
using DefenseDot.Domain.Models;
using DefenseDot.Systems.Abilities;
using DefenseDot.Systems.Economy;

namespace DefenseDot.Tests.EditMode
{
    public class AbilityUpgradeServiceTests
    {
        private sealed class StubData : AbilityData { }
        private sealed class StubCore : IAbilityCommandTarget
        {
            public AbilityLoadout Loadout { get; } = new AbilityLoadout(6, 6);
            public int leveled;
            public int removed;
            public AbilityInstance AddAbility(AbilityData d)
            {
                if (!Loadout.TryAdd(d)) return null;
                return Loadout.Actives.Count > 0 ? Loadout.Actives[Loadout.Actives.Count - 1] : null;
            }
            public void LevelUpAbility(AbilityInstance i) { leveled++; Loadout.LevelUp(i); }
            public void RemoveAbility(AbilityInstance i) { removed++; Loadout.Remove(i); }
        }

        private static AbilityInstance Ability(int baseCost, int level, int maxLevel = 5)
        {
            StubData d = ScriptableObject.CreateInstance<StubData>();
            d.baseCost = baseCost;
            d.maxLevel = maxLevel;
            AbilityInstance inst = new AbilityInstance(d, level);
            inst.acquiredRound = 1;
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
        public void TryUpgrade_WithEnoughGold_SpendsAndLevelsUp()
        {
            EconomyModel economy = new EconomyModel();
            economy.Initialize(1000);
            StubCore core = new StubCore();
            AbilityInstance a = Ability(30, 1);
            AbilityUpgradeService service = new AbilityUpgradeService(core, economy, Config());
            int cost = service.GetUpgradeCost(a);

            bool ok = service.TryUpgrade(a);

            Assert.IsTrue(ok);
            Assert.AreEqual(1, core.leveled);
            Assert.AreEqual(1000 - cost, economy.Gold.Value);
        }

        [Test]
        public void TryUpgrade_WithoutEnoughGold_NoChange()
        {
            EconomyModel economy = new EconomyModel();
            economy.Initialize(10);
            StubCore core = new StubCore();
            AbilityUpgradeService service = new AbilityUpgradeService(core, economy, Config());

            bool ok = service.TryUpgrade(Ability(30, 1));

            Assert.IsFalse(ok);
            Assert.AreEqual(0, core.leveled);
            Assert.AreEqual(10, economy.Gold.Value);
        }

        [Test]
        public void TryUpgrade_AtMaxLevel_BlocksWithoutSpending()
        {
            EconomyModel economy = new EconomyModel();
            economy.Initialize(1000);
            StubCore core = new StubCore();
            AbilityUpgradeService service = new AbilityUpgradeService(core, economy, Config());

            bool ok = service.TryUpgrade(Ability(30, level: 5, maxLevel: 5));

            Assert.IsFalse(ok);
            Assert.AreEqual(0, core.leveled);
            Assert.AreEqual(1000, economy.Gold.Value, "MAX면 헛돈 차감 없음");
        }

        [Test]
        public void Dismiss_RefundsAndRemoves()
        {
            EconomyModel economy = new EconomyModel();
            economy.Initialize(0);
            StubCore core = new StubCore();
            AbilityInstance a = Ability(30, level: 3);
            AbilityUpgradeService service = new AbilityUpgradeService(core, economy, Config());
            int refund = service.GetRefund(a);

            service.Dismiss(a);

            Assert.AreEqual(1, core.removed);
            Assert.AreEqual(refund, economy.Gold.Value);
        }
    }
}
