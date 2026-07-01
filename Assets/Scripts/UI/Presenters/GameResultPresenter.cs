using UnityEngine;
using UnityEngine.SceneManagement;
using DefenseDot.Domain;
using DefenseDot.Domain.Models;
using DefenseDot.UI.Base;
using DefenseDot.UI.Views;

namespace DefenseDot.UI.Presenters
{
    /// <summary> 게임 단계 변화를 구독해 결과 패널을 띄우고 재시작을 처리합니다. </summary>
    public sealed class GameResultPresenter : UIPresenter<GameResultView>
    {
        private readonly GameFlowModel flow;

        public GameResultPresenter(GameResultView view, GameContext ctx) : base(view)
        {
            flow = ctx.Flow;
        }

        protected override void OnInitialize()
        {
            Time.timeScale = 1f;
            flow.OnPhaseChanged += HandlePhaseChanged;
            view.OnRestart += HandleRestart;
            view.Hide();
        }

        protected override void OnDispose()
        {
            flow.OnPhaseChanged -= HandlePhaseChanged;
            view.OnRestart -= HandleRestart;
        }

        private void HandlePhaseChanged(GamePhase phase)
        {
            if (phase == GamePhase.Victory) { Time.timeScale = 0f; view.ShowResult(true); }
            else if (phase == GamePhase.GameOver) { Time.timeScale = 0f; view.ShowResult(false); }
        }

        private void HandleRestart()
        {
            Time.timeScale = 1f;
            SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
        }
    }
}
