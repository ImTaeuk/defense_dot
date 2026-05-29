using System;
using System.Collections.Generic;
using Unity.Collections;
using Unity.Jobs;
using Unity.Burst;
using UnityEngine;
using DefenseDot.Data;
using Cysharp.Threading.Tasks;

namespace DefenseDot.Systems.Pathfinding
{
    /// <summary>
    /// JPS(Jump Point Search) 알고리즘을 Job System과 Burst 컴파일러를 사용하여 
    /// 비동기적으로 수행하는 서비스 클래스입니다.
    /// </summary>
    public class PathfindingService : MonoBehaviour
    {
        private static PathfindingService instance;
        public static PathfindingService Instance => instance;

        private void Awake()
        {
            if (instance == null) instance = this;
            else Destroy(gameObject);
        }

        /// <summary>
        /// 특정 맵 데이터에서 시작점에서 목표점까지의 경로를 비동기로 요청합니다.
        /// </summary>
        /// <param name="mapData">맵 데이터</param>
        /// <param name="start">시작 셀 좌표</param>
        /// <param name="end">목표 셀 좌표</param>
        /// <param name="callback">경로 계산 완료 시 호출될 콜백 (경로 리스트 전달)</param>
        public void RequestPath(MapData mapData, Vector2Int start, Vector2Int end, Action<List<Vector2Int>> callback)
        {
            // 실제 구현에서는 여기서 NativeArray로 맵 정보를 넘기고 Job을 스케줄링합니다.
            // 현재는 구조 설계를 위해 비동기 처리를 위한 코루틴이나 별도 핸들러를 시뮬레이션합니다.
            StartCoroutine(CalculatePathAsync(mapData, start, end, callback));
        }

        private System.Collections.IEnumerator CalculatePathAsync(MapData mapData, Vector2Int start, Vector2Int end, Action<List<Vector2Int>> callback)
        {
            // 1. NativeArray 준비 (맵 그리드 정보)
            NativeArray<int> gridData = new NativeArray<int>(mapData.width * mapData.height, Allocator.TempJob);
            for (int i = 0; i < mapData.grid.Length; i++)
            {
                CellType type = mapData.grid[i].type;
                // Path, Spawn, Core만 이동 가능(0), 나머지는 이동 불가(1)로 가정
                gridData[i] = (type == CellType.Path || type == CellType.Spawn || type == CellType.Core) ? 0 : 1;
            }

            NativeList<Vector2Int> resultPath = new NativeList<Vector2Int>(Allocator.TempJob);

            // 2. Job 생성 및 스케줄링
            JPSJob jpsJob = new JPSJob
            {
                Width = mapData.width,
                Height = mapData.height,
                Start = start,
                End = end,
                Grid = gridData,
                Result = resultPath
            };

            JobHandle handle = jpsJob.Schedule();

            // 3. 완료 대기 (Burst 컴파일된 Job이 백그라운드에서 실행됨)
            while (!handle.IsCompleted)
            {
                await UniTask.Yield(cancellationToken: destroyCancellationToken);
            }
            handle.Complete();

            // 4. 결과 반환
            List<Vector2Int> finalPath = new List<Vector2Int>();
            for (int i = 0; i < resultPath.Length; i++)
            {
                finalPath.Add(resultPath[i]);
            }

            callback?.Invoke(finalPath);

            // 5. 메모리 해제
            gridData.Dispose();
            resultPath.Dispose();
        }
    }

    /// <summary>
    /// Burst 컴파일을 사용하여 JPS 연산을 가속화하는 Job 구조체입니다.
    /// </summary>
    [BurstCompile]
    public struct JPSJob : IJob
    {
        public int Width;
        public int Height;
        public Vector2Int Start;
        public Vector2Int End;

        [ReadOnly] public NativeArray<int> Grid;
        public NativeList<Vector2Int> Result;

        public void Execute()
        {
            // JPS 알고리즘의 핵심 로직이 들어갈 자리입니다.
            // A*와 달리 'Jump Point'를 찾아 직선 이동을 최적화합니다.
            
            // 시뮬레이션: 시작점과 끝점만 추가 (실제 JPS 로직은 맵 탐색 필요)
            Result.Add(Start);
            
            // TODO: 실제 JPS Jump Point 탐색 알고리즘 구현
            // 1. OpenList/ClosedList 관리
            // 2. 가로/세로/대각선 Jump 탐색
            // 3. 부모 노드 추적을 통한 경로 복원
            
            Result.Add(End);
        }
    }
}
