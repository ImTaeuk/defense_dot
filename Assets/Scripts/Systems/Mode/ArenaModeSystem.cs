// 아레나 모드 부트스트랩 — ArenaView config로 모델 생성·바인딩 후 ArenaMode 생성
using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using UnityEngine;
using DefenseDot.Data;
using DefenseDot.Domain;
using DefenseDot.Domain.Models;
using DefenseDot.Systems.Arena;
using DefenseDot.Systems.Tower;
using DefenseDot.Systems.Abilities;
using DefenseDot.Systems.Loading;
using DefenseDot.Systems.Visual;

namespace DefenseDot.Systems.Mode
{
    /// <summary>
    /// 아레나 모드 합성 루트입니다. ArenaView가 소유한 config로 ArenaModel을 만들어
    /// 바인딩한 뒤 ArenaMode를 생성하고, 타워 능력 준비를 로딩에 등록합니다.
    /// </summary>
    public class ArenaModeSystem : ModeSystem, ISceneWarmup
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

        /// <summary> 타워 비주얼로 쓸 캐릭터 프리팹(애니메이션·연출 포함). </summary>
        [SerializeField] private CharacterVisual characterVisualPrefab;

        [Header("카드 시스템 (A3)")]
        /// <summary> 카드 선택 허브 설정(정지·곡선·티어). </summary>
        [SerializeField] private DefenseDot.Systems.Cards.ArenaCardConfig cardConfig;

        /// <summary> "신규 능력" 카드 후보 풀. </summary>
        [SerializeField] private DefenseDot.Systems.Cards.AbilityPool abilityPool;

        private TowerAbilitySystem towerAbility;

        /// <summary> 타워 능력 시스템. 현재 소비처는 에디터 치트 도구뿐입니다. </summary>
        public TowerAbilitySystem TowerAbility => towerAbility;

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
            builder.CoreTarget = towerAbility;

            // 계보를 소유한 쪽이 합성 시스템을 만든다. null 세트는 FusionSystem 이 건너뛴다
            builder.Fusion = new DefenseDot.Systems.Cards.FusionSystem(
                new[] { universalLineage, CharacterLineage });
        }

        /// <summary> 타워 능력 시스템을 한 프레임 진행시킵니다. </summary>
        /// <param name="deltaTime">경과 시간</param>
        public override void Tick(float deltaTime)
        {
            towerAbility?.Tick(deltaTime);
        }

        /// <summary> 타워 능력 시스템을 만들고 캐릭터 연출·의존성을 연결합니다. </summary>
        /// <param name="ctx">코어 중심·타겟 탐색기·풀을 담은 모드 컨텍스트</param>
        private void SetupTower(ModeContext ctx)
        {
            if (towerData == null || ctx.TargetFinder == null)
                return;

            towerAbility = new TowerAbilitySystem();

            // 캐릭터 비주얼을 먼저 생성해 발사점(총구)을 확보 → 능력 시스템에 주입
            CharacterVisual characterVisual = SpawnCharacterVisual(ctx);
            Transform fireOrigin = characterVisual != null ? characterVisual.FirePoint : null;

            // 모션·캐스트 애니메이션·기본 공격 속도를 먼저 주입해야 무기가 올바른 값으로 생성된다
            if (characterVisual != null)
                towerAbility.SetAttackMotion(characterVisual);

            towerAbility.SetCastAnimation(towerData.castAnimation);
            towerAbility.SetBaseAttackSpeed(towerData.attackSpeed);

            // 타워 기본 공격을 스타터 맨 앞에 합성(중복은 로드아웃 Contains가 방어)
            List<AbilityData> starters = new List<AbilityData>();
            if (towerData.basicAttack != null)
                starters.Add(towerData.basicAttack);

            starters.AddRange(starterAbilities);

            towerAbility.Setup(ctx.TargetFinder, ctx.CoreCenter, ctx.CombatState, starters, ctx.Pooling, fireOrigin);

            // 예열은 로딩이 기다린다. 로더 없는 씬 단독 실행이면 스스로 수행
            if (SceneLoadManager.Instance != null)
                SceneLoadManager.Instance.RegisterWarmup(this);
            else
                WarmupAsync(destroyCancellationToken).Forget();

            // 총구 확보 후 비주얼에 능력 시스템 연동
            if (characterVisual != null)
                characterVisual.Setup(towerAbility, ctx.TargetFinder, ctx.Flow, ctx.Core);
        }

        /// <summary> 캐릭터 3D 연출 타워를 코어 위치에 생성해 비주얼을 반환합니다. </summary>
        /// <param name="ctx">코어 중심 좌표를 담은 모드 컨텍스트</param>
        private CharacterVisual SpawnCharacterVisual(ModeContext ctx)
        {
            if (characterVisualPrefab == null)
                return null;

            return Instantiate(characterVisualPrefab, ctx.CoreCenter, Quaternion.identity);
        }

        /// <summary> 타워 능력 시스템을 정리합니다. </summary>
        private void OnDestroy()
        {
            towerAbility?.Dispose();
        }

        /// <summary> 타워 능력을 사용 가능한 상태로 만듭니다(예열 후 장착). </summary>
        /// <param name="cancellationToken">씬 파괴 등으로 중단할 때 쓰는 토큰</param>
        public async UniTask WarmupAsync(System.Threading.CancellationToken cancellationToken)
        {
            if (towerAbility == null)
                return;

            bool canceled = await towerAbility.WarmupStartersAsync()
                .AttachExternalCancellation(cancellationToken)
                .SuppressCancellationThrow();
            if (canceled)
                return;

            towerAbility.EquipAll();   // 예열 성패 무관하게 장착
        }
    }
}
