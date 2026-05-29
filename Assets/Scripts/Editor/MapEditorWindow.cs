using UnityEngine;
using UnityEditor;
using UnityEngine.UIElements;
using UnityEditor.UIElements;
using DefenseDot.Data;
using DefenseDot.Systems.Grid;
using DefenseDot.Systems.Pathfinding;
using System.Collections.Generic;

namespace DefenseDot.Editor
{
    /// <summary>
    /// 2D 그리드 맵을 시각적으로 편집할 수 있는 커스텀 에디터 윈도우입니다.
    /// </summary>
    public class MapEditorWindow : EditorWindow
    {
        private MapData selectedMap;
        private CellType currentBrush = CellType.Path;
        private int currentPrefabIndex = 0;

        private VisualElement gridContainer;
        private VisualElement zoomContainer;
        private ScrollView scrollView;
        private ScrollView prefabList;
        private ObjectField mapDataField;
        private VisualElement typeButtonsRoot;
        private Dictionary<CellType, Button> brushButtons = new Dictionary<CellType, Button>();
        private List<Button> prefabButtons = new List<Button>();

        private float zoomScale = 1f;
        private const float MinZoom = 0.2f;
        private const float MaxZoom = 5f;
        private const float CellSize = 40f;

        private bool isPanning;
        private Vector2 lastMousePos;

        [MenuItem("DefenseDot/Map Editor")]
        public static void OpenWindow()
        {
            MapEditorWindow wnd = GetWindow<MapEditorWindow>();
            wnd.titleContent = new GUIContent("Map Editor");
            wnd.minSize = new Vector2(500, 600);
        }

        public void CreateGUI()
        {
            // UXML 로드
            var visualTree = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>("Assets/Scripts/Editor/MapEditor.uxml");
            if (visualTree == null) return;
            VisualElement root = visualTree.Instantiate();
            root.style.flexGrow = 1;
            rootVisualElement.Add(root);

            // 참조 가져오기
            scrollView = root.Q<ScrollView>(className: "grid-scroll");
            zoomContainer = root.Q<VisualElement>("zoomContainer");
            gridContainer = root.Q<VisualElement>("gridContainer");
            prefabList = root.Q<ScrollView>("prefab-list");
            mapDataField = root.Q<ObjectField>("mapDataField");
            typeButtonsRoot = root.Q<VisualElement>("type-buttons");
            var saveButton = root.Q<Button>("saveButton");
            var bakeButton = root.Q<Button>("bakeButton");
            var generateSceneButton = root.Q<Button>("generateSceneButton");

            // ScrollView의 내부 컨테이너 스타일 조정 (중앙 정렬 지원)
            var contentContainer = scrollView.contentContainer;
            contentContainer.style.flexGrow = 1;
            contentContainer.style.alignItems = Align.Center;
            contentContainer.style.justifyContent = Justify.Center;

            // MapData 필드 설정
            mapDataField.objectType = typeof(MapData);
            mapDataField.allowSceneObjects = false; // 에셋만 선택 가능하도록 제한
            mapDataField.RegisterValueChangedCallback(evt => {
                if (evt.newValue != null && !(evt.newValue is MapData))
                {
                    mapDataField.value = null;
                    return;
                }
                selectedMap = evt.newValue as MapData;
                RefreshPrefabList();
                RefreshGrid();
            });

            saveButton.clicked += SaveMap;
            bakeButton.clicked += BakePaths;
            generateSceneButton.clicked += GenerateInScene;

            // 이벤트 등록 (확대/축소 및 팬)
            root.RegisterCallback<WheelEvent>(OnWheel, TrickleDown.TrickleDown);
            root.RegisterCallback<PointerDownEvent>(OnPointerDown, TrickleDown.TrickleDown);
            root.RegisterCallback<PointerMoveEvent>(OnPointerMove, TrickleDown.TrickleDown);
            root.RegisterCallback<PointerUpEvent>(OnPointerUp, TrickleDown.TrickleDown);

            // 브러쉬 버튼 생성
            CreateBrushButtons();
            
            RefreshPrefabList();
            RefreshGrid();
        }

        private void OnWheel(WheelEvent evt)
        {
            if (evt.ctrlKey || evt.commandKey)
            {
                float delta = -evt.delta.y * 0.05f;
                float oldScale = zoomScale;
                zoomScale = Mathf.Clamp(zoomScale + delta, MinZoom, MaxZoom);

                if (Mathf.Abs(oldScale - zoomScale) > 0.001f)
                {
                    ApplyZoom();
                    evt.StopPropagation();
                }
            }
        }

        private void OnPointerDown(PointerDownEvent evt)
        {
            if (evt.button == 2) // Middle Mouse Button
            {
                isPanning = true;
                lastMousePos = evt.position;
                rootVisualElement.CapturePointer(evt.pointerId);
                evt.StopPropagation();
            }
        }

        private void OnPointerMove(PointerMoveEvent evt)
        {
            if (isPanning)
            {
                if ((evt.pressedButtons & 4) == 0)
                {
                    StopPanning(evt.pointerId);
                    return;
                }

                Vector2 currentMousePos = evt.position;
                Vector2 delta = currentMousePos - lastMousePos;
                lastMousePos = currentMousePos;

                scrollView.scrollOffset -= delta;
                evt.StopPropagation();
            }
        }

        private void OnPointerUp(PointerUpEvent evt)
        {
            if (isPanning && evt.button == 2)
            {
                StopPanning(evt.pointerId);
                evt.StopPropagation();
            }
        }

        private void StopPanning(int pointerId)
        {
            isPanning = false;
            if (rootVisualElement.HasPointerCapture(pointerId))
            {
                rootVisualElement.ReleasePointer(pointerId);
            }
        }

        private void ApplyZoom()
        {
            if (selectedMap == null) return;

            float baseWidth = selectedMap.width * CellSize;
            float baseHeight = selectedMap.height * CellSize;

            gridContainer.transform.scale = new Vector3(zoomScale, zoomScale, 1);
            gridContainer.style.width = baseWidth;
            gridContainer.style.height = baseHeight;

            zoomContainer.style.width = baseWidth * zoomScale;
            zoomContainer.style.height = baseHeight * zoomScale;
            
            zoomContainer.style.alignItems = Align.Center;
            zoomContainer.style.justifyContent = Justify.Center;
        }

        private void CreateBrushButtons()
        {
            typeButtonsRoot.Clear();
            brushButtons.Clear();

            foreach (CellType type in System.Enum.GetValues(typeof(CellType)))
            {
                Button btn = new Button(() => SetBrush(type)) { text = type.ToString() };
                btn.AddToClassList("type-btn");
                if (type == currentBrush) btn.AddToClassList("type-btn--active");
                
                typeButtonsRoot.Add(btn);
                brushButtons[type] = btn;
            }
        }

        private void SetBrush(CellType type)
        {
            brushButtons[currentBrush].RemoveFromClassList("type-btn--active");
            currentBrush = type;
            brushButtons[currentBrush].AddToClassList("type-btn--active");
            
            currentPrefabIndex = 0;
            RefreshPrefabList();
        }

        private void RefreshPrefabList()
        {
            prefabList.Clear();
            prefabButtons.Clear();

            if (selectedMap == null)
            {
                prefabList.Add(new Label("No MapData assigned"));
                return;
            }

            MapPalette palette = selectedMap.palette;
            if (palette == null)
            {
                palette = Resources.Load<MapPalette>("Default/Default_MapPalette");
            }

            if (palette == null)
            {
                prefabList.Add(new Label("No Palette found (Set Palette or use Default)"));
                return;
            }

            var prefabs = palette.GetPrefabs(currentBrush);
            if (prefabs == null || prefabs.Count == 0)
            {
                prefabList.Add(new Label($"No prefabs for {currentBrush}"));
                return;
            }

            for (int i = 0; i < prefabs.Count; i++)
            {
                int index = i;
                GameObject prefab = prefabs[i];
                string prefabName = prefab != null ? prefab.name : "Null";
                
                Button btn = new Button(() => SetPrefab(index)) { text = $"[{index}]\n{prefabName}" };
                btn.AddToClassList("prefab-btn");
                if (index == currentPrefabIndex) btn.AddToClassList("prefab-btn--active");
                
                prefabList.Add(btn);
                prefabButtons.Add(btn);
            }
        }

        private void SetPrefab(int index)
        {
            if (currentPrefabIndex < prefabButtons.Count)
                prefabButtons[currentPrefabIndex].RemoveFromClassList("prefab-btn--active");
            
            currentPrefabIndex = index;
            
            if (currentPrefabIndex < prefabButtons.Count)
                prefabButtons[currentPrefabIndex].AddToClassList("prefab-btn--active");
        }

        private void RefreshGrid()
        {
            gridContainer.Clear();
            if (selectedMap == null) return;

            if (selectedMap.grid == null || selectedMap.grid.Length != selectedMap.width * selectedMap.height)
            {
                selectedMap.Initialize();
            }

            ApplyZoom();

            for (int y = 0; y < selectedMap.height; y++)
            {
                for (int x = 0; x < selectedMap.width; x++)
                {
                    VisualElement cell = new VisualElement();
                    cell.AddToClassList("cell");
                    GridCell cellData = selectedMap.GetCell(x, y);
                    UpdateCellVisual(cell, cellData.type);
                    
                    cell.style.left = x * CellSize;
                    cell.style.top = (selectedMap.height - 1 - y) * CellSize;
                    cell.style.width = CellSize;
                    cell.style.height = CellSize;

                    // 좌표 및 프리팹 인덱스 레이블 추가
                    Label coordLabel = new Label($"{x},{y}\nID:{cellData.prefabIndex}");
                    coordLabel.AddToClassList("cell-label");
                    coordLabel.pickingMode = PickingMode.Ignore; 
                    cell.Add(coordLabel);

                    int curX = x;
                    int curY = y;
                    
                    cell.RegisterCallback<PointerDownEvent>(evt => {
                        if (evt.button == 0) PaintCell(curX, curY, cell);
                    });
                    cell.RegisterCallback<PointerEnterEvent>(evt => {
                        if (evt.pressedButtons == 1) PaintCell(curX, curY, cell);
                    });

                    gridContainer.Add(cell);
                }
            }
        }

        private void PaintCell(int x, int y, VisualElement cell)
        {
            if (selectedMap == null) return;
            selectedMap.SetCell(x, y, currentBrush, currentPrefabIndex);
            UpdateCellVisual(cell, currentBrush);
            
            Label label = cell.Q<Label>();
            if (label != null) label.text = $"{x},{y}\nID:{currentPrefabIndex}";

            EditorUtility.SetDirty(selectedMap);
        }

        private void UpdateCellVisual(VisualElement cell, CellType type)
        {
            foreach (CellType t in System.Enum.GetValues(typeof(CellType)))
            {
                cell.RemoveFromClassList(GetClassForType(t));
            }
            cell.AddToClassList(GetClassForType(type));
        }

        private string GetClassForType(CellType type)
        {
            switch (type)
            {
                case CellType.RedCell: return "cell--red-cell";
                case CellType.Path: return "cell--path";
                case CellType.TowerSlot: return "cell--tower-slot";
                case CellType.Spawn: return "cell--spawn";
                case CellType.Core: return "cell--core";
                default: return "cell--empty";
            }
        }

        private void BakePaths()
        {
            if (selectedMap == null) return;

            List<Vector2Int> spawnPositions = new List<Vector2Int>();
            List<Vector2Int> corePositions = new List<Vector2Int>();

            for (int y = 0; y < selectedMap.height; y++)
            {
                for (int x = 0; x < selectedMap.width; x++)
                {
                    CellType type = selectedMap.GetCellType(x, y);
                    if (type == CellType.Spawn) spawnPositions.Add(new Vector2Int(x, y));
                    else if (type == CellType.Core) corePositions.Add(new Vector2Int(x, y));
                }
            }

            if (spawnPositions.Count == 0 || corePositions.Count == 0)
            {
                EditorUtility.DisplayDialog("Map Editor", "Baking 실패: Spawn 지점이나 Core 지점이 맵에 존재하지 않습니다.", "OK");
                return;
            }

            selectedMap.bakedPaths.Clear();
            foreach (var spawn in spawnPositions)
            {
                List<Vector2Int> path = PathfindingUtils.FindPath(selectedMap, spawn, corePositions[0]);
                
                if (path != null)
                {
                    selectedMap.bakedPaths.Add(new BakedPath {
                        spawnPos = spawn,
                        corePos = corePositions[0],
                        path = path
                    });
                }
            }

            EditorUtility.SetDirty(selectedMap);
            AssetDatabase.SaveAssets();
            Debug.Log($"Map '{selectedMap.name}' paths baked successfully! ({selectedMap.bakedPaths.Count} paths found)");
        }

        private void SaveMap()
        {
            if (selectedMap == null) return;
            AssetDatabase.SaveAssets();
            Debug.Log($"Map '{selectedMap.name}' saved successfully!");
        }

        private void GenerateInScene()
        {
            if (selectedMap == null)
            {
                EditorUtility.DisplayDialog("Map Editor", "No MapData selected to generate!", "OK");
                return;
            }

            MapVisualizer visualizer = Object.FindAnyObjectByType<MapVisualizer>();

            if (visualizer == null)
            {
                if (EditorUtility.DisplayDialog("Map Editor", "Scene에 MapVisualizer가 없습니다. 새로 생성할까요?", "Yes", "No"))
                {
                    GameObject go = new GameObject("MapVisualizer");
                    visualizer = go.AddComponent<MapVisualizer>();
                }
                else
                {
                    return;
                }
            }

            Undo.RegisterCompleteObjectUndo(visualizer.gameObject, "Generate 3D Map");
            visualizer.SetupAndGenerate(selectedMap);
            SceneView.lastActiveSceneView?.FrameSelected();
        }
    }
}
