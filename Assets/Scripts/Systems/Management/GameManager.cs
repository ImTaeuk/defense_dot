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
        [SerializeField] private float coreMaxHp = 40f;

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

        // 서비스 (합성 루트가 생성·주입)
        private EnemyRegistry registry;
        private TargetFinder targetFinder;
        private EconomyController economyController;
        private IGameMode mode;

        private void Awake()
        {
            // Domain 모델 생성 (최하위 계층, 외부 의존 없음)
            Economy = new EconomyModel();
            Core = new CoreModel();
            Wave = new WaveModel();
            Flow = new GameFlowModel();
            Combat = new CombatModel();

            Economy.Initialize(startGold);
            Core.Configure(coreMaxHp);
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
            if (spawner != null) spawner.SetContext(mode, registry, Combat, Wave);

            // 승패 사건 구독
            Core.OnCoreDestroyed += HandleCoreDestroyed;
            Wave.OnWaveCleared += HandleVictory;

            // UI 연결 (UI 합성 루트에 주입)
            if (uiRoot != null)
                uiRoot.Inject(Economy, Core, Wave, modeBootstrap.EnemyDisplayCapacity);

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
