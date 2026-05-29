using UnityEngine;
using UnityEngine.InputSystem;
using DefenseDot.Data;

namespace DefenseDot.Systems.Grid
{
    /// <summary>
    /// 그리드 선택 및 하이라이트 시각화를 담당하는 액터 컴포넌트입니다.
    /// </summary>
    public class GridSelectionActor : MonoBehaviour
    {
        public enum GridOrientation { XY, XZ }

        [Header("Settings")]
        [SerializeField] private GridOrientation orientation = GridOrientation.XZ;
        [SerializeField] private MapData mapData;
        [SerializeField] private GameObject highlightPrefab;
        [SerializeField] private Camera targetCamera;

        [Header("Input")]
        [SerializeField] private InputActionAsset inputActions;
        
        private GridSelectionLogic selectionLogic;
        private GameObject highlightInstance;
        private InputAction pointAction;

        private void Awake()
        {
            if (targetCamera == null) targetCamera = Camera.main;
            
            float cellSize = (mapData != null) ? mapData.cellSize : 1f;
            selectionLogic = new GridSelectionLogic(cellSize);

            if (highlightPrefab != null)
            {
                highlightInstance = Instantiate(highlightPrefab, transform);
                highlightInstance.SetActive(false);
            }

            var uiMap = inputActions.FindActionMap("UI");
            pointAction = uiMap.FindAction("Point");
        }

        private void OnEnable() => pointAction?.Enable();
        private void OnDisable() => pointAction?.Disable();

        private void Update()
        {
            if (pointAction == null || targetCamera == null) return;

            Vector2 mousePos = pointAction.ReadValue<Vector2>();
            Ray ray = targetCamera.ScreenPointToRay(mousePos);

            // 평면 설정 (XZ는 상단뷰, XY는 정면뷰)
            Vector3 upVector = (orientation == GridOrientation.XZ) ? Vector3.up : Vector3.back;
            Plane groundPlane = new Plane(upVector, transform.position);

            if (groundPlane.Raycast(ray, out float enter))
            {
                Vector3 hitPoint = ray.GetPoint(enter);
                
                // 상대 좌표 계산
                Vector3 localPoint = hitPoint - transform.position;

                // POCO 로직 업데이트 (XZ/XY 대응)
                selectionLogic.UpdateHover(orientation == GridOrientation.XZ ? localPoint : new Vector3(localPoint.x, 0, localPoint.y), (cellCoords) => {
                    UpdateHighlightVisual(cellCoords);
                });
            }
        }

        private void UpdateHighlightVisual(Vector2Int coords)
        {
            if (highlightInstance == null) return;

            if (mapData != null && (coords.x < 0 || coords.x >= mapData.width || coords.y < 0 || coords.y >= mapData.height))
            {
                highlightInstance.SetActive(false);
                return;
            }

            highlightInstance.SetActive(true);
            float cellSize = (mapData != null) ? mapData.cellSize : 1f;
            
            Vector3 offset = new Vector3(coords.x * cellSize + cellSize * 0.5f, 0, coords.y * cellSize + cellSize * 0.5f);
            
            if (orientation == GridOrientation.XZ)
            {
                highlightInstance.transform.position = transform.position + new Vector3(offset.x, 0.05f, offset.z);
                highlightInstance.transform.rotation = Quaternion.Euler(90, 0, 0); // Sprite를 지면에 눕힘
            }
            else
            {
                highlightInstance.transform.position = transform.position + new Vector3(offset.x, offset.z, -0.05f);
                highlightInstance.transform.rotation = Quaternion.Euler(0, 0, 0); // Sprite가 정면을 보게 함
            }
}
    }
}
