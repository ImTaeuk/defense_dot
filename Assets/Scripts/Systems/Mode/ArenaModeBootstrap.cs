// 아레나 모드 부트스트랩 — ArenaView config로 모델 생성·바인딩 후 ArenaMode 생성
using UnityEngine;
using DefenseDot.Data;
using DefenseDot.Domain.Models;
using DefenseDot.Systems.Arena;
using DefenseDot.Systems.Tower;

namespace DefenseDot.Systems.Mode
{
    /// <summary>
    /// 아레나 모드 합성 루트입니다. ArenaView가 소유한 config로 ArenaModel을 만들어
    /// 바인딩한 뒤 ArenaMode를 생성합니다.
    /// </summary>
    public class ArenaModeBootstrap : ModeBootstrap
    {
        [SerializeField] private ArenaView arenaView;

        /// <summary> 중앙에 생성할 타워 데이터입니다. (추후 선택 UI 주입점) </summary>
        [SerializeField] private TowerData centerTowerData;

        /// <summary> 아레나 모드의 적 수 표시 한계(수용 한계)입니다. </summary>
        public override int EnemyDisplayCapacity =>
            arenaView != null && arenaView.Config != null ? arenaView.Config.maxAlive : 80;

        public override IGameMode CreateMode(ModeContext ctx)
        {
            var arenaModel = new ArenaModel();
            ArenaConfig config = arenaView != null ? arenaView.Config : null;
            if (config != null)
            {
                arenaModel.Initialize(config.arenaRadius, config.coreRadius,
                    config.spawnInnerMargin, config.spawnOuterMargin, config.maxAlive);
            }
            float height = config != null ? config.enemyHeight : 0.8f;
            if (arenaView != null) arenaView.Bind(arenaModel);
            BindPresentation(ctx);
            SpawnCenterTower(ctx, config);
            return new ArenaMode(arenaModel, ctx.CoreCenter, height);
        }

        /// <summary> 아레나 중앙에 타워 1개를 생성하고 의존성을 주입합니다. 사거리는 맵 전체 반경으로 설정합니다. </summary>
        private void SpawnCenterTower(ModeContext ctx, ArenaConfig config)
        {
            if (centerTowerData == null || centerTowerData.prefab == null || ctx.TargetFinder == null) return;

            TowerData data = Instantiate(centerTowerData);              // 런타임 복제 → 원본 불변
            if (config != null) data.attackRange = config.arenaRadius;  // 맵 전체 반경
            GameObject go = Instantiate(data.prefab);
            go.name = "ArenaCenterTower";
            TowerActor tower = go.GetComponent<TowerActor>();
            if (tower == null) tower = go.AddComponent<TowerActor>();
            go.transform.position = ctx.CoreCenter;
            tower.Initialize(data);
            tower.SetTargetFinder(ctx.TargetFinder);
        }
    }
}
