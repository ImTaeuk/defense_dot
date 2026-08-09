// UI 합성에 필요한 모든 모델·설정을 홀드하는 주입 컨텍스트
using DefenseDot.Domain.Models;
using DefenseDot.Data;
using DefenseDot.Systems.Tower;
using DefenseDot.Systems.Cards;
using DefenseDot.Systems.Abilities;
using DefenseDot.Core.Pooling;

namespace DefenseDot.Domain
{
    /// <summary> UI 합성에 필요한 모델·설정을 홀드하는 주입 컨텍스트입니다. (전역 아님) </summary>
    public sealed class GameContext
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
        /// <summary> 레벨·처치 누적 모델입니다. </summary>
        public LevelModel Level { get; }
        /// <summary> 적 수용 한계입니다. </summary>
        public int EnemyCapacity { get; }
        /// <summary> 타워 로스터입니다. </summary>
        public TowerRoster Roster { get; }
        /// <summary> 타워 배치 컨트롤러입니다. </summary>
        public TowerPlacementController Placement { get; }
        /// <summary> 카드 설정입니다. </summary>
        public ArenaCardConfig CardConfig { get; }
        /// <summary> 능력 풀입니다. </summary>
        public AbilityPool AbilityPool { get; }
        /// <summary> 능력 명령 대상입니다(타워가 자기 능력을 내어줍니다). </summary>
        public IAbilityCommandTarget AbilityTarget { get; }
        /// <summary> 공용 풀링 매니저입니다. </summary>
        public PoolSystem Pooling { get; }
        /// <summary> 합성의 단일 원천 시스템입니다(계보 데이터 소유). </summary>
        public FusionSystem Fusion { get; }

        /// <summary> 모든 의존성을 주입받습니다. </summary>
        public GameContext(EconomyModel economy, CoreModel core, WaveModel wave, ScoreModel score,
            RoundTimerModel timer, GameFlowModel flow, LevelModel level, int enemyCapacity,
            TowerRoster roster, TowerPlacementController placement, ArenaCardConfig cardConfig,
            AbilityPool abilityPool, IAbilityCommandTarget abilityTarget, PoolSystem pooling,
            FusionSystem fusion)
        {
            Economy = economy; Core = core; Wave = wave; Score = score; Timer = timer;
            Flow = flow; Level = level; EnemyCapacity = enemyCapacity; Roster = roster;
            Placement = placement; CardConfig = cardConfig; AbilityPool = abilityPool;
            AbilityTarget = abilityTarget; Pooling = pooling; Fusion = fusion;
        }
    }
}
