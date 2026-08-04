using DefenseDot.Systems.Cards;

namespace DefenseDot.Domain.Models
{
    /// <summary> 플레이어 레벨·처치 누적·레벨업 통지를 소유하는 모델. </summary>
    public sealed class LevelModel : BaseModel
    {
        /// <summary> 카드 설정이 없을 때 쓰는 기본 곡선. ArenaCardConfig 의 기본값과 같게 유지한다. </summary>
        private const int DEFAULT_CURVE_BASE = 8;

        private const int DEFAULT_CURVE_PER_LEVEL = 4;

        private const int MIN_KILLS = 3;

        /// <summary> 레벨 곡선을 가진 카드 설정. 없으면 기본 곡선으로 계산한다. </summary>
        private readonly ArenaCardConfig config;

        private readonly ReactiveProperty<LevelProgress> progress;

        public int Level { get; private set; } = 1;
        public int Kills { get; private set; }
        public int KillsToNextLevel { get; private set; }
        public int PendingLevelUps { get; private set; }

        /// <summary> 레벨 진척(레벨/처치/남은/비율) 상태입니다. (읽기 전용 RP) </summary>
        public IReadOnlyReactiveProperty<LevelProgress> Progress => progress;

        public event System.Action OnLevelUp;

        /// <summary> 레벨 곡선을 가진 카드 설정을 받습니다. null 이면 기본 곡선을 씁니다. </summary>
        /// <param name="config">곡선 계수를 가진 카드 설정</param>
        public LevelModel(ArenaCardConfig config)
        {
            this.config = config;
            KillsToNextLevel = KillsFor(Level);
            progress = new ReactiveProperty<LevelProgress>(Snapshot());
        }

        /// <summary> 처치 1회 집계. 곡선 도달 시 레벨업(다중 가능). </summary>
        public void RegisterKill()
        {
            Kills++;
            bool leveled = false;
            while (Kills >= KillsToNextLevel)
            {
                Kills -= KillsToNextLevel;
                Level++;
                KillsToNextLevel = KillsFor(Level);
                PendingLevelUps++;
                leveled = true;
            }
            progress.Value = Snapshot();
            if (leveled) OnLevelUp?.Invoke();
        }

        /// <summary> 대기 레벨업 1건 소비. 없으면 false. </summary>
        public bool TryConsumePending()
        {
            if (PendingLevelUps <= 0) return false;
            PendingLevelUps--;
            return true;
        }

        /// <summary> 해당 레벨에서 다음 레벨까지 필요한 처치 수입니다. </summary>
        /// <param name="level">기준 레벨</param>
        private int KillsFor(int level)
        {
            if (config != null)
                return config.KillsToNextLevel(level);

            return System.Math.Max(MIN_KILLS, DEFAULT_CURVE_BASE + level * DEFAULT_CURVE_PER_LEVEL);
        }

        private LevelProgress Snapshot() => new LevelProgress(Level, Kills, KillsToNextLevel);
    }
}