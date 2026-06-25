# SP1 Grid 빌드 모달 Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** 빈 타워 슬롯 클릭 시 슬롯을 강조하고 빌드 모달로 타워를 골라 골드로 구매·설치한다 (P0 즉시설치 스텁 대체).

**Architecture:** `TowerPlacementController` 를 "슬롯 선택(이벤트) + 설치(`PlaceAt`)"로 분리하고, 빌드 모달을 기존 HUD 와 동일한 MVP(View+Presenter)로 만들어 `UIRoot` 에 합류시킨다. 게임플레이↔UI 연결은 `ModeBootstrap.PlacementController` 를 `GameManager` 가 `UIRoot.Inject` 로 전달하는 합성 루트 주입으로 처리한다.

**Tech Stack:** Unity 6000.x / C# / uGUI + TextMeshPro / InputSystem / ScriptableObject. 비동기·테스트 프레임워크 미사용.

**선행 스펙:** [2026-06-09-grid-tower-build-modal-design.md](../specs/2026-06-09-grid-tower-build-modal-design.md)

---

## 이 계획 공통 규칙 (모든 태스크 적용)

- **브랜치 `feat/hd2d-phase3-billboard`** (현재 작업 브랜치) 에서 진행.
- **Unity 실행 불가**: 컴파일·PlayMode 를 이 세션에서 못 돌린다. 코드 작성 후 정적 참조 검증만 하고 **"컴파일됨/동작함"을 주장하지 말 것**. 검증은 사용자 Unity.
- **TDD 비적용 사유**: SP1 은 MonoBehaviour·uGUI·InputSystem 중심이라 단위 테스트 시임이 없다(스펙 §6 — 시임 분리는 SP2 재검토). 따라서 PlayMode 수동 검증을 1차 검증으로 한다. 가짜 테스트를 만들지 않는다.
- **커밋 정책**: **구현만 — 태스크별 커밋 금지.** 전부 끝나면 diff 제시 → 사용자 **명시 승인** → `commit` 스킬로 scoped 일괄 커밋(Task 10).
- **scoped staging**: `git add .`/`-A` 금지. 만든 파일 경로만 명시.
- **신규 `.cs`**: `.cs.meta` 직접 생성(충돌 검사한 32-hex GUID, `head -c16 /dev/urandom | od -An -tx1 | tr -d ' \n'`). 형식은 기존 스크립트 `.cs.meta`(`MonoImporter` 블록) 참고.
- **컨벤션**: event 는 `On*`, 구독 핸들러는 `Handle*`. 라이프사이클 함수(Awake/OnEnable 등)에 `=>` 금지. 검증은 `IsValid()` bool. 접근 제한자 명시. 주석 한국어 `<summary>`.

---

## File Structure

**신규 (코드)**
- `Assets/Scripts/Data/TowerRoster.cs` — 구매 가능 `TowerData[]` SO
- `Assets/Scripts/UI/Views/TowerBuildModalView.cs` — uGUI dumb View (로스터 버튼·affordability·OnTowerChosen)
- `Assets/Scripts/UI/Presenters/TowerBuildPresenter.cs` — 선택↔모달↔구매↔설치 중재 (IPresenter)

**수정**
- `Assets/Scripts/Systems/Tower/TowerPlacementController.cs` — 선택/설치 분리
- `Assets/Scripts/Systems/Mode/ModeBootstrap.cs` — `PlacementController` virtual
- `Assets/Scripts/Systems/Mode/GridDefenseModeBootstrap.cs` — override + Bind 변경
- `Assets/Scripts/UI/InGame/UIRoot.cs` — Inject 인자 + 조건부 Presenter
- `Assets/Scripts/Systems/Management/GameManager.cs` — Inject 호출 1줄

**에디터/씬 (사용자)** — 모달 uGUI 프리팹·강조 오브젝트 제작, `TowerRoster` 에셋(테스트 2~3종), `UIRoot`/`TowerPlacementController` 결선

---

## Task 1: TowerRoster (ScriptableObject)

**Files:** Create `Assets/Scripts/Data/TowerRoster.cs` (+ `.meta`)

- [ ] **Step 1: 작성**

```csharp
using UnityEngine;

namespace DefenseDot.Data
{
    /// <summary> 빌드 모달에 노출할 구매 가능 타워 목록입니다. </summary>
    [CreateAssetMenu(fileName = "TowerRoster", menuName = "DefenseDot/TowerRoster")]
    public class TowerRoster : ScriptableObject
    {
        [Tooltip("구매 가능한 타워 목록")]
        public TowerData[] towers;
    }
}
```

- [ ] **Step 2: .meta 생성** — 32-hex GUID 로 `TowerRoster.cs.meta` 작성 (`MonoImporter` 블록).

---

## Task 2: TowerPlacementController — 선택/설치 분리

**Files:** Modify `Assets/Scripts/Systems/Tower/TowerPlacementController.cs` (전체 교체)

- [ ] **Step 1: 전체 교체** — 아래로 파일 전체를 대체:

```csharp
// 타워 배치 컨트롤러 — 빈 슬롯 클릭 시 선택·강조(이벤트), 설치는 PlaceAt 로 분리
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.EventSystems;
using DefenseDot.Data;

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
        private InputAction pointAction;
        private InputAction clickAction;
        private readonly Dictionary<Vector2Int, TowerActor> occupied = new Dictionary<Vector2Int, TowerActor>();
        private bool hasSelection;

        /// <summary> 빈 슬롯이 선택됨 (셀, 월드 위치). </summary>
        public event System.Action<Vector2Int, Vector3> OnSlotSelected;
        /// <summary> 선택이 해제됨. </summary>
        public event System.Action OnSlotDeselected;

        /// <summary> 합성 루트가 타겟 탐색기를 주입합니다. </summary>
        public void Bind(TargetFinder finder)
        {
            targetFinder = finder;
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
```

- [ ] **Step 2: 정적 확인** — `TowerData`/`MapData`/`CellType.TowerSlot`/`TowerActor.Initialize`/`SetTargetFinder` 참조 유효. `using DefenseDot.Domain.Models` 제거됨(economy 미사용) 확인.

---

## Task 3: ModeBootstrap / GridDefenseModeBootstrap — controller 노출

**Files:** Modify `Assets/Scripts/Systems/Mode/ModeBootstrap.cs`, `GridDefenseModeBootstrap.cs`

- [ ] **Step 1: ModeBootstrap** — `using` 추가 + virtual 프로퍼티 추가.

`using DefenseDot.Systems.Visual.Camera;` 아래에 추가:
```csharp
using DefenseDot.Systems.Tower;
```
`public abstract int EnemyDisplayCapacity { get; }` 아래에 추가:
```csharp
        /// <summary> 이 모드의 타워 배치 컨트롤러입니다. 없으면 null (빌드 모달 미생성). </summary>
        public virtual TowerPlacementController PlacementController => null;
```

- [ ] **Step 2: GridDefenseModeBootstrap** — override 추가 + Bind 변경.

`EnemyDisplayCapacity` override 아래에 추가:
```csharp
        /// <summary> 그리드 모드의 타워 배치 컨트롤러입니다. </summary>
        public override TowerPlacementController PlacementController => placement;
```
`CreateMode` 내 Bind 호출 변경:
```csharp
            if (placement != null) placement.Bind(ctx.TargetFinder);
```
(기존 `placement.Bind(ctx.Economy, ctx.TargetFinder)` 대체)

---

## Task 4: TowerBuildModalView (uGUI dumb View)

**Files:** Create `Assets/Scripts/UI/Views/TowerBuildModalView.cs` (+ `.meta`)

- [ ] **Step 1: 작성**

```csharp
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using DefenseDot.Data;

namespace DefenseDot.UI.Views
{
    /// <summary> 빈 슬롯 선택 시 구매 가능 타워를 나열하는 빌드 모달 View 입니다. (dumb) </summary>
    public class TowerBuildModalView : MonoBehaviour
    {
        [SerializeField] private GameObject panel;
        [SerializeField] private Transform buttonContainer;
        [SerializeField] private Button buttonPrefab;

        /// <summary> 타워 버튼이 선택됨. </summary>
        public event System.Action<TowerData> OnTowerChosen;
        private readonly List<Button> spawned = new List<Button>();

        /// <summary> 로스터로 버튼을 구성하고 패널을 표시합니다. cost > gold 면 버튼 비활성. </summary>
        public void Show(TowerRoster roster, int gold)
        {
            Clear();
            if (roster != null && roster.towers != null)
            {
                foreach (TowerData tower in roster.towers)
                {
                    if (tower == null) continue;
                    Button button = Instantiate(buttonPrefab, buttonContainer);
                    TextMeshProUGUI label = button.GetComponentInChildren<TextMeshProUGUI>();
                    if (label != null) label.text = $"{tower.towerName}\n{tower.cost}G";
                    button.interactable = gold >= tower.cost;
                    TowerData captured = tower;
                    button.onClick.AddListener(() => OnTowerChosen?.Invoke(captured));
                    spawned.Add(button);
                }
            }
            if (panel != null) panel.SetActive(true);
        }

        /// <summary> 모달을 숨깁니다. </summary>
        public void Hide()
        {
            if (panel != null) panel.SetActive(false);
        }

        private void Clear()
        {
            foreach (Button button in spawned) if (button != null) Destroy(button.gameObject);
            spawned.Clear();
        }
    }
}
```

- [ ] **Step 2: .meta 생성** — 32-hex GUID 로 `TowerBuildModalView.cs.meta`.
- [ ] **Step 3: 정적 확인** — `using TMPro;`(TextMeshProUGUI, 프로젝트 표준)·`UnityEngine.UI`(Button) 유효. `onClick` 람다는 라이프사이클 아님(허용).

---

## Task 5: TowerBuildPresenter (IPresenter)

**Files:** Create `Assets/Scripts/UI/Presenters/TowerBuildPresenter.cs` (+ `.meta`)

- [ ] **Step 1: 작성**

```csharp
using UnityEngine;
using DefenseDot.Data;
using DefenseDot.Domain.Models;
using DefenseDot.Systems.Tower;
using DefenseDot.UI.Views;

namespace DefenseDot.UI.Presenters
{
    /// <summary>
    /// 슬롯 선택과 빌드 모달, 구매·설치를 중재하는 Presenter 입니다.
    /// 모달 표시 상태(선택 셀)만 다루므로 BaseModel 없이 IPresenter 를 직접 구현합니다.
    /// </summary>
    public class TowerBuildPresenter : IPresenter
    {
        private readonly TowerBuildModalView view;
        private readonly TowerRoster roster;
        private readonly EconomyModel economy;
        private readonly TowerPlacementController placement;
        private Vector2Int currentCell;

        /// <summary> 모달 뷰·로스터·경제 모델·배치 컨트롤러를 주입받습니다. </summary>
        public TowerBuildPresenter(TowerBuildModalView view, TowerRoster roster, EconomyModel economy, TowerPlacementController placement)
        {
            this.view = view;
            this.roster = roster;
            this.economy = economy;
            this.placement = placement;
        }

        /// <summary> 선택·구매 사건을 구독하고 모달을 초기 숨김 처리합니다. </summary>
        public void Initialize()
        {
            placement.OnSlotSelected += HandleSlotSelected;
            placement.OnSlotDeselected += HandleDeselected;
            view.OnTowerChosen += HandleTowerChosen;
            view.Hide();
        }

        /// <summary> 구독을 해제합니다. </summary>
        public void Dispose()
        {
            placement.OnSlotSelected -= HandleSlotSelected;
            placement.OnSlotDeselected -= HandleDeselected;
            view.OnTowerChosen -= HandleTowerChosen;
        }

        private void HandleSlotSelected(Vector2Int cell, Vector3 worldPos)
        {
            currentCell = cell;
            view.Show(roster, economy.Gold);
        }

        private void HandleTowerChosen(TowerData data)
        {
            if (!economy.TrySpend(data.cost)) return;
            if (!placement.PlaceAt(currentCell, data)) economy.AddGold(data.cost);
            else view.Hide();
        }

        private void HandleDeselected()
        {
            view.Hide();
        }
    }
}
```

- [ ] **Step 2: .meta 생성** — 32-hex GUID 로 `TowerBuildPresenter.cs.meta`.
- [ ] **Step 3: 정적 확인** — `IPresenter`(동일 네임스페이스)·`EconomyModel.Gold/TrySpend/AddGold`·`TowerPlacementController.OnSlotSelected/OnSlotDeselected/PlaceAt`·`TowerBuildModalView.OnTowerChosen/Show/Hide` 시그니처 일치.

---

## Task 6: UIRoot — 빌드 Presenter 조건부 합류

**Files:** Modify `Assets/Scripts/UI/InGame/UIRoot.cs`

- [ ] **Step 1: using 추가** — 상단에:
```csharp
using DefenseDot.Data;
using DefenseDot.Systems.Tower;
```

- [ ] **Step 2: 직렬화 필드 추가** — `[SerializeField] private HUDView hudView;` 아래:
```csharp
        [SerializeField] private TowerBuildModalView buildModalView;
        [SerializeField] private TowerRoster towerRoster;
```

- [ ] **Step 3: Inject 시그니처 + 조건부 Presenter** — 기존 `Inject` 를 교체:
```csharp
        public void Inject(EconomyModel economy, CoreModel core, WaveModel wave, int enemyCapacity,
                           TowerPlacementController placement)
        {
            presenters.Add(new HUDPresenter(hudView, new HUDModel(), economy, core, wave, enemyCapacity));

            if (placement != null && buildModalView != null && towerRoster != null)
                presenters.Add(new TowerBuildPresenter(buildModalView, towerRoster, economy, placement));

            foreach (IPresenter presenter in presenters) presenter.Initialize();
        }
```

---

## Task 7: GameManager — controller 전달

**Files:** Modify `Assets/Scripts/Systems/Management/GameManager.cs`

- [ ] **Step 1: Inject 호출 변경** — `Start()` 의 UI 연결부:
```csharp
            if (uiRoot != null)
                uiRoot.Inject(Economy, Core, Wave, modeBootstrap.EnemyDisplayCapacity, modeBootstrap.PlacementController);
```
(기존 4-인자 호출 대체. `modeBootstrap` 은 이미 보유, `using DefenseDot.Systems.Tower;` 도 이미 존재)

- [ ] **Step 2: 정적 확인** — 전체 신규/수정 참조 일관성 재점검. 컴파일은 사용자 Unity.

---

## Task 8: 에디터/씬 작업 (사용자 Unity)

- [ ] **TowerRoster 에셋** 생성(`Create > DefenseDot > TowerRoster`), 테스트용 `TowerData` 2~3종 등록 (기존 Tower Test + 비용/스탯 다른 1~2종 복제)
- [ ] **빌드 모달 uGUI 프리팹/오브젝트**: `panel`(루트) + `buttonContainer`(Vertical/Grid Layout) + `buttonPrefab`(`Button` + 자식 `TextMeshProUGUI`). `TowerBuildModalView` 부착, 세 필드 결선
- [ ] **강조 오브젝트**: 슬롯 크기 쿼드/데칼, 비활성 시작
- [ ] **결선**: `UIRoot` 에 `buildModalView`·`towerRoster` 할당. `TowerPlacementController` 에 `highlight` 할당, `towerData` 잔재 필드 무시(제거됨)
- [ ] **EventSystem**: 씬에 InputSystem UI Input Module 의 EventSystem 존재 확인 (모달 버튼 클릭·IsPointerOverGameObject 용)

---

## Task 9: PlayMode 검증 (사용자)

- [ ] **V1** 빈 슬롯 클릭 → 슬롯 강조 + 모달 오픈, 즉시 설치 안 됨
- [ ] **V2** 살 수 있는 타워 선택 → 골드 차감 + 설치 + 모달 닫힘
- [ ] **V3** 골드 부족 타워 → 버튼 비활성
- [ ] **V4** 바깥/다른 빈 슬롯 클릭 → 닫힘·강조 해제 / 새 슬롯 전환
- [ ] **V5** 점유 슬롯 클릭 → 강조만 (재설치 안 됨)
- [ ] **V6** Arena 씬 진입 → 빌드 모달 없음, 기존 동작 유지

---

## Task 10: 일괄 커밋 (V 검증 후 · 사용자 명시 승인)

- [ ] **Step 1: lint** — 본 작업 신규/수정 `.cs` 만 범위로 `lint` 스킬 수행.
- [ ] **Step 2: scoped 커밋** (사용자 "커밋해줘" 후) — 신규 3 `.cs`+`.meta`, 수정 5 `.cs`, (에디터 작업분 씬/프리팹/에셋 포함 여부는 사용자 지시에 따름).

```bash
git add Assets/Scripts/Data/TowerRoster.cs Assets/Scripts/Data/TowerRoster.cs.meta \
        Assets/Scripts/UI/Views/TowerBuildModalView.cs Assets/Scripts/UI/Views/TowerBuildModalView.cs.meta \
        Assets/Scripts/UI/Presenters/TowerBuildPresenter.cs Assets/Scripts/UI/Presenters/TowerBuildPresenter.cs.meta \
        Assets/Scripts/Systems/Tower/TowerPlacementController.cs \
        Assets/Scripts/Systems/Mode/ModeBootstrap.cs Assets/Scripts/Systems/Mode/GridDefenseModeBootstrap.cs \
        Assets/Scripts/UI/InGame/UIRoot.cs Assets/Scripts/Systems/Management/GameManager.cs
git commit -m "feat: 그리드 타워 슬롯 선택·강조 및 빌드 모달 구매 설치"
```

---

## Self-Review (작성자 점검)

- **스펙 커버리지**: 컨트롤러 분리(Task2), 로스터(Task1), 모달 뷰(Task4), Presenter(Task5), 교차 배선(Task3·6·7), 강조(Task2 highlight + Task8), affordability·환불(Task5 Gold/TrySpend/AddGold), 엣지(IsPointerOverGameObject·PlaceAt 환불·점유 슬롯 강조만) — 전부 태스크 대응. 업그레이드·판매·밸런싱은 SP2/SP3 로 제외(스펙과 일치).
- **플레이스홀더 스캔**: 모든 코드 단계 완전 코드. "TBD/적절히" 없음.
- **타입 일관성**: 이벤트 `OnSlotSelected(Vector2Int,Vector3)`/`OnSlotDeselected()`/`OnTowerChosen(TowerData)`, 핸들러 `HandleSlotSelected/HandleDeselected/HandleTowerChosen`, `PlaceAt(Vector2Int,TowerData):bool`, `Bind(TargetFinder)`, `Show(TowerRoster,int)`/`Hide()`, `Inject(...,TowerPlacementController)`, `PlacementController` — 선언·호출 일치. `EconomyModel.Gold/TrySpend/AddGold` 실제 API 확인됨.
