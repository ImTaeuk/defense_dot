using UnityEngine;
using TMPro;

namespace DefenseDot.UI.Views
{
    /// <summary>
    /// 골드 수치를 표시하는 하위 View입니다.
    /// </summary>
    public class GoldView : MonoBehaviour
    {
        [SerializeField] private TextMeshProUGUI goldText;

        private void Awake()
        {
            if (!IsValid()) Debug.LogError($"[GoldView] goldText 미할당: {name}", this);
        }

        /// <summary>
        /// 골드 텍스트를 갱신합니다.
        /// </summary>
        public void SetGold(int gold)
        {
            if (goldText == null) return;
            goldText.text = gold.ToString();
        }

        private bool IsValid() => goldText != null;
    }
}
