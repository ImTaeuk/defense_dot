// 사망 연출 계약 — 연출 재생 후 완료 콜백
namespace DefenseDot.Systems.Enemy
{
    /// <summary>
    /// 사망 연출을 재생하고 완료 시 콜백하는 비주얼 계약입니다.
    /// 구현이 없으면 액터는 즉시 풀로 반환됩니다.
    /// </summary>
    public interface IDeathVisual
    {
        /// <summary> 사망 연출을 재생하고, 끝나면 onComplete를 호출합니다. </summary>
        void PlayDeath(System.Action onComplete);
    }
}
