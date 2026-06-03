// 적 이동 전략 추상화 — 공전/경로추종을 좌표계 독립으로 교체
namespace DefenseDot.Systems.Enemy
{
    /// <summary>
    /// 적의 이동 방식을 캡슐화하는 전략(Strategy) 인터페이스입니다.
    /// 공전(아레나)·경로추종(타워디펜스) 등 좌표계에 독립적으로 교체됩니다.
    /// </summary>
    public interface IMovementStrategy
    {
        /// <summary>
        /// 매 프레임 이동을 갱신합니다.
        /// </summary>
        void Tick(float deltaTime);

        /// <summary>
        /// 목표(경로 끝/코어)에 도달했는지 여부입니다.
        /// </summary>
        bool HasReachedGoal { get; }
    }
}
