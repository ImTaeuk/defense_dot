using UnityEngine;
using DefenseDot.Data;

namespace DefenseDot.Systems.Grid
{
    /// <summary>
    /// MapData를 기반으로 3D 타일맵을 생성하고 에디터 상에서 시각적인 가이드를 제공하는 통합 컴포넌트입니다.
    /// </summary>
    public class MapVisualizer : MonoBehaviour
    {
        [Header("Data References")]
        [SerializeField, Tooltip("논리적 맵 데이터")] 
        private MapData mapData;

        [Header("Gizmos Settings")]
        [SerializeField, Tooltip("기즈모 표시 여부")] 
        private bool showGizmos = true;
        [SerializeField, Tooltip("와이어프레임 표시 여부")] 
        private bool showWireframe = true;

        [Header("Hierarchy")]
        [SerializeField, Tooltip("생성된 타일들이 담길 부모 오브젝트")]
        private Transform container;

        /// <summary>
        /// 외부(에디터 등)에서 맵 데이터를 주입하고 즉시 3D 맵을 생성합니다.
        /// </summary>
        public void SetupAndGenerate(MapData data)
        {
            this.mapData = data;
            GenerateMap();
        }

        /// <summary>
        /// 인스펙터 컨텍스트 메뉴를 통해 3D 맵을 자동 생성합니다.
        /// </summary>
        [ContextMenu("Generate 3D Map")]
public void GenerateMap()
        {
            if (mapData == null)
            {
                Debug.LogError("[MapVisualizer] MapData가 할당되지 않았습니다.");
                return;
            }

            // 맵에 할당된 팔레트가 없으면 기본 팔레트 로드 시도
            MapPalette palette = mapData.palette;
            if (palette == null)
            {
                palette = Resources.Load<MapPalette>("Default/Default_MapPalette");
                if (palette != null) Debug.LogWarning("[MapVisualizer] MapData에 팔레트가 없어 기본 팔레트를 사용합니다.");
            }

            if (palette == null)
            {
                Debug.LogError("[MapVisualizer] 팔레트(Palette)를 찾을 수 없습니다. Default_MapPalette 에셋이 필요합니다.");
                return;
            }

            ClearExistingMap();

            float cellSize = mapData.cellSize;
            Vector3 origin = transform.position;

            for (int y = 0; y < mapData.height; y++)
            {
                for (int x = 0; x < mapData.width; x++)
                {
                    GridCell cellData = mapData.GetCell(x, y);
                    GameObject prefab = palette.GetPrefab(cellData.type, cellData.prefabIndex);

                    // 특정 인덱스에 프리팹이 없으면 해당 타입의 0번 인덱스 시도
                    if (prefab == null && cellData.prefabIndex != 0)
                    {
                        prefab = palette.GetPrefab(cellData.type, 0);
                    }

                    if (prefab != null)
                    {
                        // 유니티 3D 좌표계(XZ)에 맞춰 위치 계산
                        Vector3 pos = origin + new Vector3(x * cellSize + cellSize * 0.5f, 0, y * cellSize + cellSize * 0.5f);
                        GameObject instance = Instantiate(prefab, pos, Quaternion.identity, container != null ? container : transform);
                        instance.name = $"Cell_{x}_{y}_{cellData.type}_{cellData.prefabIndex}";
                    }
                }
            }

            Debug.Log($"[MapVisualizer] '{mapData.name}' 기반으로 3D 맵 생성이 완료되었습니다.");
        }

        /// <summary>
        /// 기존에 생성된 모든 타일을 제거합니다.
        /// </summary>
        public void ClearExistingMap()
        {
            Transform parent = container != null ? container : transform;
            for (int i = parent.childCount - 1; i >= 0; i--)
            {
                DestroyImmediate(parent.GetChild(i).gameObject);
            }
        }

        /// <summary>
        /// 씬 뷰에서 그리드 데이터를 시각화합니다. (XZ 평면 기반)
        /// </summary>
        private void OnDrawGizmos()
        {
            if (!showGizmos || mapData == null) return;

            Vector3 origin = transform.position;
            float cellSize = mapData.cellSize;

            for (int y = 0; y < mapData.height; y++)
            {
                for (int x = 0; x < mapData.width; x++)
                {
                    CellType type = mapData.GetCellType(x, y);
                    Gizmos.color = mapData.GetColorForType(type);

                    Vector3 center = origin + new Vector3(
                        x * cellSize + cellSize * 0.5f, 
                        0.01f, 
                        y * cellSize + cellSize * 0.5f
                    );
                    
                    Vector3 size = new Vector3(cellSize, 0.05f, cellSize);
                    Gizmos.DrawCube(center, size);

                    if (showWireframe)
                    {
                        Gizmos.color = new Color(1, 1, 1, 0.2f);
                        Gizmos.DrawWireCube(center, size);
                    }
                }
            }
        }
    }
}
