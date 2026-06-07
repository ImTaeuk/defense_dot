// 그리드 디펜스 모드 부트스트랩 — MapData·타워 배치 소유, GridDefenseMode 생성
using UnityEngine;
using DefenseDot.Data;
using DefenseDot.Systems.Tower;

namespace DefenseDot.Systems.Mode
{
    /// <summary>
    /// 그리드 타워디펜스 모드 합성 루트입니다. 맵 데이터와 타워 배치 컨트롤러를 보유하고
    /// GridDefenseMode를 생성하며, 타워 배치에 필요한 의존성을 주입합니다.
    /// </summary>
    public class GridDefenseModeBootstrap : ModeBootstrap
    {
        [SerializeField] private MapData mapData;
        [SerializeField] private TowerPlacementController placement;
        [SerializeField] private int enemyDisplayCapacity = 80;

        /// <summary> 그리드 모드의 적 수 표시 한계입니다. </summary>
        public override int EnemyDisplayCapacity => enemyDisplayCapacity;

        public override IGameMode CreateMode(ModeContext ctx)
        {
            if (placement != null) placement.Bind(ctx.Economy, ctx.TargetFinder);
            BindCamera(ctx);
            return new GridDefenseMode(ctx.Core, mapData, ctx.SpawnOrigin);
        }
    }
}
