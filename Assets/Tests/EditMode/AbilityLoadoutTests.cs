using NUnit.Framework;
using UnityEngine;
using DefenseDot.Systems.Abilities;

namespace DefenseDot.Tests.EditMode
{
    public class AbilityLoadoutTests
    {
        private sealed class StubActive : ActiveAbilityData
        {
            protected override void Fire(in AbilityContext ctx, AbilityInstance self, DefenseDot.Core.ITargetable target) { }
        }
        private sealed class StubPassive : PassiveAbilityData
        {
            public float perLevelBonus = 2f;
            public override void ApplyModifiers(AbilityModifiers mods, int level) { mods.damageBonus += perLevelBonus * level; }
        }

        private static StubActive NewActive(int maxLevel = 5)
        {
            var a = ScriptableObject.CreateInstance<StubActive>();
            a.maxLevel = maxLevel;
            return a;
        }

        private static StubActive MakeBasic()
        {
            var a = NewActive();
            a.tier = AbilityTier.Basic;
            return a;
        }

        private static StubActive MakeSignature()
        {
            var a = NewActive();
            a.tier = AbilityTier.Signature;
            return a;
        }

        [Test]
        public void CanAdd_AllowsBasic_WhenNoneEquipped()
        {
            var loadout = new AbilityLoadout();
            Assert.IsFalse(loadout.HasBasicAttack());
            Assert.IsTrue(loadout.CanAdd(MakeBasic()));
        }

        [Test]
        public void CanAdd_RejectsSecondBasic()
        {
            var loadout = new AbilityLoadout();
            loadout.TryAdd(MakeBasic());          // tier=Basic 스텁
            Assert.IsFalse(loadout.CanAdd(MakeBasic()), "Basic은 1개만");
        }

        [Test]
        public void CanAdd_AllowsNonBasic()
        {
            var loadout = new AbilityLoadout();
            loadout.TryAdd(MakeBasic());
            Assert.IsTrue(loadout.CanAdd(MakeSignature()), "비-Basic 능력은 추가 가능");
        }

        [Test]
        public void CanAdd_AllowsBasicAgain_AfterBasicRemoved()
        {
            var loadout = new AbilityLoadout();
            loadout.TryAdd(MakeBasic());
            loadout.Remove(loadout.Actives[0]);   // 합성이 주축을 재료로 소진한 상황

            Assert.IsFalse(loadout.HasBasicAttack());
            Assert.IsTrue(loadout.CanAdd(MakeBasic()), "주축이 비면 합성 결과가 새 주축으로 들어올 수 있어야 한다");
        }

        private static StubPassive NewPassive()
        {
            var p = ScriptableObject.CreateInstance<StubPassive>();
            p.maxLevel = 5;
            return p;
        }

        [Test]
        public void TryAdd_RoutesActiveAndPassiveBySubclass()
        {
            var lo = new AbilityLoadout(6, 6);
            Assert.IsTrue(lo.TryAdd(NewActive()));
            Assert.IsTrue(lo.TryAdd(NewPassive()));
            Assert.AreEqual(1, lo.Actives.Count);
            Assert.AreEqual(1, lo.Passives.Count);
        }

        [Test]
        public void TryAdd_WhenActiveFull_ReturnsFalse()
        {
            var lo = new AbilityLoadout(2, 6);
            Assert.IsTrue(lo.TryAdd(NewActive()));
            Assert.IsTrue(lo.TryAdd(NewActive()));
            Assert.IsFalse(lo.TryAdd(NewActive()), "액티브 슬롯 한계 초과는 거부");
        }

        [Test]
        public void TryAdd_Duplicate_ReturnsFalse()
        {
            var lo = new AbilityLoadout(6, 6);
            var a = NewActive();
            Assert.IsTrue(lo.TryAdd(a));
            Assert.IsFalse(lo.TryAdd(a), "이미 보유한 능력은 추가 대신 LevelUp 대상");
        }

        [Test]
        public void LevelUp_IncrementsClampedToMax()
        {
            var lo = new AbilityLoadout(6, 6);
            var a = NewActive(maxLevel: 2);
            lo.TryAdd(a);
            var inst = lo.Actives[0];
            lo.LevelUp(inst);
            Assert.AreEqual(2, inst.level);
            lo.LevelUp(inst);
            Assert.AreEqual(2, inst.level, "maxLevel에서 클램프");
        }

        [Test]
        public void Remove_RemovesInstance()
        {
            var lo = new AbilityLoadout(6, 6);
            lo.TryAdd(NewActive());
            lo.Remove(lo.Actives[0]);
            Assert.AreEqual(0, lo.Actives.Count);
        }

        [Test]
        public void Modifiers_SumsPassivesAndRecalcsOnChange()
        {
            var lo = new AbilityLoadout(6, 6);
            lo.TryAdd(NewPassive());           // level1 → +2
            Assert.AreEqual(2f, lo.Modifiers.damageBonus, 1e-4f);
            lo.TryAdd(NewPassive());           // +2 → 합 4
            Assert.AreEqual(4f, lo.Modifiers.damageBonus, 1e-4f);
            lo.LevelUp(lo.Passives[0]);        // 첫째 level2 → +4, 둘째 +2 → 합 6
            Assert.AreEqual(6f, lo.Modifiers.damageBonus, 1e-4f);
            lo.Remove(lo.Passives[0]);         // 둘째만 → +2
            Assert.AreEqual(2f, lo.Modifiers.damageBonus, 1e-4f);
        }
    }
}
