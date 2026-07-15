// 계보 그래프의 능력 노드 — 이름·아이콘 + 재료 입력2·출력1
using UnityEngine;
using UnityEngine.UIElements;
using UnityEditor.Experimental.GraphView;
using DefenseDot.Systems.Abilities;

namespace DefenseDot.EditorTools
{
    /// <summary> 능력 1개를 나타내는 계보 그래프 노드입니다. </summary>
    public sealed class AbilityNode : Node
    {
        /// <summary> 이 노드가 나타내는 능력 설계도. </summary>
        public AbilityData ability;
        /// <summary> 합성 재료 A 입력 포트. </summary>
        public Port inputA;
        /// <summary> 합성 재료 B 입력 포트. </summary>
        public Port inputB;
        /// <summary> 다른 합성의 재료로 나가는 출력 포트. </summary>
        public Port output;

        /// <summary> 능력으로 노드를 구성합니다(이름·아이콘·포트). </summary>
        /// <param name="data">표시할 능력 설계도.</param>
        public AbilityNode(AbilityData data)
        {
            ability = data;
            title = NodeTitle(data);

            if (data != null && data.icon != null)
            {
                var img = new Image { sprite = data.icon, scaleMode = ScaleMode.ScaleToFit };
                img.style.width = 48;
                img.style.height = 48;
                img.style.marginLeft = 6;
                img.style.marginRight = 6;
                img.style.marginTop = 4;
                img.style.marginBottom = 4;
                mainContainer.Insert(1, img);
            }

            inputA = InstantiatePort(Orientation.Horizontal, Direction.Input, Port.Capacity.Single, typeof(AbilityData));
            inputA.portName = "재료 A";
            inputContainer.Add(inputA);

            inputB = InstantiatePort(Orientation.Horizontal, Direction.Input, Port.Capacity.Single, typeof(AbilityData));
            inputB.portName = "재료 B";
            inputContainer.Add(inputB);

            output = InstantiatePort(Orientation.Horizontal, Direction.Output, Port.Capacity.Multi, typeof(AbilityData));
            output.portName = "재료로";
            outputContainer.Add(output);

            RefreshExpandedState();
            RefreshPorts();
        }

        /// <summary> 노드 제목을 능력 표시명(없으면 에셋명)으로 정합니다. </summary>
        /// <param name="data">대상 능력.</param>
        private static string NodeTitle(AbilityData data)
        {
            if (data == null)
                return "(없음)";
            if (!string.IsNullOrEmpty(data.displayName))
                return data.displayName;
            return data.name;
        }
    }
}
