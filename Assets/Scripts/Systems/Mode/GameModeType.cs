// 게임 모드 종류 정의 — 원형 아레나 / 그리드 타워디펜스
namespace DefenseDot.Systems.Mode
{
    /// <summary>
    /// 게임 모드 종류입니다. 적 이동·패배 조건이 모드별로 달라집니다.
    /// </summary>
    public enum GameModeType
    {
        /// <summary> 중앙 코어를 적이 공전하는 원형 아레나 모드 </summary>
        Arena,
        /// <summary> 셀 경로를 적이 이동하는 그리드 타워디펜스 모드 </summary>
        GridDefense
    }
}
