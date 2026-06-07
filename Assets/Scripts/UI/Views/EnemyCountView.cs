using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace DefenseDot.UI.Views
{
    /// <summary>
    /// 생존 적 수와 수용 한계를 표시하고 위험 게이지를 갱신하는 하위 View입니다.
    /// </summary>
    public class EnemyCountView : MonoBehaviour
    {
        [SerializeField] private TextMeshProUGUI countText;
        [SerializeField] private Image fillBar;
        [SerializeField] private Color normalColor = new Color(1f, 0.72f, 0.42f);
        [SerializeField] private Color warnColor = new Color(1f, 0.53f, 0.33f);
        [SerializeField] private Color dangerColor = new Color(1f, 0.2f, 0.27f);

        private void Awake()
        {
            if (!IsValid()) Debug.LogError($"[EnemyCountView] 참조 미할당: {name}", this);
        }

        /// <summary>
        /// 적 수 텍스트와 위험 게이지를 갱신합니다.
        /// </summary>
        public void SetEnemyCount(int alive, int capacity)
        {
            float ratio = capacity > 0 ? alive / (float)capacity : 0f;
            if (countText != null) countText.text = $"잔여 적 {alive} / {capacity}";
            if (fillBar == null) return;
            fillBar.fillAmount = ratio;
            fillBar.color = ratio > 0.75f ? dangerColor : ratio > 0.5f ? warnColor : normalColor;
        }

        private bool IsValid() => countText != null && fillBar != null;
    }
}
