using UnityEngine;
using UnityEngine.UIElements;
using DefenseDot.Systems.Enemy;

namespace DefenseDot.UI.InGame
{
    /// <summary>
    /// EnemySpawner의 데이터를 WaveHUD(UI)에 반영하는 Presenter 클래스입니다.
    /// </summary>
    public class WaveHUDPresenter : MonoBehaviour
    {
        [Header("References")]
        public UIDocument uiDocument;
        public EnemySpawner spawner;

        private Label waveLabel;
        private Label enemiesLabel;

        private void OnEnable()
        {
            if (uiDocument == null) uiDocument = GetComponent<UIDocument>();
            
            var root = uiDocument.rootVisualElement;
            waveLabel = root.Q<Label>("wave-label");
            enemiesLabel = root.Q<Label>("enemies-label");

            if (spawner != null)
            {
                spawner.OnWaveChanged += UpdateWaveUI;
                spawner.OnEnemiesRemainingChanged += UpdateEnemiesUI;
            }
        }

        private void OnDisable()
        {
            if (spawner != null)
            {
                spawner.OnWaveChanged -= UpdateWaveUI;
                spawner.OnEnemiesRemainingChanged -= UpdateEnemiesUI;
            }
        }

        private void UpdateWaveUI(int current, int total)
        {
            if (waveLabel != null)
                waveLabel.text = $"WAVE: {current} / {total}";
        }

        private void UpdateEnemiesUI(int remaining)
        {
            if (enemiesLabel != null)
                enemiesLabel.text = $"ENEMIES: {remaining}";
        }
    }
}
