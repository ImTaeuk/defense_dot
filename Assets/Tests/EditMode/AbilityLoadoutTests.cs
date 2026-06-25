using NUnit.Framework;
using UnityEngine;
using DefenseDot.Systems.Abilities;

namespace DefenseDot.Tests.EditMode
{
    public class AbilityLoadoutTests
    {
        private sealed class StubActive : ActiveAbilityData
        {
            public override void Tick(in AbilityContext ctx, AbilityInstance self, float deltaTime) { }
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
