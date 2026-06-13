using UnityEngine;
using UnityEngine.SceneManagement;
using DefenseDot.Domain;
using DefenseDot.Domain.Models;
using DefenseDot.UI.Views;

namespace DefenseDot.UI.Presenters
{
    /// <summary>
    /// 게임 단계 변화를 구독해 결과 패널을 띄우고, 정지·재시작을 처리하는 Presenter 입니다.
    /// </summary>
    public class GameResultPresenter : IPresenter
    {
        private readonly GameResultView view;
        private readonly GameFlowModel flow;

        /// <summary> 결과 뷰와 게임 진행 모델을 주입받습니다. </summary>
        public GameResultPresenter(GameResultView view, GameFlowModel flow)
        {
            this.view = view;
            this.flow = flow;
        }

        /// <summary> 단계 변화·재시작을 구독하고, 잔여 timeScale 을 복구합니다. </summary>
        public void Initialize()
        {
            Time.timeScale = 1f;
            flow.OnPhaseChanged += HandlePhaseChanged;
            view.OnRestart += HandleRestart;
            view.Hide();
        }

        /// <summary> 구독을 해제합니다. </summary>
        public void Dispose()
        {
            flow.OnPhaseChanged -= HandlePhaseChanged;
            view.OnRestart -= HandleRestart;
        }

        private void HandlePhaseChanged(GamePhase phase)
        {
            if (phase == GamePhase.Victory) { Time.timeScale = 0f; view.Show(true); }
            else if (phase == GamePhase.GameOver) { Time.timeScale = 0f; view.Show(false); }
        }

        private void HandleRestart()
        {
            Time.timeScale = 1f;
            SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
        }
    }
}
