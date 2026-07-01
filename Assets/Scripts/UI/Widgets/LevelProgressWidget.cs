using UnityEngine;
using UnityEngine.UI;
using TMPro;
using DefenseDot.UI.Base;
using DefenseDot.Domain.Models;

namespace DefenseDot.UI.Widgets
{
    /// <summary> 레벨과 다음 레벨업까지 진척(게이지+남은 처치)을 표시하는 위젯입니다. </summary>
    public sealed class LevelProgressWidget : UIWidget<LevelProgress>
    {
        [SerializeField] private TextMeshProUGUI levelText;
        [SerializeField] private TextMeshProUGUI remainingText;
        [SerializeField] private Image barFill;

        [Header("레벨업 펄스")]
        [SerializeField] private float pulseDuration = 0.35f;
        [SerializeField] private float pulseScale = 0.25f;

        private int lastLevel = -1;
        private float pulseRemaining;
        private RectTransform gaugeRect;
        private Color baseFillColor;
        private bool captured;

        /// <summary> 레벨 표시와 진척 게이지를 갱신하고, 레벨 상승 시 펄스를 켭니다. </summary>
        public override void SetData(LevelProgress data)
        {
            if (levelText != null) levelText.text = $"Lv {data.Level}";
            if (remainingText != null) remainingText.text = $"남은 {data.Remaining}";
            if (barFill != null) barFill.fillAmount = Mathf.Clamp01(data.Ratio);

            CaptureOnce();
            if (lastLevel >= 0 && data.Level > lastLevel) pulseRemaining = pulseDuration;
            lastLevel = data.Level;
        }

        // 게이지 참조·기본색을 최초 1회 캐시
        private void CaptureOnce()
        {
            if (captured || barFill == null) return;
            gaugeRect = barFill.transform.parent as RectTransform;
            baseFillColor = barFill.color;
            captured = true;
        }

        private void Update()
        {
            if (pulseRemaining <= 0f) return;
            pulseRemaining -= Time.unscaledDeltaTime;
            float k = Mathf.Clamp01(pulseRemaining / pulseDuration);
            if (gaugeRect != null) gaugeRect.localScale = Vector3.one * (1f + pulseScale * k);
            if (barFill != null) barFill.color = Color.Lerp(baseFillColor, Color.white, k);
            if (pulseRemaining <= 0f) ResetPulse();
        }

        private void ResetPulse()
        {
            pulseRemaining = 0f;
            if (gaugeRect != null) gaugeRect.localScale = Vector3.one;
            if (barFill != null) barFill.color = baseFillColor;
        }
    }
}
