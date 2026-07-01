namespace DefenseDot.Domain.Models
{
    /// <summary> 플레이어 레벨·처치 누적·레벨업 통지를 소유하는 모델. </summary>
    public sealed class LevelModel : BaseModel
    {
        private readonly System.Func<int, int> curve;
        private readonly ReactiveProperty<LevelProgress> progress;

        public int Level { get; private set; } = 1;
        public int Kills { get; private set; }
        public int KillsToNextLevel { get; private set; }
        public int PendingLevelUps { get; private set; }

        /// <summary> 레벨 진척(레벨/처치/남은/비율) 상태입니다. (읽기 전용 RP) </summary>
        public IReadOnlyReactiveProperty<LevelProgress> Progress => progress;

        public event System.Action OnLevelUp;

        public LevelModel(System.Func<int, int> curve)
        {
            this.curve = curve;
            KillsToNextLevel = curve(Level);
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
                KillsToNextLevel = curve(Level);
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

        private LevelProgress Snapshot() => new LevelProgress(Level, Kills, KillsToNextLevel);
    }
}
