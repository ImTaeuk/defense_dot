// 아레나 HUD 뷰 — 패널의 value TMP·바를 값(숫자) 전용으로 갱신
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using DefenseDot.UI.Models;
using DefenseDot.UI.Presenters;

namespace DefenseDot.UI.Views
{
    /// <summary>
    /// 아레나 HUD 뷰입니다. 라벨은 패널이 직접 표시하므로 value(숫자)만 갱신합니다.
    /// </summary>
    public class ArenaHudView : HudRoot
    {
        [SerializeField] private TextMeshProUGUI roundValue;
        [SerializeField] private TextMeshProUGUI timeValue;
        [SerializeField] private TextMeshProUGUI goldValue;
        [SerializeField] private TextMeshProUGUI scoreValue;
        [SerializeField] private TextMeshProUGUI enemyValue;
        [SerializeField] private Image timeBarFill;
        [SerializeField] private Image enemyBarFill;

        /// <summary> 주어진 컨텍스트로 Arena HUD 프레젠터를 생성합니다. </summary>
        public override IPresenter Bind(in HudContext ctx)
            => new ArenaHudPresenter(this, new ArenaHudModel(),
                ctx.Wave, ctx.Economy, ctx.Score, ctx.Timer, ctx.EnemyCapacity);

        /// <summary> 라운드 표시를 갱신합니다. </summary>
        public void SetRound(int current, int total)
        {
            if (roundValue != null) roundValue.text = $"{current} / {total}";
        }

        /// <summary> 남은 시간 표시를 갱신합니다. </summary>
        public void SetTime(float remaining)
        {
            if (timeValue != null) timeValue.text = $"{Mathf.CeilToInt(remaining)}s";
        }

        /// <summary> 시간바를 갱신합니다. </summary>
        public void SetTimeBar(float ratio)
        {
            if (timeBarFill != null) timeBarFill.fillAmount = Mathf.Clamp01(ratio);
        }

        /// <summary> 골드 표시를 갱신합니다. </summary>
        public void SetGold(int amount)
        {
            if (goldValue != null) goldValue.text = amount.ToString();
        }

        /// <summary> 점수 표시를 갱신합니다. </summary>
        public void SetScore(int score)
        {
            if (scoreValue != null) scoreValue.text = score.ToString("N0");
        }

        /// <summary> 적 수 표시를 갱신합니다. </summary>
        public void SetEnemies(int alive, int capacity)
        {
            if (enemyValue != null) enemyValue.text = $"{alive} / {capacity}";
        }

        /// <summary> 적 바를 갱신합니다. </summary>
        public void SetEnemyBar(float ratio)
        {
            if (enemyBarFill != null) enemyBarFill.fillAmount = Mathf.Clamp01(ratio);
        }
    }
}
