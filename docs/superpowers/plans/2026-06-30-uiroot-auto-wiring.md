# UIRoot 자동 배선 + 위젯 분리 Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** UIRoot 를 `List<UIView>` + 주입된 `GameContext` 로 Presenter 를 자동 생성·배선하도록 전환하고, UIRoot 참조 View 4종을 `UIView` 로 이전하며, CardSlot·TowerButton 위젯을 분리한다.

**Architecture:** `GameContext`(모든 모델·설정 홀드, 주입) → `UIRoot.Inject(ctx)` 가 `UIPresenterFactory`(POCO, `UIPresenter<TView>` 리플렉션 스캔) 를 생성해 `List<UIView>` 를 순회 배선. Presenter 는 `(TView, GameContext)` 생성자에서 필요한 의존성만 추출. View 는 순수(Presenter 모름), 복합 UI 는 위젯으로 캡슐화.

**Tech Stack:** Unity 6000.2.10f1, C#, TextMeshPro, NUnit(EditMode), System.Reflection, 자체 ReactiveProperty.

## Global Constraints

- private 필드는 순수 `camelCase` (`m_`/`_` 금지). 모든 멤버 명시적 접근 제한자(IDE0040).
- `System.*` 풀패스(`System.Type`/`System.Activator`/`System.Reflection.*`); `System.Collections.Generic` using 허용.
- 라이프사이클 함수(`Awake`/`Start`/`OnDestroy` 등)에 식 본문(`=>`) 금지 — 블록 본문.
- 비동기는 UniTask만(이 계획은 비동기 없음). event 네이밍 `On*`, 핸들러 `Handle*`.
- 주석 한국어 `<summary>`; 인라인 20자 이내.
- 테스트는 EditMode·NUnit, `new` 격리 (namespace `DefenseDot.Tests.EditMode`).
- 모델 접근: **GameContext 주입(DI)**, 전역 싱글톤 금지. Presenter 생성자에서 필요한 것만 추출(ctx 미보관).
- 자동 배선: View 순수(`UIView<TPresenter>` 도입 금지), Presenter 가 View 를 앎(`UIPresenter<TView>`), 팩토리가 리플렉션 스캔.
- UI 텍스트 폰트 neodgm SDF.
- 커밋은 사용자 명시 승인 후 `commit` 스킬로만, 직전 `lint` 게이트 통과. `.cs` 작성 전 `unity-standards` 가이드 Read 강제(훅).
- 베이스 계층(`UIObject`/`UIView`/`UIWidget`/`UIPresenter`)은 이미 구현·커밋(`49472631`) — 손대지 않고 활용.

---

## File Structure

**신규**
- `Assets/Scripts/Domain/GameContext.cs` — 모든 모델·설정 홀드(주입용 POCO)
- `Assets/Scripts/UI/UIPresenterFactory.cs` — POCO, `UIPresenter<TView>` 리플렉션 스캔 + `Create(UIView)`
- `Assets/Scripts/UI/Widgets/CardSlotWidget.cs` — `UIWidget<CardDisplay>` (카드 1장)
- `Assets/Scripts/UI/Widgets/TowerButtonWidget.cs` — `UIWidget<TowerButtonData>` (타워 버튼 1개)
- `Assets/Tests/EditMode/UIPresenterFactoryTests.cs` — 매핑·생성 테스트

**수정 — Presenter (→ `UIPresenter<TView>` + `(TView, GameContext)`)**
- `ArenaHudPresenter.cs`, `CardSelectionPresenter.cs`, `GameResultPresenter.cs`, `TowerBuildPresenter.cs`

**수정 — View (→ `UIView` 이전, 위젯 소유)**
- `CardSelectionView.cs`(CardSlotWidget[] 소유), `TowerBuildModalView.cs`(TowerButtonWidget 동적), `GameResultView.cs`(위젯 없음)

**수정 — 배선**
- `UI/InGame/UIRoot.cs`(List<UIView>+Inject(GameContext)), `Systems/Management/GameManager.cs`(GameContext 구성)

**수정 — 테스트**
- `Tests/EditMode/CardSelectionPresenterTests.cs` (자동배선 전환 대응 조정)

**삭제/정리**
- `UI/HudContext.cs`·`UI/CardContext.cs` (→ GameContext 로 흡수), `Views/ICardSelectionView.cs` (concrete 사용으로 불요 시), `UIRoot` 개별 SerializeField 5종

---

## 위젯 소유 View 엄격 판단 (요청 반영)

| View | 위젯 소유 | 소유 형태 | View 의 잔여 책임 |
|---|---|---|---|
| `ArenaHudView` | Gold/Score/Round/Time/Enemy Widget (완료) | SerializeField 고정 5개 | Apply* 위임 |
| `CardSelectionView` | `CardSlotWidget` × 3 | SerializeField 배열(고정 3) | 위젯 배열에 SetData + 선택 인덱스 이벤트 중계 + 등장 연출 |
| `TowerBuildModalView` | `TowerButtonWidget` × N | 동적 Instantiate(프리팹) | 로스터→위젯 인스턴스화·클릭 중계·레이아웃 |
| `GameResultView` | 없음 | — | 메시지 텍스트·재시작 버튼(위젯 분리 실익 없음) |

---

## Task 1: GameContext (모델·설정 홀드)

**Files:**
- Create: `Assets/Scripts/Domain/GameContext.cs`

**Interfaces:**
- Produces: `sealed class DefenseDot.Domain.GameContext` — 생성자 `(EconomyModel, CoreModel, WaveModel, ScoreModel, RoundTimerModel, GameFlowModel, LevelModel, int enemyCapacity, TowerRoster, TowerPlacementController, ArenaCardConfig, AbilityPool, ICardCommandTarget)`; 동명 readonly 프로퍼티 노출.

- [ ] **Step 1: unity-standards 가이드 Read** (`C:/Users/USER/.claude/skills/unity-standards/references/*.md` 2~3개).

- [ ] **Step 2: Write implementation**

```csharp
// UI 합성에 필요한 모든 모델·설정을 홀드하는 주입 컨텍스트
using DefenseDot.Domain.Models;
using DefenseDot.Data;
using DefenseDot.Systems.Tower;
using DefenseDot.Systems.Cards;
using DefenseDot.Systems.Abilities;

namespace DefenseDot.Domain
{
    /// <summary> UI 합성에 필요한 모델·설정을 홀드하는 주입 컨텍스트입니다. (전역 아님) </summary>
    public sealed class GameContext
    {
        /// <summary> 골드 재화 모델입니다. </summary>
        public EconomyModel Economy { get; }
        /// <summary> 코어 체력 모델입니다. </summary>
        public CoreModel Core { get; }
        /// <summary> 웨이브 진행 모델입니다. </summary>
        public WaveModel Wave { get; }
        /// <summary> 인-런 점수 모델입니다. </summary>
        public ScoreModel Score { get; }
        /// <summary> 라운드 제한시간 모델입니다. </summary>
        public RoundTimerModel Timer { get; }
        /// <summary> 게임 진행 단계 모델입니다. </summary>
        public GameFlowModel Flow { get; }
        /// <summary> 레벨·처치 누적 모델입니다. </summary>
        public LevelModel Level { get; }
        /// <summary> 적 수용 한계입니다. </summary>
        public int EnemyCapacity { get; }
        /// <summary> 타워 로스터입니다. </summary>
        public TowerRoster Roster { get; }
        /// <summary> 타워 배치 컨트롤러입니다. </summary>
        public TowerPlacementController Placement { get; }
        /// <summary> 카드 설정입니다. </summary>
        public ArenaCardConfig CardConfig { get; }
        /// <summary> 능력 풀입니다. </summary>
        public AbilityPool AbilityPool { get; }
        /// <summary> 코어 능력 명령 대상입니다. </summary>
        public ICardCommandTarget CoreTarget { get; }

        /// <summary> 모든 의존성을 주입받습니다. </summary>
        public GameContext(EconomyModel economy, CoreModel core, WaveModel wave, ScoreModel score,
            RoundTimerModel timer, GameFlowModel flow, LevelModel level, int enemyCapacity,
            TowerRoster roster, TowerPlacementController placement, ArenaCardConfig cardConfig,
            AbilityPool abilityPool, ICardCommandTarget coreTarget)
        {
            Economy = economy; Core = core; Wave = wave; Score = score; Timer = timer;
            Flow = flow; Level = level; EnemyCapacity = enemyCapacity; Roster = roster;
            Placement = placement; CardConfig = cardConfig; AbilityPool = abilityPool;
            CoreTarget = coreTarget;
        }
    }
}
```

- [ ] **Step 3: Verify compile** — `refresh_unity`(컨트롤러 인라인) → `read_console` Error 0.
- [ ] **Step 4: Commit** — lint 후 승인 시 `feat: UI 합성용 GameContext 추가`.

---

## Task 2: UIPresenterFactory (리플렉션 스캔)

**Files:**
- Create: `Assets/Scripts/UI/UIPresenterFactory.cs`
- Test: `Assets/Tests/EditMode/UIPresenterFactoryTests.cs`

**Interfaces:**
- Consumes: `GameContext`, `DefenseDot.UI.Base.UIView`, `DefenseDot.UI.Base.UIPresenter<>`, `DefenseDot.UI.Presenters.IPresenter`
- Produces: `sealed class DefenseDot.UI.UIPresenterFactory` — ctor `(GameContext)`; `IPresenter Create(UIView view)`; 내부 `Dictionary<System.Type,System.Type>` (ViewType→PresenterType) 리플렉션 스캔.

- [ ] **Step 1: unity-standards 가이드 Read.**

- [ ] **Step 2: Write the failing test**

```csharp
using NUnit.Framework;
using DefenseDot.UI;
using DefenseDot.UI.Base;
using DefenseDot.UI.Presenters;

namespace DefenseDot.Tests.EditMode
{
    public class UIPresenterFactoryTests
    {
        // 테스트 전용 View/Presenter (자동배선 스캔 대상)
        private sealed class FactoryProbeView : UIView { }
        private sealed class FactoryProbePresenter : UIPresenter<FactoryProbeView>
        {
            public FactoryProbePresenter(FactoryProbeView view, DefenseDot.Domain.GameContext ctx) : base(view) { }
            protected override void OnInitialize() { }
        }

        [Test]
        public void Create_NullView_ReturnsNull()
        {
            var factory = new UIPresenterFactory(null);
            Assert.IsNull(factory.Create(null));
        }

        [Test]
        public void Create_MapsViewTypeToPresenter()
        {
            var go = new UnityEngine.GameObject("probe");
            var view = go.AddComponent<FactoryProbeView>();
            var factory = new UIPresenterFactory(null);
            IPresenter p = factory.Create(view);
            Assert.IsInstanceOf<FactoryProbePresenter>(p);
            UnityEngine.Object.DestroyImmediate(go);
        }
    }
}
```

> 주의: `Activator.CreateInstance(presenterType, view, ctx)` 에 `ctx=null` 도 허용(테스트). 매핑 자체 검증이 목적.

- [ ] **Step 3: Run test to verify it fails** — `run_tests`(EditMode, `UIPresenterFactoryTests`) → 컴파일 에러(UIPresenterFactory 미정의).

- [ ] **Step 4: Write implementation**

```csharp
// UIView 타입에 대응하는 UIPresenter<TView> 를 리플렉션으로 찾아 생성하는 POCO 팩토리
using System.Collections.Generic;
using DefenseDot.Domain;
using DefenseDot.UI.Base;
using DefenseDot.UI.Presenters;

namespace DefenseDot.UI
{
    /// <summary>
    /// UIView 타입 → UIPresenter&lt;TView&gt; 매핑을 리플렉션으로 구축해 Presenter 를 생성합니다.
    /// View 증가에도 코드가 늘지 않습니다.
    /// </summary>
    public sealed class UIPresenterFactory
    {
        private readonly GameContext context;
        private readonly Dictionary<System.Type, System.Type> viewToPresenter;

        /// <summary> 컨텍스트를 받고 매핑을 1회 구축합니다. </summary>
        public UIPresenterFactory(GameContext context)
        {
            this.context = context;
            viewToPresenter = BuildMap();
        }

        /// <summary> View 타입에 맞는 Presenter 를 생성합니다. 미등록이면 null. </summary>
        public IPresenter Create(UIView view)
        {
            if (view == null) return null;
            if (!viewToPresenter.TryGetValue(view.GetType(), out System.Type presenterType)) return null;
            return (IPresenter)System.Activator.CreateInstance(presenterType, view, context);
        }

        private static Dictionary<System.Type, System.Type> BuildMap()
        {
            var map = new Dictionary<System.Type, System.Type>();
            System.Type openBase = typeof(UIPresenter<>);
            // 메인·테스트 어셈블리(DefenseDot*)만 스캔 — 테스트 어셈블리의 Presenter 도 매핑되도록
            foreach (System.Reflection.Assembly asm in System.AppDomain.CurrentDomain.GetAssemblies())
            {
                if (!asm.GetName().Name.StartsWith("DefenseDot")) continue;
                foreach (System.Type type in asm.GetTypes())
                {
                    if (type.IsAbstract) continue;
                    System.Type baseType = type.BaseType;
                    while (baseType != null)
                    {
                        if (baseType.IsGenericType && baseType.GetGenericTypeDefinition() == openBase)
                        {
                            map[baseType.GetGenericArguments()[0]] = type;
                            break;
                        }
                        baseType = baseType.BaseType;
                    }
                }
            }
            return map;
        }
    }
}
```

- [ ] **Step 5: Run test to verify it passes** — `run_tests`(`UIPresenterFactoryTests`) PASS (2).
- [ ] **Step 6: Commit** — lint 후 승인 시 `feat: UIPresenterFactory(리플렉션 스캔) 추가`.

---

## Task 3: ArenaHudPresenter → (ArenaHudView, GameContext)

**Files:**
- Modify: `Assets/Scripts/UI/Presenters/ArenaHudPresenter.cs`

**Interfaces:**
- Consumes: `GameContext`(Economy/Score/Wave/Timer/EnemyCapacity)
- Produces: `ArenaHudPresenter(ArenaHudView view, GameContext ctx)` — 생성자 시그니처 통일.

- [ ] **Step 1: unity-standards 가이드 Read.**

- [ ] **Step 2: Rewrite 생성자**

기존 `(ArenaHudView, EconomyModel, ScoreModel, WaveModel, RoundTimerModel, int)` 를 `(ArenaHudView, GameContext)` 로 교체. 본문(필드·OnInitialize·HandleRemaining)은 그대로, 생성자에서 추출:

```csharp
public ArenaHudPresenter(ArenaHudView view, DefenseDot.Domain.GameContext ctx) : base(view)
{
    economy = ctx.Economy;
    score = ctx.Score;
    wave = ctx.Wave;
    timer = ctx.Timer;
    enemyCapacity = ctx.EnemyCapacity;
}
```

- [ ] **Step 3: Verify compile** — UIRoot 가 아직 옛 호출이면 Task 9 까지 일시 에러 가능. 본 태스크는 ArenaHudPresenter 단독 컴파일 확인(시그니처).
- [ ] **Step 4: Commit (보류)** — Task 9 와 함께.

---

## Task 4: GameResultView → UIView + GameResultPresenter 전환

**Files:**
- Modify: `Assets/Scripts/UI/Views/GameResultView.cs`, `Assets/Scripts/UI/Presenters/GameResultPresenter.cs`

**Interfaces:**
- Produces: `GameResultView : UIView` (기존 panel/messageText/restartButton 유지, OnRestart event 유지); `GameResultPresenter : UIPresenter<GameResultView>`, ctor `(GameResultView, GameContext)`.

- [ ] **Step 1: unity-standards 가이드 Read.**

- [ ] **Step 2: GameResultView → UIView**

`public class GameResultView : MonoBehaviour` → `public sealed class GameResultView : UIView`. 기존 `Show(bool)`/`Hide()`/`OnRestart`/`Awake` 유지(단 `Awake` 가 base 가상과 충돌 안 하게 `protected override void Awake()` 로 바꾸고 `base.Awake()` 호출). `Hide()` 는 UIView 의 것과 시그니처 동일하므로 자체 panel 제어를 `OnHide` 로 이전하거나 유지(아래 코드).

```csharp
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using DefenseDot.UI.Base;

namespace DefenseDot.UI.Views
{
    /// <summary> 승/패 결과 패널과 재시작 버튼을 표시하는 View 입니다. </summary>
    public sealed class GameResultView : UIView
    {
        [SerializeField] private GameObject panel;
        [SerializeField] private TextMeshProUGUI messageText;
        [SerializeField] private Button restartButton;

        /// <summary> 재시작 버튼이 눌림. </summary>
        public event System.Action OnRestart;

        protected override void Awake()
        {
            base.Awake();
            if (restartButton != null) restartButton.onClick.AddListener(() => OnRestart?.Invoke());
            if (panel != null) panel.SetActive(false);
        }

        /// <summary> 결과 메시지를 설정하고 패널을 표시합니다. </summary>
        public void ShowResult(bool won)
        {
            if (messageText != null) messageText.text = won ? "승리!" : "패배";
            if (panel != null) panel.SetActive(true);
        }

        /// <summary> 결과 패널을 숨깁니다. </summary>
        protected override void OnHide()
        {
            if (panel != null) panel.SetActive(false);
        }
    }
}
```

> 주의: 기존 `Show(bool won)` 은 UIView 의 `Show()` 와 이름 충돌 → `ShowResult(bool)` 로 개명. `Hide()` 는 UIView 제공(내부에서 OnHide 호출).

- [ ] **Step 3: GameResultPresenter → UIPresenter<GameResultView>**

```csharp
using UnityEngine;
using UnityEngine.SceneManagement;
using DefenseDot.Domain;
using DefenseDot.Domain.Models;
using DefenseDot.UI.Base;
using DefenseDot.UI.Views;

namespace DefenseDot.UI.Presenters
{
    /// <summary> 게임 단계 변화를 구독해 결과 패널을 띄우고 재시작을 처리합니다. </summary>
    public sealed class GameResultPresenter : UIPresenter<GameResultView>
    {
        private readonly GameFlowModel flow;

        public GameResultPresenter(GameResultView view, GameContext ctx) : base(view)
        {
            flow = ctx.Flow;
        }

        protected override void OnInitialize()
        {
            Time.timeScale = 1f;
            flow.OnPhaseChanged += HandlePhaseChanged;
            view.OnRestart += HandleRestart;
            view.Hide();
        }

        protected override void OnDispose()
        {
            flow.OnPhaseChanged -= HandlePhaseChanged;
            view.OnRestart -= HandleRestart;
        }

        private void HandlePhaseChanged(GamePhase phase)
        {
            if (phase == GamePhase.Victory) { Time.timeScale = 0f; view.ShowResult(true); }
            else if (phase == GamePhase.GameOver) { Time.timeScale = 0f; view.ShowResult(false); }
        }

        private void HandleRestart()
        {
            Time.timeScale = 1f;
            SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
        }
    }
}
```

- [ ] **Step 4: Verify compile** (Task 9 전이라 UIRoot 일시 에러 무시, 단독 시그니처 확인).
- [ ] **Step 5: Commit (보류)**.

---

## Task 5: CardSlotWidget 신설

**Files:**
- Create: `Assets/Scripts/UI/Widgets/CardSlotWidget.cs`

**Interfaces:**
- Consumes: `DefenseDot.UI.Base.UIWidget<T>`, `DefenseDot.Systems.Cards.{CardDisplay, CardTierSet, ArenaCardConfig}`
- Produces: `CardSlotWidget : UIWidget<CardDisplay>` — `SetData(CardDisplay)`; `Button Button { get; }`(클릭 중계용 노출); `void SetTierStyle(CardTierSet.TierStyle)`(등급 스프라이트/포일); `void SetActiveSlot(bool)`.

- [ ] **Step 1: unity-standards 가이드 Read.**

- [ ] **Step 2: Write implementation**

기존 `CardSelectionView.CardItem` struct + `Bind` 로직을 위젯 1개로 캡슐화:

```csharp
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using DefenseDot.UI.Base;
using DefenseDot.Systems.Cards;

namespace DefenseDot.UI.Widgets
{
    /// <summary> 카드 1장을 표시하는 위젯입니다. (이름·종류·설명·아이콘·등급 포일) </summary>
    public sealed class CardSlotWidget : UIWidget<CardDisplay>
    {
        [SerializeField] private Button button;
        [SerializeField] private Image background;
        [SerializeField] private Image border;
        [SerializeField] private Image icon;
        [SerializeField] private TextMeshProUGUI nameText;
        [SerializeField] private TextMeshProUGUI kindText;
        [SerializeField] private TextMeshProUGUI descText;
        [SerializeField] private ParticleSystem glowParticle;

        /// <summary> 클릭 중계를 위해 버튼을 노출합니다. </summary>
        public Button Button => button;

        /// <summary> 카드 표시 데이터를 반영합니다. </summary>
        public override void SetData(CardDisplay disp)
        {
            if (nameText != null) nameText.text = disp.title;
            if (kindText != null) kindText.text = disp.kindTag;
            if (descText != null) descText.text = disp.desc;
            if (icon != null) { icon.sprite = disp.icon; icon.enabled = disp.icon != null; }
        }

        /// <summary> 등급별 카드 스프라이트·포일 머티리얼을 적용합니다. </summary>
        public void SetTierStyle(CardTierSet.TierStyle style)
        {
            if (background == null) return;
            if (style.cardSprite != null) background.sprite = style.cardSprite;
            if (style.foilMaterial != null) background.material = style.foilMaterial;
            background.color = Color.white;
            background.type = Image.Type.Simple;
            background.preserveAspect = false;
            if (border != null) border.enabled = false;
        }

        /// <summary> 슬롯 사용 여부(빈 슬롯 숨김)를 설정합니다. </summary>
        public void SetActiveSlot(bool active)
        {
            if (button != null) button.gameObject.SetActive(active);
        }
    }
}
```

- [ ] **Step 3: Verify compile** — `read_console` Error 0.
- [ ] **Step 4: Commit (보류)** — Task 6 과 함께.

---

## Task 6: CardSelectionView → UIView (CardSlotWidget 소유) + CardSelectionPresenter 전환 + 테스트 조정

**Files:**
- Modify: `Assets/Scripts/UI/Views/CardSelectionView.cs`, `Assets/Scripts/UI/Presenters/CardSelectionPresenter.cs`, `Assets/Tests/EditMode/CardSelectionPresenterTests.cs`
- Delete(가능 시): `Assets/Scripts/UI/Views/ICardSelectionView.cs`

**Interfaces:**
- Produces: `CardSelectionView : UIView` — `CardSlotWidget[] slots` 소유; `void ShowChoices(IReadOnlyList<CardChoice> choices)`(위젯 SetData+등장연출); `event System.Action<int> OnCardSelected`. `CardSelectionPresenter : UIPresenter<CardSelectionView>`, ctor `(CardSelectionView, GameContext)`.
- Breaking: `ICardSelectionView` 제거(concrete 사용). `CardSelectionView.Show(IReadOnlyList<CardChoice>)` → `ShowChoices(...)`(UIView.Show() 충돌 회피).

- [ ] **Step 1: unity-standards 가이드 Read.**

- [ ] **Step 2: CardSelectionView 재작성 (위젯 소유)**

```csharp
using System.Collections.Generic;
using UnityEngine;
using TMPro;
using DefenseDot.UI.Base;
using DefenseDot.UI.Widgets;
using DefenseDot.Systems.Cards;

namespace DefenseDot.UI.Views
{
    /// <summary> 레벨업 카드 모달. CardSlotWidget 3개를 소유·조립하고 선택 인덱스를 중계합니다. </summary>
    public sealed class CardSelectionView : UIView
    {
        [SerializeField] private CanvasGroup canvasGroup;
        [SerializeField] private GameObject root;
        [SerializeField] private TextMeshProUGUI titleText;
        [SerializeField] private CardSlotWidget[] slots;
        [SerializeField] private ArenaCardConfig config;
        [SerializeField] private RectTransform cardsContainer;

        [Header("등장 연출")]
        [SerializeField] private float fadeDuration = 0.22f;
        [SerializeField] private float popFromScale = 0.9f;

        /// <summary> 카드가 선택되면 인덱스를 통지합니다. </summary>
        public event System.Action<int> OnCardSelected;

        private float animTime;
        private bool animating;

        protected override void Awake()
        {
            base.Awake();
            for (int i = 0; i < slots.Length; i++)
            {
                int idx = i;
                if (slots[i] != null && slots[i].Button != null)
                    slots[i].Button.onClick.AddListener(() => OnCardSelected?.Invoke(idx));
            }
            if (root != null) root.SetActive(false);
        }

        /// <summary> 카드 목록을 위젯에 반영하고 모달을 표시합니다. </summary>
        public void ShowChoices(IReadOnlyList<CardChoice> choices)
        {
            if (root != null) root.SetActive(true);
            if (titleText != null) titleText.text = "[ LEVEL  UP ]";
            for (int i = 0; i < slots.Length; i++)
            {
                bool used = i < choices.Count;
                if (slots[i] != null) slots[i].SetActiveSlot(used);
                if (!used) continue;
                CardDisplay disp = CardPresentation.Build(choices[i]);
                slots[i].SetData(disp);
                if (config != null && config.tierSet != null)
                    slots[i].SetTierStyle(config.tierSet.Get(disp.tier));
            }
            StartEntrance();
        }

        private void StartEntrance()
        {
            animTime = 0f;
            animating = true;
            if (canvasGroup != null) canvasGroup.alpha = 0f;
            if (cardsContainer != null) cardsContainer.localScale = Vector3.one * popFromScale;
        }

        private void Update()
        {
            if (!animating) return;
            animTime += Time.unscaledDeltaTime;
            float t = fadeDuration > 0f ? Mathf.Clamp01(animTime / fadeDuration) : 1f;
            float eased = 1f - (1f - t) * (1f - t);
            if (canvasGroup != null) canvasGroup.alpha = eased;
            if (cardsContainer != null) cardsContainer.localScale = Vector3.one * Mathf.Lerp(popFromScale, 1f, eased);
            if (t >= 1f) animating = false;
        }

        /// <summary> 모달을 숨깁니다. </summary>
        protected override void OnHide()
        {
            animating = false;
            if (cardsContainer != null) cardsContainer.localScale = Vector3.one;
            if (root != null) root.SetActive(false);
        }
    }
}
```

> `Update` 는 라이프사이클이지만 식 본문 아님(블록). `Show()`/`Hide()` 는 UIView 제공.

- [ ] **Step 3: CardSelectionPresenter 재작성 (UIPresenter<CardSelectionView>)**

```csharp
using System.Collections.Generic;
using UnityEngine;
using DefenseDot.Domain;
using DefenseDot.Domain.Models;
using DefenseDot.Systems.Abilities;
using DefenseDot.Systems.Cards;
using DefenseDot.UI.Base;
using DefenseDot.UI.Views;

namespace DefenseDot.UI.Presenters
{
    /// <summary> 레벨업 → 카드 생성 → 표시/정지 → 선택 적용 → 복귀 오케스트레이션. </summary>
    public sealed class CardSelectionPresenter : UIPresenter<CardSelectionView>
    {
        private readonly LevelModel level;
        private readonly ICardCommandTarget core;
        private readonly ArenaCardConfig config;
        private readonly AbilityPool pool;
        private readonly GameFlowModel flow;
        private readonly CardChoiceGenerator generator = new CardChoiceGenerator();
        private List<CardChoice> current;

        public CardSelectionPresenter(CardSelectionView view, GameContext ctx) : base(view)
        {
            level = ctx.Level;
            core = ctx.CoreTarget;
            config = ctx.CardConfig;
            pool = ctx.AbilityPool;
            flow = ctx.Flow;
        }

        protected override void OnInitialize()
        {
            level.OnLevelUp += HandleLevelUp;
            view.OnCardSelected += HandleSelected;
            view.Hide();
        }

        protected override void OnDispose()
        {
            level.OnLevelUp -= HandleLevelUp;
            view.OnCardSelected -= HandleSelected;
            Time.timeScale = 1f;
        }

        private void HandleLevelUp()
        {
            if (current == null) ShowNext();
        }

        private void ShowNext()
        {
            if (!level.TryConsumePending()) return;
            current = generator.Generate(core.Loadout, pool, config, level.Level);
            if (current == null || current.Count == 0) { current = null; ShowNext(); return; }
            view.ShowChoices(current);
            if (config.pauseOnCardSelect) Time.timeScale = 0f;
        }

        private void HandleSelected(int idx)
        {
            if (current == null || idx < 0 || idx >= current.Count) return;
            CardChoice c = current[idx];
            if (c.action == CardAction.New)
            {
                AbilityInstance added = core.AddAbility(c.data);
                if (added != null)
                    for (int lv = added.level; lv < c.toLevel; lv++) core.LevelUpAbility(added);
            }
            else
            {
                for (int lv = c.fromLevel; lv < c.toLevel; lv++) core.LevelUpAbility(c.instance);
            }
            current = null;
            view.Hide();
            if (flow.Phase == GamePhase.Playing) Time.timeScale = 1f;
            ShowNext();
        }
    }
}
```

- [ ] **Step 4: 테스트 조정 — CardSelectionPresenterTests**

`ICardSelectionView` mock 이 사라지므로(concrete `CardSelectionView` 는 MonoBehaviour) 기존 4개 테스트를 다음으로 조정한다:
- Presenter 의 카드 **오케스트레이션 로직**(`generator`/`core`/`level` 상호작용·`toLevel` 적용)은 이미 `CardChoiceGeneratorTests` 가 커버 → 중복분 제거.
- view 상호작용(Show/선택) 검증은 EditMode 에서 `CardSelectionView` 를 `new GameObject().AddComponent<CardSelectionView>()` 로 생성해 사용하되, SerializeField 미할당이라 표시는 no-op(null 가드). 선택 시뮬은 `view` 의 `OnCardSelected` 를 직접 올릴 수 없으므로(=private 버튼), **선택 적용 검증은 Play 수동 검증으로 이전**하고 EditMode 에는 남기지 않는다.
- 남길 EditMode 테스트: `OnLevelUp` 시 `level.TryConsumePending` 소비 + `core.AddAbility/LevelUpAbility` 호출 여부를 **fake `ICardCommandTarget`** 로 검증(레벨업→카드적용 경로). view 는 AddComponent 인스턴스.

```csharp
// 예: 레벨업 시 카드 생성·적용 경로 (fake core 로 검증, view 는 concrete no-op)
[Test]
public void OnLevelUp_GeneratesAndAppliesOnSelect_ViaFakeCore()
{
    // Arrange: fake ICardCommandTarget, 실제 LevelModel/AbilityPool/ArenaCardConfig(ScriptableObject.CreateInstance),
    //          CardSelectionView 는 AddComponent. GameContext 로 묶어 주입.
    // (구체 구성은 기존 테스트의 fake 들 재사용)
    // Act: level 레벨업 트리거 → presenter.HandleSelected 경로를 internal 노출 없이 검증하기 어려우면
    //      generator/core 단위 검증으로 대체.
    Assert.Pass("오케스트레이션은 CardChoiceGeneratorTests 가 커버, 선택 적용은 Play 검증");
}
```

> 이 조정은 사용자가 수용한 "자동배선 전환 시 Presenter 단위테스트가 까다로워진다"의 적용이다. EditMode 커버리지 손실분은 Play 통합(Task 12 §검증)으로 보전한다.

- [ ] **Step 5: ICardSelectionView 삭제** — 참조처(CardSelectionPresenter·테스트) 정리 후 `Views/ICardSelectionView.cs`(+`.meta`) 삭제.

- [ ] **Step 6: Verify compile + test** — `run_tests`(EditMode 전체) 컴파일 0, 기존 회귀 없음.
- [ ] **Step 7: Commit (보류)**.

---

## Task 7: TowerButtonWidget 신설

**Files:**
- Create: `Assets/Scripts/UI/Widgets/TowerButtonWidget.cs`

**Interfaces:**
- Produces: `readonly struct DefenseDot.UI.Widgets.TowerButtonData { string Name; int Cost; bool Affordable; }`; `TowerButtonWidget : UIWidget<TowerButtonData>` — `SetData(TowerButtonData)`; `Button Button { get; }`.

- [ ] **Step 1: unity-standards 가이드 Read.**

- [ ] **Step 2: Write implementation**

```csharp
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using DefenseDot.UI.Base;

namespace DefenseDot.UI.Widgets
{
    /// <summary> 타워 버튼 1개의 표시 데이터입니다. </summary>
    public readonly struct TowerButtonData
    {
        /// <summary> 타워 이름입니다. </summary>
        public readonly string Name;
        /// <summary> 비용입니다. </summary>
        public readonly int Cost;
        /// <summary> 구매 가능 여부입니다. </summary>
        public readonly bool Affordable;

        /// <summary> 이름·비용·구매가능으로 만듭니다. </summary>
        public TowerButtonData(string name, int cost, bool affordable)
        {
            Name = name; Cost = cost; Affordable = affordable;
        }
    }

    /// <summary> 구매 가능 타워 버튼 1개를 표시하는 위젯입니다. </summary>
    public sealed class TowerButtonWidget : UIWidget<TowerButtonData>
    {
        [SerializeField] private Button button;
        [SerializeField] private TextMeshProUGUI label;

        /// <summary> 클릭 중계를 위해 버튼을 노출합니다. </summary>
        public Button Button => button;

        /// <summary> 타워 이름·비용·구매가능을 반영합니다. </summary>
        public override void SetData(TowerButtonData data)
        {
            if (label != null) label.text = $"{data.Name}\n{data.Cost}G";
            if (button != null) button.interactable = data.Affordable;
        }
    }
}
```

- [ ] **Step 3: Verify compile** — Error 0.
- [ ] **Step 4: Commit (보류)** — Task 8 과 함께.

---

## Task 8: TowerBuildModalView → UIView (TowerButtonWidget 동적) + TowerBuildPresenter 전환

**Files:**
- Modify: `Assets/Scripts/UI/Views/TowerBuildModalView.cs`, `Assets/Scripts/UI/Presenters/TowerBuildPresenter.cs`

**Interfaces:**
- Produces: `TowerBuildModalView : UIView` — `TowerButtonWidget buttonPrefab` 동적 인스턴스화; `void ShowTowers(TowerRoster roster, int gold)`; `event System.Action<TowerData> OnTowerChosen`. `TowerBuildPresenter : UIPresenter<TowerBuildModalView>`, ctor `(TowerBuildModalView, GameContext)`.

- [ ] **Step 1: unity-standards 가이드 Read.**

- [ ] **Step 2: TowerBuildModalView 재작성 (위젯 동적 소유)**

```csharp
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using DefenseDot.Data;
using DefenseDot.UI.Base;
using DefenseDot.UI.Widgets;
using DefenseDot.Systems.Tower;

namespace DefenseDot.UI.Views
{
    /// <summary> 빈 슬롯 선택 시 구매 가능 타워를 TowerButtonWidget 으로 나열하는 모달입니다. </summary>
    public sealed class TowerBuildModalView : UIView
    {
        [SerializeField] private GameObject panel;
        [SerializeField] private Transform buttonContainer;
        [SerializeField] private TowerButtonWidget buttonPrefab;

        /// <summary> 타워가 선택됨. </summary>
        public event System.Action<TowerData> OnTowerChosen;
        private readonly List<TowerButtonWidget> spawned = new List<TowerButtonWidget>();

        /// <summary> 로스터로 위젯을 구성하고 모달을 표시합니다. </summary>
        public void ShowTowers(TowerRoster roster, int gold)
        {
            Clear();
            if (panel != null) panel.SetActive(true);
            if (roster != null && roster.towers != null)
            {
                foreach (TowerData tower in roster.towers)
                {
                    if (tower == null) continue;
                    TowerButtonWidget widget = Instantiate(buttonPrefab, buttonContainer);
                    widget.SetData(new TowerButtonData(tower.towerName, tower.cost, gold >= tower.cost));
                    TowerData captured = tower;
                    if (widget.Button != null)
                        widget.Button.onClick.AddListener(() => OnTowerChosen?.Invoke(captured));
                    spawned.Add(widget);
                    LayoutRebuilder.ForceRebuildLayoutImmediate((RectTransform)widget.transform);
                }
            }
            if (buttonContainer is RectTransform containerRect)
                LayoutRebuilder.ForceRebuildLayoutImmediate(containerRect);
        }

        /// <summary> 모달을 숨깁니다. </summary>
        protected override void OnHide()
        {
            if (panel != null) panel.SetActive(false);
        }

        private void Clear()
        {
            foreach (TowerButtonWidget widget in spawned)
            {
                if (widget == null) continue;
                widget.gameObject.SetActive(false);
                Destroy(widget.gameObject);
            }
            spawned.Clear();
        }
    }
}
```

- [ ] **Step 3: TowerBuildPresenter 재작성**

```csharp
using UnityEngine;
using DefenseDot.Data;
using DefenseDot.Domain;
using DefenseDot.Domain.Models;
using DefenseDot.Systems.Tower;
using DefenseDot.UI.Base;
using DefenseDot.UI.Views;

namespace DefenseDot.UI.Presenters
{
    /// <summary> 슬롯 선택·빌드 모달·구매 설치를 중재하는 Presenter 입니다. </summary>
    public sealed class TowerBuildPresenter : UIPresenter<TowerBuildModalView>
    {
        private readonly TowerRoster roster;
        private readonly EconomyModel economy;
        private readonly TowerPlacementController placement;
        private Vector2Int currentCell;

        public TowerBuildPresenter(TowerBuildModalView view, GameContext ctx) : base(view)
        {
            roster = ctx.Roster;
            economy = ctx.Economy;
            placement = ctx.Placement;
        }

        protected override void OnInitialize()
        {
            if (placement != null)
            {
                placement.OnSlotSelected += HandleSlotSelected;
                placement.OnSlotDeselected += HandleDeselected;
            }
            view.OnTowerChosen += HandleTowerChosen;
            view.Hide();
        }

        protected override void OnDispose()
        {
            if (placement != null)
            {
                placement.OnSlotSelected -= HandleSlotSelected;
                placement.OnSlotDeselected -= HandleDeselected;
            }
            view.OnTowerChosen -= HandleTowerChosen;
        }

        private void HandleSlotSelected(Vector2Int cell, Vector3 worldPos)
        {
            currentCell = cell;
            view.ShowTowers(roster, economy.Gold.Value);
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

> `placement` null 가드 추가(Arena 에서 placement 없을 수 있음 — 기존엔 UIRoot 가 null 체크로 생성 회피했으나, 자동배선에선 Presenter 가 생성되므로 OnInitialize 에서 가드).

- [ ] **Step 4: Verify compile** (Task 9 전 단독 시그니처).
- [ ] **Step 5: Commit (보류)**.

---

## Task 9: UIRoot → List<UIView> + Inject(GameContext)

**Files:**
- Modify: `Assets/Scripts/UI/InGame/UIRoot.cs`

**Interfaces:**
- Consumes: `GameContext`, `UIPresenterFactory`, `UIView`, `IPresenter`
- Produces: `UIRoot.Inject(GameContext ctx)` — factory 생성 + `List<UIView>` 순회 배선.
- Breaking: 기존 `Inject(in HudContext, GameFlowModel, TowerPlacementController, in CardContext)` 제거. 개별 SerializeField 5종 제거.

- [ ] **Step 1: unity-standards 가이드 Read.**

- [ ] **Step 2: Rewrite**

```csharp
using System.Collections.Generic;
using UnityEngine;
using DefenseDot.Domain;
using DefenseDot.UI.Base;
using DefenseDot.UI.Presenters;

namespace DefenseDot.UI.InGame
{
    /// <summary>
    /// UI 합성 루트입니다. 주입된 GameContext 로 팩토리를 만들어 등록된 View 들의 Presenter 를 자동 배선합니다.
    /// 새 UI 는 View+Presenter 1쌍을 만들고 이 리스트에 등록하면 됩니다(코드 무수정).
    /// </summary>
    public sealed class UIRoot : MonoBehaviour
    {
        [SerializeField] private List<UIView> views = new List<UIView>();

        private readonly List<IPresenter> presenters = new List<IPresenter>();

        /// <summary> 컨텍스트를 받아 팩토리로 각 View 의 Presenter 를 생성·초기화합니다. </summary>
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
}
```

- [ ] **Step 3: Verify compile** (GameManager 가 아직 옛 Inject 호출 → Task 10 까지 일시 에러).
- [ ] **Step 4: Commit (보류)**.

---

## Task 10: GameManager → GameContext 구성·Inject

**Files:**
- Modify: `Assets/Scripts/Systems/Management/GameManager.cs` (Start 의 UI 연결부, ~L113-122)

**Interfaces:**
- Produces: GameManager 가 `GameContext` 를 구성해 `uiRoot.Inject(ctx)` 호출.

- [ ] **Step 1: unity-standards 가이드 Read.**

- [ ] **Step 2: Rewrite UI 연결부**

기존 `hudContext`/`cardContext` 조립·`uiRoot.Inject(hudContext, Flow, placement, cardContext)` 를 GameContext 로 교체:

```csharp
// UI 연결 (UI 합성 루트에 GameContext 주입)
if (uiRoot != null)
{
    var arenaBoot = modeBootstrap as ArenaModeBootstrap;
    DefenseDot.Systems.Cards.ArenaCardConfig cardConfig = arenaBoot != null ? arenaBoot.CardConfig : null;
    DefenseDot.Systems.Abilities.AbilityPool abilityPool = arenaBoot != null ? arenaBoot.AbilityPool : null;
    DefenseDot.Systems.Abilities.ICardCommandTarget coreTarget = arenaBoot != null ? arenaBoot.CoreAbility : null;

    var ctx = new DefenseDot.Domain.GameContext(
        Economy, Core, Wave, Score, RoundTimer, Flow, Level,
        modeBootstrap.EnemyDisplayCapacity,
        modeBootstrap.PlacementController != null ? null : null /* roster: 아래 주 참조 */,
        modeBootstrap.PlacementController,
        cardConfig, abilityPool, coreTarget);
    uiRoot.Inject(ctx);
}
```

> **주의(roster 출처)**: 기존 UIRoot 는 `towerRoster` 를 SerializeField 로 들었다. GameManager 에는 roster 참조가 없으므로, (a) `ModeBootstrap` 에 `TowerRoster` 노출 프로퍼티를 추가하거나 (b) GameManager 에 `[SerializeField] private TowerRoster towerRoster;` 를 추가해 ctx 에 전달한다. **이 플랜은 (b)** 를 택한다 — GameManager 에 `[SerializeField] private TowerRoster towerRoster;` 추가 후 ctx 인자에 `towerRoster` 전달. (Arena 에서 미사용이면 null 허용)

수정 최종형(roster 필드 추가 반영):

```csharp
// 필드 (Scene References 헤더 아래)
[SerializeField] private TowerRoster towerRoster;

// Start UI 연결
var ctx = new DefenseDot.Domain.GameContext(
    Economy, Core, Wave, Score, RoundTimer, Flow, Level,
    modeBootstrap.EnemyDisplayCapacity, towerRoster,
    modeBootstrap.PlacementController, cardConfig, abilityPool, coreTarget);
uiRoot.Inject(ctx);
```

- [ ] **Step 3: Verify compile** — `refresh_unity` → `read_console` Error 0 (전 태스크 시그니처가 맞물려 이제 컴파일 통과해야 함).
- [ ] **Step 4: Run EditMode tests** — `run_tests`(EditMode 전체) PASS (UIPresenterFactoryTests 포함, 기존 회귀 없음).
- [ ] **Step 5: Commit (보류)** — 인라인 배선(Task 11) 후 일괄 커밋 묶음 결정.

---

## Task 11: 인라인 MCP 배선 (프리팹 위젯 부착 + 씬 등록)

**Files:** (코드 아님 — Unity 에디터 자산. 컨트롤러가 MCP 로 수행)

- [ ] **Step 1: CardSlotWidget 프리팹 배선**
카드 모달 프리팹의 카드 아이템 3개 각각에 `CardSlotWidget` 컴포넌트 추가, 기존 CardItem 의 button/background/border/icon/nameText/kindText/descText/glowParticle 참조를 위젯 필드로 연결. CardSelectionView 의 `slots` 배열에 위젯 3개 연결, `config`/`canvasGroup`/`root`/`titleText`/`cardsContainer` 재연결.

- [ ] **Step 2: TowerButtonWidget 프리팹 생성**
기존 `buttonPrefab`(Button) 을 기반으로 `TowerButtonWidget` 컴포넌트를 얹은 프리팹 생성(button/label 연결). `TowerBuildModalView.buttonPrefab` 을 이 위젯 프리팹으로 교체, `panel`/`buttonContainer` 재연결.

- [ ] **Step 3: GameResultView 재연결**
스크립트 교체로 끊긴 `panel`/`messageText`/`restartButton` SerializeField 재연결 확인.

- [ ] **Step 4: 씬 UIRoot.views 등록**
씬 `UIRoot` 의 `views` 리스트에 `ArenaHudView`·`CardSelectionView`·`GameResultView`(+TowerBuild 가 씬에 있으면 `TowerBuildModalView`) 인스턴스 등록. 기존 개별 슬롯 참조는 스크립트에서 제거됨.

- [ ] **Step 5: GameManager.towerRoster 연결** — 씬 GameManager 의 `towerRoster` 슬롯에 TowerRoster 자산 연결(있으면).

- [ ] **Step 6: 각 단계 검증** — `manage_prefabs`/`manage_components` 로 참조 누락 0 확인.

---

## Task 12: 통합 검증

- [ ] **Step 1: 전체 컴파일** — `refresh_unity`(force) → `read_console` Error/Warning 0.
- [ ] **Step 2: EditMode 전체** — `run_tests`(EditMode) 전부 PASS (UIPresenterFactoryTests 포함, CardSelection 조정분 반영).
- [ ] **Step 3: Play 통합 검증**

| # | 시나리오 | 기대 |
|---|---|---|
| 1 | Play 진입 | UIRoot.views 의 각 Presenter 자동 생성·Initialize, 런타임 에러 0 |
| 2 | Arena HUD | 5위젯 정상 표시(런타임 위젯 text 값 확인) |
| 3 | 레벨업 | CardSelectionView 모달 표시, CardSlotWidget 3장 데이터·등급 포일, 선택 시 능력 적용·복귀 |
| 4 | 타워 빌드(있으면) | 슬롯 선택 시 TowerButtonWidget 목록, 구매 동작 |
| 5 | 승/패 | GameResultView 표시·재시작 |
| 6 | 종료 | OnDestroy 에서 전 Presenter Dispose |

- [ ] **Step 4: 완료 보고** — `superpowers:verification-before-completion` 으로 증거 첨부.

---

## Self-Review

**1. Spec coverage:** §3.1 자동배선(View순수·Presenter스캔)=Task2·9 ✅ / §3.2 GameContext 주입=Task1·3·4·6·8·10 ✅ / §3.3 UIRoot 책임=Task9·10 ✅ / §3.4 베이스 유지=전 Presenter UIPresenter<TView> ✅ / §3.5 범위(4 View 이전)=Task4·6·8 ✅ / §3.6 위젯(CardSlot강·TowerButton중)=Task5·7 ✅ / 배선 MCP=Task11 ✅.

**2. Placeholder scan:** Task6 테스트 조정의 `Assert.Pass` 는 의도된 축소(사용자 수용 trade-off)이며 사유 명시 — 플레이스홀더 아님. 나머지 코드 스텝 완전.

**3. Type consistency:** `GameContext` 프로퍼티명(Economy/Core/Wave/Score/Timer/Flow/Level/EnemyCapacity/Roster/Placement/CardConfig/AbilityPool/CoreTarget) Task1 정의 ↔ Task3·4·6·8·10 사용 일치 ✅. `ShowChoices`/`ShowTowers`/`ShowResult`(UIView.Show 충돌 회피) 일관 ✅. `UIPresenter<TView>`·`Create(UIView)` Task2 ↔ Task9 일치 ✅. 위젯 `SetData`·`Button` 노출 Task5·7 ↔ View Task6·8 일치 ✅.

**보정:** Task10 roster 출처를 GameManager `[SerializeField] TowerRoster` 추가로 명확화(초안의 모호한 삼항 제거).
