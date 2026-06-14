// 게임 모드 추상화 — 스폰 위치/이동 전략 생성·도달 처리·패배 판정을 모드별로 분기
using UnityEngine;
using DefenseDot.Systems.Enemy;

namespace DefenseDot.Systems.Mode
{
    /// <summary>
    /// 게임 모드별 동작(스폰 위치, 이동 전략 생성, 적 도달 처리, 패배 판정)을 캡슐화하는 인터페이스입니다.
    /// </summary>
    public interface IGameMode
    {
        /// <summary> 현재 모드 종류입니다. </summary>
        GameModeType ModeType { get; }

        /// <summary> spawnIndex번째 적의 월드 스폰 위치를 반환합니다. (아레나=극좌표, TD=경로 시작점) </summary>
        Vector3 GetSpawnWorldPosition(int spawnIndex);

        /// <summary> 적에게 주입할 이동 전략을 생성합니다. (아레나=공전, TD=경로추종) </summary>
        IMovementStrategy CreateMovementStrategy(IMovableActor actor, float moveSpeed, int spawnIndex);

        /// <summary> 적이 목표에 도달했을 때 처리합니다. (TD=코어 피해, 아레나=무시) </summary>
        void OnEnemyReachedGoal(float damage);

        /// <summary> 활성 적 수를 근거로 패배 여부를 판정합니다. (아레나=수용 한계, TD=false) </summary>
        bool CheckDefeat(int activeEnemyCount);

        /// <summary> 웨이브를 모두 클리어하면 승리하는지 여부입니다. (아레나=false: 무한 생존) </summary>
        bool WinsOnWaveClear { get; }

        /// <summary> 코어 HP를 수용 한계로 표시하는 모드면 true와 표시 HP(=한계−생존수)를 반환합니다. (아레나=true, TD=false) </summary>
        bool TryGetCapacityHp(int activeEnemyCount, out float hp);
    }
}
