using System.Collections.Generic;
using UnityEngine;

namespace DefenseDot.Data
{
    /// <summary>
    /// 그리드의 각 셀 정보를 담는 구조체입니다.
    /// </summary>
    [System.Serializable]
    public struct GridCell
    {
        public CellType type;
        public int prefabIndex;
    }

    /// <summary>
    /// 그리드의 각 셀이 가질 수 있는 유형입니다.
    /// </summary>
    public enum CellType
    {
        /// <summary> 이동 불가 지점 </summary>
        RedCell,
        /// <summary> 적이 이동하는 경로 </summary>
        Path,
        /// <summary> 타워를 설치할 수 있는 슬롯 </summary>
        TowerSlot,
        /// <summary> 적이 소환되는 시작점 </summary>
        Spawn,
        /// <summary> 적이 도달해야 하는 최종 목적지 </summary>
        Core
    }

    /// <summary>
    /// 맵의 그리드 데이터를 저장하는 ScriptableObject입니다.
    /// </summary>
    [CreateAssetMenu(fileName = "NewMapData", menuName = "DefenseDot/MapData")]
    public class MapData : ScriptableObject
    {
        /// <summary> 그리드의 가로 셀 개수 </summary>
        public int width = 10;
        /// <summary> 그리드의 세로 셀 개수 </summary>
        public int height = 10;
        /// <summary> 셀의 월드 크기 </summary>
        public float cellSize = 1f;

        /// <summary> 이 맵 데이터가 사용하는 팔레트 참조 </summary>
        public MapPalette palette;

        /// <summary> 
        /// 그리드 데이터를 1차원 배열로 저장합니다. 
        /// </summary>
        [HideInInspector]
        public GridCell[] grid;

        /// <summary>
        /// 베이킹된 경로 데이터 리스트입니다.
        /// </summary>
        [HideInInspector]
        public List<BakedPath> bakedPaths = new List<BakedPath>();

        /// <summary>
        /// 그리드를 지정된 크기로 초기화합니다.
        /// </summary>
        public void Initialize()
        {
            grid = new GridCell[width * height];
            // 기본값은 RedCell(0), prefabIndex(0)입니다.
        }

        /// <summary>
        /// 특정 좌표의 셀 정보를 반환합니다.
        /// </summary>
        public GridCell GetCell(int x, int y)
        {
            if (grid == null || grid.Length == 0) Initialize();
            if (x < 0 || x >= width || y < 0 || y >= height) return new GridCell { type = CellType.RedCell, prefabIndex = 0 };
            return grid[y * width + x];
        }

        /// <summary>
        /// 특정 좌표의 셀 유형을 반환합니다. (호환성 유지)
        /// </summary>
        public CellType GetCellType(int x, int y) => GetCell(x, y).type;

        /// <summary>
        /// 특정 좌표의 셀 정보를 설정합니다.
        /// </summary>
        public void SetCell(int x, int y, CellType type, int prefabIndex = 0)
        {
            if (grid == null || grid.Length == 0) Initialize();
            if (x < 0 || x >= width || y < 0 || y >= height) return;
            grid[y * width + x] = new GridCell { type = type, prefabIndex = prefabIndex };
        }

        /// <summary>
        /// 셀 유형에 따른 기즈모 색상을 반환합니다.
        /// </summary>
        public Color GetColorForType(CellType type)
        {
            return type switch
            {
                CellType.RedCell => new Color(0.8f, 0.1f, 0.1f, 0.85f),    // 진한 빨강 (이동 불가)
                CellType.Path => new Color(0.3f, 0.8f, 0.5f, 0.85f),       // 초록색 계열 (이동 가능)
                CellType.TowerSlot => new Color(0.6f, 0.2f, 0.8f, 0.85f),  // 보라색 (설치 구역)
CellType.Spawn => new Color(1f, 0.8f, 0f, 0.85f),          // 금색 (적 스폰)
                CellType.Core => new Color(0.5f, 0.7f, 1f, 0.85f),       // 연한 파란색 (목적지)
_ => Color.white
            };
        }
}

    /// <summary>
    /// 베이킹된 단일 경로 정보를 담는 클래스입니다.
    /// </summary>
    [System.Serializable]
    public class BakedPath
    {
        public Vector2Int spawnPos;
        public Vector2Int corePos;
        public List<Vector2Int> path;
    }
}
