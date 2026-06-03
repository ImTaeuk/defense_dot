// 웨이브 HUD — WaveModel을 구독하여 웨이브·잔여 적 수 표시
using UnityEngine;
using UnityEngine.UIElements;
using DefenseDot.Domain.Models;

namespace DefenseDot.UI.InGame
{
    /// <summary>
    /// WaveModel의 데이터를 WaveHUD(UIToolkit)에 반영하는 Presenter 클래스입니다.
    /// GameManager가 Bind로 모델을 주입합니다.
    /// </summary>
    public class WaveHUDPresenter : MonoBehaviour
    {
        [Header("References")]
        public UIDocument uiDocument;

        private Label waveLabel;
        private Label enemiesLabel;
        private WaveModel waveModel;

        private void OnEnable()
        {
            if (uiDocument == null) uiDocument = GetComponent<UIDocument>();

            var root = uiDocument.rootVisualElement;
            waveLabel = root.Q<Label>("wave-label");
            enemiesLabel = root.Q<Label>("enemies-label");

            Subscribe();
        }

        private void OnDisable() => Unsubscribe();

        /// <summary>
        /// 표시할 WaveModel을 주입합니다. (GameManager가 호출)
        /// </summary>
        public void Bind(WaveModel model)
        {
            Unsubscribe();
            waveModel = model;
            Subscribe();

            if (waveModel != null)
            {
                UpdateWaveUI(waveModel.Current, waveModel.Total);
                UpdateEnemiesUI(waveModel.Remaining);
            }
        }

        private void Subscribe()
        {
            if (waveModel == null) return;
            waveModel.OnWaveChanged += UpdateWaveUI;
            waveModel.OnRemainingChanged += UpdateEnemiesUI;
        }

        private void Unsubscribe()
        {
            if (waveModel == null) return;
            waveModel.OnWaveChanged -= UpdateWaveUI;
            waveModel.OnRemainingChanged -= UpdateEnemiesUI;
        }

        private void UpdateWaveUI(int current, int total)
        {
            if (waveLabel != null) waveLabel.text = $"WAVE: {current} / {total}";
        }

        private void UpdateEnemiesUI(int remaining)
        {
            if (enemiesLabel != null) enemiesLabel.text = $"ENEMIES: {remaining}";
        }
    }
}
