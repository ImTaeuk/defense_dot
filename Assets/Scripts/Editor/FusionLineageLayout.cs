// 계보 노드 자동 배치 — 깊이별 계층(잎=좌측, 결과=우측)
using System.Collections.Generic;
using UnityEngine;
using UnityEditor.Experimental.GraphView;

namespace DefenseDot.EditorTools
{
    /// <summary> 계보 노드를 깊이(재료→결과) 계층으로 자동 배치하는 헬퍼입니다. </summary>
    public static class FusionLineageLayout
    {
        private const float ColumnWidth = 260f;
        private const float RowHeight = 170f;
        private const float OriginX = 40f;
        private const float OriginY = 40f;

        /// <summary> 노드들의 입력 연결을 보고 깊이를 계산해 위치를 배치합니다. </summary>
        /// <param name="nodes">배치할 능력 노드 목록.</param>
        public static void Apply(List<AbilityNode> nodes)
        {
            if (nodes == null || nodes.Count == 0)
                return;

            // 1. 깊이 계산 (잎=0, 결과=재료최대깊이+1) — DAG 안정화 반복
            var depth = new Dictionary<AbilityNode, int>();
            foreach (AbilityNode n in nodes)
                depth[n] = 0;

            bool changed = true;
            int guard = 0;
            while (changed && guard++ < 200)
            {
                changed = false;
                foreach (AbilityNode n in nodes)
                {
                    int d = 0;
                    AbilityNode sa = SourceNode(n.inputA);
                    AbilityNode sb = SourceNode(n.inputB);
                    if (sa != null && depth.ContainsKey(sa))
                        d = Mathf.Max(d, depth[sa] + 1);
                    if (sb != null && depth.ContainsKey(sb))
                        d = Mathf.Max(d, depth[sb] + 1);
                    if (d != depth[n])
                    {
                        depth[n] = d;
                        changed = true;
                    }
                }
            }

            // 2. 깊이별 세로 스택 배치
            var rowInColumn = new Dictionary<int, int>();
            foreach (AbilityNode n in nodes)
            {
                int col = depth[n];
                if (!rowInColumn.TryGetValue(col, out int row))
                    row = 0;
                rowInColumn[col] = row + 1;

                float x = OriginX + col * ColumnWidth;
                float y = OriginY + row * RowHeight;
                n.SetPosition(new Rect(x, y, 180f, 140f));
            }
        }

        /// <summary> 입력 포트에 연결된 소스 노드를 반환합니다(없으면 null). </summary>
        /// <param name="input">조회할 입력 포트.</param>
        private static AbilityNode SourceNode(Port input)
        {
            if (input == null)
                return null;
            foreach (Edge e in input.connections)
                return e.output != null ? e.output.node as AbilityNode : null;
            return null;
        }
    }
}
