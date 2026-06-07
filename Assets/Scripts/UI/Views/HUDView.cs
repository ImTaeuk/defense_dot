using UnityEngine;

namespace DefenseDot.UI.Views
{
    /// <summary>
    /// 하위 View 4종을 통솔하는 통합 HUD 루트 View입니다.
    /// </summary>
    public class HUDView : MonoBehaviour, IView
    {
        [Header("Sub Views")]
        [SerializeField] private GoldView goldView;
        [SerializeField] private HealthView healthView;
        [SerializeField] private RoundView roundView;
        [SerializeField] private EnemyCountView enemyCountView;

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

        /// <summary>
        /// HUD를 화면에 표시합니다.
        /// </summary>
        public void Show() => gameObject.SetActive(true);

        /// <summary>
        /// HUD를 화면에서 숨깁니다.
        /// </summary>
        public void Hide() => gameObject.SetActive(false);
    }
}
