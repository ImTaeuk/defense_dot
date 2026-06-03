// HUD 프레젠터 — 도메인 모델(Economy/Core/Wave)을 구독해 HUD 갱신
using DefenseDot.UI.Models;
using DefenseDot.UI.Views;
using DefenseDot.Domain.Models;

namespace DefenseDot.UI.Presenters
{
    /// <summary>
    /// 도메인 모델(Economy/Core/Wave)을 구독하여 HUD의 데이터(Model)와 화면(View)을 갱신하는 Presenter입니다.
    /// </summary>
    public class HUDPresenter : BasePresenter<HUDView, HUDModel>
    {
        private readonly EconomyModel economy;
        private readonly CoreModel core;
        private readonly WaveModel wave;

        /// <summary>
        /// HUDPresenter의 생성자입니다. 구독할 도메인 모델을 주입받습니다.
        /// </summary>
        public HUDPresenter(HUDView view, HUDModel model, EconomyModel economy, CoreModel core, WaveModel wave)
            : base(view, model)
        {
            this.economy = economy;
            this.core = core;
            this.wave = wave;
        }

        /// <summary>
        /// 도메인 모델 변경 사건을 구독하고 초기값을 즉시 반영합니다.
        /// </summary>
        public override void Initialize()
        {
            economy.OnGoldChanged += HandleGoldChanged;
            core.OnHealthChanged += HandleHealthChanged;
            wave.OnWaveChanged += HandleWaveChanged;

            HandleGoldChanged(economy.Gold);
            HandleHealthChanged(core.HealthRatio);
            HandleWaveChanged(wave.Current, wave.Total);
        }

        /// <summary>
        /// 구독을 해제하여 메모리 누수를 방지합니다. (Lapsed Listener 방지)
        /// </summary>
        public override void Dispose()
        {
            economy.OnGoldChanged -= HandleGoldChanged;
            core.OnHealthChanged -= HandleHealthChanged;
            wave.OnWaveChanged -= HandleWaveChanged;
        }

        private void HandleGoldChanged(int gold)
        {
            model.CurrentGold = gold;
            view.UpdateGold(gold);
        }

        private void HandleHealthChanged(float ratio)
        {
            model.CoreHealth = ratio;
            view.UpdateHealth(ratio);
        }

        private void HandleWaveChanged(int current, int total)
        {
            model.CurrentWave = current;
            view.UpdateWave(current);
        }
    }
}
