using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using UnityEngine;
using DefenseDot.Core.Pooling;
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
        private readonly PoolManager pooling;
        private readonly CardChoiceGenerator generator = new CardChoiceGenerator();
        private List<CardChoice> current;

        public CardSelectionPresenter(CardSelectionView view, GameContext ctx) : base(view)
        {
            level = ctx.Level;
            core = ctx.CoreTarget;
            config = ctx.CardConfig;
            pool = ctx.AbilityPool;
            flow = ctx.Flow;
            pooling = ctx.Pooling;
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
            if (current == null) TryShowNextLevelUpCard();
        }

        /// <summary> 대기 중인 레벨업이 남아 있으면 다음 카드 선택지를 띄웁니다(연속 레벨업 순차 처리). 없으면 종료. </summary>
        private void TryShowNextLevelUpCard()
        {
            if (!level.TryConsumePending()) return;   // 대기 레벨업 없으면 종료
            current = generator.Generate(core.Loadout, pool, config, level.Level);
            if (current == null || current.Count == 0) { current = null; TryShowNextLevelUpCard(); return; }   // 후보 0개면 다음 대기 소비
            view.ShowChoices(current);
            if (config.pauseOnCardSelect) Time.timeScale = 0f;   // 카드 선택 중 정지
        }

        private void HandleSelected(int idx)
        {
            if (current == null || idx < 0 || idx >= current.Count) return;
            CardChoice choice = current[idx];
            current = null;   // 예열 대기 중 재선택 가드
            ApplySelectedAsync(choice).Forget();
        }

        /// <summary> 선택 카드의 이펙트를 예열한 뒤 적용하고 카드 UI를 닫습니다. </summary>
        private async UniTaskVoid ApplySelectedAsync(CardChoice choice)
        {
            try
            {
                await CardChoiceApplier.ApplyAsync(core, choice, pooling);
            }
            finally
            {
                // 예열 성공/실패 무관하게 UI·시간 복원(soft-lock 방지). 실패 이펙트는 스폰 시점에 개별 처리.
                view.Hide();                                             // 방금 카드 화면 닫기
                if (flow.Phase == GamePhase.Playing) Time.timeScale = 1f;
                TryShowNextLevelUpCard();                                              // 대기 레벨업 더 있으면 다음 카드
            }
        }
    }
}
