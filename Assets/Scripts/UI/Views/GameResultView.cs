using UnityEngine;
using UnityEngine.UI;
using TMPro;
using DefenseDot.UI.Base;

namespace DefenseDot.UI.Views
{
    /// <summary> 승/패 결과 패널과 재시작 버튼을 표시하는 View 입니다. </summary>
    public sealed class GameResultView : UIView
    {
        [SerializeField] private GameObject panel;
        [SerializeField] private TextMeshProUGUI messageText;
        [SerializeField] private Button restartButton;

        /// <summary> 재시작 버튼이 눌림. </summary>
        public event System.Action OnRestart;

        protected override void Awake()
        {
            base.Awake();
            if (restartButton != null) restartButton.onClick.AddListener(() => OnRestart?.Invoke());
            if (panel != null) panel.SetActive(false);
        }

        /// <summary> 결과 메시지를 설정하고 패널을 표시합니다. </summary>
        public void ShowResult(bool won)
        {
            Show();   // gameObject 활성화(UIView)
            if (messageText != null) messageText.text = won ? "승리!" : "패배";
            if (panel != null) panel.SetActive(true);
        }

        /// <summary> 결과 패널을 숨깁니다. </summary>
        protected override void OnHide()
        {
            if (panel != null) panel.SetActive(false);
        }
    }
}
