# Defense Dot Architecture Guidelines

## 1. Coding Convention
- **Casing**: Use **CamelCase** for all identifiers.
  - **PascalCase** (UpperCamelCase) for Classes, Interfaces, Methods, and Properties.
  - **camelCase** (lowerCamelCase) for Fields and Local Variables.
- **No Underscores**: Do **NOT** use underscores (`_`) in any field names, variable names, or prefixes (e.g., use `actor` instead of `actor`).
- **Documentation**: 모든 public/protected 멤버에 XML `<summary>` 태그를 사용하여 한국어로 설명을 작성해야 합니다.

## 2. UI System (MVP Pattern)
- **BasePresenter<TView, TModel>**: 모든 Presenter의 기반 클래스.
- **IView**: UI 컴포넌트 참조 및 업데이트를 위한 인터페이스. Presenter는 이 인터페이스를 통해서만 View와 통신합니다.
- **BaseModel**: UI 데이터 상태 관리.
- Presenter에서 유니티 UI 컴포넌트(Button, Text 등)에 직접 접근하는 것을 금지합니다.

## 3. Actor & AI (POCO-Actor-BT)
- **ActorBase<TData>**: 모든 인게임 액터(몬스터, 타워 등)의 공통 기반 추상 클래스.
- **Actor Interfaces**:
  - `IActor`: 위치 및 상태 관리의 기본.
  - `IDamageable`: 체력 관리 및 피해 입기 기능.
  - `IMovableActor`: 이동 가능 액터 (Monster 등).
  - `ICombatActor`: 공격 가능 액터 (Tower, Monster 등).
  - `ITargetable`: 타워의 타겟이 될 수 있는 속성.
  - `IPoolable`: 오브젝트 풀링 지원.
- **POCO Logic**:
  - 물리/수학적 계산 로직은 순수 C# 클래스로 분리합니다 (예: `PathFollowerLogic`, `CombatLogic`).
  - **생성자 주입**: POCO 클래스는 생성자에서 해당 인터페이스(예: `IMovableActor`)를 매개변수로 받아 캐싱해야 합니다.
  - **상태 체크**: 로직 실행 전 액터의 `IsMovableState()` 등을 호출하여 실행 가능 여부를 반드시 확인해야 합니다.
- **Behavior Tree**: 액터의 지능(AI)을 노드 기반으로 관리하며, 액터의 상태(`ActorState`)를 관찰하여 판단합니다.

## 4. Pathfinding (JPS + Job/Burst)
- **JPS (Jump Point Search)**: 그리드 기반 최적화 경로 탐색 알고리즘 사용.
- **Job System & Burst**: 성능 최적화를 위해 경로 탐색 연산은 백그라운 Job으로 수행하고 Burst 컴파일을 적용합니다.
- **비동기 처리**: `PathfindingService`를 통해 비동기적으로 경로를 요청하고 콜백으로 결과를 받습니다.

## 5. Map & Data
- **MapData**: 그리드 데이터(RedCell, Path, TowerSlot, Spawn, Core)를 저장하는 ScriptableObject.
  - **MapPalette Reference**: 각 맵 데이터는 자신만의 비주얼 팔레트(`MapPalette`)를 참조합니다.
  - **GridCell Struct**: 각 셀은 `CellType`뿐만 아니라 해당 타입의 프리팹 리스트 중 어떤 프리팹을 사용할지 결정하는 `prefabIndex` 정보를 함께 저장합니다.
- **MapPalette**: `CellType`별로 여러 개의 3D 프리팹을 리스트 형태로 관리합니다. 이를 통해 같은 'Path' 타입이라도 다양한 비주얼을 섞어서 사용할 수 있습니다.
- **Coordinate System**: 유니티의 표준 좌표계(Bottom-Left = 0,0 / Y-up)를 기반으로 합니다.
  - **Map Data**: (X, Y) 2차원 그리드 좌표를 사용합니다.
  - **3D World Mapping**: 3D 타일맵 배치 시 Map X는 World X로, **Map Y는 World Z (Forward)**로 매핑됩니다. World Y는 높이(Up) 축으로 사용됩니다.
  - **MapEditor**: UI Toolkit 기반의 커스텀 에디터 윈도우.
    - 시각적 일치를 위해 Y축을 반전하여 출력합니다 (Visual Top = High Map Y / High World Z).
    - 셀 내부에 (X, Y) 좌표 및 적용된 **Prefab ID(Index)**를 함께 표시합니다.
    - 브러쉬 선택 후 하단에 나타나는 **Select Prefab** 리스트를 통해 구체적인 모델을 선택할 수 있습니다.

## 6. Grid Selection & Interaction
- **Input Map**: `UI/Point`를 사용하여 마우스 위치를 추적하고, `UI/Click`을 사용하여 선택을 수행합니다.
- **POCO Selection Logic**: 마우스 스크린 좌표를 그리드 좌표로 변환하고 현재 선택된 셀 상태를 관리하는 로직은 순수 C# 클래스로 작성합니다.
- **Actor Interaction**: 3D 타일맵 환경에서 Raycast 혹은 Plane Intersection을 통해 정확한 그리드 위치를 산출합니다.
- **Visual Feedback**: 선택된 셀은 테두리 강조(Highlight) 효과를 통해 사용자에게 피드백을 제공합니다.

## 7. 3D Map Visualization
- **Source of Truth**: `MapData`가 맵 구조의 유일한 원천 데이터입니다.
- **MapVisualizer (Integrated)**: `MapData`와 `MapPalette`를 결합하여 3D 타일맵을 자동 생성하고, 씬 뷰 기즈모 가이드 및 와이어프레임을 동시에 제공하는 통합 컴포넌트입니다.
- **Automated Sync**: `Generate 3D Map` 기능을 통해 데이터와 비주얼을 항상 동기화 상태로 유지합니다.
