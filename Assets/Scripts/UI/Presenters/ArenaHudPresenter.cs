// Arena HUD 프레젠터 — 도메인 RP를 위젯에 Bind
using DefenseDot.Domain;
using DefenseDot.UI.Base;
using DefenseDot.UI.Views;
using DefenseDot.Domain.Models;

namespace DefenseDot.UI.Presenters
{
    /// <summary>
    /// 아레나 HUD 프레젠터입니다. Score/Wave/RoundTimer/Level RP를
    /// 라운드·시간·점수·적·레벨 위젯에 Bind합니다.
    /// </summary>
    public sealed class ArenaHudPresenter : UIPresenter<ArenaHudView>
    {
        private readonly ScoreModel score;
        private readonly WaveModel wave;
        private readonly RoundTimerModel timer;
        private readonly LevelModel level;
        private readonly int enemyCapacity;

        /// <summary> GameContext 에서 필요한 모델을 추출해 주입받습니다. </summary>
        public ArenaHudPresenter(ArenaHudView view, GameContext ctx) : base(view)
        {
            score = ctx.Score;
            wave = ctx.Wave;
            timer = ctx.Timer;
            level = ctx.Level;
            enemyCapacity = ctx.EnemyCapacity;
        }

        /// <summary> 도메인 RP를 위젯에 바인딩합니다. </summary>
        protected override void OnInitialize()
        {
            Bind(score.Score, view.ApplyScore);
            Bind(wave.Progress, view.ApplyRound);
            Bind(timer.Time, view.ApplyTime);
            Bind(wave.RemainingEnemies, HandleRemaining);
            Bind(level.Progress, view.ApplyLevel);
        }

        private void HandleRemaining(int alive)
        {
            view.ApplyEnemies(new EnemyState(alive, enemyCapacity));
        }
    }
}
