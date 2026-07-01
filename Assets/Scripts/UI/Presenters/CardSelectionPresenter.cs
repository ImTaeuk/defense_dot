using System.Collections.Generic;
using UnityEngine;
using DefenseDot.Domain;
using DefenseDot.Domain.Models;
using DefenseDot.Systems.Abilities;
using DefenseDot.Systems.Cards;
using DefenseDot.UI.Base;
using DefenseDot.UI.Views;

namespace DefenseDot.UI.Presenters
{
    /// <summary> 레벨업 → 카드 생성 → 표시/정지 → 선택 적용 → 복귀 오케스트레이션. </summary>
    public sealed class CardSelectionPresenter : UIPresenter<CardSelectionView>
    {
        private readonly LevelModel level;
        private readonly ICardCommandTarget core;
        private readonly ArenaCardConfig config;
        private readonly AbilityPool pool;
        private readonly GameFlowModel flow;
        private readonly CardChoiceGenerator generator = new CardChoiceGenerator();
        private List<CardChoice> current;

        public CardSelectionPresenter(CardSelectionView view, GameContext ctx) : base(view)
        {
            level = ctx.Level;
            core = ctx.CoreTarget;
            config = ctx.CardConfig;
            pool = ctx.AbilityPool;
            flow = ctx.Flow;
        }

        protected override void OnInitialize()
        {
            level.OnLevelUp += HandleLevelUp;
            view.OnCardSelected += HandleSelected;
            view.Hide();
        }

        protected override void OnDispose()
        {
            level.OnLevelUp -= HandleLevelUp;
            view.OnCardSelected -= HandleSelected;
            Time.timeScale = 1f;
        }

        private void HandleLevelUp()
        {
            if (current == null) ShowNext();
        }

        private void ShowNext()
        {
            if (!level.TryConsumePending()) return;
            current = generator.Generate(core.Loadout, pool, config, level.Level);
            if (current == null || current.Count == 0) { current = null; ShowNext(); return; }
            view.ShowChoices(current);
            if (config.pauseOnCardSelect) Time.timeScale = 0f;
        }

        private void HandleSelected(int idx)
        {
            if (current == null || idx < 0 || idx >= current.Count) return;
            CardChoiceApplier.Apply(core, current[idx]);
            current = null;
            view.Hide();
            if (flow.Phase == GamePhase.Playing) Time.timeScale = 1f;
            ShowNext();
        }
    }
}
