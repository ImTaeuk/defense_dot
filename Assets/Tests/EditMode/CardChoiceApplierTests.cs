using NUnit.Framework;
using UnityEngine;
using DefenseDot.Systems.Abilities;
using DefenseDot.Systems.Cards;
using DefenseDot.Core;

namespace DefenseDot.Tests.EditMode
{
    /// <summary>
    /// CardChoiceApplier 단위 테스트.
    /// 카드 선택 → 능력 적용의 레벨업 횟수(off-by-one) 회귀를 방어한다.
    /// </summary>
    public class CardChoiceApplierTests
    {
        private sealed class StubActive : ActiveAbilityData
        {
            public override void Tick(in AbilityContext ctx, AbilityInstance self, float deltaTime) { }
            protected override void Fire(in AbilityContext ctx, AbilityInstance self, ITargetable target) { }
        }

        private sealed class StubCore : ICardCommandTarget
        {
            public AbilityLoadout Loadout { get; }
            public int added;
            public int leveled;

            public StubCore(int actives = 6, int passives = 6)
                => Loadout = new AbilityLoadout(actives, passives);

            public AbilityInstance AddAbility(AbilityData d)
            {
                added++;
                if (!Loadout.TryAdd(d)) return null;
                var list = d is PassiveAbilityData ? Loadout.Passives : Loadout.Actives;
                return list.Count > 0 ? list[list.Count - 1] : null;
            }

            public void LevelUpAbility(AbilityInstance i) { leveled++; Loadout.LevelUp(i); }
        }

        private static StubActive Active(int max = 5)
        {
            var a = ScriptableObject.CreateInstance<StubActive>();
            a.maxLevel = max;
            return a;
        }

        [Test]
        public void Apply_NewCard_AddsOnceAndLevelsUpToTarget()
        {
            var core = new StubCore();
            CardChoice choice = CardChoice.NewCard(Active(), CardTier.New, toLevel: 3);

            CardChoiceApplier.ApplyAsync(core, choice, null).GetAwaiter().GetResult();

            Assert.AreEqual(1, core.added, "신규 추가 1회");
            Assert.AreEqual(2, core.leveled, "레벨 1 → 3 이면 레벨업 2회");
        }

        [Test]
        public void Apply_NewCardAtLevelOne_AddsOnceWithoutLevelUp()
        {
            var core = new StubCore();
            CardChoice choice = CardChoice.NewCard(Active(), CardTier.New, toLevel: 1);

            CardChoiceApplier.ApplyAsync(core, choice, null).GetAwaiter().GetResult();

            Assert.AreEqual(1, core.added);
            Assert.AreEqual(0, core.leveled, "목표가 시작 레벨과 같으면 레벨업 없음");
        }

        [Test]
        public void Apply_LevelCard_LevelsUpFromCurrentToTarget()
        {
            var core = new StubCore();
            var inst = new AbilityInstance(Active(), level: 2);
            CardChoice choice = CardChoice.LevelCard(inst, CardTier.New, toLevel: 5);

            CardChoiceApplier.ApplyAsync(core, choice, null).GetAwaiter().GetResult();

            Assert.AreEqual(0, core.added, "레벨 카드는 신규 추가 없음");
            Assert.AreEqual(3, core.leveled, "레벨 2 → 5 이면 레벨업 3회");
        }

        [Test]
        public void Apply_NewCardWhenSlotFull_NoLevelUpAndNoThrow()
        {
            var core = new StubCore(actives: 0, passives: 0);
            CardChoice choice = CardChoice.NewCard(Active(), CardTier.New, toLevel: 3);

            Assert.DoesNotThrow(() => CardChoiceApplier.ApplyAsync(core, choice, null).GetAwaiter().GetResult());
            Assert.AreEqual(0, core.leveled, "추가 실패(null) 시 레벨업하지 않음");
        }
    }
}
