// HUD 프레젠터 조립에 필요한 도메인 모델 묶음(파라미터 오브젝트)
using DefenseDot.Domain.Models;

namespace DefenseDot.UI
{
    /// <summary>
    /// HUD 프레젠터 조립에 필요한 도메인 모델·설정을 묶은 파라미터 오브젝트입니다.
    /// 각 HudRoot가 필요한 항목만 선택해 사용합니다.
    /// </summary>
    public readonly struct HudContext
    {
        /// <summary>골드 재화 모델입니다.</summary>
        public readonly EconomyModel Economy;

        /// <summary>코어 체력 모델입니다. (Grid HUD 전용)</summary>
        public readonly CoreModel Core;

        /// <summary>웨이브 진행 모델입니다.</summary>
        public readonly WaveModel Wave;

        /// <summary>인-런 점수 모델입니다. (Arena HUD 전용)</summary>
        public readonly ScoreModel Score;

        /// <summary>라운드 제한시간 모델입니다. (Arena HUD 전용)</summary>
        public readonly RoundTimerModel Timer;

        /// <summary>적 수용 한계입니다.</summary>
        public readonly int EnemyCapacity;

        /// <summary> HUD 조립에 필요한 모델·설정을 묶습니다. </summary>
        public HudContext(EconomyModel economy, CoreModel core, WaveModel wave,
            ScoreModel score, RoundTimerModel timer, int enemyCapacity)
        {
            Economy = economy;
            Core = core;
            Wave = wave;
            Score = score;
            Timer = timer;
            EnemyCapacity = enemyCapacity;
        }
    }
}
