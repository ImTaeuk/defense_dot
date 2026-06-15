using UnityEngine;
using DefenseDot.UI.Models;
using DefenseDot.UI.Presenters;

namespace DefenseDot.UI.Views
{
    /// <summary>
    /// 하위 View 4종을 통솔하는 통합 HUD 루트 View입니다. (Grid 모드)
    /// </summary>
    public class HUDView : HudRoot
    {
        [Header("Sub Views")]
        [SerializeField] private GoldView goldView;
        [SerializeField] private HealthView healthView;
        [SerializeField] private RoundView roundView;
        [SerializeField] private EnemyCountView enemyCountView;

        /// <summary>
        /// 주어진 컨텍스트로 Grid HUD 프레젠터를 생성합니다.
        /// </summary>
        public override IPresenter Bind(in HudContext ctx)
            => new HUDPresenter(this, new HUDModel(), ctx.Economy, ctx.Core, ctx.Wave, ctx.EnemyCapacity);

        /// <summary>
        /// 골드 표시를 갱신합니다.
        /// </summary>
        public void UpdateGold(int gold) => goldView?.SetGold(gold);

        /// <summary>
        /// 체력 표시를 갱신합니다.
        /// </summary>
        public void UpdateHealth(float current, float max, float ratio) => healthView?.SetHealth(current, max, ratio);

        /// <summary>
        /// 라운드 표시를 갱신합니다.
        /// </summary>
        public void UpdateRound(int current, int total) => roundView?.SetRound(current, total);

        /// <summary>
        /// 적 수 표시를 갱신합니다.
        /// </summary>
        public void UpdateEnemyCount(int alive, int capacity) => enemyCountView?.SetEnemyCount(alive, capacity);
    }
}
