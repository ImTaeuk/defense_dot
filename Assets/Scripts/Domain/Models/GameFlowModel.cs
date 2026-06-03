// 게임 진행 단계 상태를 소유·통지하는 도메인 모델
using UnityEngine;

namespace DefenseDot.Domain.Models
{
    /// <summary>
    /// 게임 진행 단계(Ready/Playing/GameOver/Victory)를 소유하고 통지하는 도메인 모델입니다.
    /// </summary>
    [System.Serializable]
    public class GameFlowModel : BaseModel
    {
        [SerializeField] private GamePhase phase = GamePhase.Ready;

        /// <summary>
        /// 게임 단계가 변경되면 발생합니다. (변경된 단계)
        /// </summary>
        [field: System.NonSerialized]
        public event System.Action<GamePhase> OnPhaseChanged;

        /// <summary>
        /// 현재 게임 단계입니다.
        /// </summary>
        public GamePhase Phase => phase;

        /// <summary>
        /// 게임이 진행 중인지 여부입니다.
        /// </summary>
        public bool IsPlaying => phase == GamePhase.Playing;

        /// <summary>
        /// 게임 단계를 전이하고 통지합니다.
        /// </summary>
        public void SetPhase(GamePhase next)
        {
            if (SetField(ref phase, next)) OnPhaseChanged?.Invoke(phase);
        }
    }
}
