using NUnit.Framework;
using System.Collections.Generic;
using UnityEngine;
using DefenseDot.Domain;
using DefenseDot.Domain.Models;
using DefenseDot.Systems.Abilities;
using DefenseDot.Systems.Cards;
using DefenseDot.UI.Views;
using DefenseDot.UI.Presenters;
using DefenseDot.Core;

namespace DefenseDot.Tests.EditMode
{
    public class CardSelectionPresenterTests
    {
        private sealed class StubView : ICardSelectionView
        {
            public int showCount; public int hideCount; public IReadOnlyList<CardChoice> last;
            public event System.Action<int> OnCardSelected;
            public void Show(IReadOnlyList<CardChoice> c) { showCount++; last = c; }
            public void Hide() { hideCount++; }
            public void Click(int i) { OnCardSelected?.Invoke(i); }
        }

        private sealed class StubCore : ICardCommandTarget
        {
            public AbilityLoadout Loadout { get; } = new AbilityLoadout(6, 6);
            public int added; public int leveled;
            public bool AddAbility(AbilityData d) { added++; return Loadout.TryAdd(d); }
            public void LevelUpAbility(AbilityInstance i) { leveled++; Loadout.LevelUp(i); }
        }

        private sealed class StubActive : ActiveAbilityData
        {
            public override void Tick(in AbilityContext ctx, AbilityInstance self, float deltaTime) { }
            protected override void Fire(in AbilityContext ctx, AbilityInstance self, ITargetable target) { }
        }

        private static StubActive Active() { var a = ScriptableObject.CreateInstance<StubActive>(); a.maxLevel = 5; return a; }

        private static ArenaCardConfig Config(bool pause)
        {
            var c = ScriptableObject.CreateInstance<ArenaCardConfig>();
            c.choiceCount = 3; c.pauseOnCardSelect = pause;
            c.newCardChanceEarly = 1f; c.newCardChanceLate = 1f;
            c.curveBase = 0; c.curvePerLevel = 0; // kills=3
            return c;
        }

        private static AbilityPool Pool(params AbilityData[] a)
        {
            var p = ScriptableObject.CreateInstance<AbilityPool>();
            p.abilities.AddRange(a);
            return p;
        }

        [TearDown] public void Reset() => Time.timeScale = 1f;

        private static (CardSelectionPresenter p, StubView v, StubCore core, LevelModel lvl) Make(ArenaCardConfig cfg, AbilityPool pool)
        {
            var v = new StubView();
            var core = new StubCore();
            var lvl = new LevelModel(cfg.KillsToNextLevel);
            var flow = new GameFlowModel(); flow.SetPhase(GamePhase.Playing);
            var gen = new CardChoiceGenerator(() => 0f);
            var p = new CardSelectionPresenter(v, lvl, gen, core, cfg, pool, flow);
            p.Initialize();
            return (p, v, core, lvl);
        }

        [Test]
        public void OnLevelUp_ShowsModalAndPauses()
        {
            var cfg = Config(pause: true);
            var (p, v, core, lvl) = Make(cfg, Pool(Active(), Active(), Active()));
            for (int i = 0; i < 3; i++) lvl.RegisterKill(); // kills=3 → 레벨업
            Assert.AreEqual(1, v.showCount);
            Assert.AreEqual(3, v.last.Count);
            Assert.AreEqual(0f, Time.timeScale);
        }

        [Test]
        public void SelectNewCard_AddsAbility_HidesAndResumes()
        {
            var cfg = Config(pause: true);
            var (p, v, core, lvl) = Make(cfg, Pool(Active(), Active(), Active()));
            for (int i = 0; i < 3; i++) lvl.RegisterKill();
            v.hideCount = 0; // Initialize()의 초기 Hide 제외 → 선택 후 Hide만 격리
            v.Click(0);
            Assert.AreEqual(1, core.added);
            Assert.AreEqual(1, v.hideCount);
            Assert.AreEqual(1f, Time.timeScale);
        }

        [Test]
        public void EmptyChoices_DoesNotShow()
        {
            var cfg = Config(pause: true);
            var (p, v, core, lvl) = Make(cfg, Pool()); // 빈 풀 + 보유 없음
            for (int i = 0; i < 3; i++) lvl.RegisterKill();
            Assert.AreEqual(0, v.showCount);
        }
    }
}
