// HUD 프레젠터 — 도메인 모델(Economy/Core/Wave) RP를 구독해 통합 HUD 갱신
using DefenseDot.UI.Models;
using DefenseDot.UI.Views;
using DefenseDot.Domain.Models;

namespace DefenseDot.UI.Presenters
{
    /// <summary>
    /// 도메인 모델(Economy/Core/Wave) RP를 구독하여 통합 HUD의 데이터(Model)와 화면(View)을 갱신하는 Presenter입니다.
    /// </summary>
    public class HUDPresenter : BasePresenter<HUDView, HUDModel>, IPresenter
    {
        private readonly EconomyModel economy;
        private readonly CoreModel core;
        private readonly WaveModel wave;
        private readonly int enemyCapacity;

        private System.IDisposable goldSub;
        private System.IDisposable healthSub;
        private System.IDisposable progressSub;
        private System.IDisposable remainingSub;

        /// <summary>
        /// HUDPresenter의 생성자입니다. 구독할 도메인 모델과 적 수용 한계를 주입받습니다.
        /// </summary>
        public HUDPresenter(HUDView view, HUDModel model, EconomyModel economy, CoreModel core, WaveModel wave, int enemyCapacity)
            : base(view, model)
        {
            this.economy = economy;
            this.core = core;
            this.wave = wave;
            this.enemyCapacity = enemyCapacity;
        }

        /// <summary>
        /// RP를 구독합니다. Subscribe가 즉시 현재값을 1회 통지합니다.
        /// </summary>
        public override void Initialize()
        {
            goldSub = economy.Gold.Subscribe(HandleGoldChanged);
            healthSub = core.Health.Subscribe(HandleHealthState);
            progressSub = wave.Progress.Subscribe(HandleWaveProgress);
            remainingSub = wave.RemainingEnemies.Subscribe(HandleRemaining);
        }

        /// <summary>
        /// 구독 토큰을 해제하여 메모리 누수를 방지합니다. (Lapsed Listener 방지)
        /// </summary>
        public override void Dispose()
        {
            goldSub?.Dispose();
            healthSub?.Dispose();
            progressSub?.Dispose();
            remainingSub?.Dispose();
        }

        private void HandleGoldChanged(int gold)
        {
            model.CurrentGold = gold;
            view.UpdateGold(gold);
        }

        private void HandleHealthState(HealthState s)
        {
            model.CoreHealth = s.Ratio;
            view.UpdateHealth(s.Hp, s.MaxHp, s.Ratio);
        }

        private void HandleWaveProgress(WaveProgress p)
        {
            model.CurrentWave = p.Current;
            model.RoundTotal = p.Total;
            view.UpdateRound(p.Current, p.Total);
        }

        private void HandleRemaining(int alive)
        {
            model.EnemyAlive = alive;
            model.EnemyCapacity = enemyCapacity;
            view.UpdateEnemyCount(alive, enemyCapacity);
        }
    }
}
