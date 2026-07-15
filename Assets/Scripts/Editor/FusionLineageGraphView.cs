// 계보(FusionRecipeSet)를 노드 그래프로 편집하는 캔버스
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;
using UnityEditor;
using UnityEditor.Experimental.GraphView;
using DefenseDot.Systems.Abilities;
using DefenseDot.Systems.Cards;

namespace DefenseDot.EditorTools
{
    /// <summary> 선택된 계보를 노드·엣지로 로드/편집하고 변경을 에셋에 저장하는 그래프 뷰입니다. </summary>
    public sealed class FusionLineageGraphView : GraphView
    {
        /// <summary> 현재 편집 중인 계보 에셋. </summary>
        private FusionRecipeSet current;

        /// <summary> 조작기·격자·변경 콜백을 설정합니다. </summary>
        public FusionLineageGraphView()
        {
            style.flexGrow = 1;
            SetupZoom(ContentZoomer.DefaultMinScale, ContentZoomer.DefaultMaxScale);
            this.AddManipulator(new ContentDragger());
            this.AddManipulator(new SelectionDragger());
            this.AddManipulator(new RectangleSelector());
            Insert(0, new GridBackground());
            graphViewChanged = OnGraphViewChanged;
        }

        /// <summary> 계보를 그래프로 로드합니다(기존 그래프는 비움). </summary>
        /// <param name="lineage">표시할 계보 에셋(null이면 비움).</param>
        public void Load(FusionRecipeSet lineage)
        {
            current = lineage;

            // 로드 중 변경 콜백 억제
            graphViewChanged = null;
            DeleteElements(new List<GraphElement>(graphElements));
            graphViewChanged = OnGraphViewChanged;

            if (lineage == null || lineage.recipes == null)
                return;

            var nodeByAbility = new Dictionary<AbilityData, AbilityNode>();
            var made = new List<AbilityNode>();

            for (int i = 0; i < lineage.recipes.Count; i++)
            {
                FusionRecipe r = lineage.recipes[i];
                if (r.result == null)
                    continue;

                AbilityNode res = GetOrCreate(r.result, nodeByAbility, made);
                if (r.materialA != null)
                    Connect(GetOrCreate(r.materialA, nodeByAbility, made).output, res.inputA);
                if (r.materialB != null)
                    Connect(GetOrCreate(r.materialB, nodeByAbility, made).output, res.inputB);
            }

            FusionLineageLayout.Apply(made);
        }

        /// <summary> 능력에 대응하는 노드를 얻거나 새로 만들어 그래프에 추가합니다. </summary>
        /// <param name="data">대상 능력.</param>
        /// <param name="map">능력→노드 캐시.</param>
        /// <param name="made">생성된 노드 누적 목록.</param>
        private AbilityNode GetOrCreate(AbilityData data, Dictionary<AbilityData, AbilityNode> map, List<AbilityNode> made)
        {
            if (map.TryGetValue(data, out AbilityNode n))
                return n;
            n = new AbilityNode(data);
            map[data] = n;
            made.Add(n);
            AddElement(n);
            return n;
        }

        /// <summary> 출력 포트와 입력 포트를 엣지로 연결합니다. </summary>
        /// <param name="output">소스 출력 포트.</param>
        /// <param name="input">대상 입력 포트.</param>
        private void Connect(Port output, Port input)
        {
            Edge edge = output.ConnectTo(input);
            AddElement(edge);
        }

        /// <summary> 검색으로 고른 능력을 새 노드로 추가합니다. </summary>
        /// <param name="data">추가할 능력.</param>
        /// <param name="position">배치 위치(그래프 좌표).</param>
        public void AddAbilityNode(AbilityData data, Vector2 position)
        {
            var n = new AbilityNode(data);
            n.SetPosition(new Rect(position, new Vector2(180f, 140f)));
            AddElement(n);
        }

        /// <summary> 캔버스 우클릭 시 프로젝트의 능력을 노드로 추가하는 메뉴를 만듭니다. </summary>
        /// <param name="evt">컨텍스트 메뉴 이벤트.</param>
        public override void BuildContextualMenu(ContextualMenuPopulateEvent evt)
        {
            Vector2 pos = contentViewContainer.WorldToLocal(evt.mousePosition);
            string[] guids = AssetDatabase.FindAssets("t:AbilityData");
            for (int i = 0; i < guids.Length; i++)
            {
                var a = AssetDatabase.LoadAssetAtPath<AbilityData>(AssetDatabase.GUIDToAssetPath(guids[i]));
                if (a == null)
                    continue;
                string label = !string.IsNullOrEmpty(a.displayName) ? a.displayName : a.name;
                AbilityData captured = a;
                evt.menu.AppendAction("능력 추가/" + label, _ => AddAbilityNode(captured, pos));
            }
            base.BuildContextualMenu(evt);
        }

        /// <summary> 출력↔입력, 서로 다른 노드끼리만 연결 가능하도록 후보 포트를 거릅니다. </summary>
        /// <param name="startPort">연결 시작 포트.</param>
        /// <param name="nodeAdapter">노드 어댑터(미사용).</param>
        public override List<Port> GetCompatiblePorts(Port startPort, NodeAdapter nodeAdapter)
        {
            var compatible = new List<Port>();
            ports.ForEach(p =>
            {
                if (p == startPort)
                    return;
                if (p.node == startPort.node)
                    return;
                if (p.direction == startPort.direction)
                    return;
                compatible.Add(p);
            });
            return compatible;
        }

        /// <summary> 그래프 변경 후 다음 프레임에 계보를 재구성해 저장합니다. </summary>
        /// <param name="change">그래프 변경 내역.</param>
        private GraphViewChange OnGraphViewChanged(GraphViewChange change)
        {
            schedule.Execute(Save).ExecuteLater(1);
            return change;
        }

        /// <summary> 현재 그래프 상태에서 레시피 목록을 재구성해 에셋에 저장합니다. </summary>
        public void Save()
        {
            if (current == null)
                return;

            var recipes = new List<FusionRecipe>();
            foreach (Node node in nodes)
            {
                if (!(node is AbilityNode an))
                    continue;
                AbilityData a = SourceAbility(an.inputA);
                AbilityData b = SourceAbility(an.inputB);
                if (a == null || b == null)
                    continue;
                recipes.Add(new FusionRecipe { materialA = a, materialB = b, result = an.ability });
            }

            current.recipes = recipes;
            EditorUtility.SetDirty(current);
            AssetDatabase.SaveAssets();
        }

        /// <summary> 입력 포트에 연결된 소스 노드의 능력을 반환합니다(없으면 null). </summary>
        /// <param name="input">조회할 입력 포트.</param>
        private static AbilityData SourceAbility(Port input)
        {
            foreach (Edge e in input.connections)
            {
                var src = e.output != null ? e.output.node as AbilityNode : null;
                return src != null ? src.ability : null;
            }
            return null;
        }
    }
}
