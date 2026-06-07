// 아레나 모드 부트스트랩 — ArenaView config로 모델 생성·바인딩 후 ArenaMode 생성
using UnityEngine;
using DefenseDot.Data;
using DefenseDot.Domain.Models;
using DefenseDot.Systems.Arena;

namespace DefenseDot.Systems.Mode
{
    /// <summary>
    /// 아레나 모드 합성 루트입니다. ArenaView가 소유한 config로 ArenaModel을 만들어
    /// 바인딩한 뒤 ArenaMode를 생성합니다.
    /// </summary>
    public class ArenaModeBootstrap : ModeBootstrap
    {
        [SerializeField] private ArenaView arenaView;

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
            BindCamera(ctx);
            return new ArenaMode(arenaModel, ctx.CoreCenter, height);
        }
    }
}
