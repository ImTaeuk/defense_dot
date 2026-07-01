# UIRoot 자동 배선 재설계 — GameContext 주입 + 팩토리 리플렉션

**작성일**: 2026-06-30
**상태**: 설계 확정 (대화형 협의·적대 검토 반영)
**범위**: UIRoot 의 View 배선을 `List<UIView>` + 자동 팩토리로 전환, UIRoot 참조 View 전부 `UIView` 이전
**선행**: UI 베이스 계층(`UIObject`/`UIView`/`UIWidget`/`UIPresenter`)·`ReactiveProperty` 이미 구현·커밋(`49472631`)

---

## 1. 목표

UIRoot 가 View 마다 개별 `SerializeField` 를 들고 `Inject` 에서 타입별로 `new XxxPresenter(...)` 하는 구조를, **`List<UIView>` 하나 + 자동 팩토리**로 바꾼다. 새 UI 를 추가해도 UIRoot·팩토리·GameManager 코드는 수정하지 않는다. 이미 구축된 UI 베이스 계층(공통 메소드 분리)은 그대로 유지·활용한다.

## 2. 현재 문제

- **UIRoot 가 모든 구체 타입을 안다** — View 별 `SerializeField`(`arenaHud`/`buildModalView`/`towerRoster`/`gameResultView`/`cardSelectionView`) + `Inject` 의 타입별 분기. 새 UI = 필드 + 분기 추가로 계속 성장.
- **미사용 필드** — Arena 에서 안 쓰는 `buildModalView`/`towerRoster` 가 빈 슬롯으로 남음.
- **합성 루트의 UI 책임 누수** — GameManager 가 Presenter 생성·배선 세부에 개입.

## 3. 설계 결정 (확정)

### 3.1 자동 배선 — View 순수 + Presenter 가 View 를 안다

- **View 는 Presenter 를 모른다(순수).** `UIView<TPresenter>` 같은 역의존을 두지 않는다. *(협의: 제네릭으로 View 가 Presenter 를 선언하는 초안은 의존 방향이 거꾸로라 폐기)*
- **Presenter 가 View 를 안다** — 기존 `UIPresenter<TView>` 구조 그대로. MVP 정석.
- **팩토리가 Presenter 를 리플렉션 스캔**해 `Dictionary<ViewType, PresenterType>` 매핑을 1회 구축. View 를 받으면 그 매핑으로 Presenter 타입을 찾아 생성. `switch` 없음 → View 가 늘어도 팩토리 무성장.

### 3.2 모델 접근 — GameContext 주입 (DI 유지, 전역 아님)

- **GameContext**: 모든 도메인 모델을 홀드하는 묶음 객체. `static Instance` 를 두지 않고 **주입**한다. *(적대 검토: 전역 싱글톤은 전역 가변상태·숨은 의존성. 단 DI 의 실질 이점은 "의존성이 컴파일 타임에 명시·강제됨" 하나로 수렴 — 그 하나를 위해 주입 채택. "추적 불가" 논거는 과장이라 철회)*
- **Presenter 생성자 `(TView view, GameContext ctx)`** 로 통일. 생성자에서 `ctx` 의 **필요한 모델만 추출해 구체 필드로 저장**하고 `ctx` 는 보관하지 않는다(Parameter Object, 서비스 로케이터 아님). → 의존성이 생성자에 드러남.
- 모델 단위테스트는 여전히 `new` 로 격리(영향 없음). Presenter 단위테스트는 fake `GameContext` 주입으로 가능.

### 3.3 책임 분배 — UIRoot 가 UI 초기화 전담

- **GameManager(합성 루트)**: 모델 생성 → `GameContext` 구성 → `uiRoot.Inject(ctx)` **한 번 위임**. UI 초기화 세부(팩토리·배선)는 모른다. *(협의: GameManager 가 팩토리를 만들어 넘기면 합성 루트가 UI 세부를 아는 책임 누수 → UIRoot 가 전담)*
- **UIRoot**: `Inject(GameContext ctx)` 안에서 `UIPresenterFactory` 를 생성하고 `List<UIView>` 를 순회하며 배선·`Initialize`. `OnDestroy` 에서 일괄 `Dispose`.

### 3.4 베이스 계층 — 그대로 유지 (공통 메소드 분리)

이번 재설계는 아래 베이스의 공통을 **손대지 않고 활용**한다(가독성 핵심):

| 베이스 | 분리된 공통 |
|---|---|
| `UIObject` | RectTransform 캐싱 · Depth |
| `UIView` | Show/Hide/OnShow/OnShown 생명주기 |
| `UIWidget<T>` | SetData 표시 계약 |
| `UIPresenter<TView>` | Initialize/Dispose/재진입 가드 + **Bind**(구독+초기값+토큰 자동해제+OnShow 재반영) |

→ 각 Presenter 는 `OnInitialize` 에서 `Bind(...)` 한 줄씩만. 각 View 는 위젯 조립·`Apply*` 위임만. 중복 0.

### 3.5 범위 — UIRoot 참조 View 전부 UIView 이전

- `ArenaHudView`(완료) + `CardSelectionView`·`GameResultView`·`TowerBuildModalView` 를 `UIView` 로 이전, 한 `List<UIView>` 로 통일.
- **미사용 필드 소멸** — 씬마다 그 씬에 있는 View 만 리스트에 담으므로 빈 슬롯 개념이 사라진다.

### 3.6 위젯 분리 후보 (UIWidget 랩핑)

View 이전 시 반복·복합 UI 요소를 `UIWidget`/`UIWidget<T>` 로 랩핑해 표시 로직을 캡슐화한다(베이스가 흡수한 공통 위에 얹힘).

| View | 위젯 후보 | 랩핑 강도 | 내용 |
|---|---|:---:|---|
| `CardSelectionView` | `CardSlotWidget : UIWidget<CardDisplay>` | **강** | 현재 `CardItem` struct(button·background·border·icon·name·kind·desc·glow) + `Bind` 가 사실상 위젯 그 자체. 위젯 1개로 캡슐화하고 3개 인스턴스를 조립. View 는 위젯 배열 + 선택 이벤트 중계만 |
| `TowerBuildModalView` | `TowerButtonWidget : UIWidget<TowerData>` | 중 | 동적 생성 버튼(라벨·비용·interactable)을 위젯 프리팹으로. View 는 로스터→위젯 인스턴스화·클릭 중계만 |
| `GameResultView` | (분리 실익 낮음) | 약 | 메시지+버튼 단순. 공통 ButtonWidget 도입 시에만 RestartButton 분리 |

- **카드 데이터 갱신**: 카드는 RP 가 아니라 레벨업 시 동적 3장이므로 `SetData(CardDisplay)` 로 갱신한다(Bind 아님). `CardSelectionPresenter` 가 `choices` → 각 위젯 `SetData` + 선택 인덱스 중계.
- **공통화 주의**: 골드 표시는 ArenaHud `GoldWidget` 과 Grid `GoldView` 가 중복 — Grid 이전(후속) 시 `GoldWidget` 으로 통합 가능. 버튼류(카드·타워·재시작)는 표시가 제각각이라 단일 `ButtonWidget` 강제는 과함(각 위젯 유지가 YAGNI 부합).

## 4. 핵심 코드 골자

```csharp
// GameContext — 모든 모델 홀드 (주입, 전역 아님)
public sealed class GameContext
{
    public EconomyModel Economy { get; }
    public WaveModel Wave { get; }
    public ScoreModel Score { get; }
    public RoundTimerModel Timer { get; }
    public CoreModel Core { get; }
    public GameFlowModel Flow { get; }
    public LevelModel Level { get; }
    public int EnemyCapacity { get; }
    // … placement / roster / cardConfig 등 UI 합성 재료
    public GameContext(EconomyModel economy, WaveModel wave, /* … */) { /* 대입 */ }
}

// Presenter — (TView, GameContext) 생성자에서 필요한 것만 추출
public sealed class ArenaHudPresenter : UIPresenter<ArenaHudView>
{
    private readonly EconomyModel economy;
    private readonly ScoreModel score;
    private readonly WaveModel wave;
    private readonly RoundTimerModel timer;
    private readonly int enemyCapacity;

    public ArenaHudPresenter(ArenaHudView view, GameContext ctx) : base(view)
    {
        economy = ctx.Economy;
        score = ctx.Score;
        wave = ctx.Wave;
        timer = ctx.Timer;
        enemyCapacity = ctx.EnemyCapacity;
    }

    protected override void OnInitialize()
    {
        Bind(economy.Gold, view.ApplyGold);
        Bind(score.Score, view.ApplyScore);
        Bind(wave.Progress, view.ApplyRound);
        Bind(timer.Time, view.ApplyTime);
        Bind(wave.RemainingEnemies, HandleRemaining);
    }

    private void HandleRemaining(int alive)
        => view.ApplyEnemies(new DefenseDot.Domain.Models.EnemyState(alive, enemyCapacity));
}

// 팩토리 — Presenter 리플렉션 스캔, switch 없음
public sealed class UIPresenterFactory
{
    private readonly GameContext ctx;
    private readonly System.Collections.Generic.Dictionary<System.Type, System.Type> map;

    public UIPresenterFactory(GameContext ctx)
    {
        this.ctx = ctx;
        map = BuildViewToPresenterMap();   // UIPresenter<TView> 구현 스캔 → {ViewType: PresenterType}
    }

    public IPresenter Create(UIView view)
    {
        if (view == null) return null;
        if (!map.TryGetValue(view.GetType(), out System.Type presenterType)) return null;
        return (IPresenter)System.Activator.CreateInstance(presenterType, view, ctx);
    }
}

// UIRoot — ctx 받아 factory 생성·배선 전담
public sealed class UIRoot : MonoBehaviour
{
    [SerializeField] private System.Collections.Generic.List<UIView> views
        = new System.Collections.Generic.List<UIView>();
    private readonly System.Collections.Generic.List<IPresenter> presenters
        = new System.Collections.Generic.List<IPresenter>();

    public void Inject(GameContext ctx)
    {
        var factory = new UIPresenterFactory(ctx);
        foreach (UIView view in views)
        {
            IPresenter presenter = factory.Create(view);
            if (presenter == null) continue;
            presenters.Add(presenter);
            presenter.Initialize();
        }
    }

    private void OnDestroy()
    {
        foreach (IPresenter presenter in presenters) presenter.Dispose();
        presenters.Clear();
    }
}

// GameManager — 모델·ctx 구성 후 한 번 위임
// var ctx = new GameContext(Economy, Wave, Score, RoundTimer, Core, Flow, Level, capacity, ...);
// uiRoot.Inject(ctx);
```

## 5. 협의·검토 반영 요약

| 논점 | 결론 |
|---|---|
| 생성 책임 위치(View 자가/Binder/팩토리) | **중앙 팩토리(C)** |
| 통합 범위 | **전부 UIView 이전(가)** — 미사용 필드 자연 소멸 |
| 팩토리 = MonoBehaviour? | **POCO** (순수 C#, 테스트 가능) |
| `switch` vs 자동화 | **자동화(리플렉션 스캔)** — View 증가에 무성장 |
| View↔Presenter 의존 방향 | **Presenter→View** (View 순수), 초안의 `UIView<TPresenter>` 폐기 |
| 모델 접근(DI vs 전역) | **GameContext 주입(DI)** — "의존성 컴파일 타임 명시" 하나가 매력. "추적 불가" 논거는 과장이라 철회 |
| 팩토리·배선 소유 | **UIRoot 전담**, GameManager 는 ctx 위임만(책임 누수 제거) |
| 베이스 계층 | **유지** — 공통(Bind·생명주기)은 베이스가 흡수, 중복 0 |

## 6. 영향 파일

| 구분 | 파일 |
|---|---|
| 신규 | `UI/UIContext`→`Domain/GameContext.cs`(모델 홀드), `UI/UIPresenterFactory.cs`(POCO·리플렉션 스캔) |
| 신규 — 위젯 | `UI/Widgets/CardSlotWidget.cs`(강), `UI/Widgets/TowerButtonWidget.cs`(중) |
| 수정 — UIView 이전 | `Views/CardSelectionView.cs`·`Views/GameResultView.cs`·`Views/TowerBuildModalView.cs` (→ `UIView` 상속, `ICardSelectionView` 등 인터페이스 정리) |
| 수정 — Presenter | `ArenaHudPresenter`·`CardSelectionPresenter`·`GameResultPresenter`·`TowerBuildPresenter` → `UIPresenter<TView>` + `(TView, GameContext)` 생성자 |
| 수정 — 배선 | `UI/InGame/UIRoot.cs`(List<UIView>+factory 전담), `Systems/Management/GameManager.cs`(GameContext 구성·`Inject(ctx)`) |
| 수정 — Base | `UIView.cs` (Presenter 타입을 팩토리가 스캔하므로 View 측 변경 최소; `UIView<TPresenter>` **도입하지 않음**) |
| 폐기 | 기존 UIRoot 개별 `SerializeField` 5종 + 타입별 분기, `HudRoot`(잔여 시) |

## 7. 마이그레이션 순서

1. `GameContext` 신설 — 모델 홀드 묶음. GameManager 가 구성.
2. `UIPresenterFactory`(POCO) 신설 — `UIPresenter<TView>` 리플렉션 스캔 매핑 + `Create`.
3. Presenter 생성자 통일 — 각 Presenter `(TView, GameContext)` 로, 생성자에서 모델 추출.
4. UIView 이전 — Card/Result/TowerBuild 를 `UIView` 상속으로(이미 ArenaHud 완료).
5. UIRoot 전환 — `List<UIView>` + `Inject(GameContext)` 내부 factory·배선. GameManager 는 `Inject(ctx)` 위임.
6. 씬 재배선 — UIRoot `views` 리스트에 각 View 등록. 개별 슬롯 제거.
7. 테스트·검증 — 팩토리 매핑 단위테스트(EditMode), 컴파일 0, Play 통합(전 패널 정상 표시·생명주기).

## 8. 검증 시나리오

| # | 시나리오 | 기대 |
|---|---|---|
| 1 | 팩토리 매핑 스캔 | `UIPresenter<TView>` 구현 전부 `{ViewType:PresenterType}` 로 매핑(EditMode) |
| 2 | 팩토리 생성 | `Create(view)` 가 올바른 Presenter 인스턴스 반환, 미등록 View 는 null |
| 3 | Play — Arena | HUD 5위젯 정상 표시(기존 동작 유지) |
| 4 | Play — 카드/결과 | 레벨업 모달·결과 패널 정상(이전된 View) |
| 5 | 새 View 추가 | View+Presenter 1쌍 + 리스트 등록만으로 동작, 배선 코드 무수정 |
| 6 | 종료 | OnDestroy 에서 전 Presenter Dispose(구독 해제) |

## 9. 비범위

- 모델 자체의 전역 싱글톤화(B) — 채택 안 함(주입 유지).
- DI 컨테이너(Zenject 등) 도입 — 과함.
- Grid HUD(`HUDView` 계열) — 별도 후속(Arena 가 활성).
- 리플렉션 성능 최적화(Source Generator 등) — 생성 1회라 불필요, YAGNI.
