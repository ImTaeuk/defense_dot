// Arena HUD 프레젠터 — 도메인 RP를 위젯에 Bind
using DefenseDot.UI.Base;
using DefenseDot.UI.Views;
using DefenseDot.Domain.Models;

namespace DefenseDot.UI.Presenters
{
    /// <summary>
    /// 아레나 HUD 프레젠터입니다. Economy/Score/Wave/RoundTimer RP를
    /// 라운드·시간·골드·점수·적 위젯에 Bind합니다.
    /// </summary>
    public sealed class ArenaHudPresenter : UIPresenter<ArenaHudView>
    {
        private readonly EconomyModel economy;
        private readonly ScoreModel score;
        private readonly WaveModel wave;
        private readonly RoundTimerModel timer;
        private readonly int enemyCapacity;

        /// <summary> 구독할 도메인 모델과 적 수용 한계를 주입받습니다. </summary>
        public ArenaHudPresenter(ArenaHudView view, EconomyModel economy, ScoreModel score,
            WaveModel wave, RoundTimerModel timer, int enemyCapacity) : base(view)
        {
            this.economy = economy;
            this.score = score;
            this.wave = wave;
            this.timer = timer;
            this.enemyCapacity = enemyCapacity;
        }

        /// <summary> 도메인 RP를 위젯에 바인딩합니다. </summary>
        protected override void OnInitialize()
        {
            Bind(economy.Gold, view.ApplyGold);
            Bind(score.Score, view.ApplyScore);
            Bind(wave.Progress, view.ApplyRound);
            Bind(timer.Time, view.ApplyTime);
            Bind(wave.RemainingEnemies, HandleRemaining);
        }

        private void HandleRemaining(int alive)
        {
            view.ApplyEnemies(new EnemyState(alive, enemyCapacity));
        }
    }
}
