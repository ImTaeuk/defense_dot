using DefenseDot.UI.Models;
using DefenseDot.UI.Views;
using DefenseDot.Core;

namespace DefenseDot.UI.Presenters
{
    /// <summary>
    /// HUD의 데이터(Model)와 화면(View) 사이를 연결하고 이벤트를 처리하는 Presenter 클래스입니다.
    /// </summary>
    public class HUDPresenter : BasePresenter<HUDView, HUDModel>
    {
        /// <summary>
        /// HUDPresenter의 생성자입니다.
        /// </summary>
        public HUDPresenter(HUDView view, HUDModel model) : base(view, model) { }

        /// <summary>
        /// 전역 게임 이벤트를 구독하여 데이터 변화를 감시합니다.
        /// </summary>
        public override void Initialize()
        {
            GameEvents.OnGoldChanged += HandleGoldChanged;
            GameEvents.OnWaveChanged += HandleWaveChanged;
            GameEvents.OnCoreHealthChanged += HandleHealthChanged;
        }

        /// <summary>
        /// 구독했던 이벤트를 해제하여 메모리 누수를 방지합니다.
        /// </summary>
        public override void Dispose()
        {
            GameEvents.OnGoldChanged -= HandleGoldChanged;
            GameEvents.OnWaveChanged -= HandleWaveChanged;
            GameEvents.OnCoreHealthChanged -= HandleHealthChanged;
        }

        /// <summary>
        /// 골드 변경 이벤트를 처리하고 모델/뷰를 갱신합니다.
        /// </summary>
        private void HandleGoldChanged(int gold)
        {
            model.CurrentGold = gold;
            view.UpdateGold(gold);
        }

        /// <summary>
        /// 웨이브 변경 이벤트를 처리하고 모델/뷰를 갱신합니다.
        /// </summary>
        private void HandleWaveChanged(int wave)
        {
            model.CurrentWave = wave;
            view.UpdateWave(wave);
        }

        /// <summary>
        /// 체력 변경 이벤트를 처리하고 모델/뷰를 갱신합니다.
        /// </summary>
        private void HandleHealthChanged(float health)
        {
            model.CoreHealth = health;
            view.UpdateHealth(health);
        }
    }
}
