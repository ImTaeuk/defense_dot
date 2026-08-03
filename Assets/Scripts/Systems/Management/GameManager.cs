// 합성 루트 — 도메인 모델 생성·주입과 승패 판정 총괄
using Cysharp.Threading.Tasks;
using UnityEngine;
using DefenseDot.Domain;
using DefenseDot.Domain.Models;
using DefenseDot.Systems.Enemy;
using DefenseDot.Systems.Economy;
using DefenseDot.Systems.Loading;
using DefenseDot.Systems.Mode;
using DefenseDot.Systems.Tower;
using DefenseDot.Data;
using DefenseDot.Systems.Core;
using DefenseDot.UI.InGame;

namespace DefenseDot.Systems.Management
{
    /// <summary>
    /// 게임 전역을 총괄하는 합성 루트(Composition Root)입니다.
    /// 모든 도메인 모델을 생성·보유하고 하위 시스템에 주입하며, 승패를 판정합니다.
    /// </summary>
    public class GameManager : MonoBehaviour, DefenseDot.Core.ICombatState, SceneLoadManager.ILoadingObserver
    {
        [Header("Startup")]
        [SerializeField] private ModeBootstrap modeBootstrap;
        [SerializeField] private int startGold = 300;

        [Header("Scene References")]
        [SerializeField] private EnemySpawner spawner;
        [SerializeField] private CoreController coreController;
        [SerializeField] private UIRoot uiRoot;
        [SerializeField] private TowerRoster towerRoster;

        /// <summary>골드 재화 모델입니다.</summary>
        public EconomyModel Economy { get; private set; }

        /// <summary>코어 체력 모델입니다.</summary>
        public CoreModel Core { get; private set; }

        /// <summary>웨이브 진행 모델입니다.</summary>
        public WaveModel Wave { get; private set; }

        /// <summary>게임 진행 단계 모델입니다.</summary>
        public GameFlowModel Flow { get; private set; }

        /// <summary>전투 집계 모델입니다.</summary>
        public CombatModel Combat { get; private set; }

        /// <summary>인-런 점수 모델입니다.</summary>
        public ScoreModel Score { get; private set; }

        /// <summary>라운드 제한시간 모델입니다.</summary>
        public RoundTimerModel RoundTimer { get; private set; }

        /// <summary>플레이어 레벨·처치 누적 모델입니다. (Arena 카드 허브)</summary>
        public LevelModel Level { get; private set; }

        /// <summary>현재 라운드(웨이브). 조건부 데미지(각성)용.</summary>
        public int Round => Wave != null ? Wave.Current : 1;
        /// <summary>생존 적 수. 조건부 데미지(쇄도)용.</summary>
        public int AliveEnemyCount => spawner != null ? spawner.ActiveEnemyCount : 0;

        // 하위 시스템 (합성 루트가 생성·주입)
        private EnemyRegistry registry;
        private TargetFinder targetFinder;
        private EconomySystem economySystem;
        private DefenseDot.Core.Pooling.PoolSystem poolSystem;

        // DEBUG: 치트 도구 접근용 — 실제 타워 등장 시스템 구현 시 삭제
        /// <summary>적 타겟 탐색기입니다. Start 이후 non-null. (DEBUG)</summary>
        public TargetFinder TargetFinder => targetFinder;
        private IGameMode mode;

        private void Awake()
        {
            // Domain 모델 생성 (최하위 계층, 외부 의존 없음)
            Economy = new EconomyModel();
            Core = new CoreModel();
            Wave = new WaveModel();
            Flow = new GameFlowModel();
            Combat = new CombatModel();
            Score = new ScoreModel();
            RoundTimer = new RoundTimerModel();

            Economy.Initialize(startGold);
            Core.Configure(modeBootstrap != null ? modeBootstrap.CoreMaxHp : 40f);
        }

        private void Start()
        {
            // 코어 GameObject ↔ CoreModel 연결 (외부 의존이므로 Start에서)
            if (coreController != null) coreController.Bind(Core);

            // 하위 시스템 생성·배선
            registry = new EnemyRegistry();
            targetFinder = new TargetFinder(registry);
            economySystem = new EconomySystem(Economy, Combat);
            economySystem.Initialize();

            // 능력 배선 전 풀 생성
            var assetLoader = new DefenseDot.Systems.Assets.AssetLoader();
            poolSystem = new DefenseDot.Core.Pooling.PoolSystem(assetLoader);

            mode = CreateMode();

            // 의존성 주입
            if (spawner != null) spawner.SetContext(mode, registry, Combat, Wave, RoundTimer, Score);

            // 승패 사건 구독
            Core.OnCoreDestroyed += HandleCoreDestroyed;
            Wave.OnWaveCleared += HandleVictory;

            // 레벨·카드 시스템 (Arena 전용) — CombatModel 처치 → LevelModel 레벨업
            var arenaBoot = modeBootstrap as ArenaModeBootstrap;
            DefenseDot.Systems.Cards.ArenaCardConfig cardConfig = arenaBoot != null ? arenaBoot.CardConfig : null;
            DefenseDot.Systems.Cards.AbilityPool abilityPool = arenaBoot != null ? arenaBoot.AbilityPool : null;
            System.Func<int, int> curve;
            if (cardConfig != null) curve = cardConfig.KillsToNextLevel;
            else curve = lv => Mathf.Max(3, 8 + lv * 4);
            Level = new LevelModel(curve);
            Combat.OnEnemyKilled += HandleEnemyKilledForLevel;

            // UI 연결 (UI 합성 루트에 GameContext 주입)
            if (uiRoot != null)
            {
                DefenseDot.Systems.Abilities.IAbilityCommandTarget coreTarget = arenaBoot != null ? arenaBoot.TowerAbility : null;
                DefenseDot.Systems.Cards.FusionRecipeSet universal = arenaBoot != null ? arenaBoot.UniversalLineage : null;
                DefenseDot.Systems.Cards.FusionRecipeSet character = arenaBoot != null ? arenaBoot.CharacterLineage : null;
                var fusion = new DefenseDot.Systems.Cards.FusionSystem(
                    new[] { universal, character });   // null 세트는 FusionSystem이 건너뜀
                var ctx = new DefenseDot.Domain.GameContext(
                    Economy, Core, Wave, Score, RoundTimer, Flow, Level,
                    modeBootstrap.EnemyDisplayCapacity, towerRoster,
                    modeBootstrap.PlacementController, cardConfig, abilityPool, coreTarget, poolSystem,
                    fusion);
                uiRoot.Inject(ctx);
            }

            // 로딩 개시 — 준비가 끝나면 OnLoadingStateChanged가 게임을 시작한다
            if (SceneLoadManager.Instance == null)
            {
                BeginPlay();   // 로더 없는 씬 단독 실행
                return;
            }

            // 개시자는 자기 개시 전 상태를 볼 필요가 없다(이전 세션의 Complete를 읽는 사고 방지)
            SceneLoadManager.Instance.RegisterObserver(this, shouldNotifyImmediately: false);
            SceneLoadManager.Instance.WarmupAllAsync(destroyCancellationToken).Forget();
        }

        /// <summary> 로딩이 끝났을 때 게임을 시작합니다. </summary>
        public void OnLoadingStateChanged()
        {
            if (SceneLoadManager.Instance == null)
                return;

            if (SceneLoadManager.Instance.State != SceneLoadManager.LoadingState.Complete)
                return;

            BeginPlay();
        }

        /// <summary> 플레이 단계로 전이하고 웨이브를 개시합니다. </summary>
        private void BeginPlay()
        {
            if (Flow.IsPlaying) return;   // 완료 통보가 두 번 와도 웨이브는 한 번만

            Flow.SetPhase(GamePhase.Playing);
            if (spawner != null) spawner.BeginWaves();
        }

        private IGameMode CreateMode()
        {
            if (modeBootstrap == null)
            {
                Debug.LogError("[GameManager] ModeBootstrap이 할당되지 않았습니다.");
                return null;
            }
            Vector3 origin = spawner != null ? spawner.transform.position : transform.position;
            Vector3 center = coreController != null ? coreController.CorePosition : transform.position;
            var ctx = new ModeContext(Core, Economy, targetFinder, origin, center, Flow, this, poolSystem);
            return modeBootstrap.CreateMode(ctx);
        }

        private void Update()
        {
            if (!Flow.IsPlaying || mode == null || spawner == null) return;

            // 아레나: 라운드 제한시간 진행 (Grid는 내부 가드로 통과)
            spawner.TickRound(Time.deltaTime);

            // 모드가 소유한 시스템 진행
            modeBootstrap.Tick(Time.deltaTime);

            // 아레나: 코어 HP를 수용 헤드룸(한계−생존수)으로 표시
            if (mode.TryGetCapacityHp(spawner.ActiveEnemyCount, out float capacityHp)) Core.SetCurrent(capacityHp);

            // 아레나 수용 한계 패배 판정 (TD는 항상 false)
            if (mode.CheckDefeat(spawner.ActiveEnemyCount)) TriggerGameOver();
        }

        private void HandleEnemyKilledForLevel(int reward)
        {
            if (Level != null) Level.RegisterKill();
        }

        private void HandleCoreDestroyed() => TriggerGameOver();

        private void TriggerGameOver()
        {
            if (Flow.Phase != GamePhase.Playing) return;
            Flow.SetPhase(GamePhase.GameOver);
        }

        private void HandleVictory()
        {
            if (Flow.Phase != GamePhase.Playing) return;
            Flow.SetPhase(GamePhase.Victory);
        }

        private void OnDestroy()
        {
            if (SceneLoadManager.Instance != null) SceneLoadManager.Instance.UnregisterObserver(this);
            economySystem?.Dispose();
            poolSystem?.Dispose();
            if (Core != null) Core.OnCoreDestroyed -= HandleCoreDestroyed;
            if (Wave != null) Wave.OnWaveCleared -= HandleVictory;
            if (Combat != null) Combat.OnEnemyKilled -= HandleEnemyKilledForLevel;
        }
    }
}
