# SP1: Grid 슬롯 선택·강조 + 빌드 모달 설계

**작성일**: 2026-06-09
**상태**: 설계 (사용자 검토 대기)
**상위**: TASK-005 B (Grid 슬롯 배치 + 구매) — 풀 모달 분해의 1번째 서브프로젝트

---

## 1. 목표 / 범위

빈 타워 슬롯을 클릭하면 **슬롯이 강조**되고 **빌드 모달**이 열려, 로스터에서 타워를 골라 **골드로 구매·설치**한다. 현재의 "클릭→즉시 단일 타워 설치" P0 스텁을 대체한다.

**이번 범위(SP1)**
- 빈 슬롯 클릭 → 강조 + 빌드 모달
- 모달: 구매 가능 타워 로스터(이름·비용), 골드 부족 시 비활성, 닫기
- 선택 → 골드 차감 → 설치
- 취소(바깥/Esc/다른 슬롯) → 모달 닫힘·강조 해제

**범위 밖**
- 점유 타워 클릭 시 **관리(업그레이드·판매) 모달** → SP2
- 다종 타워 본격 밸런싱 → SP3 (SP1은 테스트용 2~3종)

---

## 2. 아키텍처

기존 UI는 MVP(UIRoot→Presenter→View, 도메인 모델 주입). 빌드 모달도 동일 패턴으로 합류한다. 게임플레이 측 `TowerPlacementController`는 "입력 감지+설치" 단일 책임을 **"슬롯 선택(이벤트)"과 "설치(PlaceAt)"로 분리**한다.

```
[클릭] → TowerPlacementController (게임플레이)
            │ OnSlotSelected(cell, worldPos)       ← 빈 슬롯 강조 + 이벤트
            ▼
        TowerBuildPresenter (UI, UIRoot 소유)
            │ view.Show(roster, gold)
            ▼
        TowerBuildModalView (uGUI, dumb)
            │ OnTowerChosen(towerData)
            ▼
        TowerBuildPresenter
            │ economy.TrySpend(cost) → placement.PlaceAt(cell, data)
            ▼
        TowerPlacementController.PlaceAt → 타워 생성·주입
```

**게임플레이 ↔ UI 교차 배선**: `ModeBootstrap`에 `virtual TowerPlacementController PlacementController => null` 추가, `GridDefenseModeBootstrap`이 override 하여 자신의 `placement` 반환. `GameManager`가 `uiRoot.Inject(..., modeBootstrap.PlacementController)`로 전달 → `UIRoot`가 controller 가 있을 때만 `TowerBuildPresenter` 생성. (Arena 모드는 null → 모달 미생성. GameManager 는 모드 비종속 유지)

---

## 3. 컴포넌트

### 3.1 `TowerPlacementController` (리팩토링)
- **제거**: `[SerializeField] TowerData towerData`(P0 단일), `OnClick→TryPlace` 즉시 설치 경로, `Bind`의 `EconomyModel`(골드는 Presenter 소유).
- **추가**:
  - `[SerializeField] GameObject highlight;` (선택 슬롯 위치로 이동·토글)
  - `public event System.Action<Vector2Int, Vector3> OnSlotSelected;`
  - `public event System.Action OnSlotDeselected;`
  - `public void Bind(TargetFinder finder);` (economy 제거)
  - `public bool PlaceAt(Vector2Int cell, TowerData data);` — 슬롯·점유 재검증 → 프리팹 생성·위치·`Initialize`·`SetTargetFinder`·`occupied` 등록. 성공 시 true.
- **클릭 처리**: `EventSystem.current.IsPointerOverGameObject()` 면 무시(모달 버튼 클릭이 월드로 새지 않게). 빈 `TowerSlot` → 강조 ON + `OnSlotSelected`. 점유 슬롯 → 강조만(SP2 자리). 슬롯 밖 → 강조 OFF + `OnSlotDeselected`.

### 3.2 `TowerRoster` (신규 ScriptableObject)
- `[CreateAssetMenu]`, `public TowerData[] towers;` — 구매 가능 타워 목록. SP1 테스트용 2~3종 에셋 생성.

### 3.3 `TowerBuildModalView` (신규, MVP View, uGUI)
- 패널 + 로스터 버튼 컨테이너 + 버튼 프리팹(이름·비용 텍스트).
- `public void Show(TowerRoster roster, int gold);` — 버튼 생성/갱신, `cost > gold` 면 버튼 `interactable=false`.
- `public void Hide();`
- `public event System.Action<TowerData> OnTowerChosen;` — 버튼 클릭 시 해당 `TowerData` 발행.
- 표시 위치: 화면 고정 패널(하단/중앙). dumb — 골드/배치 로직 없음.

### 3.4 `TowerBuildPresenter` (신규, MVP Presenter, `BasePresenter` 계열)
- 생성자: `(TowerBuildModalView view, TowerRoster roster, EconomyModel economy, TowerPlacementController placement)`.
- `Initialize()`: `placement.OnSlotSelected += HandleSlotSelected; placement.OnSlotDeselected += HandleDeselected; view.OnTowerChosen += HandleTowerChosen;`. 모달 초기 Hide.
- `HandleSlotSelected(cell, pos)`: `currentCell=cell; view.Show(roster, economy.Gold);`
- `HandleTowerChosen(data)`: `if (economy.TrySpend(data.cost)) { if (!placement.PlaceAt(currentCell, data)) economy.AddGold(data.cost); else view.Hide(); }`
- (선택) `economy.OnGoldChanged` 구독 → 모달 열린 채 골드 변동 시 버튼 affordability 갱신. SP1 은 Show 시점 스냅샷으로 충분.
- `HandleDeselected()`: `view.Hide();`
- `Dispose()`: 구독 해제.

### 3.5 배선 변경
- `ModeBootstrap`: `public virtual TowerPlacementController PlacementController => null;`
- `GridDefenseModeBootstrap`: `public override TowerPlacementController PlacementController => placement;` / `CreateMode`의 `placement.Bind(ctx.Economy, ctx.TargetFinder)` → `placement.Bind(ctx.TargetFinder)`.
- `GameManager.Start`: `uiRoot.Inject(Economy, Core, Wave, modeBootstrap.EnemyDisplayCapacity, modeBootstrap.PlacementController);`
- `UIRoot`: `[SerializeField] TowerBuildModalView buildModalView; [SerializeField] TowerRoster towerRoster;` / `Inject(..., TowerPlacementController placement)` 에서 `if (placement != null && buildModalView != null && towerRoster != null) presenters.Add(new TowerBuildPresenter(buildModalView, towerRoster, economy, placement));`

---

## 4. 데이터 흐름 (정상 경로)

1. 빈 슬롯 좌클릭 → 컨트롤러: 강조 ON, `OnSlotSelected(cell, world)`.
2. Presenter: `view.Show(roster, gold)`. 모달 표시, 살 수 없는 타워 버튼 비활성.
3. 타워 버튼 클릭 → `view.OnTowerChosen(data)`.
4. Presenter: `economy.TrySpend(data.cost)` 성공 → `placement.PlaceAt(cell, data)` → 타워 생성·주입·`occupied` 등록 → `view.Hide()`.
5. (강조는 Hide/Deselect 시 해제)

---

## 5. 엣지 케이스 / 에러 처리

| 상황 | 처리 |
|---|---|
| 골드 부족 | 해당 타워 버튼 `interactable=false` (Show 시 `gold` 기준). TrySpend 실패 시 설치 안 함 |
| TrySpend 성공했으나 PlaceAt 실패(슬롯이 그새 점유 등) | 환불 후 모달 유지 (단일 플레이라 사실상 미발생, 방어적) |
| 모달 열린 채 버튼 클릭 | `IsPointerOverGameObject()` 가드로 월드 클릭 무시 |
| 모달 열린 채 다른 빈 슬롯 클릭 | 새 슬롯으로 선택 전환 + 모달 갱신 |
| 점유 슬롯 클릭 | 강조만 (SP2 관리 모달 자리) |
| 바깥/Esc | `OnSlotDeselected` → 모달 Hide + 강조 OFF |
| 로스터/모달뷰 미할당 | `UIRoot.Inject` 가드로 Presenter 미생성 (NRE 방지) |

---

## 6. 테스트

- **PlayMode(수동, 사용자)**: GridScene 진입 → 빈 슬롯 클릭(강조+모달) → 타워 선택(골드 차감·설치) → 골드 부족 타워 비활성 → 바깥 클릭(닫힘) → 점유 슬롯 재설치 차단.
- **EditMode(선택)**: Presenter 의 "구매 가능 여부/차감" 로직을 `TowerPlacementController` 대신 작은 시임(예: `IPlacement`)으로 분리하면 단위 테스트 가능. SP1 에선 과설계 위험 → PlayMode 우선, 시임은 SP2 에서 재검토.

---

## 7. 영향 파일

**수정**: `TowerPlacementController.cs`, `GridDefenseModeBootstrap.cs`, `ModeBootstrap.cs`, `GameManager.cs`, `UIRoot.cs`
**신규**: `TowerRoster.cs`(SO) + 테스트용 `TowerData` 2~3종, `TowerBuildModalView.cs`, `TowerBuildPresenter.cs`
**씬/에디터(사용자)**: 빌드 모달 uGUI 프리팹·강조 오브젝트 제작, `UIRoot`/`TowerPlacementController` 결선, `TowerRoster` 에셋 작성

---

## 8. 확정 / 미해결

- ✅ `EconomyModel`: `Gold`(getter)·`CanAfford(int)`·`TrySpend(int)`·`AddGold(int)`·`OnGoldChanged` 확인 — affordability·차감·환불에 그대로 사용.
- 강조 오브젝트의 형태(쿼드/데칼)와 셀 정렬 — 제작은 에디터, 코드는 위치 이동만 (계획 단계 무관, 사용자 제작).
