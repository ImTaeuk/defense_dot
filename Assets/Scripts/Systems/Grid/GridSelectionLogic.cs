using UnityEngine;

namespace DefenseDot.Systems.Grid
{
    /// <summary>
    /// 마우스 입력을 기반으로 그리드 선택 로직을 처리하는 POCO 클래스입니다.
    /// </summary>
    public class GridSelectionLogic
    {
        private Vector2Int lastSelectedCell = new Vector2Int(-1, -1);
        private readonly float cellSize;

        /// <summary>
        /// GridSelectionLogic 생성자
        /// </summary>
        /// <param name="cellSize">그리드 한 칸의 크기</param>
        public GridSelectionLogic(float cellSize)
        {
            this.cellSize = cellSize;
        }

        /// <summary>
        /// 월드 좌표를 그리드 좌표로 변환하고 선택 상태가 변경되었는지 확인합니다.
        /// </summary>
        /// <param name="localPos">그리드 시스템 기준 로컬 좌표</param>
        /// <param name="onCellChanged">셀이 변경되었을 때 호출될 콜백</param>
        public void UpdateHover(Vector3 localPos, System.Action<Vector2Int> onCellChanged)
        {
            Vector2Int currentCell = new Vector2Int(
                Mathf.FloorToInt(localPos.x / cellSize),
                Mathf.FloorToInt(localPos.z / cellSize)
            );

            if (currentCell != lastSelectedCell)
            {
                lastSelectedCell = currentCell;
                onCellChanged?.Invoke(currentCell);
            }
        }

        /// <summary>
        /// 현재 선택된 그리드 좌표를 반환합니다.
        /// </summary>
        public Vector2Int GetCurrentCell() => lastSelectedCell;
    }
}
