using UnityEngine;
using TMPro;
using DefenseDot.UI;

namespace DefenseDot.UI.Views
{
    /// <summary>
    /// 화면 상단 HUD UI를 시각적으로 제어하는 View 클래스입니다.
    /// </summary>
    public class HUDView : MonoBehaviour, IView
    {
        [Header("UI References")]
        [SerializeField, Tooltip("골드 표시 텍스트")] 
        private TextMeshProUGUI goldText;
        [SerializeField, Tooltip("웨이브 표시 텍스트")] 
        private TextMeshProUGUI waveText;
        [SerializeField, Tooltip("체력 표시 텍스트")] 
        private TextMeshProUGUI healthText;

        /// <summary>
        /// 골드 텍스트를 최신값으로 갱신합니다.
        /// </summary>
        public void UpdateGold(int gold) => goldText.text = $"Gold: {gold}";

        /// <summary>
        /// 웨이브 텍스트를 최신값으로 갱신합니다.
        /// </summary>
        public void UpdateWave(int wave) => waveText.text = $"Wave: {wave}";

        /// <summary>
        /// 체력 텍스트를 최신값으로 갱신합니다.
        /// </summary>
        public void UpdateHealth(float health) => healthText.text = $"HP: {health}";

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
