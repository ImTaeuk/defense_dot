using System.Collections.Generic;
using UnityEngine;
using DefenseDot.Domain;
using DefenseDot.Domain.Models;
using DefenseDot.Systems.Abilities;
using DefenseDot.Systems.Cards;
using DefenseDot.UI.Views;

namespace DefenseDot.UI.Presenters
{
    /// <summary> 레벨업 → 카드 생성 → 표시/정지 → 선택 적용 → 복귀 오케스트레이션. </summary>
    public sealed class CardSelectionPresenter : IPresenter
    {
        private readonly ICardSelectionView view;
        private readonly LevelModel level;
        private readonly CardChoiceGenerator generator;
        private readonly ICardCommandTarget core;
        private readonly ArenaCardConfig config;
        private readonly AbilityPool pool;
        private readonly GameFlowModel flow;
        private List<CardChoice> current;

        public CardSelectionPresenter(ICardSelectionView view, LevelModel level, CardChoiceGenerator generator, ICardCommandTarget core, ArenaCardConfig config, AbilityPool pool, GameFlowModel flow)
        {
            this.view = view;
            this.level = level;
            this.generator = generator;
            this.core = core;
            this.config = config;
            this.pool = pool;
            this.flow = flow;
        }

        public void Initialize()
        {
            level.OnLevelUp += HandleLevelUp;
            view.OnCardSelected += HandleSelected;
            view.Hide();
        }

        private void HandleLevelUp()
        {
            if (current == null) ShowNext();   // 모달 표시 중이면 선택 후 드레인
        }

        private void ShowNext()
        {
            if (!level.TryConsumePending()) return;
            current = generator.Generate(core.Loadout, pool, config, level.Level);
            if (current == null || current.Count == 0)
            {
                current = null;
                ShowNext();
                return;
            }
            view.Show(current);
            if (config.pauseOnCardSelect) Time.timeScale = 0f;
        }

        private void HandleSelected(int idx)
        {
            if (current == null || idx < 0 || idx >= current.Count) return;
            CardChoice c = current[idx];
            if (c.action == CardAction.New)
            {
                AbilityInstance added = core.AddAbility(c.data);
                if (added != null)
                    for (int lv = added.level; lv < c.toLevel; lv++) core.LevelUpAbility(added);
            }
            else
            {
                for (int lv = c.fromLevel; lv < c.toLevel; lv++) core.LevelUpAbility(c.instance);
            }
            current = null;
            view.Hide();
            if (flow.Phase == GamePhase.Playing) Time.timeScale = 1f;
            ShowNext();
        }

        public void Dispose()
        {
            level.OnLevelUp -= HandleLevelUp;
            view.OnCardSelected -= HandleSelected;
            Time.timeScale = 1f;
        }
    }
}
