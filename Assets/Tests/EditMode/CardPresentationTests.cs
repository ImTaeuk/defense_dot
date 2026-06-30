using NUnit.Framework;
using UnityEngine;
using DefenseDot.Systems.Abilities;
using DefenseDot.Systems.Cards;
using DefenseDot.Core;

namespace DefenseDot.Tests.EditMode
{
    public class CardPresentationTests
    {
        private sealed class StubActive : ActiveAbilityData
        {
            public override void Tick(in AbilityContext ctx, AbilityInstance self, float deltaTime) { }
            protected override void Fire(in AbilityContext ctx, AbilityInstance self, ITargetable target) { }
        }

        [Test]
        public void Build_NewCard_UsesNameAndActiveTag()
        {
            var a = ScriptableObject.CreateInstance<StubActive>();
            a.displayName = "샷"; a.description = "기본 발사";
            var disp = CardPresentation.Build(CardChoice.NewCard(a, CardTier.New, 1));
            Assert.AreEqual("샷", disp.title);
            StringAssert.Contains("액티브", disp.kindTag);
            Assert.AreEqual("기본 발사", disp.desc);
            Assert.AreEqual(CardTier.New, disp.tier);
        }

        [Test]
        public void Build_LevelCard_ShowsLevelTransition()
        {
            var a = ScriptableObject.CreateInstance<StubActive>();
            a.displayName = "샷"; a.maxLevel = 5;
            var inst = new AbilityInstance(a, 2);
            var disp = CardPresentation.Build(CardChoice.LevelCard(inst, CardTier.Upgrade, 3));
            StringAssert.Contains("Lv2", disp.desc);
            StringAssert.Contains("Lv3", disp.desc);
            Assert.AreEqual(CardTier.Upgrade, disp.tier);
        }

        [Test]
        public void Build_SuperLuckyCard_ShowsStarMarker()
        {
            var a = ScriptableObject.CreateInstance<StubActive>();
            a.displayName = "샷"; a.maxLevel = 5;
            var inst = new AbilityInstance(a, 1);
            var disp = CardPresentation.Build(CardChoice.LevelCard(inst, CardTier.SuperLucky, 4));
            StringAssert.Contains("★★", disp.desc);
            StringAssert.Contains("Lv4", disp.desc);
            Assert.AreEqual(CardTier.SuperLucky, disp.tier);
        }
    }
}
