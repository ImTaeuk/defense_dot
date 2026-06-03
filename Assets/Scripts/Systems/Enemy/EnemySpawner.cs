// 적 스포너 — 웨이브 소환, 풀링, 처치/도달 분기, WaveModel 갱신
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
    /// 모드·레지스트리·모델은 GameManager(합성 루트)가 주입합니다.
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

        private int currentWaveIndex = -1;
        private int activeEnemyCount = 0;
        private bool isSpawning = false;

        // prefab별 경량 풀 (필드 보관 컬렉션 → 일반 new 허용)
        private readonly Dictionary<GameObject, Queue<MonsterActor>> pools = new Dictionary<GameObject, Queue<MonsterActor>>();

        /// <summary>
        /// 현재 활성 적 수입니다. (아레나 수용 한계 패배 판정용)
        /// </summary>
        public int ActiveEnemyCount => activeEnemyCount;

        /// <summary>
        /// 합성 루트에서 의존성을 주입합니다.
        /// </summary>
        public void SetContext(IGameMode gameMode, EnemyRegistry enemyRegistry, CombatModel combatModel, WaveModel wave)
        {
            mode = gameMode;
            registry = enemyRegistry;
            combat = combatModel;
            waveModel = wave;
        }

        /// <summary>
        /// 웨이브 진행을 시작합니다. (주입 완료 후 GameManager가 호출)
        /// </summary>
        public void BeginWaves()
        {
            if (waveSequence != null && waveSequence.waves.Count > 0)
            {
                StartNextWave();
            }
        }

        public void StartNextWave()
        {
            if (isSpawning) return;

            currentWaveIndex++;
            if (currentWaveIndex < waveSequence.waves.Count)
            {
                waveModel?.SetWave(currentWaveIndex + 1, waveSequence.waves.Count);
                SpawnWaveRoutineAsync(waveSequence.waves[currentWaveIndex]).Forget();
            }
            else
            {
                // 모든 웨이브 소진 → 승리 통지
                waveModel?.MarkWaveCleared();
            }
        }

        private async UniTask SpawnWaveRoutineAsync(WaveData wave)
        {
            isSpawning = true;

            foreach (var entry in wave.entries)
            {
                for (int i = 0; i < entry.count; i++)
                {
                    SpawnEnemy(entry.enemyData);
                    await UniTask.Delay(System.TimeSpan.FromSeconds(entry.spawnInterval), cancellationToken: destroyCancellationToken);
                }
            }

            isSpawning = false;
            CheckWaveComplete();
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
        /// 적 처치 처리 — 보상 통지 후 회수합니다.
        /// </summary>
        public void HandleEnemyKilled(MonsterActor actor)
        {
            combat?.RegisterKill(actor.RewardGold);
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
            if (activeEnemyCount == 0 && !isSpawning)
            {
                DelayedNextWaveAsync().Forget();
            }
        }

        private async UniTask DelayedNextWaveAsync()
        {
            await UniTask.Delay(2000, cancellationToken: destroyCancellationToken);
            StartNextWave();
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
