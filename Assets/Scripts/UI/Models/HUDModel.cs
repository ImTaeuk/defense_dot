using DefenseDot.UI;

namespace DefenseDot.UI.Models
{
    /// <summary>
    /// 통합 HUD에서 관리하는 데이터 상태 스냅샷 모델입니다. (표시용 캐시, 통지 없음)
    /// </summary>
    public class HUDModel : BaseModel
    {
        /// <summary>
        /// 현재 플레이어의 소지 골드입니다.
        /// </summary>
        public int CurrentGold { get; set; }

        /// <summary>
        /// 현재 진행 중인 웨이브(라운드) 번호입니다.
        /// </summary>
        public int CurrentWave { get; set; }

        /// <summary>
        /// 전체 웨이브(라운드) 수입니다.
        /// </summary>
        public int RoundTotal { get; set; }

        /// <summary>
        /// 코어(본진)의 현재 체력 비율입니다.
        /// </summary>
        public float CoreHealth { get; set; }

        /// <summary>
        /// 현재 생존 적 수입니다.
        /// </summary>
        public int EnemyAlive { get; set; }

        /// <summary>
        /// 적 수용 한계입니다.
        /// </summary>
        public int EnemyCapacity { get; set; }
    }
}
