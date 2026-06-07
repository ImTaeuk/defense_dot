using UnityEngine;
using TMPro;

namespace DefenseDot.UI.Views
{
    /// <summary>
    /// 라운드 번호를 표시하는 하위 View입니다.
    /// </summary>
    public class RoundView : MonoBehaviour
    {
        [SerializeField] private TextMeshProUGUI roundText;

        private void Awake()
        {
            if (!IsValid()) Debug.LogError($"[RoundView] roundText 미할당: {name}", this);
        }

        /// <summary>
        /// 라운드 텍스트를 갱신합니다.
        /// </summary>
        public void SetRound(int current, int total)
        {
            if (roundText == null) return;
            roundText.text = $"라운드 {current}/{total}";
        }

        private bool IsValid() => roundText != null;
    }
}
