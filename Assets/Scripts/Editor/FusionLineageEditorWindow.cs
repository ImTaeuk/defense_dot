// 합성 계보 비주얼 에디터 창 — 계보 선택 + 그래프 캔버스
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;
using UnityEditor;
using UnityEditor.UIElements;
using DefenseDot.Systems.Cards;

namespace DefenseDot.EditorTools
{
    /// <summary> 프로젝트의 합성 계보를 골라 노드 그래프로 편집하는 에디터 창입니다. </summary>
    public sealed class FusionLineageEditorWindow : EditorWindow
    {
        /// <summary> 편집 캔버스. </summary>
        private FusionLineageGraphView graph;
        /// <summary> 프로젝트에서 찾은 계보 에셋 목록. </summary>
        private readonly List<FusionRecipeSet> lineages = new List<FusionRecipeSet>();
        /// <summary> 계보 선택 드롭다운. </summary>
        private PopupField<string> dropdown;

        /// <summary> 메뉴에서 창을 엽니다. </summary>
        [MenuItem("DefenseDot/Fusion Lineage Editor")]
        private static void Open()
        {
            var window = GetWindow<FusionLineageEditorWindow>();
            window.titleContent = new GUIContent("Fusion Lineage");
            window.minSize = new Vector2(640f, 420f);
        }

        /// <summary> 툴바와 그래프를 구성하고 계보 목록을 로드합니다. </summary>
        private void OnEnable()
        {
            graph = new FusionLineageGraphView();
            rootVisualElement.Add(BuildToolbar());
            rootVisualElement.Add(graph);
            ReloadLineages();
        }

        /// <summary> 상단 툴바(새로고침·계보 선택·안내)를 만듭니다. </summary>
        private VisualElement BuildToolbar()
        {
            var bar = new Toolbar();

            var refresh = new ToolbarButton(ReloadLineages) { text = "새로고침" };
            bar.Add(refresh);

            dropdown = new PopupField<string>(new List<string> { "(없음)" }, 0);
            dropdown.RegisterValueChangedCallback(_ => LoadSelected());
            bar.Add(dropdown);

            var hint = new ToolbarButton(() => { }) { text = "노드 추가: 캔버스 우클릭 → 능력 추가" };
            hint.SetEnabled(false);
            bar.Add(hint);

            return bar;
        }

        /// <summary> 프로젝트의 모든 계보 에셋을 찾아 드롭다운을 채우고 첫 항목을 로드합니다. </summary>
        private void ReloadLineages()
        {
            lineages.Clear();
            var names = new List<string>();
            string[] guids = AssetDatabase.FindAssets("t:FusionRecipeSet");
            for (int i = 0; i < guids.Length; i++)
            {
                var lineage = AssetDatabase.LoadAssetAtPath<FusionRecipeSet>(AssetDatabase.GUIDToAssetPath(guids[i]));
                if (lineage == null)
                    continue;
                lineages.Add(lineage);
                names.Add(lineage.name);
            }
            if (names.Count == 0)
                names.Add("(계보 없음)");

            if (dropdown != null)
            {
                dropdown.choices = names;
                dropdown.index = 0;
            }
            LoadSelected();
        }

        /// <summary> 드롭다운에서 선택된 계보를 그래프에 로드합니다. </summary>
        private void LoadSelected()
        {
            if (graph == null)
                return;
            int i = dropdown != null ? dropdown.index : 0;
            FusionRecipeSet selected = (i >= 0 && i < lineages.Count) ? lineages[i] : null;
            graph.Load(selected);
        }
    }
}
