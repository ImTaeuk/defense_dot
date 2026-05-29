using System;

namespace DefenseDot.Core
{
    /// <summary>
    /// 시스템 간의 낮은 결합도를 위한 전역 게임 이벤트 센터입니다.
    /// 관찰자(Observer) 패턴의 핵심 역할을 수행합니다.
    /// </summary>
    public static class GameEvents
    {
        // === UI 및 상태 관련 이벤트 ===

        /// <summary>
        /// 소지 골드가 변경되었을 때 발생합니다. (매개변수: 변경된 골드 총량)
        /// </summary>
        public static Action<int> OnGoldChanged;

        /// <summary>
        /// 현재 웨이브 단계가 변경되었을 때 발생합니다. (매개변수: 현재 웨이브 번호)
        /// </summary>
        public static Action<int> OnWaveChanged;

        /// <summary>
        /// 코어(본진)의 체력이 변경되었을 때 발생합니다. (매개변수: 현재 체력 %)
        /// </summary>
        public static Action<float> OnCoreHealthChanged;


        // === 게임플레이 진행 관련 이벤트 ===

        /// <summary>
        /// 적이 처치되었을 때 발생합니다. (매개변수: 획득할 골드 보상)
        /// </summary>
        public static Action<int> OnEnemyDied;

        /// <summary>
        /// 게임 오버 상태가 되었을 때 발생합니다.
        /// </summary>
        public static Action OnGameOver;

        /// <summary>
        /// 한 웨이브의 모든 적을 소탕했을 때 발생합니다.
        /// </summary>
        public static Action OnWaveCleared;
    }
}
