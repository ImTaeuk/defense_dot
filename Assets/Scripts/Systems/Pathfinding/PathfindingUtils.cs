using System.Collections.Generic;
using UnityEngine;
using DefenseDot.Data;

namespace DefenseDot.Systems.Pathfinding
{
    public static class PathfindingUtils
    {
        /// <summary>
        /// BFS를 사용하여 맵에서 두 지점 사이의 최단 경로를 찾습니다 (에디터 베이킹용).
        /// </summary>
        public static List<Vector2Int> FindPath(MapData mapData, Vector2Int start, Vector2Int end)
        {
            Queue<Vector2Int> queue = new Queue<Vector2Int>();
            queue.Enqueue(start);

            Dictionary<Vector2Int, Vector2Int> cameFrom = new Dictionary<Vector2Int, Vector2Int>();
            cameFrom[start] = start;

            Vector2Int[] directions = {
                new Vector2Int(0, 1), new Vector2Int(0, -1),
                new Vector2Int(1, 0), new Vector2Int(-1, 0)
            };

            bool found = false;
            while (queue.Count > 0)
            {
                Vector2Int current = queue.Dequeue();
                if (current == end)
                {
                    found = true;
                    break;
                }

                foreach (var dir in directions)
                {
                    Vector2Int next = current + dir;
                    if (next.x >= 0 && next.x < mapData.width && next.y >= 0 && next.y < mapData.height)
                    {
                        var cell = mapData.GetCell(next.x, next.y);
                        bool isWalkable = cell.type == CellType.Path || cell.type == CellType.Spawn || cell.type == CellType.Core;
                        
                        if (isWalkable && !cameFrom.ContainsKey(next))
                        {
                            cameFrom[next] = current;
                            queue.Enqueue(next);
                        }
                    }
                }
            }

            if (!found) return null;

            // 경로 복원
            List<Vector2Int> path = new List<Vector2Int>();
            Vector2Int curr = end;
            while (curr != start)
            {
                path.Add(curr);
                curr = cameFrom[curr];
            }
            path.Add(start);
            path.Reverse();

            return path;
        }
    }
}
