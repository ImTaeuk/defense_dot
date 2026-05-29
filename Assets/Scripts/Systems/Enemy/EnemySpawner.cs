using UnityEngine;
using DefenseDot.Data;
using System.Collections.Generic;
using System;
using Cysharp.Threading.Tasks;

namespace DefenseDot.Systems.Enemy
{
    /// <summary>
    /// 웨이브 데이터를 기반으로 적을 순차적으로 소환하고 상태를 관리합니다.
    /// </summary>
    public class EnemySpawner : MonoBehaviour
    {
        [Header("Data References")]
        public MapData mapData;
        public WaveSequence waveSequence;

        [Header("Hierarchy")]
        [SerializeField] private Transform container;

        // UI 연동을 위한 이벤트
        public event Action<int, int> OnWaveChanged; // (현재 웨이브, 전체 웨이브)
        public event Action<int> OnEnemiesRemainingChanged;

        private int currentWaveIndex = -1;
        private int activeEnemyCount = 0;
        private List<MonsterActor> spawnedEnemies = new List<MonsterActor>();
        private bool isSpawning = false;

        private void Start()
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
                SpawnWaveRoutineAsync(waveSequence.waves[currentWaveIndex]).Forget();
                OnWaveChanged?.Invoke(currentWaveIndex + 1, waveSequence.waves.Count);
            }
            else
            {
                Debug.Log("All Waves Completed!");
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

            // 웨이브의 모든 적 소환 후 다음 웨이브 대기 (또는 수동 시작)
            // await UniTask.Delay(System.TimeSpan.FromSeconds(wave.nextWaveDelay), cancellationToken: destroyCancellationToken);
            // StartNextWave();
        }

        private void SpawnEnemy(EnemyData data)
        {
            if (mapData.bakedPaths.Count == 0) return;

            // 라운드 로빈 방식으로 경로 할당 (원본 HTML 방식)
            int pathIndex = activeEnemyCount % mapData.bakedPaths.Count;
            var bakedPath = mapData.bakedPaths[pathIndex];

            GameObject go = Instantiate(data.prefab, container != null ? container : transform);
            MonsterActor actor = go.GetComponent<MonsterActor>();

            if (actor == null) actor = go.AddComponent<MonsterActor>();

            actor.SetSpawner(this);
            // Y값을 0.8f로 설정하여 타일 위에 소환
            Vector3 spawnWorldPos = new Vector3(bakedPath.spawnPos.x + 0.5f, 0.8f, bakedPath.spawnPos.y + 0.5f);
            actor.transform.position = transform.position + spawnWorldPos;

            actor.Initialize(data);
            actor.MoveToPath(bakedPath.path);

            // 적 제거 시 카운트 관리를 위해 콜백 등록 (나중에 Actor에 OnDie/OnReachCore 추가 필요)
            activeEnemyCount++;
            spawnedEnemies.Add(actor);
            OnEnemiesRemainingChanged?.Invoke(activeEnemyCount);

            // 임시: 10초 후 자동 제거 시뮬레이션 (나중에 실제 로직으로 대체)
            // StartCoroutine(RemoveEnemyAfterDelay(actor, 10f));
        }

        public void HandleEnemyRemoved(MonsterActor actor)
        {
            if (spawnedEnemies.Contains(actor))
            {
                spawnedEnemies.Remove(actor);
                activeEnemyCount--;
                OnEnemiesRemainingChanged?.Invoke(activeEnemyCount);

                // 모든 적 처치 시 다음 웨이브 체크
                if (activeEnemyCount == 0 && !isSpawning)
                {
                    DelayedNextWaveAsync().Forget();
                }
            }
        }

        private async UniTask DelayedNextWaveAsync()
        {
            await UniTask.Delay(2000, cancellationToken: destroyCancellationToken);
            StartNextWave();
        }
    }
}
