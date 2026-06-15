// Arena HUD 프레젠터 — Wave/Economy/Score/RoundTimer 구독해 Arena HUD 갱신
using DefenseDot.UI.Models;
using DefenseDot.UI.Views;
using DefenseDot.Domain.Models;

namespace DefenseDot.UI.Presenters
{
    /// <summary>
    /// 아레나 HUD 프레젠터입니다. Wave/Economy/Score/RoundTimer 모델을 구독해
    /// 라운드·시간·골드·점수·적을 갱신합니다. (Arena 패널은 체력 행이 없어 CoreModel 미사용)
    /// </summary>
    public class ArenaHudPresenter : BasePresenter<ArenaHudView, ArenaHudModel>, IPresenter
    {
        private readonly WaveModel wave;
        private readonly EconomyModel economy;
        private readonly ScoreModel score;
        private readonly RoundTimerModel timer;
        private readonly int enemyCapacity;

        /// <summary> ArenaHudPresenter의 생성자입니다. </summary>
        public ArenaHudPresenter(ArenaHudView view, ArenaHudModel model,
            WaveModel wave, EconomyModel economy, ScoreModel score, RoundTimerModel timer, int enemyCapacity)
            : base(view, model)
        {
            this.wave = wave;
            this.economy = economy;
            this.score = score;
            this.timer = timer;
            this.enemyCapacity = enemyCapacity;
        }

        /// <summary> 모델 변경 사건을 구독하고 초기값을 즉시 반영합니다. </summary>
        public override void Initialize()
        {
            wave.OnWaveChanged += HandleWaveChanged;
            wave.OnRemainingChanged += HandleRemainingChanged;
            economy.OnGoldChanged += HandleGoldChanged;
            score.OnScoreChanged += HandleScoreChanged;
            timer.OnTimeChanged += HandleTimeChanged;

            HandleWaveChanged(wave.Current, wave.Total);
            HandleRemainingChanged(wave.Remaining);
            HandleGoldChanged(economy.Gold);
            HandleScoreChanged(score.Score);
            HandleTimeChanged(timer.Remaining, timer.Duration);
        }

        /// <summary> 구독을 해제합니다. (Lapsed Listener 방지) </summary>
        public override void Dispose()
        {
            wave.OnWaveChanged -= HandleWaveChanged;
            wave.OnRemainingChanged -= HandleRemainingChanged;
            economy.OnGoldChanged -= HandleGoldChanged;
            score.OnScoreChanged -= HandleScoreChanged;
            timer.OnTimeChanged -= HandleTimeChanged;
        }

        private void HandleWaveChanged(int current, int total)
        {
            model.CurrentWave = current;
            model.RoundTotal = total;
            view.SetRound(current, total);
        }

        private void HandleRemainingChanged(int alive)
        {
            model.EnemyAlive = alive;
            model.EnemyCapacity = enemyCapacity;
            view.SetEnemies(alive, enemyCapacity);
            view.SetEnemyBar(enemyCapacity > 0 ? (float)alive / enemyCapacity : 0f);
        }

        private void HandleGoldChanged(int gold)
        {
            model.CurrentGold = gold;
            view.SetGold(gold);
        }

        private void HandleScoreChanged(int value)
        {
            model.Score = value;
            view.SetScore(value);
        }

        private void HandleTimeChanged(float remaining, float duration)
        {
            model.TimeRemaining = remaining;
            view.SetTime(remaining);
            view.SetTimeBar(duration > 0f ? remaining / duration : 0f);
        }
    }
}
