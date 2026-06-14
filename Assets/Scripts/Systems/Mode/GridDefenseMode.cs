// 그리드 타워디펜스 모드 — 경로 시작점 스폰, 경로추종 전략, 적 도달 시 코어 피해
using UnityEngine;
using DefenseDot.Data;
using DefenseDot.Domain.Models;
using DefenseDot.Systems.Enemy;
using DefenseDot.Systems.Pathfinding;

namespace DefenseDot.Systems.Mode
{
    /// <summary>
    /// 그리드 타워디펜스 모드입니다. 적이 셀 경로를 따라 이동하며, 코어 도달 시 코어 체력이 감소합니다.
    /// </summary>
    public class GridDefenseMode : IGameMode
    {
        private readonly CoreModel coreModel;
        private readonly MapData mapData;
        private readonly Vector3 origin;

        public GameModeType ModeType => GameModeType.GridDefense;

        public GridDefenseMode(CoreModel coreModel, MapData mapData, Vector3 origin)
        {
            this.coreModel = coreModel;
            this.mapData = mapData;
            this.origin = origin;
        }

        private BakedPath PathFor(int spawnIndex)
        {
            if (mapData == null || mapData.bakedPaths.Count == 0) return null;
            return mapData.bakedPaths[spawnIndex % mapData.bakedPaths.Count];
        }

        public Vector3 GetSpawnWorldPosition(int spawnIndex)
        {
            BakedPath path = PathFor(spawnIndex);
            if (path == null) return origin;
            return origin + new Vector3(path.spawnPos.x + 0.5f, 0.8f, path.spawnPos.y + 0.5f);
        }

        public IMovementStrategy CreateMovementStrategy(IMovableActor actor, float moveSpeed, int spawnIndex)
        {
            var follower = new PathFollowerLogic(actor, moveSpeed);
            BakedPath path = PathFor(spawnIndex);
            if (path != null) follower.SetPath(path.path);
            return follower;
        }

        public void OnEnemyReachedGoal(float damage) => coreModel.ApplyDamage(damage);

        public bool CheckDefeat(int activeEnemyCount) => false;

        public bool WinsOnWaveClear => true;

        /// <summary> Grid는 코어 HP를 본진 피해로 관리하므로 수용 HP 표시 안 함. </summary>
        public bool TryGetCapacityHp(int activeEnemyCount, out float hp)
        {
            hp = 0f;
            return false;
        }
    }
}
