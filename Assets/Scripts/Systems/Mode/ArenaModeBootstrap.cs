// 아레나 모드 부트스트랩 — ArenaView config로 모델 생성·바인딩 후 ArenaMode 생성
using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using UnityEngine;
using DefenseDot.Data;
using DefenseDot.Domain.Models;
using DefenseDot.Systems.Arena;
using DefenseDot.Systems.Tower;
using DefenseDot.Systems.Abilities;

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

        /// <summary> 코어 스타터 능력(샷·오비탈 등). 카드 획득(A3) 전 기본 장착. </summary>
        [SerializeField] private List<AbilityData> starterAbilities = new List<AbilityData>();

        /// <summary> 코어 비주얼로 쓸 Aris 타워 프리팹(애니메이션·연출 포함). </summary>
        [SerializeField] private GameObject arisTowerPrefab;

        [Header("카드 시스템 (A3)")]
        /// <summary> 카드 선택 허브 설정(정지·곡선·티어). </summary>
        [SerializeField] private DefenseDot.Systems.Cards.ArenaCardConfig cardConfig;

        /// <summary> "신규 능력" 카드 후보 풀. </summary>
        [SerializeField] private DefenseDot.Systems.Cards.AbilityPool abilityPool;

        private CoreAbilitySystem coreAbility;

        /// <summary> 카드 허브 설정. </summary>
        public DefenseDot.Systems.Cards.ArenaCardConfig CardConfig => cardConfig;

        /// <summary> 신규 카드 능력 풀. </summary>
        public DefenseDot.Systems.Cards.AbilityPool AbilityPool => abilityPool;

        /// <summary> 코어 능력 시스템(카드 명령 대상). CreateMode 이후 non-null. </summary>
        public CoreAbilitySystem CoreAbility => coreAbility;

        /// <summary> 선택된 타워의 합성 계보(카드 생성용). </summary>
        public DefenseDot.Systems.Cards.FusionRecipeSet FusionLineage => centerTowerData != null ? centerTowerData.fusionLineage : null;

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

            // 코어: 디버그 단일공격 제거 + 능력 시스템 부착
            TowerBehaviorTree debugBt = go.GetComponent<TowerBehaviorTree>();
            if (debugBt != null) Destroy(debugBt);
            coreAbility = go.AddComponent<CoreAbilitySystem>();

            // Aris 비주얼을 먼저 생성해 발사점(총구)을 확보 → 능력 시스템에 주입
            ArisTowerVisual arisVisual = ReplaceWithArisVisual(go, ctx);
            Transform fireOrigin = arisVisual != null ? arisVisual.FirePoint : null;

            // 모션·기본 공격 속도를 먼저 주입해야 무기가 올바른 값으로 생성된다
            if (arisVisual != null) coreAbility.SetAttackMotion(arisVisual);
            coreAbility.SetBaseAttackSpeed(data.attackSpeed);

            coreAbility.Setup(ctx.TargetFinder, ctx.CoreCenter, ctx.Flow, ctx.CombatState, starterAbilities, ctx.Pooling, fireOrigin);
            StartCoreAbilities(coreAbility).Forget();   // 예열 → 장착 순서 조율

            // 총구 확보 후 비주얼에 능력 시스템 연동
            if (arisVisual != null) arisVisual.Setup(coreAbility, ctx.TargetFinder, ctx.Flow, ctx.Core);
        }

        /// <summary> 코어의 2D 스프라이트를 숨기고 Aris 3D 연출 타워를 코어 위치에 배치해 비주얼을 반환합니다. </summary>
        private ArisTowerVisual ReplaceWithArisVisual(GameObject coreTower, ModeContext ctx)
        {
            if (arisTowerPrefab == null) return null;

            SpriteRenderer[] sprites = coreTower.GetComponentsInChildren<SpriteRenderer>(true);
            for (int i = 0; i < sprites.Length; i++) sprites[i].enabled = false;

            GameObject aris = Instantiate(arisTowerPrefab, ctx.CoreCenter, Quaternion.identity);
            aris.name = "Aris_CoreTower";
            return aris.GetComponent<ArisTowerVisual>();
        }

        /// <summary> 코어 능력을 예열한 뒤 장착합니다(예열→장착 순서 조율). </summary>
        private static async UniTaskVoid StartCoreAbilities(CoreAbilitySystem core)
        {
            await core.WarmupStartersAsync();   // 예열(실패는 값으로 스킵, 예외 없음)
            core.EquipAll();                    // 장착(예열 성패 무관하게 실행)
        }
    }
}
