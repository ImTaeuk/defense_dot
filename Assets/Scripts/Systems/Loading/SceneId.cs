// 로드 대상 씬을 문자열 대신 값으로 고른다
namespace DefenseDot.Systems.Loading
{
    /// <summary> 로드할 수 있는 씬입니다. 인스펙터에서 목록으로 고릅니다. </summary>
    public enum SceneId
    {
        /// <summary> 원형 아레나 모드 </summary>
        Arena,

        /// <summary> 격자 디펜스 모드 </summary>
        Grid,
    }
}