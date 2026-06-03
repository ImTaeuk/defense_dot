// 게임 진행 단계(FSM) 정의 — 무효한 상태 조합 방지
namespace DefenseDot.Domain
{
    /// <summary>
    /// 게임 진행 단계를 정의합니다. 무효한 상태 조합을 방지하는 유한 상태 기계(FSM)입니다.
    /// </summary>
    public enum GamePhase
    {
        Ready,
        Playing,
        GameOver,
        Victory
    }
}
