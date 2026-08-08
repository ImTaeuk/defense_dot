// 아레나 모드 부트스트랩 — ArenaView config로 모델 생성·바인딩 후 ArenaMode 생성
using System.Collections.Generic;
using UnityEngine;
using DefenseDot.Data;
using DefenseDot.Domain;
using DefenseDot.Domain.Models;
using DefenseDot.Systems.Arena;
using DefenseDot.Systems.Tower;
using DefenseDot.Systems.Abilities;

namespace DefenseDot.Systems.Mode
{
    /// <summary>
    /// 아레나 모드 합성 루트입니다. ArenaView가 소유한 config로 ArenaModel을 만들어
    /// 바인딩한 뒤 ArenaMode를 생성하고, 이 판의 타워를 세워 능력·연출을 배선합니다.
    /// </summary>
    public class ArenaModeSystem : ModeSystem
    {
        [SerializeField] private ArenaView arenaView;

        /// <summary> 플레이할 타워(기본 공격·모션·공격속도·전용 계보 소유). </summary>
        [SerializeField] private TowerData towerData;

        /// <summary> 활성 공통 계보 세트(버전 갈아끼기 지점). </summary>
        [SerializeField] private DefenseDot.Systems.Cards.FusionRecipeSet universalLineage;

        /// <summary> 이 판에 쓸 타워 전용 계보. 계보는 획득 규칙이라 타워가 아니라 아레나가 소유한다. </summary>
        [SerializeField] private DefenseDot.Systems.Cards.FusionRecipeSet characterLineage;

        /// <summary> 타워 스타터 능력(샷·오비탈 등). 카드 획득(A3) 전 기본 장착. </summary>
        [SerializeField] private List<AbilityData> starterAbilities = new List<AbilityData>();

        [Header("카드 시스템 (A3)")]
        /// <summary> 카드 선택 허브 설정(정지·곡선·티어). </summary>
        [SerializeField] private DefenseDot.Systems.Cards.ArenaCardConfig cardConfig;

        /// <summary> "신규 능력" 카드 후보 풀. </summary>
        [SerializeField] private DefenseDot.Systems.Cards.AbilityPool abilityPool;

        private TowerActor tower;

        /// <summary> 이 판의 타워입니다(생성 전이면 null). 치트 도구와 FillContext 가 씁니다. </summary>
        public TowerActor Tower => tower;

        /// <summary> 이 판의 타워 전용 계보입니다(없으면 null). 치트 도구와 FillContext 가 씁니다. </summary>
        public DefenseDot.Systems.Cards.FusionRecipeSet CharacterLineage => characterLineage;

        /// <summary> 아레나 모드의 적 수 표시 한계(수용 한계)입니다. </summary>
        public override int EnemyDisplayCapacity =>
            arenaView != null && arenaView.Config != null ? arenaView.Config.maxAlive : 80;

        /// <summary> 아레나 모드의 코어 최대 HP = 수용 한계(maxAlive). </summary>
        public override float CoreMaxHp =>
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
            BindVisual(ctx);
            SetupTower(ctx);
            return new ArenaMode(arenaModel, ctx.CoreCenter, height);
        }

        /// <summary> 카드 허브 설정·능력 풀·계보 합성·능력 명령 대상을 채웁니다. </summary>
        /// <param name="builder">조립 중인 UI 컨텍스트</param>
        public override void FillContext(GameContextBuilder builder)
        {
            builder.CardConfig = cardConfig;
            builder.AbilityPool = abilityPool;
            builder.CoreTarget = tower != null ? tower.Abilities : null;

            // 계보를 소유한 쪽이 합성 시스템을 만든다. null 세트는 FusionSystem 이 건너뛴다
            builder.Fusion = new DefenseDot.Systems.Cards.FusionSystem(
                new[] { universalLineage, CharacterLineage });
        }

        /// <summary> 이 판의 타워를 생성하고 능력·연출을 배선합니다. </summary>
        /// <param name="ctx">코어 중심·타겟 탐색기·풀을 담은 모드 컨텍스트</param>
        private void SetupTower(ModeContext ctx)
        {
            if (towerData == null || ctx.TargetFinder == null)
                return;

            tower = SpawnTower(ctx);
            if (tower == null)
                return;

            // 능력은 타워가 소유한다. 구동·예열 등록도 타워가 스스로 한다
            tower.SetupAbilities(ctx.TargetFinder, ctx.CombatState, starterAbilities, ctx.Pooling, ctx.Flow);

            if (tower.Visual != null)
                tower.Visual.Setup(ctx.TargetFinder, ctx.Flow, ctx.Core);
        }

        /// <summary> 타워를 코어 위치에 생성해 액터를 반환합니다. </summary>
        /// <param name="ctx">코어 중심 좌표를 담은 모드 컨텍스트</param>
        private TowerActor SpawnTower(ModeContext ctx)
        {
            if (towerData.prefab == null)
                return null;

            GameObject instance = Instantiate(towerData.prefab, ctx.CoreCenter, Quaternion.identity);
            return instance.GetComponent<TowerActor>();
        }

    }
}
