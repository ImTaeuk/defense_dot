using DefenseDot.UI;

namespace DefenseDot.UI.Models
{
    /// <summary>
    /// HUD UI에서 관리하는 데이터 상태 모델입니다.
    /// </summary>
    public class HUDModel : BaseModel
    {
        /// <summary>
        /// 현재 플레이어의 소지 골드입니다.
        /// </summary>
        public int CurrentGold { get; set; }

        /// <summary>
        /// 현재 진행 중인 웨이브 번호입니다.
        /// </summary>
        public int CurrentWave { get; set; }

        /// <summary>
        /// 코어(본진)의 현재 체력입니다.
        /// </summary>
        public float CoreHealth { get; set; }
    }
}
