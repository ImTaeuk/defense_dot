// HUD 프레젠터 — 도메인 모델(Economy/Core/Wave)을 구독해 통합 HUD 갱신
using DefenseDot.UI.Models;
using DefenseDot.UI.Views;
using DefenseDot.Domain.Models;

namespace DefenseDot.UI.Presenters
{
    /// <summary>
    /// 도메인 모델(Economy/Core/Wave)을 구독하여 통합 HUD의 데이터(Model)와 화면(View)을 갱신하는 Presenter입니다.
    /// </summary>
    public class HUDPresenter : BasePresenter<HUDView, HUDModel>, IPresenter
    {
        private readonly EconomyModel economy;
        private readonly CoreModel core;
        private readonly WaveModel wave;
        private readonly int enemyCapacity;

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
        /// 도메인 모델 변경 사건을 구독하고 초기값을 즉시 반영합니다.
        /// </summary>
        public override void Initialize()
        {
            economy.OnGoldChanged += HandleGoldChanged;
            core.OnHealthChanged += HandleHealthChanged;
            wave.OnWaveChanged += HandleWaveChanged;
            wave.OnRemainingChanged += HandleRemainingChanged;

            HandleGoldChanged(economy.Gold);
            HandleHealthChanged(core.HealthRatio);
            HandleWaveChanged(wave.Current, wave.Total);
            HandleRemainingChanged(wave.Remaining);
        }

        /// <summary>
        /// 구독을 해제하여 메모리 누수를 방지합니다. (Lapsed Listener 방지)
        /// </summary>
        public override void Dispose()
        {
            economy.OnGoldChanged -= HandleGoldChanged;
            core.OnHealthChanged -= HandleHealthChanged;
            wave.OnWaveChanged -= HandleWaveChanged;
            wave.OnRemainingChanged -= HandleRemainingChanged;
        }

        private void HandleGoldChanged(int gold)
        {
            model.CurrentGold = gold;
            view.UpdateGold(gold);
        }

        private void HandleHealthChanged(float ratio)
        {
            model.CoreHealth = ratio;
            view.UpdateHealth(core.CurrentHp, core.MaxHp, ratio);
        }

        private void HandleWaveChanged(int current, int total)
        {
            model.CurrentWave = current;
            model.RoundTotal = total;
            view.UpdateRound(current, total);
        }

        private void HandleRemainingChanged(int alive)
        {
            model.EnemyAlive = alive;
            model.EnemyCapacity = enemyCapacity;
            view.UpdateEnemyCount(alive, enemyCapacity);
        }
    }
}
