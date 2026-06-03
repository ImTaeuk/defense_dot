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

        public override IGameMode CreateMode(ModeContext ctx)
        {
            if (placement != null) placement.Bind(ctx.Economy, ctx.TargetFinder);
            return new GridDefenseMode(ctx.Core, mapData, ctx.SpawnOrigin);
        }
    }
}
