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
    public class GridDefenseModeSystem : ModeSystem
    {
        [SerializeField] private MapData mapData;
        [SerializeField] private TowerPlacementController placement;
        [SerializeField] private Transform mapRoot;
        [SerializeField] private int enemyDisplayCapacity = 80;
        [SerializeField] private float coreHp = 40f;

        /// <summary> 그리드 모드의 적 수 표시 한계입니다. </summary>
        public override int EnemyDisplayCapacity => enemyDisplayCapacity;

        /// <summary> 그리드 모드의 코어(본진) 최대 HP입니다. </summary>
        public override float CoreMaxHp => coreHp;

        /// <summary> 그리드 모드의 타워 배치 컨트롤러입니다. </summary>
        public override TowerPlacementController PlacementController => placement;

        /// <summary> 카메라 중심을 맵의 기하 중심으로 계산합니다. (맵 원점이 좌하단이라 보정) </summary>
        protected override Vector3 GetCameraCenter(in ModeContext ctx)
        {
            if (mapData == null) return base.GetCameraCenter(ctx);
            Vector3 origin = mapRoot != null ? mapRoot.position : Vector3.zero;
            float w = mapData.width * mapData.cellSize;
            float h = mapData.height * mapData.cellSize;
            return origin + new Vector3(w * 0.5f, 0f, h * 0.5f);
        }

        public override IGameMode CreateMode(ModeContext ctx)
        {
            if (placement != null) placement.Bind(ctx.TargetFinder);
            BindPresentation(ctx);
            return new GridDefenseMode(ctx.Core, mapData, ctx.SpawnOrigin);
        }
    }
}
