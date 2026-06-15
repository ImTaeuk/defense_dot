// 합성 루트 — 도메인 모델 생성·주입과 승패 판정 총괄
using UnityEngine;
using DefenseDot.Domain;
using DefenseDot.Domain.Models;
using DefenseDot.Systems.Enemy;
using DefenseDot.Systems.Economy;
using DefenseDot.Systems.Mode;
using DefenseDot.Systems.Tower;
using DefenseDot.Systems.Core;
using DefenseDot.UI.InGame;

namespace DefenseDot.Systems.Management
{
    /// <summary>
    /// 게임 전역을 총괄하는 합성 루트(Composition Root)입니다.
    /// 모든 도메인 모델을 생성·보유하고 하위 시스템에 주입하며, 승패를 판정합니다.
    /// </summary>
    public class GameManager : MonoBehaviour
    {
        [Header("Startup")]
        [SerializeField] private ModeBootstrap modeBootstrap;
        [SerializeField] private int startGold = 300;

        [Header("Scene References")]
        [SerializeField] private EnemySpawner spawner;
        [SerializeField] private CoreController coreController;
        [SerializeField] private UIRoot uiRoot;

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

        // 서비스 (합성 루트가 생성·주입)
        private EnemyRegistry registry;
        private TargetFinder targetFinder;
        private EconomyController economyController;

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

            // 서비스 생성·배선
            registry = new EnemyRegistry();
            targetFinder = new TargetFinder(registry);
            economyController = new EconomyController(Economy, Combat);
            economyController.Initialize();

            mode = CreateMode();

            // 의존성 주입
            if (spawner != null) spawner.SetContext(mode, registry, Combat, Wave, RoundTimer, Score);

            // 승패 사건 구독
            Core.OnCoreDestroyed += HandleCoreDestroyed;
            Wave.OnWaveCleared += HandleVictory;

            // UI 연결 (UI 합성 루트에 주입)
            if (uiRoot != null)
            {
                var hudContext = new DefenseDot.UI.HudContext(
                    Economy, Core, Wave, Score, RoundTimer, modeBootstrap.EnemyDisplayCapacity);
                uiRoot.Inject(hudContext, Flow, modeBootstrap.PlacementController);
            }

            // 게임 시작
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
            var ctx = new ModeContext(Core, Economy, targetFinder, origin, center);
            return modeBootstrap.CreateMode(ctx);
        }

        private void Update()
        {
            if (!Flow.IsPlaying || mode == null || spawner == null) return;

            // 아레나: 라운드 제한시간 진행 (Grid는 내부 가드로 통과)
            spawner.TickRound(Time.deltaTime);

            // 아레나: 코어 HP를 수용 헤드룸(한계−생존수)으로 표시
            if (mode.TryGetCapacityHp(spawner.ActiveEnemyCount, out float capacityHp)) Core.SetCurrent(capacityHp);

            // 아레나 수용 한계 패배 판정 (TD는 항상 false)
            if (mode.CheckDefeat(spawner.ActiveEnemyCount)) TriggerGameOver();
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
            economyController?.Dispose();
            if (Core != null) Core.OnCoreDestroyed -= HandleCoreDestroyed;
            if (Wave != null) Wave.OnWaveCleared -= HandleVictory;
        }
    }
}
