// 타워 배치 컨트롤러 — 클릭 시 슬롯 검증·골드 차감·타워 생성
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using DefenseDot.Data;
using DefenseDot.Domain.Models;

namespace DefenseDot.Systems.Tower
{
    /// <summary>
    /// 마우스 클릭으로 타워를 배치하는 컨트롤러입니다.
    /// TowerSlot 검증 → 점유 검사 → 골드 차감 → 타워 생성 순으로 처리합니다.
    /// </summary>
    public class TowerPlacementController : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private MapData mapData;
        [SerializeField] private Camera targetCamera;
        [SerializeField] private TowerData towerData;   // P0: 단일 타워 종류
        [SerializeField] private Transform container;

        [Header("Input")]
        [SerializeField] private InputActionAsset inputActions;

        // 주입 의존성
        private EconomyModel economy;
        private TargetFinder targetFinder;

        private InputAction pointAction;
        private InputAction clickAction;
        private readonly Dictionary<Vector2Int, TowerActor> occupied = new Dictionary<Vector2Int, TowerActor>();

        /// <summary>
        /// 합성 루트에서 경제 모델과 타겟 탐색기를 주입합니다.
        /// </summary>
        public void Bind(EconomyModel economyModel, TargetFinder finder)
        {
            economy = economyModel;
            targetFinder = finder;
        }

        private void Awake()
        {
            if (targetCamera == null) targetCamera = Camera.main;

            if (inputActions != null)
            {
                var uiMap = inputActions.FindActionMap("UI");
                pointAction = uiMap?.FindAction("Point");
                clickAction = uiMap?.FindAction("Click");
            }
        }

        private void OnEnable()
        {
            pointAction?.Enable();
            clickAction?.Enable();
            if (clickAction != null) clickAction.performed += OnClick;
        }

        private void OnDisable()
        {
            if (clickAction != null) clickAction.performed -= OnClick;
            pointAction?.Disable();
            clickAction?.Disable();
        }

        private void OnClick(InputAction.CallbackContext ctx)
        {
            if (mapData == null || towerData == null || economy == null) return;
            if (pointAction == null || targetCamera == null) return;

            TryPlace(CurrentCell());
        }

        private Vector2Int CurrentCell()
        {
            Vector2 mousePos = pointAction.ReadValue<Vector2>();
            Ray ray = targetCamera.ScreenPointToRay(mousePos);
            Plane ground = new Plane(Vector3.up, transform.position);

            if (ground.Raycast(ray, out float enter))
            {
                Vector3 hit = ray.GetPoint(enter);
                Vector3 local = hit - transform.position;
                float cellSize = (mapData != null) ? mapData.cellSize : 1f;
                return new Vector2Int(Mathf.FloorToInt(local.x / cellSize), Mathf.FloorToInt(local.z / cellSize));
            }
            return new Vector2Int(-1, -1);
        }

        private void TryPlace(Vector2Int cell)
        {
            // 1) 설치 가능한 슬롯인지 검증
            if (mapData.GetCellType(cell.x, cell.y) != CellType.TowerSlot) return;
            // 2) 이미 점유된 셀인지 검사
            if (occupied.ContainsKey(cell)) return;
            // 3) 골드 검사 및 차감 (실패 시 배치 취소)
            if (!economy.TrySpend(towerData.cost)) return;

            // 4) 타워 생성·배치
            GameObject go = Instantiate(towerData.prefab, container != null ? container : transform);
            TowerActor tower = go.GetComponent<TowerActor>();
            if (tower == null) tower = go.AddComponent<TowerActor>();

            tower.transform.position = transform.position + new Vector3(cell.x + 0.5f, 0.8f, cell.y + 0.5f);
            tower.Initialize(towerData);
            tower.SetTargetFinder(targetFinder);

            occupied[cell] = tower;
        }
    }
}
