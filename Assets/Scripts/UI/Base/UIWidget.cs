// 복합 UI 요소(텍스트+이펙트 등)를 래핑하는 위젯 베이스
namespace DefenseDot.UI.Base
{
    /// <summary>
    /// 복합 UI 요소를 래핑하는 위젯 베이스입니다.
    /// 부모-자식 구성은 허용하되 형제 위젯을 참조하지 않습니다.
    /// </summary>
    public abstract class UIWidget : UIObject
    {
    }

    /// <summary>
    /// 표시 데이터(DTO) T로 갱신되는 위젯입니다. 표시 포맷팅을 스스로 소유합니다.
    /// </summary>
    /// <typeparam name="T">바인딩 표시 데이터 타입</typeparam>
    public abstract class UIWidget<T> : UIWidget
    {
        /// <summary> DTO로 이 위젯의 표시를 갱신합니다. </summary>
        public abstract void SetData(T data);
    }
}
