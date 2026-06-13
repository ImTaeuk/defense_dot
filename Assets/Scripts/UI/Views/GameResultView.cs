using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace DefenseDot.UI.Views
{
    /// <summary> 승/패 결과 패널과 재시작 버튼을 표시하는 View 입니다. (dumb) </summary>
    public class GameResultView : MonoBehaviour
    {
        [SerializeField] private GameObject panel;
        [SerializeField] private TextMeshProUGUI messageText;
        [SerializeField] private Button restartButton;

        /// <summary> 재시작 버튼이 눌림. </summary>
        public event System.Action OnRestart;

        private void Awake()
        {
            if (restartButton != null) restartButton.onClick.AddListener(() => OnRestart?.Invoke());
            if (panel != null) panel.SetActive(false);
        }

        /// <summary> 결과 패널을 표시합니다. won=true 승리, false 패배. </summary>
        public void Show(bool won)
        {
            if (messageText != null) messageText.text = won ? "승리!" : "패배";
            if (panel != null) panel.SetActive(true);
        }

        /// <summary> 결과 패널을 숨깁니다. </summary>
        public void Hide()
        {
            if (panel != null) panel.SetActive(false);
        }
    }
}
