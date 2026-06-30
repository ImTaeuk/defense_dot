using System.Collections.Generic;
using UnityEngine;
using DefenseDot.Domain.Models;
using DefenseDot.Data;
using DefenseDot.Systems.Tower;
using DefenseDot.Systems.Cards;
using DefenseDot.UI.Views;
using DefenseDot.UI.Presenters;

namespace DefenseDot.UI.InGame
{
    /// <summary>
    /// UI 계층의 단일 합성 루트입니다. 합성 루트가 주입한 모델로 모든 프레젠터를 조립·관리합니다.
    /// 새 UI 추가 시 View 참조와 프레젠터 생성 한 줄만 더합니다.
    /// </summary>
    public class UIRoot : MonoBehaviour
    {
        [Header("Views")]
        [SerializeField] private ArenaHudView arenaHud;
        [SerializeField] private TowerBuildModalView buildModalView;
        [SerializeField] private TowerRoster towerRoster;
        [SerializeField] private GameResultView gameResultView;
        [SerializeField] private CardSelectionView cardSelectionView;

        private readonly List<IPresenter> presenters = new List<IPresenter>();

        /// <summary>
        /// 합성 루트가 HUD 컨텍스트·게임 흐름·배치 컨트롤러를 주입합니다.
        /// HUD는 자신이 자신의 프레젠터를 조립하므로 UIRoot은 모드를 알지 못합니다.
        /// </summary>
        public void Inject(in HudContext ctx, GameFlowModel flow, TowerPlacementController placement, in CardContext card)
        {
            if (arenaHud != null)
                presenters.Add(new ArenaHudPresenter(arenaHud, ctx.Economy, ctx.Score,
                    ctx.Wave, ctx.Timer, ctx.EnemyCapacity));

            if (placement != null && buildModalView != null && towerRoster != null)
                presenters.Add(new TowerBuildPresenter(buildModalView, towerRoster, ctx.Economy, placement));

            if (gameResultView != null)
                presenters.Add(new GameResultPresenter(gameResultView, flow));

            if (cardSelectionView != null && card.Level != null && card.Config != null && card.Core != null)
                presenters.Add(new CardSelectionPresenter(cardSelectionView, card.Level, new CardChoiceGenerator(), card.Core, card.Config, card.Pool, card.Flow));

            foreach (IPresenter presenter in presenters)
                presenter.Initialize();
        }

        private void OnDestroy()
        {
            foreach (IPresenter presenter in presenters)
                presenter.Dispose();
            presenters.Clear();
        }
    }
}
