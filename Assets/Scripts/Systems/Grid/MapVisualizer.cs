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

        [Header("Camera Settings")]
        [SerializeField, Tooltip("카메라 포커스 시 적용할 여유 공간 배율")]
        private float cameraPadding = 1.1f;
        [SerializeField, Tooltip("쿼터뷰 각도 (Pitch)")]
        private float cameraPitch = 30f;
        [SerializeField, Tooltip("쿼터뷰 각도 (Yaw)")]
        private float cameraYaw = 45f;

        /// <summary>
        /// 외부(에디터 등)에서 맵 데이터를 주입하고 즉시 3D 맵을 생성합니다.
        /// </summary>
        public void SetupAndGenerate(MapData data)
        {
            this.mapData = data;
            GenerateMap();
            FocusCameraOnMap();
        }

        /// <summary>
        /// 현재 메인 카메라를 맵 전체가 보이도록 비스듬하게 배치합니다.
        /// </summary>
        [ContextMenu("Focus Camera on Map")]
        public void FocusCameraOnMap()
        {
            Camera mainCam = Camera.main;
            if (mainCam == null)
            {
                Debug.LogWarning("[MapVisualizer] Main Camera를 찾을 수 없습니다.");
                return;
            }

            if (mapData == null) return;

            // 1. 맵의 논리적 중심 계산
            float mapW = mapData.width * mapData.cellSize;
            float mapH = mapData.height * mapData.cellSize;
            Vector3 mapCenter = transform.position + new Vector3(mapW * 0.5f, 0, mapH * 0.5f);

            // 2. 카메라 회전 설정 (블루 아카이브 스타일 쿼터뷰)
            mainCam.transform.rotation = Quaternion.Euler(cameraPitch, cameraYaw, 0);

            // 3. 맵을 화면에 맞추기 위한 거리/사이즈 계산
            // 대각선 길이를 고려하여 맵 전체가 잘리지 않도록 함
            float diagonal = Mathf.Sqrt(mapW * mapW + mapH * mapH);
            
            if (mainCam.orthographic)
            {
                // 직교 투영: 맵의 대각선 절반 값을 기본으로 패딩 적용
                mainCam.orthographicSize = (diagonal * 0.5f) * cameraPadding;
            }
            else
            {
                // 원근 투영: FOV와 대각선 길이를 고려하여 뒤로 후퇴
                float fov = mainCam.fieldOfView;
                float halfFovRad = fov * 0.5f * Mathf.Deg2Rad;
                float distance = (diagonal * 0.5f) / Mathf.Tan(halfFovRad);
                mainCam.transform.position = mapCenter - mainCam.transform.forward * (distance * cameraPadding);
                return;
            }

            // 4. 카메라 위치 이동 (중심에서 뒤로 후퇴)
            // 거리는 충분히 멀리 두어도 Orthographic에서는 크기에 영향 없음
            mainCam.transform.position = mapCenter - mainCam.transform.forward * 50f;

            Debug.Log($"[MapVisualizer] Camera focused on map: {mapData.name}");
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
