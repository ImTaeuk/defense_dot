// 아레나 HUD 뷰 — 위젯들을 조립하고 Presenter가 위젯 단위로 Bind한다
using UnityEngine;
using DefenseDot.UI.Base;
using DefenseDot.UI.Widgets;
using DefenseDot.Domain.Models;

namespace DefenseDot.UI.Views
{
    /// <summary>
    /// 아레나 HUD 뷰입니다. 표시 포맷은 각 위젯이 소유하며, View는 위젯 조립과 위임만 합니다.
    /// </summary>
    public sealed class ArenaHudView : UIView
    {
        [SerializeField] private GoldWidget goldWidget;
        [SerializeField] private ScoreWidget scoreWidget;
        [SerializeField] private RoundWidget roundWidget;
        [SerializeField] private TimeWidget timeWidget;
        [SerializeField] private EnemyWidget enemyWidget;

        /// <summary> 골드 위젯을 갱신합니다. </summary>
        public void ApplyGold(int gold)
        {
            if (goldWidget != null) goldWidget.SetData(gold);
        }

        /// <summary> 점수 위젯을 갱신합니다. </summary>
        public void ApplyScore(int score)
        {
            if (scoreWidget != null) scoreWidget.SetData(score);
        }

        /// <summary> 라운드 위젯을 갱신합니다. </summary>
        public void ApplyRound(WaveProgress progress)
        {
            if (roundWidget != null) roundWidget.SetData(progress);
        }

        /// <summary> 시간 위젯을 갱신합니다. </summary>
        public void ApplyTime(TimerState time)
        {
            if (timeWidget != null) timeWidget.SetData(time);
        }

        /// <summary> 적 위젯을 갱신합니다. </summary>
        public void ApplyEnemies(EnemyState enemies)
        {
            if (enemyWidget != null) enemyWidget.SetData(enemies);
        }
    }
}
