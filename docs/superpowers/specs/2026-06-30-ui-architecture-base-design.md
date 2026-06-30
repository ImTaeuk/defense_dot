# UI 아키텍처 베이스 계층 재설계

**작성일**: 2026-06-30
**상태**: 설계 승인 (4-렌즈 적대 검증 반영)
**범위**: 베이스 계층 신설 + HUD 전체 이전

---

## 1. 목표

흩어진 UI 베이스(`BasePresenter`/`IView`/`HudRoot`/`UIRoot` 혼재)를 `UIObject` 정점의 통일된 계층으로 정렬하고, Model 통지·View 바인딩을 표준화하며, HUD를 새 구조로 이전한다. **기존 constructor-DI는 유지한다.**

## 2. 현재 문제 (혼재 진단)

| 영역 | 통일된 곳 | 벗어난 곳 |
|---|---|---|
| Presenter 베이스 | `BasePresenter<TView,TModel>` (ArenaHud·HUD) | `IPresenter` 직접 (Card·Result·TowerBuild) |
| View 베이스 | `HudRoot : MonoBehaviour, IView` (ArenaHud·HUD) | 맨 `MonoBehaviour` (Card·Result·Gold·Health·EnemyCount·Round) |
| View 인터페이스 | `IView`(Show/Hide) | `ICardSelectionView` 별도 |
| Widget/View 혼동 | — | `GoldView`·`HealthView`·`EnemyCountView`·`RoundView`는 사실상 위젯인데 View로 명명 |
| BaseModel | — | `UI.BaseModel`(빈 껍데기) + `Domain.Models.BaseModel`(SetField) **2개 공존** |
| 중간 Model | — | `ArenaHudModel`/`HUDModel` = 도메인 Model 값을 또 저장하는 **이중 캐싱** |

## 3. 설계 결정

### 3.1 UI 계층

- **UIObject** : `abstract MonoBehaviour` — **얇게** 유지. `RectTransform` 캐싱 + `UIDepth` 만. cross-cutting 동작은 인터페이스(`IUIShowable` 등)로 분리해 god-base 화를 막는다. *(검증: abstract MonoBehaviour 단일상속 잠금 완화)*
- **UIWidget** : `UIObject` — 복합 UI 요소(버튼+텍스트+이펙트) 래핑. 부모-자식 OK, **형제 참조 금지**. 자기 표시값의 **포맷팅을 스스로 소유**. *(검증: 포맷팅을 Presenter에서 위젯으로)*
- **UIWidget\<T>** : `UIWidget` — `T` = 바인딩 DTO. `Initialize(T data)`. **UIView는 concrete `UIWidget<T>`를 타입 필드로 참조**(비제네릭 베이스로 묶어 다운캐스트하지 않는다). *(검증: 이종 컬렉션 타입 문제)*
- **UIView** : `UIObject`, `IUIShowable` — `UIWidget`들로 구성된 패널. `Show()/Hide()` + 가상 `OnShow()/OnHide()`, `UIInitType`, `UIDepth`. **View는 Presenter를 모른다.**
- **UIPresenter\<T> where T : UIView** : 순수 C#, `IPresenter`. View만 제네릭, Model은 필드 직접 보유. `Bind()` 헬퍼.
- **UIRoot** : `MonoBehaviour`(기존 확장) — View 초기화 → **Presenter 생성·View 주입·초기화** → `UIDepth` 배치(풀링 런타임 생성 포함). **Presenter 생성을 UIRoot가 소유**(`HudRoot.Bind` 자기설치 폐지). *(검증: 'View는 Presenter를 모름' 실현)*

### 3.2 Model 계층

- **constructor-DI 유지** — `GameManager`(단일 합성루트)가 Model 생성 → `UIRoot.Inject` → Presenter 생성자. **레지스트리(Service Locator) 도입하지 않는다.** *(검증 high: 전역 정적 레지스트리는 기존 DI 퇴행·테스트 격리 파괴)*
- **BaseModel 통일** — `Domain.Models.BaseModel`(SetField 보유) 하나로. 빈 `UI.BaseModel` 제거.
- **ReactiveProperty\<T> (자체 경량)** — **단일 스칼라 전용**(예: `Economy.Gold`, `Score.Score`). 쓰기는 **private**(모델 메서드 내부에서만), UI에는 읽기 전용(`IReadOnlyReactiveProperty<T>`)으로 노출해 `TrySpend`/`AddGold` 등 불변식을 보존. *(검증: RP가 기존 private setter 캡슐화를 침식하지 않게)*
- **다중 스칼라는 별도 struct 래퍼** — `RP<WaveProgress>`(current,total), `RP<TimerState>`(remaining,duration), `RP<HealthState>`(hp,maxHp,ratio). 두 값이 **한 트랜잭션으로 원자 통지**되어 비율 계산 중간상태 깜빡임을 막는다. *(검증 high: 다인자·파생 이벤트가 RP<단일값>에 안 맞음 / 사용자 결정: 다중 스칼라는 별도 래퍼 유도)*
- **신호형은 RP 아님** — `OnLevelUp`(무페이로드)은 event 유지.
- **ForceNotify** — equality를 우회한 강제 통지 API. `EconomyModel.Initialize`의 `gold=-1` 더미 해킹을 대체. *(검증: 재시작 시 동일값이 묵살되어 첫 표시 누락되는 문제)*

### 3.3 바인딩

- **UIPresenter.Bind(IReadOnlyReactiveProperty\<V>, Action\<V>)** — 구독 + `IDisposable` 토큰 집계 + `Dispose()` 일괄 해제. *(검증: (RP,Action) 토큰 단위 추적으로 부분 해제·중복 구독 방지)*
- **초기값 반영 타이밍** — Bind 시점 1회 + **`OnShow()` 시점 재반영**. `InactiveOnStart`/풀링으로 비활성 상태에서 Bind된 View가 활성화될 때 stale 표시를 막는다. *(검증 high: Bind 초기값이 inactive/풀링 View와 충돌)*
- **Initialize 재진입 가드** — 풀 재사용으로 `Initialize`가 2회 호출돼도 중복 구독되지 않게.

### 3.4 enum

- `UIDepth = { HUD, Fixed, Popup, System }` (낮을수록 뒤, 높을수록 앞)
- `UIInitType = { ActiveOnStart, InactiveOnStart }`

## 4. HUD 이전 범위·매핑

| 위젯 (신규 `UIWidget<T>`) | DTO | 도메인 Model 소스 |
|---|---|---|
| `GoldWidget` | `long`/`int` | `EconomyModel.Gold` (RP) |
| `ScoreWidget` | `int` | `ScoreModel.Score` (RP) |
| `HealthWidget` | `HealthState{hp,maxHp,ratio}` | `CoreModel` (RP\<struct>) |
| `EnemyWidget` | `EnemyState{alive,capacity}` | `WaveModel.Remaining` + capacity (RP\<struct>) |
| `RoundWidget` | `WaveProgress{current,total}` | `WaveModel` (RP\<struct>) |
| `TimeWidget` | `TimerState{remaining,duration}` | `RoundTimerModel` (RP\<struct>) |

- **UI 중간 Model 제거**: `ArenaHudModel`/`HUDModel` 삭제 → Presenter가 도메인 Model RP를 위젯에 직접 Bind.
- **포맷팅은 위젯 소유**: `$"{current} / {total}"`, `N0`, `CeilToInt` 등 현재 View의 문자열 조립을 위젯으로 이동. Presenter는 도메인 Model→DTO 매핑만.
- **모놀리식 `ArenaHudView` → 위젯 조립식**: 현재 8개 TMP 직접 보유를 위젯 컴포넌트 트리로 분해.

## 5. 검증 반영 (4-렌즈 적대 검증, 2026-06-30)

| 발견 (심각도) | 반영 |
|---|---|
| 레지스트리는 DI 퇴행 (high×3) | **레지스트리 폐기**, constructor-DI 유지 |
| RP 전면 래핑이 다인자/파생 이벤트와 불일치 (high×2) | RP는 **단일 스칼라 전용** + 다값은 **struct 래퍼** |
| Bind 초기값이 inactive/풀링 View와 충돌 (high) | **OnShow 시점 재반영** 훅 |
| ArenaHudView 위젯 분해 작업량 (high) | **Prefab Variant** 점진 이전 (§6) |
| UIObject abstract 단일상속 잠금 (med) | **인터페이스 분리**(IUIShowable) + 얇은 UIObject |
| UIWidget\<T> 이종 컬렉션 타입 문제 (med) | 위젯은 **concrete 타입 필드**로 참조 |
| UIView/UIWidget 포맷팅 위치 모호 (med) | 포맷팅 **위젯 소유**, Presenter는 매핑만 |
| HudRoot.Bind 자기설치 ↔ 'View는 Presenter 모름' (med) | Presenter 생성을 **UIRoot로 이전** |
| RP가 private setter 캡슐화 침식 (low) | RP **쓰기 private**, 읽기전용 노출 |
| gold=-1 강제통지 vs RP equality (med) | **ForceNotify** API |
| Bind 다중구독 토큰 추적 (med) | Bind가 **IDisposable 토큰** 반환·집계 |
| 씬 프리팹 SerializeField 무효화 (med) | **Prefab Variant**·점진 전환 (§6) |

## 6. 마이그레이션 순서 (점진·안전)

1. **베이스 신설** — `UIObject`·`IUIShowable`·`UIWidget`·`UIWidget<T>`·`UIView`·`UIPresenter<T>`·`ReactiveProperty<T>`. 컴파일만, 기존 무영향.
2. **Model RP 전환** — 단일 스칼라부터(`Economy.Gold`·`Score.Score`), private setter 보존. struct 래퍼(`WaveProgress`/`TimerState`/`HealthState`/`EnemyState`) 추가.
3. **HUD 위젯 분리** — 위젯 클래스 신설 + **Prefab Variant**(`ArenaHUD_Panel_V2`) 생성(원본 보존), 씬에서 V2로 전환.
4. **Presenter 이전** — `ArenaHudPresenter`/`HUDPresenter` → `UIPresenter<T>` + `Bind`. UI 중간 Model 제거. `HudRoot.Bind` → `UIRoot`로 Presenter 생성 이전.
5. **테스트 갱신·검증** — EditMode `new` 격리 패턴 유지 확인(레지스트리 없으므로 영향 최소), 컴파일 0 에러, Play 검증.

## 7. 영향 파일

| 구분 | 파일 |
|---|---|
| 신규 베이스 | `UI/Base/UIObject.cs`·`IUIShowable.cs`·`UIWidget.cs`·`UIView.cs`·`UIPresenter.cs` |
| 신규 Model 유틸 | `Domain/Models/ReactiveProperty.cs`·DTO struct들 |
| 수정 Model | `EconomyModel`·`ScoreModel`·`CoreModel`·`WaveModel`·`RoundTimerModel` (RP 전환) |
| 수정 UI | `ArenaHudView`·`HUDView`·위젯 분리·`ArenaHudPresenter`·`HUDPresenter`·`UIRoot`·`HudRoot` |
| 삭제 | `UI.BaseModel`·`ArenaHudModel`·`HUDModel` |
| 프리팹 | `ArenaHUD_Panel` → Variant |

## 8. 비범위 (YAGNI / 검증 반려)

- **Service Locator/Model 레지스트리** — 검증 반려, constructor-DI 유지.
- **RP 전면 래핑** — 단일 스칼라 + struct 래퍼만.
- **트윈/비동기(UniTask) 바인딩** — 즉시 반영만.
- **Card/Result/TowerBuild Presenter 이전** — 이번은 HUD 범위만. 이후 점진.
- **UniRx 도입** — 자체 경량 RP로 충분.
