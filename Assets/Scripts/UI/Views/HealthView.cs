using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace DefenseDot.UI.Views
{
    /// <summary>
    /// 코어 체력 텍스트와 게이지바를 표시하는 하위 View입니다.
    /// </summary>
    public class HealthView : MonoBehaviour
    {
        [SerializeField] private TextMeshProUGUI healthText;
        [SerializeField] private Image fillBar;

        private void Awake()
        {
            if (!IsValid()) Debug.LogError($"[HealthView] 참조 미할당: {name}", this);
        }

        /// <summary>
        /// 체력 텍스트와 게이지를 갱신합니다.
        /// </summary>
        public void SetHealth(float current, float max, float ratio)
        {
            if (healthText != null) healthText.text = $"{Mathf.CeilToInt(current)} / {Mathf.CeilToInt(max)}";
            if (fillBar != null) fillBar.fillAmount = ratio;
        }

        private bool IsValid() => healthText != null && fillBar != null;
    }
}
