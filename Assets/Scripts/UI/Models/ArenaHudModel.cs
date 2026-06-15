using DefenseDot.UI;

namespace DefenseDot.UI.Models
{
    /// <summary>
    /// 아레나 HUD 표시 상태 스냅샷 모델입니다. (표시용 캐시, 통지 없음)
    /// </summary>
    public class ArenaHudModel : BaseModel
    {
        /// <summary>현재 진행 중인 라운드(웨이브) 번호입니다.</summary>
        public int CurrentWave { get; set; }

        /// <summary>전체 라운드(웨이브) 수입니다.</summary>
        public int RoundTotal { get; set; }

        /// <summary>현재 라운드 남은 시간(초)입니다.</summary>
        public float TimeRemaining { get; set; }

        /// <summary>현재 소지 골드입니다.</summary>
        public int CurrentGold { get; set; }

        /// <summary>현재 누적 점수입니다.</summary>
        public int Score { get; set; }

        /// <summary>현재 생존 적 수입니다.</summary>
        public int EnemyAlive { get; set; }

        /// <summary>적 수용 한계입니다.</summary>
        public int EnemyCapacity { get; set; }
    }
}
