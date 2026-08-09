// UI 인스턴스를 어디서 얻는지 가르는 구분
namespace DefenseDot.UI.Base
{
    /// <summary> UI 인스턴스를 어디서 얻을지 가르는 구분입니다. </summary>
    public enum UIObjectType
    {
        /// <summary> 씬에 배치돼 있고 하나뿐입니다. 장부에서 꺼냅니다. </summary>
        Single,

        /// <summary> 프리팹에서 여러 개가 생깁니다. 풀에서 빌립니다. </summary>
        Poolable,
    }
}
