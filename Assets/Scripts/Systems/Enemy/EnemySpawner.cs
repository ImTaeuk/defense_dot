// 적 스포너 — 웨이브 소환, 풀링, 처치/도달 분기, WaveModel 갱신
// Grid는 클리어 게이트, Arena는 라운드 제한시간(타이머)/조기전멸로 진행
using System.Collections.Generic;
using UnityEngine;
using Cysharp.Threading.Tasks;
using DefenseDot.Data;
using DefenseDot.Domain.Models;
using DefenseDot.Systems.Mode;

namespace DefenseDot.Systems.Enemy
{
    /// <summary>
    /// 웨이브 데이터 기반으로 적을 소환·풀링하고, 처치/도달을 분기하며 WaveModel을 갱신합니다.
    /// Grid는 클리어 후 다음 웨이브, Arena는 라운드 제한시간(타이머) 만료 또는 조기 전멸로 진행합니다.
    /// </summary>
    public class EnemySpawner : MonoBehaviour
    {
        [Header("Data References")]
        public WaveSequence waveSequence;

        [Header("Hierarchy")]
        [SerializeField] private Transform container;

        // 주입 의존성
        private IGameMode mode;
        private EnemyRegistry registry;
        private CombatModel combat;
        private WaveModel waveModel;
        private RoundTimerModel timer;
        private ScoreModel score;

        private int currentWaveIndex = -1;
        private int activeEnemyCount = 0;
        private bool isSpawning = false;
        private bool allWavesSpawned = false;       // Arena: 등록 웨이브 소진 여부
        private System.Threading.CancellationTokenSource waveCts;   // 라운드 진행 시 스폰 취소

        // prefab별 경량 풀 (필드 보관 컬렉션 → 일반 new 허용)
        private readonly Dictionary<GameObject, Queue<MonsterActor>> pools = new Dictionary<GameObject, Queue<MonsterActor>>();

        /// <summary>
        /// 현재 활성 적 수입니다. (아레나 수용 한계 패배 판정용)
        /// </summary>
        public int ActiveEnemyCount => activeEnemyCount;

        /// <summary> Arena 여부(클리어로 승리하지 않는 모드)입니다. </summary>
        private bool IsArena => mode != null && !mode.WinsOnWaveClear;

        /// <summary> 현재 진행 중인 웨이브 데이터입니다. (범위 밖이면 null) </summary>
        private WaveData CurrentWave =>
            (waveSequence != null && currentWaveIndex >= 0 && currentWaveIndex < waveSequence.waves.Count)
                ? waveSequence.waves[currentWaveIndex] : null;

        /// <summary>
        /// 합성 루트에서 의존성을 주입합니다.
        /// </summary>
        public void SetContext(IGameMode gameMode, EnemyRegistry enemyRegistry, CombatModel combatModel,
            WaveModel wave, RoundTimerModel roundTimer, ScoreModel scoreModel)
        {
            mode = gameMode;
            registry = enemyRegistry;
            combat = combatModel;
            waveModel = wave;
            timer = roundTimer;
            score = scoreModel;
        }

        /// <summary>
        /// 웨이브 진행을 시작합니다. (주입 완료 후 GameManager가 호출)
        /// </summary>
        public void BeginWaves()
        {
            if (waveSequence == null || waveSequence.waves.Count == 0) return;
            if (IsArena) StartArenaWave(0);
            else StartNextWave();
        }

        // ─────────────── Grid: 클리어 게이트 진행 ───────────────

        /// <summary> Grid 전용 — 다음 웨이브로 진행합니다. </summary>
        public void StartNextWave()
        {
            if (isSpawning) return;

            currentWaveIndex++;
            if (currentWaveIndex >= waveSequence.waves.Count)
            {
                waveModel?.MarkWaveCleared();   // Grid: 즉시 승리 통지
                return;
            }
            waveModel?.SetWave(currentWaveIndex + 1, waveSequence.waves.Count);
            SpawnWaveRoutineAsync(waveSequence.waves[currentWaveIndex], destroyCancellationToken).Forget();
        }

        private async UniTask DelayedNextWaveAsync()
        {
            await UniTask.Delay(2000, cancellationToken: destroyCancellationToken);
            StartNextWave();
        }

        // ─────────────── Arena: 타이머/조기전멸 진행 ───────────────

        /// <summary> Arena 전용 — 지정 인덱스의 웨이브를 시작하고 라운드 타이머를 켭니다. </summary>
        private void StartArenaWave(int index)
        {
            currentWaveIndex = index;
            WaveData wave = waveSequence.waves[index];
            waveModel?.SetWave(index + 1, waveSequence.waves.Count);
            timer?.StartWave(wave.duration);

            waveCts?.Cancel();
            waveCts?.Dispose();
            waveCts = System.Threading.CancellationTokenSource.CreateLinkedTokenSource(destroyCancellationToken);
            SpawnWaveRoutineAsync(wave, waveCts.Token).Forget();
        }

        /// <summary> GameManager가 매 플레이 프레임 호출 — Arena 라운드 타이머를 진행합니다. </summary>
        public void TickRound(float deltaTime)
        {
            if (!IsArena || allWavesSpawned || timer == null) return;
            timer.Tick(deltaTime);
            if (timer.IsExpired) AdvanceArenaRound();
        }

        /// <summary> Arena — 다음 라운드로 진행합니다. (시간보너스는 호출자가 가산) </summary>
        private void AdvanceArenaRound()
        {
            int next = currentWaveIndex + 1;
            if (next >= waveSequence.waves.Count)
            {
                allWavesSpawned = true;     // 마지막 웨이브 소진 — 전멸 시 승리
                CheckWaveComplete();
                return;
            }
            StartArenaWave(next);
        }

        // ─────────────── 공통 스폰 ───────────────

        /// <summary> 웨이브의 적을 간격대로 스폰합니다(취소되면 중단하고 플래그만 복원). </summary>
        /// <param name="wave">스폰할 웨이브 데이터</param>
        /// <param name="token">라운드 전환 시 취소되는 토큰</param>
        private async UniTask SpawnWaveRoutineAsync(WaveData wave, System.Threading.CancellationToken token)
        {
            isSpawning = true;
            try
            {
                foreach (var entry in wave.entries)
                {
                    for (int i = 0; i < entry.count; i++)
                    {
                        SpawnEnemy(entry.enemyData);
                        await UniTask.Delay(System.TimeSpan.FromSeconds(entry.spawnInterval), cancellationToken: token);
                    }
                }
            }
            catch (System.OperationCanceledException)
            {
                return;   // 라운드 진행으로 취소됨 (finally가 플래그 복원)
            }
            finally
            {
                isSpawning = false;   // 취소 경로에서도 복원(진행 정지 방지)
            }

            CheckWaveComplete();   // 정상 완료에서만 호출
        }

        private void SpawnEnemy(EnemyData data)
        {
            if (mode == null) return;

            MonsterActor actor = GetFromPool(data);
            actor.SetSpawner(this);

            // 스폰 위치 모드 위임
            actor.transform.position = mode.GetSpawnWorldPosition(activeEnemyCount);

            actor.Initialize(data);

            // 이동 전략 모드 위임
            IMovementStrategy strategy = mode.CreateMovementStrategy(actor, data.moveSpeed, activeEnemyCount);
            actor.SetMovement(strategy);

            registry?.Register(actor);
            activeEnemyCount++;
            waveModel?.SetRemaining(activeEnemyCount);
        }

        /// <summary>
        /// 적 처치 처리 — 보상·점수 통지 후 회수합니다.
        /// </summary>
        public void HandleEnemyKilled(MonsterActor actor)
        {
            combat?.RegisterKill(actor.RewardGold);
            if (IsArena && score != null)
            {
                WaveData w = CurrentWave;
                score.AddKillScore(currentWaveIndex + 1, w != null ? w.killScoreMultiplier : 1f);
            }
            RemoveAndReturn(actor);
        }

        /// <summary>
        /// 적 코어 도달 처리 — 코어 피해 후 회수합니다. (보상 없음)
        /// </summary>
        public void HandleEnemyReached(MonsterActor actor)
        {
            mode?.OnEnemyReachedGoal(actor.CoreDamage);
            RemoveAndReturn(actor);
        }

        private void RemoveAndReturn(MonsterActor actor)
        {
            registry?.Unregister(actor);
            ReturnToPool(actor);
            activeEnemyCount--;
            waveModel?.SetRemaining(activeEnemyCount);
            CheckWaveComplete();
        }

        private void CheckWaveComplete()
        {
            if (IsArena)
            {
                if (activeEnemyCount == 0 && !isSpawning)
                {
                    if (allWavesSpawned)
                    {
                        waveModel?.MarkWaveCleared();   // 마지막 웨이브 후 전멸 → 승리
                        return;
                    }
                    WaveData w = CurrentWave;
                    score?.AddTimeBonus(timer != null ? timer.Remaining : 0f, currentWaveIndex + 1,
                        w != null ? w.timeBonusMultiplier : 1f);
                    AdvanceArenaRound();                // 조기 전멸 → 시간보너스 + 다음 라운드
                }
                return;
            }

            // Grid: 클리어 후 다음 웨이브
            if (activeEnemyCount == 0 && !isSpawning)
                DelayedNextWaveAsync().Forget();
        }

        private void OnDestroy()
        {
            waveCts?.Cancel();
            waveCts?.Dispose();
        }

        #region Pooling
        private MonsterActor GetFromPool(EnemyData data)
        {
            if (!pools.TryGetValue(data.prefab, out var queue))
            {
                queue = new Queue<MonsterActor>();
                pools[data.prefab] = queue;
            }

            MonsterActor actor;
            if (queue.Count > 0)
            {
                actor = queue.Dequeue();
                actor.gameObject.SetActive(true);
            }
            else
            {
                GameObject go = Instantiate(data.prefab, container != null ? container : transform);
                actor = go.GetComponent<MonsterActor>();
                if (actor == null) actor = go.AddComponent<MonsterActor>();
            }

            actor.OnSpawn();
            return actor;
        }

        private void ReturnToPool(MonsterActor actor)
        {
            actor.OnDespawn();
            actor.gameObject.SetActive(false);

            GameObject prefab = actor.Data != null ? actor.Data.prefab : null;
            if (prefab == null) { Destroy(actor.gameObject); return; }

            if (!pools.TryGetValue(prefab, out var queue))
            {
                queue = new Queue<MonsterActor>();
                pools[prefab] = queue;
            }
            queue.Enqueue(actor);
        }
        #endregion
    }
}
