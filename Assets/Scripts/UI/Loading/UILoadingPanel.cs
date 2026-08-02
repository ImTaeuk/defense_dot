// 로딩 진행을 구간별 실측값으로 표시만 하는 패널
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using DefenseDot.Systems.Loading;
using DefenseDot.UI.Base;

namespace DefenseDot.UI.Loading
{
    /// <summary> 로딩 진행을 구간별로 표시만 하는 패널입니다. 로드를 수행하거나 완료를 판정하지 않습니다. </summary>
    public sealed class UILoadingPanel : UIPanel, SceneLoadManager.ILoadingObserver
    {
        private const string SCENE_STAGE_TEXT = "불러오는 중";

        private const string WARMUP_STAGE_TEXT = "준비 중";

        /// <summary> 진행률을 채워 보이는 바. fillAmount로 그린다. </summary>
        [SerializeField] private Image barFill;

        /// <summary> 현재 구간을 알리는 문구. </summary>
        [SerializeField] private TextMeshProUGUI stageLabel;

        /// <summary> 진행률 백분율 표시. </summary>
        [SerializeField] private TextMeshProUGUI percentLabel;

        /// <summary> 로더 구독을 시작합니다. Awake가 아닌 이유는 로더의 Awake를 기다려야 하기 때문입니다. </summary>
        private void Start()
        {
            if (SceneLoadManager.Instance == null)
            {
                Hide();   // 로더 없는 씬에서는 로딩 화면이 설 자리가 없다
                return;
            }

            SceneLoadManager.Instance.RegisterObserver(this);
        }

        /// <summary> 로더 구독을 해제합니다. </summary>
        private void OnDestroy()
        {
            if (SceneLoadManager.Instance != null)
                SceneLoadManager.Instance.UnregisterObserver(this);
        }

        /// <summary> 현재 구간과 실측 진행률을 읽어 표시를 갱신합니다. </summary>
        public void OnLoadingStateChanged()
        {
            SceneLoadManager loader = SceneLoadManager.Instance;
            if (loader == null)
                return;

            switch (loader.State)
            {
                case SceneLoadManager.LoadingState.Idle:
                    break;   // 개시 전 — 부팅 직후의 표시 상태를 그대로 둔다

                case SceneLoadManager.LoadingState.LoadingScene:
                    ShowOnce();
                    Draw(SCENE_STAGE_TEXT, loader.SceneProgress);
                    break;

                case SceneLoadManager.LoadingState.Warmup:
                    ShowOnce();
                    Draw(WARMUP_STAGE_TEXT, loader.WarmupProgress);
                    break;

                case SceneLoadManager.LoadingState.Complete:
                    Hide();
                    break;

                default:
                    throw new System.ArgumentOutOfRangeException(nameof(loader.State), loader.State, "처리되지 않은 값입니다.");
            }
        }

        /// <summary> 구간 문구와 진행률을 그립니다. </summary>
        /// <param name="stageText">현재 구간을 알리는 문구</param>
        /// <param name="progress">해당 구간의 진행률(0~1)</param>
        private void Draw(string stageText, float progress)
        {
            float clamped = Mathf.Clamp01(progress);
            if (barFill != null)
                barFill.fillAmount = clamped;

            if (stageLabel != null)
                stageLabel.text = stageText;

            if (percentLabel != null)
                percentLabel.text = $"{Mathf.RoundToInt(clamped * 100f)}%";
        }

        /// <summary> 꺼져 있을 때만 표시합니다. 매 통보마다 OnShown이 터지는 것을 막습니다. </summary>
        private void ShowOnce()
        {
            if (gameObject.activeSelf)
                return;

            Show();
        }
    }
}