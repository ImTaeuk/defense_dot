// 원형 아레나 모드 — 극좌표 도넛 밴드 스폰, 공전 전략 생성, 수용 한계 패배
using UnityEngine;
using DefenseDot.Domain.Models;
using DefenseDot.Systems.Enemy;

namespace DefenseDot.Systems.Mode
{
    /// <summary>
    /// 원형 아레나 모드입니다. 적이 도넛 밴드에 랜덤 스폰되어 중앙 코어를 공전하며,
    /// 동시 생존 적이 수용 한계를 넘으면 패배합니다.
    /// </summary>
    public class ArenaMode : IGameMode
    {
        private readonly ArenaModel arena;
        private readonly Vector3 center;
        private readonly float enemyHeight;

        public GameModeType ModeType => GameModeType.Arena;

        public ArenaMode(ArenaModel arena, Vector3 center, float enemyHeight)
        {
            this.arena = arena;
            this.center = center;
            this.enemyHeight = enemyHeight;
        }

        public Vector3 GetSpawnWorldPosition(int spawnIndex)
        {
            float angle = Random.value * Mathf.PI * 2f;
            float radius = Random.Range(arena.SpawnMinRadius, arena.SpawnMaxRadius);
            return center + new Vector3(Mathf.Cos(angle) * radius, enemyHeight, Mathf.Sin(angle) * radius);
        }

        public IMovementStrategy CreateMovementStrategy(IMovableActor actor, float moveSpeed, int spawnIndex)
        {
            float startAngle = Random.value * Mathf.PI * 2f;
            float startRatio = Random.value;
            return new ArenaOrbitLogic(actor, center, arena, startAngle, startRatio, moveSpeed, enemyHeight);
        }

        public void OnEnemyReachedGoal(float damage)
        {
            // 코어 도달 패배 없음
        }

        public bool CheckDefeat(int activeEnemyCount) => activeEnemyCount >= arena.MaxAlive;
    }
}
