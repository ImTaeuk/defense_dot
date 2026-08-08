// 타워 배치 컨트롤러 — 빈 슬롯 클릭 시 선택·강조(이벤트), 설치는 PlaceAt 로 분리
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.EventSystems;
using DefenseDot.Core;
using DefenseDot.Core.Pooling;
using DefenseDot.Data;
using DefenseDot.Domain.Models;

namespace DefenseDot.Systems.Tower
{
    /// <summary>
    /// 빈 타워 슬롯을 클릭하면 선택·강조하고 OnSlotSelected 를 발행합니다.
    /// 실제 설치는 PlaceAt 로 분리되어 빌드 모달이 중간에 개입합니다.
    /// </summary>
    public class TowerPlacementController : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private MapData mapData;
        [SerializeField] private Camera targetCamera;
        [SerializeField] private Transform container;
        [SerializeField] private GameObject highlight;

        [Header("Input")]
        [SerializeField] private InputActionAsset inputActions;

        private TargetFinder targetFinder;

        /// <summary> 배치될 타워에 넘길 전투 상태(로드아웃 수정자가 참조). </summary>
        private ICombatState combatState;
        /// <summary> 배치될 타워에 넘길 이펙트 풀. </summary>
        private PoolSystem pool;
        /// <summary> 배치될 타워에 넘길 게임 단계. </summary>
        private GameFlowModel flow;

        private InputAction pointAction;
        private InputAction clickAction;
        private readonly Dictionary<Vector2Int, TowerActor> occupied = new Dictionary<Vector2Int, TowerActor>();
        private bool hasSelection;

        /// <summary> 빈 슬롯이 선택됨 (셀, 월드 위치). </summary>
        public event System.Action<Vector2Int, Vector3> OnSlotSelected;
        /// <summary> 선택이 해제됨. </summary>
        public event System.Action OnSlotDeselected;

        /// <summary> 합성 루트가 배치될 타워에 넘겨줄 의존성을 주입합니다. </summary>
        /// <param name="finder">사거리 안의 적을 찾는 탐색기</param>
        /// <param name="state">로드아웃 수정자가 참조할 전투 상태</param>
        /// <param name="poolSystem">이펙트 예열·스폰에 쓰는 풀</param>
        /// <param name="gameFlow">게임 단계. 플레이 중이 아니면 타워가 능력을 멈춘다</param>
        public void Bind(TargetFinder finder, ICombatState state, PoolSystem poolSystem, GameFlowModel gameFlow)
        {
            targetFinder = finder;
            combatState = state;
            pool = poolSystem;
            flow = gameFlow;
        }

        private void Awake()
        {
            if (targetCamera == null) targetCamera = Camera.main;
            if (inputActions != null)
            {
                InputActionMap uiMap = inputActions.FindActionMap("UI");
                pointAction = uiMap?.FindAction("Point");
                clickAction = uiMap?.FindAction("Click");
            }
            if (highlight != null) highlight.SetActive(false);
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
            if (mapData == null || pointAction == null || targetCamera == null) return;
            if (EventSystem.current != null && EventSystem.current.IsPointerOverGameObject()) return;

            Vector2Int cell = CurrentCell();
            if (IsBuildableEmpty(cell)) Select(cell);
            else Deselect();
        }

        private bool IsBuildableEmpty(Vector2Int cell)
        {
            return mapData.GetCellType(cell.x, cell.y) == CellType.TowerSlot && !occupied.ContainsKey(cell);
        }

        private void Select(Vector2Int cell)
        {
            hasSelection = true;
            Vector3 world = CellToWorld(cell);
            if (highlight != null)
            {
                highlight.transform.position = world;
                highlight.SetActive(true);
            }
            OnSlotSelected?.Invoke(cell, world);
        }

        private void Deselect()
        {
            if (!hasSelection) return;
            hasSelection = false;
            if (highlight != null) highlight.SetActive(false);
            OnSlotDeselected?.Invoke();
        }

        /// <summary> 셀에 타워를 설치합니다. 슬롯·점유 재검증 후 성공 시 true. (골드 무관) </summary>
        public bool PlaceAt(Vector2Int cell, TowerData data)
        {
            if (data == null || data.prefab == null) return false;
            if (mapData == null || mapData.GetCellType(cell.x, cell.y) != CellType.TowerSlot) return false;
            if (occupied.ContainsKey(cell)) return false;

            GameObject go = Instantiate(data.prefab, container != null ? container : transform);
            TowerActor tower = go.GetComponent<TowerActor>();
            if (tower == null) tower = go.AddComponent<TowerActor>();
            tower.transform.position = CellToWorld(cell);
            tower.Initialize(data);
            tower.SetTargetFinder(targetFinder);
            tower.SetupAbilities(targetFinder, combatState, null, pool, flow);   // 스타터는 그리드에 없다

            occupied[cell] = tower;
            Deselect();
            return true;
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
                float cellSize = mapData != null ? mapData.cellSize : 1f;
                return new Vector2Int(Mathf.FloorToInt(local.x / cellSize), Mathf.FloorToInt(local.z / cellSize));
            }
            return new Vector2Int(-1, -1);
        }

        private Vector3 CellToWorld(Vector2Int cell)
        {
            return transform.position + new Vector3(cell.x + 0.5f, 0.8f, cell.y + 0.5f);
        }
    }
}
