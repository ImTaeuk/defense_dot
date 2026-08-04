// UI 컨텍스트를 조립하는 그릇 — 공통은 생성자로, 모드별은 모드가 채운다
using DefenseDot.Domain.Models;
using DefenseDot.Data;
using DefenseDot.Systems.Tower;
using DefenseDot.Systems.Cards;
using DefenseDot.Systems.Abilities;
using DefenseDot.Core.Pooling;

namespace DefenseDot.Domain
{
    /// <summary>
    /// GameContext 를 조립하는 가변 그릇입니다. 합성 루트가 공통 자원을 생성자로 넣고,
    /// 모드가 자기 자원만 채운 뒤 Build 로 불변 컨텍스트를 만듭니다.
    /// </summary>
    public sealed class GameContextBuilder
    {
        /// <summary> 골드 재화 모델입니다. </summary>
        public EconomyModel Economy { get; }

        /// <summary> 코어 체력 모델입니다. </summary>
        public CoreModel Core { get; }

        /// <summary> 웨이브 진행 모델입니다. </summary>
        public WaveModel Wave { get; }

        /// <summary> 인-런 점수 모델입니다. </summary>
        public ScoreModel Score { get; }

        /// <summary> 라운드 제한시간 모델입니다. </summary>
        public RoundTimerModel Timer { get; }

        /// <summary> 게임 진행 단계 모델입니다. </summary>
        public GameFlowModel Flow { get; }

        /// <summary> 적 수용 한계입니다. </summary>
        public int EnemyCapacity { get; }

        /// <summary> 타워 로스터입니다. </summary>
        public TowerRoster Roster { get; }

        /// <summary> 공용 오브젝트 풀입니다. </summary>
        public PoolSystem Pooling { get; }

        /// <summary> 레벨·처치 누적 모델. 모드가 채운 카드 설정으로 곡선이 정해지므로 나중에 넣습니다. </summary>
        public LevelModel Level { get; set; }

        /// <summary> 타워 배치 컨트롤러. 배치가 있는 모드만 채웁니다. </summary>
        public TowerPlacementController Placement { get; set; }

        /// <summary> 카드 허브 설정. 카드로 능력을 얻는 모드만 채웁니다. </summary>
        public ArenaCardConfig CardConfig { get; set; }

        /// <summary> 신규 카드 후보 풀. 카드로 능력을 얻는 모드만 채웁니다. </summary>
        public AbilityPool AbilityPool { get; set; }

        /// <summary> 능력 명령 대상. 능력을 다루는 모드만 채웁니다. </summary>
        public IAbilityCommandTarget CoreTarget { get; set; }

        /// <summary> 합성 시스템(계보 소유). 합성이 있는 모드만 채웁니다. </summary>
        public FusionSystem Fusion { get; set; }

        /// <summary> 모드와 무관하게 항상 있는 공통 자원을 받습니다. </summary>
        /// <param name="economy">골드 재화 모델</param>
        /// <param name="core">코어 체력 모델</param>
        /// <param name="wave">웨이브 진행 모델</param>
        /// <param name="score">인-런 점수 모델</param>
        /// <param name="timer">라운드 제한시간 모델</param>
        /// <param name="flow">게임 진행 단계 모델</param>
        /// <param name="enemyCapacity">적 수 표시 한계(HUD 분모)</param>
        /// <param name="roster">타워 로스터</param>
        /// <param name="pooling">공용 오브젝트 풀</param>
        public GameContextBuilder(EconomyModel economy, CoreModel core, WaveModel wave, ScoreModel score,
            RoundTimerModel timer, GameFlowModel flow, int enemyCapacity, TowerRoster roster, PoolSystem pooling)
        {
            Economy = economy;
            Core = core;
            Wave = wave;
            Score = score;
            Timer = timer;
            Flow = flow;
            EnemyCapacity = enemyCapacity;
            Roster = roster;
            Pooling = pooling;
        }

        /// <summary> 지금까지 채운 것으로 불변 컨텍스트를 만듭니다. </summary>
        public GameContext Build()
        {
            return new GameContext(Economy, Core, Wave, Score, Timer, Flow, Level,
                EnemyCapacity, Roster, Placement, CardConfig,
                AbilityPool, CoreTarget, Pooling, Fusion);
        }
    }
}