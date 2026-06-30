# UI 아키텍처 베이스 계층 재설계 Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** 흩어진 UI 베이스(`BasePresenter`/`IView`/`HudRoot`)를 `UIObject` 정점의 통일 계층으로 정렬하고, 도메인 모델 통지를 경량 `ReactiveProperty<T>`로 표준화하며, Arena HUD를 새 구조로 이전한다.

**Architecture:** UI 계층은 `UIObject`(얇은 MonoBehaviour 베이스) → `UIWidget`/`UIWidget<T>`(포맷팅 소유) → `UIView`(패널, Show/Hide) → `UIPresenter<T:UIView>`(View만 제네릭, RP를 `Bind`) → `UIRoot`(Presenter 생성 소유)로 정렬한다. 도메인 모델은 기존 constructor-DI를 유지한 채 event를 `ReactiveProperty<T>`(단일 스칼라)·`ReactiveProperty<struct>`(다중 스칼라)로 대체한다. **레지스트리/Service Locator는 도입하지 않는다.**

**Tech Stack:** Unity 6000.2.10f1, C#, TextMeshPro, NUnit(EditMode), 자체 경량 ReactiveProperty(UniRx 미도입).

## Global Constraints

- private 필드는 순수 `camelCase` (접두어 `m_`/`_` 금지).
- 모든 멤버에 명시적 접근 제한자 (IDE0040).
- `System.*`는 풀패스 사용 (예: `System.Action`, `System.IDisposable`). `System.Collections.Generic`은 `using` 허용.
- 비동기는 UniTask만 (이 계획은 즉시 반영만, 비동기 없음).
- event 네이밍 `On*`, 구독 핸들러 `Handle*`.
- 임시 컬렉션은 `UnityEngine.Pool.CollectionPool`; 필드로 보관하는 컬렉션은 `new` 허용.
- 주석은 한국어 `<summary>`; 인라인 주석은 20자 이내·최대 2줄.
- 라이프사이클 함수(`Awake`/`Start`/`Update`/`OnEnable`/`OnDestroy` 등)에 식 본문(`=>`) 금지 — 블록 본문 사용.
- UI 텍스트 폰트는 neodgm SDF (`Assets/Font/neodgm SDF.asset`); 새 TMP 위젯도 동일.
- 테스트는 EditMode·NUnit, `new`로 격리 생성 (namespace `DefenseDot.Tests.EditMode`).
- constructor-DI 유지 — 전역 정적 레지스트리 금지.
- 커밋은 **사용자 명시 승인 후** `commit` 스킬로만 수행하며, 직전에 `lint` 스킬 게이트를 통과해야 한다. 각 태스크의 "Commit" 스텝은 이 규칙을 따른다(무단 커밋 금지).
- `.cs` 파일 작성/수정 전에는 `unity-standards` 가이드(references/*.md) Read가 강제된다(훅 하드 차단). 각 구현 태스크 시작 시 해당 주제 가이드를 먼저 Read한다.

---

## File Structure

**신규 — 베이스 계층 (`Assets/Scripts/UI/Base/`)**
- `UIDepth.cs` — 렌더 깊이 enum
- `UIInitType.cs` — 시작 활성 여부 enum
- `IUIShowable.cs` — Show/Hide 계약
- `UIObject.cs` — `abstract MonoBehaviour`, RectTransform 캐싱 + Depth
- `UIWidget.cs` — `UIObject` 위젯 베이스 + `UIWidget<T>`
- `UIView.cs` — `UIObject`, `IUIShowable`, OnShow/OnHide/OnShown
- `UIPresenter.cs` — `UIPresenter<TView>` + `Bind` 헬퍼

**신규 — 모델 유틸 (`Assets/Scripts/Domain/`)**
- `ReactiveProperty.cs` — `IReadOnlyReactiveProperty<T>` + `ReactiveProperty<T>`
- `Models/ModelStates.cs` — `WaveProgress`/`TimerState`/`HealthState` struct, `EnemyState` struct

**신규 — Arena HUD 위젯 (`Assets/Scripts/UI/Widgets/`)**
- `GoldWidget.cs`, `ScoreWidget.cs`, `RoundWidget.cs`, `TimeWidget.cs`, `EnemyWidget.cs`

**수정 — 도메인 모델 (`Assets/Scripts/Domain/Models/`)**
- `EconomyModel.cs`, `ScoreModel.cs`, `CoreModel.cs`, `WaveModel.cs`, `RoundTimerModel.cs`

**수정 — UI**
- `Views/ArenaHudView.cs`(→ `UIView`), `Presenters/ArenaHudPresenter.cs`(→ `UIPresenter`), `InGame/UIRoot.cs`(Presenter 소유)
- `Systems/Mode/ArisTowerVisual.cs`(Health 구독 마이그레이션)
- `Presenters/TowerBuildPresenter.cs`(`economy.Gold.Value`)

**수정 — 테스트**
- `Tests/EditMode/ScoreModelTests.cs`, `Tests/EditMode/RoundTimerModelTests.cs`
- 신규: `Tests/EditMode/ReactivePropertyTests.cs`, `Tests/EditMode/EconomyModelTests.cs`, `Tests/EditMode/WaveModelTests.cs`, `Tests/EditMode/CoreModelTests.cs`

**삭제 (마지막 단계)**
- `UI.BaseModel`(in `Presenters/BasePresenter.cs`), `Models/ArenaHudModel.cs`, `Models/HUDModel.cs`

**비범위 (이번 plan 제외)**
- Grid HUD(`HUDView`/`HUDPresenter`/하위 `*View` 4종) 이전 — Arena가 활성 HUD. 동일 패턴으로 후속(§Task 13 안내).
- `CardSelection`/`GameResult`/`TowerBuild` Presenter의 `UIPresenter` 이전 — 후속.

---

## 핵심 설계 결정 (구현 확정값)

RP 전환 시 **회귀 0**을 위해 모델별로 노출 방식을 구분한다:

| 모델 | RP 노출(신규) | 편의 getter(유지) | 외부 수정 |
|---|---|---|---|
| EconomyModel | `IReadOnlyReactiveProperty<int> Gold` | 없음(단일 스칼라) | `TowerBuildPresenter:50` → `economy.Gold.Value` |
| ScoreModel | `IReadOnlyReactiveProperty<int> Score` | 없음(단일 스칼라) | `ScoreModelTests` Assert/구독 갱신 |
| CoreModel | `IReadOnlyReactiveProperty<HealthState> Health` | `CurrentHp`/`MaxHp`/`HealthRatio` | `ArisTowerVisual` 구독 마이그레이션 |
| WaveModel | `IReadOnlyReactiveProperty<WaveProgress> Progress`<br>`IReadOnlyReactiveProperty<int> RemainingEnemies` | `Current`/`Total`/`Remaining`/`IsLastWave` | 없음 |
| RoundTimerModel | `IReadOnlyReactiveProperty<TimerState> Time` | `Remaining`/`Duration`/`Ratio` | `RoundTimerModelTests` 구독 갱신 |

- **복합 모델(Core/Wave/Timer)**: 편의 스칼라 getter를 유지하므로 `EnemySpawner`(`timer.Remaining`)·`GameManager`(`Wave.Current`)·`AbilityModifiers`(`HealthRatio`)는 무영향.
- **위젯 데이터 주입 메서드명은 `SetData(T)`** — 스펙의 `Initialize(T)`는 `UIView.Initialize`/`UIPresenter.Initialize`와 혼동되어 `SetData`로 확정(검토 반영). 위젯이 표시 포맷팅을 소유한다.

---

## Task 1: ReactiveProperty<T>

**Files:**
- Create: `Assets/Scripts/Domain/ReactiveProperty.cs`
- Test: `Assets/Tests/EditMode/ReactivePropertyTests.cs`

**Interfaces:**
- Produces:
  - `interface DefenseDot.Domain.IReadOnlyReactiveProperty<T>` — `T Value { get; }`, `System.IDisposable Subscribe(System.Action<T> onNext)`
  - `sealed class DefenseDot.Domain.ReactiveProperty<T> : IReadOnlyReactiveProperty<T>` — `ctor(T initialValue = default)`, `T Value { get; set; }`, `void SetValueAndForceNotify(T newValue)`
  - 계약: `Value` set은 `EqualityComparer<T>.Default` 동등 시 통지 생략. `Subscribe`는 등록 직후 현재 값을 1회 통지하고, 반환 `IDisposable.Dispose()`로 해제한다.

- [ ] **Step 1: unity-standards 가이드 Read**

`unity-standards` 스킬을 호출하고 references 중 C# 컨벤션/패턴 문서를 Read한다(.cs Write 하드게이트 해제).

- [ ] **Step 2: Write the failing test**

Create `Assets/Tests/EditMode/ReactivePropertyTests.cs`:

```csharp
using NUnit.Framework;
using DefenseDot.Domain;

namespace DefenseDot.Tests.EditMode
{
    public class ReactivePropertyTests
    {
        [Test]
        public void Subscribe_ImmediatelyNotifiesCurrentValue()
        {
            var rp = new ReactiveProperty<int>(7);
            int got = -1;
            rp.Subscribe(v => got = v);
            Assert.AreEqual(7, got);
        }

        [Test]
        public void Value_NotifiesOnChange()
        {
            var rp = new ReactiveProperty<int>(0);
            int count = 0, last = -1;
            rp.Subscribe(v => { count++; last = v; });   // 즉시 1회
            rp.Value = 5;
            Assert.AreEqual(2, count);
            Assert.AreEqual(5, last);
        }

        [Test]
        public void Value_SameValue_DoesNotNotify()
        {
            var rp = new ReactiveProperty<int>(3);
            int count = 0;
            rp.Subscribe(_ => count++);   // 즉시 1회
            rp.Value = 3;
            Assert.AreEqual(1, count);
        }

        [Test]
        public void SetValueAndForceNotify_NotifiesEvenIfEqual()
        {
            var rp = new ReactiveProperty<int>(3);
            int count = 0;
            rp.Subscribe(_ => count++);   // 즉시 1회
            rp.SetValueAndForceNotify(3);
            Assert.AreEqual(2, count);
        }

        [Test]
        public void Dispose_StopsNotifying()
        {
            var rp = new ReactiveProperty<int>(0);
            int count = 0;
            System.IDisposable token = rp.Subscribe(_ => count++);   // 즉시 1회
            token.Dispose();
            rp.Value = 9;
            Assert.AreEqual(1, count);
        }
    }
}
```

- [ ] **Step 3: Run test to verify it fails**

Run: Unity EditMode 테스트 — `mcp__UnityMCP__run_tests`(mode `EditMode`, filter `ReactivePropertyTests`).
Expected: FAIL/컴파일 에러 — `ReactiveProperty` 미정의.

- [ ] **Step 4: Write minimal implementation**

Create `Assets/Scripts/Domain/ReactiveProperty.cs`:

```csharp
// 단일 값 변경을 구독·통지하는 경량 반응형 프로퍼티
using System.Collections.Generic;

namespace DefenseDot.Domain
{
    /// <summary> 읽기 전용 반응형 프로퍼티 계약입니다. </summary>
    public interface IReadOnlyReactiveProperty<T>
    {
        /// <summary> 현재 값입니다. </summary>
        T Value { get; }

        /// <summary> 변경을 구독합니다. 구독 즉시 현재 값을 1회 통지합니다. </summary>
        System.IDisposable Subscribe(System.Action<T> onNext);
    }

    /// <summary> 값이 실제로 바뀔 때만 통지하는 경량 반응형 프로퍼티입니다. </summary>
    public sealed class ReactiveProperty<T> : IReadOnlyReactiveProperty<T>
    {
        private T value;
        private System.Action<T> onChanged;

        /// <summary> 초기값으로 생성합니다. </summary>
        public ReactiveProperty(T initialValue = default)
        {
            value = initialValue;
        }

        /// <summary> 현재 값입니다. 같은 값 대입은 통지하지 않습니다. </summary>
        public T Value
        {
            get => value;
            set
            {
                if (EqualityComparer<T>.Default.Equals(this.value, value)) return;
                this.value = value;
                onChanged?.Invoke(this.value);
            }
        }

        /// <summary> 동등 비교를 우회해 현재 값을 강제 통지합니다. (재시작 등) </summary>
        public void SetValueAndForceNotify(T newValue)
        {
            value = newValue;
            onChanged?.Invoke(value);
        }

        /// <summary> 구독하고 즉시 현재 값을 1회 통지합니다. 토큰으로 해제합니다. </summary>
        public System.IDisposable Subscribe(System.Action<T> onNext)
        {
            if (onNext == null) return EmptyDisposable.Instance;
            onChanged += onNext;
            onNext(value);
            return new Subscription(this, onNext);
        }

        private void Remove(System.Action<T> handler)
        {
            onChanged -= handler;
        }

        private sealed class Subscription : System.IDisposable
        {
            private ReactiveProperty<T> owner;
            private System.Action<T> handler;

            public Subscription(ReactiveProperty<T> owner, System.Action<T> handler)
            {
                this.owner = owner;
                this.handler = handler;
            }

            public void Dispose()
            {
                if (owner == null) return;
                owner.Remove(handler);
                owner = null;
                handler = null;
            }
        }

        private sealed class EmptyDisposable : System.IDisposable
        {
            public static readonly EmptyDisposable Instance = new EmptyDisposable();
            public void Dispose() { }
        }
    }
}
```

- [ ] **Step 5: Run test to verify it passes**

Run: `mcp__UnityMCP__run_tests`(EditMode, filter `ReactivePropertyTests`).
Expected: PASS (5 tests).

- [ ] **Step 6: Commit** — `lint` 스킬 통과 후 사용자 승인 시 `commit`(`feat: 경량 ReactiveProperty 추가`).

---

## Task 2: 모델 상태 struct (ModelStates)

**Files:**
- Create: `Assets/Scripts/Domain/Models/ModelStates.cs`

**Interfaces:**
- Produces (모두 `DefenseDot.Domain.Models`, `readonly struct`):
  - `WaveProgress` — `int Current`, `int Total`; ctor `(int current, int total)`
  - `TimerState` — `float Remaining`, `float Duration`, `float Ratio`; ctor `(float remaining, float duration)`로 `Ratio = duration>0 ? remaining/duration : 0`
  - `HealthState` — `float Hp`, `float MaxHp`, `float Ratio`; ctor `(float hp, float maxHp)`로 `Ratio = maxHp>0 ? hp/maxHp : 0`
  - `EnemyState` — `int Alive`, `int Capacity`, `float Ratio`; ctor `(int alive, int capacity)`로 `Ratio = capacity>0 ? alive/(float)capacity : 0`

- [ ] **Step 1: Write implementation** (단순 데이터 구조 — 별도 실패 테스트 생략, 컴파일·소비 태스크에서 검증)

Create `Assets/Scripts/Domain/Models/ModelStates.cs`:

```csharp
// HUD 위젯·모델이 원자적으로 주고받는 표시 상태 값 묶음
namespace DefenseDot.Domain.Models
{
    /// <summary> 웨이브 진행(현재/전체) 표시 상태입니다. </summary>
    public readonly struct WaveProgress
    {
        /// <summary> 현재 웨이브 번호입니다. </summary>
        public readonly int Current;

        /// <summary> 전체 웨이브 수입니다. </summary>
        public readonly int Total;

        /// <summary> 현재/전체로 진행 상태를 만듭니다. </summary>
        public WaveProgress(int current, int total)
        {
            Current = current;
            Total = total;
        }
    }

    /// <summary> 라운드 제한시간(남은/총/비율) 표시 상태입니다. </summary>
    public readonly struct TimerState
    {
        /// <summary> 남은 시간(초)입니다. </summary>
        public readonly float Remaining;

        /// <summary> 총 제한시간(초)입니다. </summary>
        public readonly float Duration;

        /// <summary> 남은/총 비율(0~1)입니다. </summary>
        public readonly float Ratio;

        /// <summary> 남은·총 시간으로 상태를 만들고 비율을 계산합니다. </summary>
        public TimerState(float remaining, float duration)
        {
            Remaining = remaining;
            Duration = duration;
            Ratio = duration > 0f ? remaining / duration : 0f;
        }
    }

    /// <summary> 코어 체력(현재/최대/비율) 표시 상태입니다. </summary>
    public readonly struct HealthState
    {
        /// <summary> 현재 체력입니다. </summary>
        public readonly float Hp;

        /// <summary> 최대 체력입니다. </summary>
        public readonly float MaxHp;

        /// <summary> 현재/최대 비율(0~1)입니다. </summary>
        public readonly float Ratio;

        /// <summary> 현재·최대 체력으로 상태를 만들고 비율을 계산합니다. </summary>
        public HealthState(float hp, float maxHp)
        {
            Hp = hp;
            MaxHp = maxHp;
            Ratio = maxHp > 0f ? hp / maxHp : 0f;
        }
    }

    /// <summary> 생존 적/수용 한계(비율) 표시 상태입니다. </summary>
    public readonly struct EnemyState
    {
        /// <summary> 생존 적 수입니다. </summary>
        public readonly int Alive;

        /// <summary> 적 수용 한계입니다. </summary>
        public readonly int Capacity;

        /// <summary> 위험 비율(0~1)입니다. </summary>
        public readonly float Ratio;

        /// <summary> 생존 적·수용 한계로 상태를 만들고 비율을 계산합니다. </summary>
        public EnemyState(int alive, int capacity)
        {
            Alive = alive;
            Capacity = capacity;
            Ratio = capacity > 0 ? alive / (float)capacity : 0f;
        }
    }
}
```

- [ ] **Step 2: Verify compile**

Run: `mcp__UnityMCP__refresh_unity` 후 `mcp__UnityMCP__read_console`(filter Error).
Expected: 컴파일 에러 0.

- [ ] **Step 3: Commit** — `lint` 후 승인 시 `commit`(`feat: HUD 표시 상태 struct 추가`).

---

## Task 3: UI 베이스 — enum·인터페이스·UIObject·UIView·UIWidget

**Files:**
- Create: `Assets/Scripts/UI/Base/UIDepth.cs`
- Create: `Assets/Scripts/UI/Base/UIInitType.cs`
- Create: `Assets/Scripts/UI/Base/IUIShowable.cs`
- Create: `Assets/Scripts/UI/Base/UIObject.cs`
- Create: `Assets/Scripts/UI/Base/UIWidget.cs`
- Create: `Assets/Scripts/UI/Base/UIView.cs`

**Interfaces:**
- Produces (namespace `DefenseDot.UI.Base`):
  - `enum UIDepth { HUD, Fixed, Popup, System }`
  - `enum UIInitType { ActiveOnStart, InactiveOnStart }`
  - `interface IUIShowable { void Show(); void Hide(); }`
  - `abstract class UIObject : MonoBehaviour` — `UIDepth Depth { get; }`, `RectTransform RectTransform { get; }`
  - `abstract class UIWidget : UIObject` (마커 베이스)
  - `abstract class UIWidget<T> : UIWidget` — `abstract void SetData(T data)`
  - `abstract class UIView : UIObject, IUIShowable` — `event System.Action OnShown`, `void Show()`, `void Hide()`, `protected virtual void OnShow()`, `protected virtual void OnHide()`

- [ ] **Step 1: unity-standards 가이드 Read** (MonoBehaviour/컴포넌트 패턴).

- [ ] **Step 2: Write enums**

Create `Assets/Scripts/UI/Base/UIDepth.cs`:

```csharp
namespace DefenseDot.UI.Base
{
    /// <summary> UI 렌더 깊이 계층입니다. (낮을수록 뒤, 높을수록 앞) </summary>
    public enum UIDepth
    {
        /// <summary> 상시 HUD 계층입니다. </summary>
        HUD = 0,

        /// <summary> 고정 오버레이 계층입니다. </summary>
        Fixed = 1,

        /// <summary> 팝업/모달 계층입니다. </summary>
        Popup = 2,

        /// <summary> 시스템 최상위 계층입니다. </summary>
        System = 3,
    }
}
```

Create `Assets/Scripts/UI/Base/UIInitType.cs`:

```csharp
namespace DefenseDot.UI.Base
{
    /// <summary> UI의 시작 활성 상태입니다. </summary>
    public enum UIInitType
    {
        /// <summary> 시작 시 활성입니다. </summary>
        ActiveOnStart,

        /// <summary> 시작 시 비활성입니다. (팝업/풀링) </summary>
        InactiveOnStart,
    }
}
```

- [ ] **Step 3: Write IUIShowable**

Create `Assets/Scripts/UI/Base/IUIShowable.cs`:

```csharp
namespace DefenseDot.UI.Base
{
    /// <summary> 표시/숨김이 가능한 UI 계약입니다. </summary>
    public interface IUIShowable
    {
        /// <summary> UI를 표시합니다. </summary>
        void Show();

        /// <summary> UI를 숨깁니다. </summary>
        void Hide();
    }
}
```

- [ ] **Step 4: Write UIObject**

Create `Assets/Scripts/UI/Base/UIObject.cs`:

```csharp
// Canvas 위 모든 UI의 최상위 베이스 — 얇게 유지
using UnityEngine;

namespace DefenseDot.UI.Base
{
    /// <summary>
    /// Canvas에 그려지는 모든 UI 요소의 베이스입니다.
    /// 깊이와 RectTransform 캐싱만 책임지며, 동작은 인터페이스로 분리합니다.
    /// </summary>
    public abstract class UIObject : MonoBehaviour
    {
        [SerializeField] private UIDepth depth = UIDepth.HUD;

        private RectTransform cachedRect;

        /// <summary> 이 UI의 렌더 깊이 계층입니다. </summary>
        public UIDepth Depth => depth;

        /// <summary> 캐시된 RectTransform입니다. </summary>
        public RectTransform RectTransform
        {
            get
            {
                if (cachedRect == null) cachedRect = transform as RectTransform;
                return cachedRect;
            }
        }
    }
}
```

- [ ] **Step 5: Write UIWidget + UIWidget<T>**

Create `Assets/Scripts/UI/Base/UIWidget.cs`:

```csharp
// 복합 UI 요소(텍스트+이펙트 등)를 래핑하는 위젯 베이스
namespace DefenseDot.UI.Base
{
    /// <summary>
    /// 복합 UI 요소를 래핑하는 위젯 베이스입니다.
    /// 부모-자식 구성은 허용하되 형제 위젯을 참조하지 않습니다.
    /// </summary>
    public abstract class UIWidget : UIObject
    {
    }

    /// <summary>
    /// 표시 데이터(DTO) T로 갱신되는 위젯입니다. 표시 포맷팅을 스스로 소유합니다.
    /// </summary>
    /// <typeparam name="T">바인딩 표시 데이터 타입</typeparam>
    public abstract class UIWidget<T> : UIWidget
    {
        /// <summary> DTO로 이 위젯의 표시를 갱신합니다. </summary>
        public abstract void SetData(T data);
    }
}
```

- [ ] **Step 6: Write UIView**

Create `Assets/Scripts/UI/Base/UIView.cs`:

```csharp
// 위젯들로 구성된 패널 — Presenter를 모른다
using UnityEngine;

namespace DefenseDot.UI.Base
{
    /// <summary>
    /// 위젯들로 구성된 UI 패널 베이스입니다. 표시/숨김과 표시 시점 훅을 제공합니다.
    /// View는 Presenter를 알지 못하며, 표시 시 OnShown으로만 통지합니다.
    /// </summary>
    public abstract class UIView : UIObject, IUIShowable
    {
        [SerializeField] private UIInitType initType = UIInitType.ActiveOnStart;

        /// <summary> 표시(Show)될 때 발생합니다. (Presenter 재반영용) </summary>
        public event System.Action OnShown;

        /// <summary> 시작 활성 상태를 적용합니다. </summary>
        protected virtual void Awake()
        {
            if (initType == UIInitType.InactiveOnStart) gameObject.SetActive(false);
        }

        /// <summary> 패널을 표시하고 OnShow/OnShown을 통지합니다. </summary>
        public void Show()
        {
            gameObject.SetActive(true);
            OnShow();
            OnShown?.Invoke();
        }

        /// <summary> 패널을 숨깁니다. </summary>
        public void Hide()
        {
            OnHide();
            gameObject.SetActive(false);
        }

        /// <summary> 표시 직후 훅입니다. </summary>
        protected virtual void OnShow() { }

        /// <summary> 숨김 직전 훅입니다. </summary>
        protected virtual void OnHide() { }
    }
}
```

- [ ] **Step 7: Verify compile**

Run: `mcp__UnityMCP__refresh_unity` → `mcp__UnityMCP__read_console`(Error).
Expected: 컴파일 에러 0. (기존 코드 무영향 — 신규 파일만)

- [ ] **Step 8: Commit** — `lint` 후 승인 시 `commit`(`feat: UI 베이스 계층(UIObject/UIView/UIWidget) 추가`).

---

## Task 4: UIPresenter<TView> + Bind

**Files:**
- Create: `Assets/Scripts/UI/Base/UIPresenter.cs`

**Interfaces:**
- Consumes: `DefenseDot.UI.Base.UIView`(OnShown), `DefenseDot.Domain.IReadOnlyReactiveProperty<V>`, `DefenseDot.UI.Presenters.IPresenter`(Initialize/Dispose)
- Produces (namespace `DefenseDot.UI.Base`):
  - `abstract class UIPresenter<TView> : DefenseDot.UI.Presenters.IPresenter where TView : UIView`
  - `protected readonly TView view`
  - `void Initialize()`(재진입 가드 + `view.OnShown += Refresh` + `OnInitialize()`), `void Dispose()`(토큰 일괄 해제 + `OnDispose()`)
  - `protected abstract void OnInitialize()`, `protected virtual void OnDispose()`
  - `protected void Bind<V>(IReadOnlyReactiveProperty<V> source, System.Action<V> onValue)` — 구독 토큰 집계 + OnShow 재반영 등록

- [ ] **Step 1: unity-standards 가이드 Read** (있다면 MVP/Observer 패턴 문서).

- [ ] **Step 2: Write implementation**

Create `Assets/Scripts/UI/Base/UIPresenter.cs`:

```csharp
// View(제네릭)와 도메인 RP를 잇는 Presenter 베이스 — Model은 필드로 직접 보유
using System.Collections.Generic;
using DefenseDot.Domain;
using DefenseDot.UI.Presenters;

namespace DefenseDot.UI.Base
{
    /// <summary>
    /// View만 제네릭으로 받는 Presenter 베이스입니다. Model은 파생 클래스가 필드로 보유합니다.
    /// RP를 Bind하면 구독 즉시 초기값이 반영되고, View 표시 시 현재값이 재반영됩니다.
    /// </summary>
    /// <typeparam name="TView">제어할 View 타입</typeparam>
    public abstract class UIPresenter<TView> : IPresenter where TView : UIView
    {
        /// <summary> 제어 대상 View입니다. </summary>
        protected readonly TView view;

        private readonly List<System.IDisposable> bindings = new List<System.IDisposable>();
        private readonly List<System.Action> refreshers = new List<System.Action>();
        private bool initialized;

        /// <summary> View를 주입받습니다. </summary>
        protected UIPresenter(TView view)
        {
            this.view = view;
        }

        /// <summary> 구독을 등록하고 표시 재반영 훅을 연결합니다. (재진입 무시) </summary>
        public void Initialize()
        {
            if (initialized) return;
            initialized = true;
            if (view != null) view.OnShown += Refresh;
            OnInitialize();
        }

        /// <summary> 모든 구독을 해제합니다. </summary>
        public void Dispose()
        {
            if (!initialized) return;
            initialized = false;
            if (view != null) view.OnShown -= Refresh;
            foreach (System.IDisposable binding in bindings) binding.Dispose();
            bindings.Clear();
            refreshers.Clear();
            OnDispose();
        }

        /// <summary> 구독·바인딩을 등록하는 파생 훅입니다. </summary>
        protected abstract void OnInitialize();

        /// <summary> 추가 정리 훅입니다. </summary>
        protected virtual void OnDispose() { }

        /// <summary> RP를 구독해 핸들러에 연결하고, 해제 토큰과 재반영을 집계합니다. </summary>
        protected void Bind<V>(IReadOnlyReactiveProperty<V> source, System.Action<V> onValue)
        {
            if (source == null || onValue == null) return;
            bindings.Add(source.Subscribe(onValue));
            refreshers.Add(() => onValue(source.Value));
        }

        private void Refresh()
        {
            foreach (System.Action refresher in refreshers) refresher();
        }
    }
}
```

- [ ] **Step 3: Verify compile**

Run: `mcp__UnityMCP__refresh_unity` → `read_console`(Error).
Expected: 컴파일 에러 0.

> 참고: `UIPresenter`의 Bind/Refresh 핵심 동작은 Task 1의 `ReactivePropertyTests`(즉시통지·해제)가 단위 수준에서 검증한다. View(MonoBehaviour) 결합 동작은 Task 11~12 후 Play 검증으로 확인한다.

- [ ] **Step 4: Commit** — `lint` 후 승인 시 `commit`(`feat: UIPresenter 베이스(Bind/재반영) 추가`).

---

## Task 5: EconomyModel → ReactiveProperty<int> Gold

**Files:**
- Modify: `Assets/Scripts/Domain/Models/EconomyModel.cs`
- Modify: `Assets/Scripts/UI/Presenters/TowerBuildPresenter.cs:50`
- Test: `Assets/Tests/EditMode/EconomyModelTests.cs` (create)

**Interfaces:**
- Consumes: `ReactiveProperty<int>`, `IReadOnlyReactiveProperty<int>`
- Produces: `EconomyModel.Gold : IReadOnlyReactiveProperty<int>` (RP, 쓰기 private). `AddGold/CanAfford/TrySpend/Initialize` 시그니처 불변.
- Breaking: `event OnGoldChanged` 제거. `Gold`(int) → `Gold`(RP). 외부 int 사용처 `TowerBuildPresenter`는 `.Value`로 수정.

- [ ] **Step 1: unity-standards 가이드 Read.**

- [ ] **Step 2: Write the failing test**

Create `Assets/Tests/EditMode/EconomyModelTests.cs`:

```csharp
using NUnit.Framework;
using DefenseDot.Domain.Models;

namespace DefenseDot.Tests.EditMode
{
    public class EconomyModelTests
    {
        [Test]
        public void Initialize_SetsGold()
        {
            var m = new EconomyModel();
            m.Initialize(100);
            Assert.AreEqual(100, m.Gold.Value);
        }

        [Test]
        public void Initialize_ForceNotifies_EvenSameValue()
        {
            var m = new EconomyModel();
            m.Initialize(50);
            int notified = -1;
            m.Gold.Subscribe(v => notified = v);   // 즉시 50
            m.Initialize(50);                       // 동일값이어도 통지
            Assert.AreEqual(50, notified);
        }

        [Test]
        public void AddGold_Increases()
        {
            var m = new EconomyModel();
            m.Initialize(10);
            m.AddGold(15);
            Assert.AreEqual(25, m.Gold.Value);
        }

        [Test]
        public void TrySpend_InsufficientReturnsFalse()
        {
            var m = new EconomyModel();
            m.Initialize(10);
            Assert.IsFalse(m.TrySpend(20));
            Assert.AreEqual(10, m.Gold.Value);
        }

        [Test]
        public void TrySpend_SufficientDeducts()
        {
            var m = new EconomyModel();
            m.Initialize(30);
            Assert.IsTrue(m.TrySpend(20));
            Assert.AreEqual(10, m.Gold.Value);
        }
    }
}
```

- [ ] **Step 3: Run test to verify it fails**

Run: `mcp__UnityMCP__run_tests`(EditMode, filter `EconomyModelTests`).
Expected: 컴파일 에러 — `Gold.Value` 미존재(현재 `Gold`는 int).

- [ ] **Step 4: Write implementation**

Replace `Assets/Scripts/Domain/Models/EconomyModel.cs`:

```csharp
// 골드 재화 상태를 소유·통지하는 도메인 모델
using DefenseDot.Domain;

namespace DefenseDot.Domain.Models
{
    /// <summary>
    /// 골드 재화 상태를 소유하고 변경을 통지하는 도메인 모델입니다.
    /// </summary>
    public class EconomyModel : BaseModel
    {
        private readonly ReactiveProperty<int> gold = new ReactiveProperty<int>(0);

        /// <summary> 현재 소지 골드입니다. (읽기 전용 RP) </summary>
        public IReadOnlyReactiveProperty<int> Gold => gold;

        /// <summary> 초기 골드를 설정하고 강제 통지합니다. </summary>
        public void Initialize(int startGold)
        {
            gold.SetValueAndForceNotify(startGold);
        }

        /// <summary> 골드를 가산합니다. (적 처치 보상 등) </summary>
        public void AddGold(int amount)
        {
            if (amount == 0) return;
            gold.Value += amount;
        }

        /// <summary> 비용을 감당할 수 있는지 확인합니다. </summary>
        public bool CanAfford(int cost) => gold.Value >= cost;

        /// <summary> 비용을 차감합니다. 잔액이 부족하면 false를 반환합니다. </summary>
        public bool TrySpend(int cost)
        {
            if (!CanAfford(cost)) return false;
            gold.Value -= cost;
            return true;
        }
    }
}
```

> 주의: `[System.Serializable]`/`[SerializeField]`를 제거한다(RP는 비직렬화). 합성 루트가 `Initialize`로 값을 설정하므로 인스펙터 직렬화는 불필요.

- [ ] **Step 5: Fix external usage**

Modify `Assets/Scripts/UI/Presenters/TowerBuildPresenter.cs:50` — `economy.Gold` → `economy.Gold.Value`:

```csharp
            view.Show(roster, economy.Gold.Value);
```

> `HUDPresenter`/`ArenaHudPresenter`도 현재 `economy.OnGoldChanged`/`economy.Gold`를 사용하지만, Arena는 Task 12에서 RP Bind로 교체된다. **이 태스크에서는 `ArenaHudPresenter`·`HUDPresenter`가 컴파일되도록 임시 보정**한다(아래 Step 6).

- [ ] **Step 6: 임시 컴파일 보정 (Presenter)**

`ArenaHudPresenter.cs`와 `HUDPresenter.cs`에서 `economy.OnGoldChanged += HandleGoldChanged;`(구독/해제)와 `HandleGoldChanged(economy.Gold);` 호출을 RP 구독으로 임시 교체:
- 구독: `economy.Gold.Subscribe(HandleGoldChanged)` 의 반환 토큰을 필드 `System.IDisposable goldSub`에 보관, `Dispose()`에서 `goldSub?.Dispose()`.
- `OnGoldChanged -=` 라인 및 `HandleGoldChanged(economy.Gold);`(초기 반영) 라인 삭제(Subscribe가 즉시 1회 통지).

ArenaHudPresenter 예 (Initialize/Dispose 내 골드 부분만):

```csharp
        // 필드 추가
        private System.IDisposable goldSub;

        // Initialize 내 economy.OnGoldChanged += HandleGoldChanged; 와
        // HandleGoldChanged(economy.Gold); 를 아래로 교체
        goldSub = economy.Gold.Subscribe(HandleGoldChanged);

        // Dispose 내 economy.OnGoldChanged -= HandleGoldChanged; 를 아래로 교체
        goldSub?.Dispose();
```

`HUDPresenter`도 동일 패턴으로 보정.

- [ ] **Step 7: Run test + compile**

Run: `mcp__UnityMCP__run_tests`(EditMode, filter `EconomyModelTests`) → PASS (5). 그리고 `read_console`(Error) → 0.

- [ ] **Step 8: Commit** — `lint` 후 승인 시 `commit`(`refactor: EconomyModel을 ReactiveProperty로 전환`).

---

## Task 6: ScoreModel → ReactiveProperty<int> Score

**Files:**
- Modify: `Assets/Scripts/Domain/Models/ScoreModel.cs`
- Modify: `Assets/Tests/EditMode/ScoreModelTests.cs`
- Modify: `Assets/Scripts/UI/Presenters/ArenaHudPresenter.cs`(score 구독 보정)

**Interfaces:**
- Produces: `ScoreModel.Score : IReadOnlyReactiveProperty<int>`. `AddKillScore/AddTimeBonus/Reset` 시그니처 불변.
- Breaking: `event OnScoreChanged` 제거, `Score`(int) → RP.

- [ ] **Step 1: unity-standards 가이드 Read.**

- [ ] **Step 2: Update tests to expect RP**

Replace `Assets/Tests/EditMode/ScoreModelTests.cs` — 모든 `model.Score`를 `model.Score.Value`로, `OnScoreChanged` 테스트를 Subscribe로:

```csharp
using NUnit.Framework;
using DefenseDot.Domain.Models;

namespace DefenseDot.Tests.EditMode
{
    public class ScoreModelTests
    {
        [Test]
        public void AddKillScore_AddsTenTimesRound()
        {
            var model = new ScoreModel();
            model.AddKillScore(3);
            Assert.AreEqual(30, model.Score.Value);
        }

        [Test]
        public void AddKillScore_Accumulates()
        {
            var model = new ScoreModel();
            model.AddKillScore(1);
            model.AddKillScore(2);
            Assert.AreEqual(30, model.Score.Value);
        }

        [Test]
        public void AddTimeBonus_FloorsSavedTimesTenTimesRound()
        {
            var model = new ScoreModel();
            model.AddTimeBonus(2.5f, 4);
            Assert.AreEqual(100, model.Score.Value);
        }

        [Test]
        public void AddTimeBonus_NonPositiveSaved_NoChange()
        {
            var model = new ScoreModel();
            model.AddTimeBonus(0f, 5);
            Assert.AreEqual(0, model.Score.Value);
        }

        [Test]
        public void AddKillScore_AppliesMultiplier()
        {
            var model = new ScoreModel();
            model.AddKillScore(3, 2f);
            Assert.AreEqual(60, model.Score.Value);
        }

        [Test]
        public void AddTimeBonus_AppliesMultiplier()
        {
            var model = new ScoreModel();
            model.AddTimeBonus(4f, 3, 0.5f);
            Assert.AreEqual(60, model.Score.Value);
        }

        [Test]
        public void Score_NotifiesWithNewScore()
        {
            var model = new ScoreModel();
            int notified = -1;
            model.Score.Subscribe(s => notified = s);   // 즉시 0
            model.AddKillScore(2);
            Assert.AreEqual(20, notified);
        }

        [Test]
        public void Reset_ZeroesScore()
        {
            var model = new ScoreModel();
            model.AddKillScore(5);
            model.Reset();
            Assert.AreEqual(0, model.Score.Value);
        }
    }
}
```

- [ ] **Step 3: Run test to verify it fails** — `run_tests`(filter `ScoreModelTests`) → 컴파일 에러(`Score.Value` 미존재).

- [ ] **Step 4: Write implementation**

Replace `Assets/Scripts/Domain/Models/ScoreModel.cs`:

```csharp
// 인-런 점수(처치·시간보너스)를 보유·통지하는 도메인 모델
using UnityEngine;
using DefenseDot.Domain;

namespace DefenseDot.Domain.Models
{
    /// <summary>
    /// 인-런 점수를 보유하고 통지하는 도메인 모델입니다.
    /// 처치 점수와 라운드 조기 클리어 시간보너스를 가산합니다.
    /// </summary>
    public class ScoreModel : BaseModel
    {
        private readonly ReactiveProperty<int> score = new ReactiveProperty<int>(0);

        /// <summary> 현재 누적 점수입니다. (읽기 전용 RP) </summary>
        public IReadOnlyReactiveProperty<int> Score => score;

        /// <summary> 처치 점수를 가산합니다. (floor(10 × 라운드 × 배율)) </summary>
        public void AddKillScore(int round, float multiplier = 1f)
        {
            int gained = Mathf.FloorToInt(10f * Mathf.Max(1, round) * Mathf.Max(0f, multiplier));
            if (gained <= 0) return;
            score.Value += gained;
        }

        /// <summary> 조기 클리어 시간보너스를 가산합니다. (floor(절약초 × 10 × 라운드 × 배율)) </summary>
        public void AddTimeBonus(float savedSeconds, int round, float multiplier = 1f)
        {
            int bonus = Mathf.FloorToInt(Mathf.Max(0f, savedSeconds) * 10f * Mathf.Max(1, round) * Mathf.Max(0f, multiplier));
            if (bonus <= 0) return;
            score.Value += bonus;
        }

        /// <summary> 점수를 0으로 초기화하고 강제 통지합니다. (재시작용) </summary>
        public void Reset()
        {
            score.SetValueAndForceNotify(0);
        }
    }
}
```

- [ ] **Step 5: Fix ArenaHudPresenter score subscription** — Task 5 Step 6과 동일 패턴으로 `score.OnScoreChanged`/`HandleScoreChanged(score.Score)`를 `scoreSub = score.Score.Subscribe(HandleScoreChanged);` + `scoreSub?.Dispose();`로 보정.

- [ ] **Step 6: Run test + compile** — `run_tests`(`ScoreModelTests`) PASS (8), `read_console` Error 0.

- [ ] **Step 7: Commit** — `lint` 후 승인 시 `commit`(`refactor: ScoreModel을 ReactiveProperty로 전환`).

---

## Task 7: RoundTimerModel → ReactiveProperty<TimerState> Time

**Files:**
- Modify: `Assets/Scripts/Domain/Models/RoundTimerModel.cs`
- Modify: `Assets/Tests/EditMode/RoundTimerModelTests.cs`
- Modify: `Assets/Scripts/UI/Presenters/ArenaHudPresenter.cs`(timer 구독 보정)

**Interfaces:**
- Produces: `RoundTimerModel.Time : IReadOnlyReactiveProperty<TimerState>` + 편의 getter `Remaining`/`Duration`/`Ratio`(파생) 유지 + `IsExpired` 유지. `StartWave/Tick/Reset` 시그니처 불변.
- Breaking: `event OnTimeChanged` 제거.
- Unaffected: `EnemySpawner.cs:228`(`timer.Remaining`) — 편의 getter 유지로 무영향.

- [ ] **Step 1: unity-standards 가이드 Read.**

- [ ] **Step 2: Update tests** — `Remaining`/`Duration`/`Ratio` Assert는 그대로(편의 getter 유지). `OnTimeChanged` 테스트만 `Time.Subscribe`로 교체:

Replace the `OnTimeChanged_FiresOnTick` test in `RoundTimerModelTests.cs`:

```csharp
        [Test]
        public void Time_NotifiesOnTick()
        {
            var t = new RoundTimerModel();
            t.StartWave(10f);
            float gotRemaining = -1f, gotDuration = -1f;
            t.Time.Subscribe(s => { gotRemaining = s.Remaining; gotDuration = s.Duration; });
            t.Tick(4f);
            Assert.AreEqual(6f, gotRemaining, 0.0001f);
            Assert.AreEqual(10f, gotDuration, 0.0001f);
        }
```

(나머지 테스트 `StartWave_SetsRemainingToDuration`/`Tick_*`/`Ratio_*`/`Reset_*`는 변경 없음.)

- [ ] **Step 3: Run test to verify it fails** — `run_tests`(`RoundTimerModelTests`) → 컴파일 에러(`Time` 미존재).

- [ ] **Step 4: Write implementation**

Replace `Assets/Scripts/Domain/Models/RoundTimerModel.cs`:

```csharp
// 라운드 제한시간(남은/총)을 보유·통지하는 도메인 모델
using UnityEngine;
using DefenseDot.Domain;

namespace DefenseDot.Domain.Models
{
    /// <summary>
    /// 라운드 제한시간을 보유하고 통지하는 도메인 모델입니다.
    /// 외부(스포너)가 매 프레임 Tick하며, 만료 여부를 제공합니다.
    /// </summary>
    public class RoundTimerModel : BaseModel
    {
        private readonly ReactiveProperty<TimerState> time = new ReactiveProperty<TimerState>(new TimerState(0f, 0f));

        /// <summary> 남은/총 시간 상태입니다. (읽기 전용 RP) </summary>
        public IReadOnlyReactiveProperty<TimerState> Time => time;

        /// <summary> 남은 시간(초)입니다. </summary>
        public float Remaining => time.Value.Remaining;

        /// <summary> 이번 라운드의 총 제한시간(초)입니다. </summary>
        public float Duration => time.Value.Duration;

        /// <summary> 시간바 비율(남은/총)입니다. </summary>
        public float Ratio => time.Value.Ratio;

        /// <summary> 시간이 만료되었는지 여부입니다. </summary>
        public bool IsExpired => time.Value.Remaining <= 0f;

        /// <summary> 새 라운드의 제한시간을 설정하고 강제 통지합니다. </summary>
        public void StartWave(float waveDuration)
        {
            float duration = Mathf.Max(0f, waveDuration);
            time.SetValueAndForceNotify(new TimerState(duration, duration));
        }

        /// <summary> 경과 시간만큼 남은 시간을 줄이고 통지합니다. </summary>
        public void Tick(float deltaTime)
        {
            TimerState current = time.Value;
            if (current.Remaining <= 0f) return;
            float remaining = Mathf.Max(0f, current.Remaining - deltaTime);
            time.Value = new TimerState(remaining, current.Duration);
        }

        /// <summary> 남은·총 시간을 0으로 초기화하고 강제 통지합니다. (재시작용) </summary>
        public void Reset()
        {
            time.SetValueAndForceNotify(new TimerState(0f, 0f));
        }
    }
}
```

> 참고: `TimerState`는 `readonly struct`라 `ReactiveProperty<TimerState>.Value` set 시 `EqualityComparer<TimerState>.Default`(ValueType 기본 비교)로 동등 판정된다. `Tick`은 매번 Remaining이 달라져 통지된다.

- [ ] **Step 5: Fix ArenaHudPresenter timer subscription** — `timer.OnTimeChanged`/`HandleTimeChanged(timer.Remaining, timer.Duration)`를 `timeSub = timer.Time.Subscribe(HandleTimeState);`로 보정. 임시 핸들러:

```csharp
        private System.IDisposable timeSub;

        private void HandleTimeState(DefenseDot.Domain.Models.TimerState s)
        {
            view.SetTime(s.Remaining);
            view.SetTimeBar(s.Ratio);
        }
```

기존 `HandleTimeChanged(float, float)`는 삭제. (Task 12에서 위젯 Bind로 최종 정리)

- [ ] **Step 6: Run test + compile** — `run_tests`(`RoundTimerModelTests`) PASS, `read_console` Error 0.

- [ ] **Step 7: Commit** — `lint` 후 승인 시 `commit`(`refactor: RoundTimerModel을 ReactiveProperty로 전환`).

---

## Task 8: WaveModel → Progress / RemainingEnemies RP

**Files:**
- Modify: `Assets/Scripts/Domain/Models/WaveModel.cs`
- Modify: `Assets/Scripts/UI/Presenters/ArenaHudPresenter.cs`(wave 구독 보정)
- Test: `Assets/Tests/EditMode/WaveModelTests.cs` (create)

**Interfaces:**
- Produces: `WaveModel.Progress : IReadOnlyReactiveProperty<WaveProgress>`, `WaveModel.RemainingEnemies : IReadOnlyReactiveProperty<int>` + 편의 getter `Current`/`Total`/`Remaining`/`IsLastWave` 유지. `OnWaveCleared` event 유지. `SetWave/SetRemaining/MarkWaveCleared` 시그니처 불변.
- Breaking: `event OnWaveChanged`/`OnRemainingChanged` 제거.
- Unaffected: `GameManager.cs:54`(`Wave.Current`) — 편의 getter 유지로 무영향.

- [ ] **Step 1: unity-standards 가이드 Read.**

- [ ] **Step 2: Write the failing test**

Create `Assets/Tests/EditMode/WaveModelTests.cs`:

```csharp
using NUnit.Framework;
using DefenseDot.Domain.Models;

namespace DefenseDot.Tests.EditMode
{
    public class WaveModelTests
    {
        [Test]
        public void SetWave_UpdatesProgress()
        {
            var m = new WaveModel();
            m.SetWave(2, 5);
            Assert.AreEqual(2, m.Current);
            Assert.AreEqual(5, m.Total);
            Assert.AreEqual(2, m.Progress.Value.Current);
            Assert.AreEqual(5, m.Progress.Value.Total);
        }

        [Test]
        public void SetWave_NotifiesProgress()
        {
            var m = new WaveModel();
            WaveProgress got = default;
            m.Progress.Subscribe(p => got = p);   // 즉시 (0,0)
            m.SetWave(3, 7);
            Assert.AreEqual(3, got.Current);
            Assert.AreEqual(7, got.Total);
        }

        [Test]
        public void SetRemaining_NotifiesAndDedupes()
        {
            var m = new WaveModel();
            int count = 0, last = -1;
            m.RemainingEnemies.Subscribe(v => { count++; last = v; });   // 즉시 0
            m.SetRemaining(4);
            m.SetRemaining(4);   // 동일값 — 통지 생략
            Assert.AreEqual(2, count);
            Assert.AreEqual(4, last);
        }

        [Test]
        public void IsLastWave_TrueWhenCurrentReachesTotal()
        {
            var m = new WaveModel();
            m.SetWave(5, 5);
            Assert.IsTrue(m.IsLastWave);
        }

        [Test]
        public void MarkWaveCleared_RaisesEvent()
        {
            var m = new WaveModel();
            bool fired = false;
            m.OnWaveCleared += () => fired = true;
            m.MarkWaveCleared();
            Assert.IsTrue(fired);
        }
    }
}
```

- [ ] **Step 3: Run test to verify it fails** — `run_tests`(`WaveModelTests`) → 컴파일 에러(`Progress`/`RemainingEnemies` 미존재).

- [ ] **Step 4: Write implementation**

Replace `Assets/Scripts/Domain/Models/WaveModel.cs`:

```csharp
// 웨이브 진행 상태(현재/전체/남은 적)를 소유·통지하는 도메인 모델
using DefenseDot.Domain;

namespace DefenseDot.Domain.Models
{
    /// <summary>
    /// 웨이브 진행 상태(현재/전체/남은 적)를 소유하고 통지하는 도메인 모델입니다.
    /// </summary>
    public class WaveModel : BaseModel
    {
        private readonly ReactiveProperty<WaveProgress> progress = new ReactiveProperty<WaveProgress>(new WaveProgress(0, 0));
        private readonly ReactiveProperty<int> remaining = new ReactiveProperty<int>(0);

        /// <summary> 웨이브 진행(현재/전체) 상태입니다. (읽기 전용 RP) </summary>
        public IReadOnlyReactiveProperty<WaveProgress> Progress => progress;

        /// <summary> 남은 적 수입니다. (읽기 전용 RP) </summary>
        public IReadOnlyReactiveProperty<int> RemainingEnemies => remaining;

        /// <summary> 현재 웨이브 번호입니다. </summary>
        public int Current => progress.Value.Current;

        /// <summary> 전체 웨이브 수입니다. </summary>
        public int Total => progress.Value.Total;

        /// <summary> 현재 남아있는 적 수입니다. </summary>
        public int Remaining => remaining.Value;

        /// <summary> 마지막 웨이브 여부입니다. </summary>
        public bool IsLastWave => progress.Value.Current >= progress.Value.Total;

        /// <summary> 한 웨이브의 적을 모두 소탕하면 발생합니다. </summary>
        public event System.Action OnWaveCleared;

        /// <summary> 웨이브 단계를 설정하고 강제 통지합니다. </summary>
        public void SetWave(int currentWave, int totalWaves)
        {
            progress.SetValueAndForceNotify(new WaveProgress(currentWave, totalWaves));
        }

        /// <summary> 남은 적 수를 설정하고 통지합니다. (동일값은 생략) </summary>
        public void SetRemaining(int value)
        {
            remaining.Value = value;
        }

        /// <summary> 한 웨이브 소탕을 통지합니다. (소탕 판정은 호출자가 결정) </summary>
        public void MarkWaveCleared()
        {
            OnWaveCleared?.Invoke();
        }
    }
}
```

- [ ] **Step 5: Fix ArenaHudPresenter wave subscription** — `wave.OnWaveChanged`/`wave.OnRemainingChanged` 및 초기 호출을 RP 구독으로 보정:

```csharp
        private System.IDisposable progressSub;
        private System.IDisposable remainingSub;

        // Initialize
        progressSub = wave.Progress.Subscribe(HandleWaveProgress);
        remainingSub = wave.RemainingEnemies.Subscribe(HandleRemaining);

        // Dispose
        progressSub?.Dispose();
        remainingSub?.Dispose();

        private void HandleWaveProgress(DefenseDot.Domain.Models.WaveProgress p)
        {
            view.SetRound(p.Current, p.Total);
        }

        private void HandleRemaining(int alive)
        {
            view.SetEnemies(alive, enemyCapacity);
            view.SetEnemyBar(enemyCapacity > 0 ? (float)alive / enemyCapacity : 0f);
        }
```

기존 `HandleWaveChanged(int,int)`/`HandleRemainingChanged(int)` 삭제.

- [ ] **Step 6: Run test + compile** — `run_tests`(`WaveModelTests`) PASS (5), `read_console` Error 0.

- [ ] **Step 7: Commit** — `lint` 후 승인 시 `commit`(`refactor: WaveModel을 ReactiveProperty로 전환`).

---

## Task 9: CoreModel → ReactiveProperty<HealthState> + ArisTowerVisual 마이그레이션

**Files:**
- Modify: `Assets/Scripts/Domain/Models/CoreModel.cs`
- Modify: `Assets/Scripts/Systems/Mode/ArisTowerVisual.cs`
- Modify: `Assets/Scripts/UI/Presenters/HUDPresenter.cs`(core 구독 보정 — Grid HUD 컴파일 유지)
- Test: `Assets/Tests/EditMode/CoreModelTests.cs` (create)

**Interfaces:**
- Produces: `CoreModel.Health : IReadOnlyReactiveProperty<HealthState>` + 편의 getter `CurrentHp`/`MaxHp`/`HealthRatio`(파생) 유지. `OnCoreDestroyed` event 유지. `Configure/SetCurrent/ApplyDamage` 시그니처 불변.
- Breaking: `event OnHealthChanged` 제거.
- Migrate: `ArisTowerVisual`의 `coreHp.OnHealthChanged += HandleHealthChanged`(float ratio) → `coreHp.Health.Subscribe`(HealthState).
- Unaffected: `AbilityModifiers.cs:39`(`HealthRatio`) — 편의 getter 유지로 무영향.

- [ ] **Step 1: unity-standards 가이드 Read.**

- [ ] **Step 2: Write the failing test**

Create `Assets/Tests/EditMode/CoreModelTests.cs`:

```csharp
using NUnit.Framework;
using DefenseDot.Domain.Models;

namespace DefenseDot.Tests.EditMode
{
    public class CoreModelTests
    {
        [Test]
        public void Configure_FillsToMax()
        {
            var m = new CoreModel();
            m.Configure(50f);
            Assert.AreEqual(50f, m.CurrentHp, 0.0001f);
            Assert.AreEqual(50f, m.MaxHp, 0.0001f);
            Assert.AreEqual(1f, m.HealthRatio, 0.0001f);
        }

        [Test]
        public void ApplyDamage_ReducesHpAndRatio()
        {
            var m = new CoreModel();
            m.Configure(40f);
            m.ApplyDamage(10f);
            Assert.AreEqual(30f, m.CurrentHp, 0.0001f);
            Assert.AreEqual(0.75f, m.HealthRatio, 0.0001f);
        }

        [Test]
        public void Health_NotifiesState()
        {
            var m = new CoreModel();
            m.Configure(40f);
            HealthState got = default;
            m.Health.Subscribe(s => got = s);   // 즉시 (40,40)
            m.ApplyDamage(20f);
            Assert.AreEqual(20f, got.Hp, 0.0001f);
            Assert.AreEqual(0.5f, got.Ratio, 0.0001f);
        }

        [Test]
        public void ApplyDamage_ToZero_RaisesDestroyed()
        {
            var m = new CoreModel();
            m.Configure(10f);
            bool destroyed = false;
            m.OnCoreDestroyed += () => destroyed = true;
            m.ApplyDamage(999f);
            Assert.AreEqual(0f, m.CurrentHp, 0.0001f);
            Assert.IsTrue(destroyed);
        }
    }
}
```

- [ ] **Step 3: Run test to verify it fails** — `run_tests`(`CoreModelTests`) → 컴파일 에러(`Health` 미존재).

- [ ] **Step 4: Write implementation**

Replace `Assets/Scripts/Domain/Models/CoreModel.cs`:

```csharp
// 코어(본진) 체력 상태를 소유·통지하는 도메인 모델
using UnityEngine;
using DefenseDot.Domain;

namespace DefenseDot.Domain.Models
{
    /// <summary>
    /// 코어(본진) 체력 상태를 소유하고 변경·파괴를 통지하는 도메인 모델입니다.
    /// </summary>
    public class CoreModel : BaseModel
    {
        private readonly ReactiveProperty<HealthState> health = new ReactiveProperty<HealthState>(new HealthState(40f, 40f));

        /// <summary> 코어 체력(현재/최대/비율) 상태입니다. (읽기 전용 RP) </summary>
        public IReadOnlyReactiveProperty<HealthState> Health => health;

        /// <summary> 현재 코어 체력입니다. </summary>
        public float CurrentHp => health.Value.Hp;

        /// <summary> 최대 코어 체력입니다. </summary>
        public float MaxHp => health.Value.MaxHp;

        /// <summary> 현재 체력 비율(0~1)입니다. </summary>
        public float HealthRatio => health.Value.Ratio;

        /// <summary> 코어가 파괴(HP 0)되면 발생합니다. </summary>
        public event System.Action OnCoreDestroyed;

        /// <summary> 최대 체력을 설정하고 현재 체력을 가득 채웁니다. </summary>
        public void Configure(float max)
        {
            health.SetValueAndForceNotify(new HealthState(max, max));
        }

        /// <summary> 현재 체력을 절대값으로 설정합니다. (헤드룸 표시용 — 파괴 통지 없음) </summary>
        public void SetCurrent(float value)
        {
            float max = health.Value.MaxHp;
            health.Value = new HealthState(Mathf.Clamp(value, 0f, max), max);
        }

        /// <summary> 코어에 피해를 적용합니다. HP가 0에 도달하면 파괴를 통지합니다. </summary>
        public void ApplyDamage(float amount)
        {
            HealthState current = health.Value;
            if (current.Hp <= 0f) return;
            float hp = Mathf.Max(0f, current.Hp - amount);
            health.Value = new HealthState(hp, current.MaxHp);
            if (hp <= 0f) OnCoreDestroyed?.Invoke();
        }
    }
}
```

- [ ] **Step 5: Migrate ArisTowerVisual**

In `Assets/Scripts/Systems/Mode/ArisTowerVisual.cs`:
- 필드 추가: `private System.IDisposable healthSub;`
- 구독부(현재 line 66-70) 교체:

```csharp
            if (core != null) core.SetCastReceiver(this);
            if (coreHp != null)
            {
                healthSub = coreHp.Health.Subscribe(HandleHealthChanged);
                coreHp.OnCoreDestroyed += HandleCoreDestroyed;
            }
            if (flow != null) flow.OnPhaseChanged += HandlePhaseChanged;
```

- 핸들러(현재 line 127-131) 교체:

```csharp
        private void HandleHealthChanged(DefenseDot.Domain.Models.HealthState state)
        {
            if (animator == null) return;
            animator.SetBool(LowHpHash, state.Ratio <= lowHpRatio);
        }
```

- Unsubscribe(현재 line 155-159) 교체:

```csharp
            if (coreHp != null)
            {
                healthSub?.Dispose();
                coreHp.OnCoreDestroyed -= HandleCoreDestroyed;
            }
```

- [ ] **Step 6: Fix HUDPresenter core subscription** — `core.OnHealthChanged`/`HandleHealthChanged(core.HealthRatio)`를 보정:

```csharp
        private System.IDisposable healthSub;

        // Initialize
        healthSub = core.Health.Subscribe(HandleHealthState);

        // Dispose
        healthSub?.Dispose();

        private void HandleHealthState(DefenseDot.Domain.Models.HealthState s)
        {
            model.CoreHealth = s.Ratio;
            view.UpdateHealth(s.Hp, s.MaxHp, s.Ratio);
        }
```

기존 `HandleHealthChanged(float)` 삭제.

- [ ] **Step 7: Run test + compile + Play 점검**

Run: `run_tests`(`CoreModelTests`) PASS (4), `read_console` Error 0.
Play 점검: Play 진입 → 코어 피격 시 애니메이터 LowHp 동작·코어 파괴 동작 정상(ArisTowerVisual 회귀 없음) 확인.

- [ ] **Step 8: Commit** — `lint` 후 승인 시 `commit`(`refactor: CoreModel을 ReactiveProperty로 전환, ArisTowerVisual 마이그레이션`).

---

## Task 10: Arena HUD 위젯 5종

**Files:**
- Create: `Assets/Scripts/UI/Widgets/GoldWidget.cs`
- Create: `Assets/Scripts/UI/Widgets/ScoreWidget.cs`
- Create: `Assets/Scripts/UI/Widgets/RoundWidget.cs`
- Create: `Assets/Scripts/UI/Widgets/TimeWidget.cs`
- Create: `Assets/Scripts/UI/Widgets/EnemyWidget.cs`

**Interfaces:**
- Consumes: `DefenseDot.UI.Base.UIWidget<T>`, `DefenseDot.Domain.Models.{WaveProgress,TimerState,EnemyState}`
- Produces (namespace `DefenseDot.UI.Widgets`):
  - `GoldWidget : UIWidget<int>` — `SetData(int)`
  - `ScoreWidget : UIWidget<int>` — `SetData(int)`
  - `RoundWidget : UIWidget<WaveProgress>` — `SetData(WaveProgress)`
  - `TimeWidget : UIWidget<TimerState>` — `SetData(TimerState)`
  - `EnemyWidget : UIWidget<EnemyState>` — `SetData(EnemyState)`
- 포맷팅은 기존 `ArenaHudView`의 문자열/비율 규칙을 위젯이 그대로 소유한다(라운드 `"{c} / {t}"`, 시간 `"{ceil}s"`, 골드 `ToString()`, 점수 `"N0"`, 적 `"{a} / {c}"`).

- [ ] **Step 1: unity-standards 가이드 Read.**

- [ ] **Step 2: Write GoldWidget**

```csharp
using UnityEngine;
using TMPro;
using DefenseDot.UI.Base;

namespace DefenseDot.UI.Widgets
{
    /// <summary> 골드 수치를 표시하는 위젯입니다. </summary>
    public sealed class GoldWidget : UIWidget<int>
    {
        [SerializeField] private TextMeshProUGUI valueText;

        /// <summary> 골드 표시를 갱신합니다. </summary>
        public override void SetData(int gold)
        {
            if (valueText != null) valueText.text = gold.ToString();
        }
    }
}
```

- [ ] **Step 3: Write ScoreWidget**

```csharp
using UnityEngine;
using TMPro;
using DefenseDot.UI.Base;

namespace DefenseDot.UI.Widgets
{
    /// <summary> 점수 수치를 표시하는 위젯입니다. </summary>
    public sealed class ScoreWidget : UIWidget<int>
    {
        [SerializeField] private TextMeshProUGUI valueText;

        /// <summary> 점수 표시를 갱신합니다. </summary>
        public override void SetData(int score)
        {
            if (valueText != null) valueText.text = score.ToString("N0");
        }
    }
}
```

- [ ] **Step 4: Write RoundWidget**

```csharp
using UnityEngine;
using TMPro;
using DefenseDot.UI.Base;
using DefenseDot.Domain.Models;

namespace DefenseDot.UI.Widgets
{
    /// <summary> 라운드(현재/전체)를 표시하는 위젯입니다. </summary>
    public sealed class RoundWidget : UIWidget<WaveProgress>
    {
        [SerializeField] private TextMeshProUGUI valueText;

        /// <summary> 라운드 표시를 갱신합니다. </summary>
        public override void SetData(WaveProgress data)
        {
            if (valueText != null) valueText.text = $"{data.Current} / {data.Total}";
        }
    }
}
```

- [ ] **Step 5: Write TimeWidget**

```csharp
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using DefenseDot.UI.Base;
using DefenseDot.Domain.Models;

namespace DefenseDot.UI.Widgets
{
    /// <summary> 남은 시간과 시간바를 표시하는 위젯입니다. </summary>
    public sealed class TimeWidget : UIWidget<TimerState>
    {
        [SerializeField] private TextMeshProUGUI valueText;
        [SerializeField] private Image barFill;

        /// <summary> 시간 표시와 바를 갱신합니다. </summary>
        public override void SetData(TimerState data)
        {
            if (valueText != null) valueText.text = $"{Mathf.CeilToInt(data.Remaining)}s";
            if (barFill != null) barFill.fillAmount = Mathf.Clamp01(data.Ratio);
        }
    }
}
```

- [ ] **Step 6: Write EnemyWidget**

```csharp
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using DefenseDot.UI.Base;
using DefenseDot.Domain.Models;

namespace DefenseDot.UI.Widgets
{
    /// <summary> 생존 적 수와 위험 바를 표시하는 위젯입니다. </summary>
    public sealed class EnemyWidget : UIWidget<EnemyState>
    {
        [SerializeField] private TextMeshProUGUI valueText;
        [SerializeField] private Image barFill;

        /// <summary> 적 수 표시와 위험 바를 갱신합니다. </summary>
        public override void SetData(EnemyState data)
        {
            if (valueText != null) valueText.text = $"{data.Alive} / {data.Capacity}";
            if (barFill != null) barFill.fillAmount = Mathf.Clamp01(data.Ratio);
        }
    }
}
```

- [ ] **Step 7: Verify compile** — `refresh_unity` → `read_console`(Error) 0.

- [ ] **Step 8: Commit** — `lint` 후 승인 시 `commit`(`feat: Arena HUD 위젯 5종 추가`).

---

## Task 11: ArenaHudView → UIView (위젯 조립) + 프리팹 Variant

**Files:**
- Modify: `Assets/Scripts/UI/Views/ArenaHudView.cs`
- Prefab: `Assets/...ArenaHUD_Panel`의 **Variant** 생성(원본 보존), 위젯 컴포넌트 부착·참조 연결

**Interfaces:**
- Consumes: 위젯 5종(`GoldWidget`/`ScoreWidget`/`RoundWidget`/`TimeWidget`/`EnemyWidget`)
- Produces: `ArenaHudView : UIView` — 위젯 접근자
  - `void ApplyGold(int)`, `void ApplyScore(int)`, `void ApplyRound(WaveProgress)`, `void ApplyTime(TimerState)`, `void ApplyEnemies(EnemyState)` (각각 해당 위젯 `SetData` 위임)
- Breaking: `ArenaHudView`가 `HudRoot`(IView, Bind) 상속을 벗어나 `UIView` 상속. `Bind(in HudContext)` 메서드 제거(Presenter 생성은 Task 12에서 UIRoot가 소유).

> **이 태스크부터 `UIRoot`/`GameManager` 결선이 영향**받는다. Task 12와 한 묶음으로 진행해 컴파일 깨짐 구간을 최소화한다(둘을 연속 실행).

- [ ] **Step 1: unity-standards 가이드 Read.**

- [ ] **Step 2: Rewrite ArenaHudView**

Replace `Assets/Scripts/UI/Views/ArenaHudView.cs`:

```csharp
// 아레나 HUD 뷰 — 위젯들을 조립하고 Presenter가 위젯 단위로 Bind한다
using UnityEngine;
using DefenseDot.UI.Base;
using DefenseDot.UI.Widgets;
using DefenseDot.Domain.Models;

namespace DefenseDot.UI.Views
{
    /// <summary>
    /// 아레나 HUD 뷰입니다. 표시 포맷은 각 위젯이 소유하며, View는 위젯 조립과 위임만 합니다.
    /// </summary>
    public sealed class ArenaHudView : UIView
    {
        [SerializeField] private GoldWidget goldWidget;
        [SerializeField] private ScoreWidget scoreWidget;
        [SerializeField] private RoundWidget roundWidget;
        [SerializeField] private TimeWidget timeWidget;
        [SerializeField] private EnemyWidget enemyWidget;

        /// <summary> 골드 위젯을 갱신합니다. </summary>
        public void ApplyGold(int gold)
        {
            if (goldWidget != null) goldWidget.SetData(gold);
        }

        /// <summary> 점수 위젯을 갱신합니다. </summary>
        public void ApplyScore(int score)
        {
            if (scoreWidget != null) scoreWidget.SetData(score);
        }

        /// <summary> 라운드 위젯을 갱신합니다. </summary>
        public void ApplyRound(WaveProgress progress)
        {
            if (roundWidget != null) roundWidget.SetData(progress);
        }

        /// <summary> 시간 위젯을 갱신합니다. </summary>
        public void ApplyTime(TimerState time)
        {
            if (timeWidget != null) timeWidget.SetData(time);
        }

        /// <summary> 적 위젯을 갱신합니다. </summary>
        public void ApplyEnemies(EnemyState enemies)
        {
            if (enemyWidget != null) enemyWidget.SetData(enemies);
        }
    }
}
```

- [ ] **Step 3: Prefab Variant 생성 + 위젯 부착**

Unity에서 (MCP 또는 에디터):
1. 기존 `ArenaHUD_Panel` 프리팹의 **Variant**를 생성(`ArenaHUD_Panel_V2`) — 원본 보존.
2. Variant에서 각 value 행 오브젝트에 위젯 컴포넌트를 추가하고 TMP/Image 참조를 연결:
   - 골드 행 → `GoldWidget.valueText`
   - 점수 행 → `ScoreWidget.valueText`
   - 라운드 행 → `RoundWidget.valueText`
   - 시간 행 → `TimeWidget.valueText` + `barFill`(timeBarFill)
   - 적 행 → `EnemyWidget.valueText` + `barFill`(enemyBarFill)
3. Variant 루트의 `ArenaHudView`에 위젯 5개를 연결. 모든 TMP는 neodgm SDF 폰트 유지.

검증: `mcp__UnityMCP__manage_prefabs`/`manage_gameobject`로 컴포넌트·참조 누락 0 확인.

- [ ] **Step 4: 컴파일은 Task 12에서 함께 검증** (이 시점 `ArenaHudPresenter`/`UIRoot`가 옛 `Bind`를 참조해 깨짐 — 정상. Task 12로 즉시 진행).

- [ ] **Step 5: (커밋 보류)** — Task 12 완료 후 함께 커밋.

---

## Task 12: ArenaHudPresenter → UIPresenter + UIRoot 소유 + 중간 Model/잔재 삭제

**Files:**
- Modify: `Assets/Scripts/UI/Presenters/ArenaHudPresenter.cs`
- Modify: `Assets/Scripts/UI/InGame/UIRoot.cs`
- Delete: `Assets/Scripts/UI/Models/ArenaHudModel.cs`
- Modify: `Assets/Scripts/UI/Presenters/BasePresenter.cs`(빈 `UI.BaseModel` 제거)
- Modify: `Assets/Scripts/UI/Views/HudRoot.cs` 또는 `ArenaHudView` 씬 참조 정리

**Interfaces:**
- Consumes: `UIPresenter<ArenaHudView>`, 도메인 모델 RP(`Economy.Gold`, `Score.Score`, `Wave.Progress`, `Wave.RemainingEnemies`, `Timer.Time`), `EnemyState`
- Produces: `ArenaHudPresenter : UIPresenter<ArenaHudView>` — ctor `(ArenaHudView view, EconomyModel economy, ScoreModel score, WaveModel wave, RoundTimerModel timer, int enemyCapacity)`; `OnInitialize`에서 위젯별 `Bind`.
- Produces: `UIRoot`가 `ArenaHudView`를 직접 참조하고 `ArenaHudPresenter`를 생성·소유한다(`HudRoot.Bind` 자기설치 폐지).

- [ ] **Step 1: unity-standards 가이드 Read.**

- [ ] **Step 2: Rewrite ArenaHudPresenter**

Replace `Assets/Scripts/UI/Presenters/ArenaHudPresenter.cs`:

```csharp
// Arena HUD 프레젠터 — 도메인 RP를 위젯에 Bind
using DefenseDot.UI.Base;
using DefenseDot.UI.Views;
using DefenseDot.Domain.Models;

namespace DefenseDot.UI.Presenters
{
    /// <summary>
    /// 아레나 HUD 프레젠터입니다. Economy/Score/Wave/RoundTimer RP를
    /// 라운드·시간·골드·점수·적 위젯에 Bind합니다.
    /// </summary>
    public sealed class ArenaHudPresenter : UIPresenter<ArenaHudView>
    {
        private readonly EconomyModel economy;
        private readonly ScoreModel score;
        private readonly WaveModel wave;
        private readonly RoundTimerModel timer;
        private readonly int enemyCapacity;

        /// <summary> 구독할 도메인 모델과 적 수용 한계를 주입받습니다. </summary>
        public ArenaHudPresenter(ArenaHudView view, EconomyModel economy, ScoreModel score,
            WaveModel wave, RoundTimerModel timer, int enemyCapacity) : base(view)
        {
            this.economy = economy;
            this.score = score;
            this.wave = wave;
            this.timer = timer;
            this.enemyCapacity = enemyCapacity;
        }

        /// <summary> 도메인 RP를 위젯에 바인딩합니다. </summary>
        protected override void OnInitialize()
        {
            Bind(economy.Gold, view.ApplyGold);
            Bind(score.Score, view.ApplyScore);
            Bind(wave.Progress, view.ApplyRound);
            Bind(timer.Time, view.ApplyTime);
            Bind(wave.RemainingEnemies, HandleRemaining);
        }

        private void HandleRemaining(int alive)
        {
            view.ApplyEnemies(new EnemyState(alive, enemyCapacity));
        }
    }
}
```

- [ ] **Step 3: UIRoot가 ArenaHudView 직접 참조·Presenter 생성**

Modify `Assets/Scripts/UI/InGame/UIRoot.cs`:
- `[SerializeField] private HudRoot hud;` → `[SerializeField] private ArenaHudView arenaHud;` (씬 참조 재연결 필요)
- `Inject` 내 `if (hud != null) presenters.Add(hud.Bind(ctx));` → 직접 생성:

```csharp
            if (arenaHud != null)
                presenters.Add(new ArenaHudPresenter(arenaHud, ctx.Economy, ctx.Score,
                    ctx.Wave, ctx.Timer, ctx.EnemyCapacity));
```

(나머지 `TowerBuild`/`GameResult`/`CardSelection` 생성부는 그대로.)

- [ ] **Step 4: 잔재 삭제·정리**
- `Assets/Scripts/UI/Models/ArenaHudModel.cs` 삭제(+ `.meta`).
- `Assets/Scripts/UI/Presenters/BasePresenter.cs`에서 빈 `public abstract class BaseModel` 제거(`IView`는 Grid HUD가 아직 사용하므로 유지). `using System;`은 유지.
- `ArenaHudView`가 더 이상 `HudRoot`를 상속하지 않으므로, `HudRoot`는 Grid HUD(`HUDView`)만 사용. `HudRoot` 자체는 보존(Grid HUD 후속용).

> 주의: `IView`/`BaseModel`을 동시에 지우면 `BasePresenter<TView,TModel>`/`HUDPresenter`/`HUDModel`이 깨진다. 이번 범위는 Arena만이므로 `BaseModel`(빈 UI 모델)만 제거하고, Grid HUD 잔재(`HUDModel`/`HUDView`/`HUDPresenter`)는 비범위로 **유지**한다(Task 13에서 안내).
> `HUDModel : BaseModel`(UI.BaseModel) 이었으므로, `UI.BaseModel` 제거 시 `HUDModel`이 깨진다. → 이번엔 `UI.BaseModel`을 **삭제하지 않고**, `ArenaHudModel`만 삭제한다. `UI.BaseModel` 최종 제거는 Grid HUD 이전(Task 13) 시 수행한다.

**[정정] Step 4 확정 동작:**
- 삭제: `ArenaHudModel.cs`만.
- 유지(비범위): `UI.BaseModel`, `HUDModel`, `HUDView`, `HUDPresenter`, `HudRoot`, Grid 하위 `*View` 4종.

- [ ] **Step 5: 씬 참조 재연결**

씬의 `UIRoot` 컴포넌트에서 `arenaHud` 슬롯에 `ArenaHUD_Panel_V2`(Task 11) 루트의 `ArenaHudView`를 연결. (구 `hud` 슬롯 대체)

- [ ] **Step 6: Verify compile + Play**

Run: `refresh_unity` → `read_console`(Error) 0.
Play 검증: Play 진입 →
- 골드/점수/라운드/시간/적 표시가 초기값부터 정상 반영(Bind 즉시통지).
- 적 처치 시 골드·점수·적 수 갱신.
- **카드 선택(timeScale=0) 중에도 표시 정합** + 카드 종료 후 시간바 진행.
- 게임 재시작 시 값 초기화 정상(ForceNotify).

- [ ] **Step 7: Commit** — `lint` 후 승인 시 `commit`(`refactor: ArenaHud를 UIPresenter/위젯 Bind 구조로 이전, 중간 Model 제거`). Task 11 변경분 포함.

---

## Task 13: 전체 검증 + Grid HUD 후속 안내

**Files:**
- (검증 전용; 코드 변경 없음)

- [ ] **Step 1: 전체 EditMode 테스트**

Run: `mcp__UnityMCP__run_tests`(mode `EditMode`, 전체).
Expected: 신규/수정 포함 전부 PASS. 특히 `ReactivePropertyTests`, `EconomyModelTests`, `ScoreModelTests`, `RoundTimerModelTests`, `WaveModelTests`, `CoreModelTests`.

- [ ] **Step 2: 컴파일 0 에러/경고**

Run: `mcp__UnityMCP__read_console`(Error/Warning). Expected: 신규 에러·경고 0.

- [ ] **Step 3: Play 통합 검증 (Critical Path)**

| 시나리오 | Expected |
|---|---|
| 게임 시작 | HUD 5요소 초기값 표시(Bind 즉시통지) |
| 적 처치 | 골드↑·점수↑·적수 갱신 |
| 라운드 진행 | 시간바 감소, 라운드 표기 갱신 |
| 카드 선택(timeScale=0) | HUD 정합 유지, 종료 후 정상 |
| 코어 피격 | ArisTowerVisual LowHp/파괴 동작 정상 |
| 재시작 | 모든 값 초기화 |

- [ ] **Step 4: Grid HUD 후속 안내(비범위 명시)**

`HUDView`/`HUDPresenter`/`HUDModel`/`UI.BaseModel`/`HudRoot`/하위 `*View` 4종은 이번 범위에서 제외했다(Arena가 활성 HUD). Grid HUD를 동일 구조로 이전하려면 후속 plan에서:
1. `HealthWidget : UIWidget<HealthState>` 신설(현재 `HealthView` 대체).
2. `GoldView`/`RoundView`/`EnemyCountView`를 위젯으로 승격(또는 Arena 위젯 재사용).
3. `HUDView : UIView`, `HUDPresenter : UIPresenter<HUDView>`로 이전.
4. `UI.BaseModel`/`HUDModel` 삭제, `HudRoot`/`IView` 제거(전 사용처 이전 후).

- [ ] **Step 5: 완료 보고** — `superpowers:verification-before-completion`로 증거(테스트 출력·Play 결과) 첨부 후 완료 선언.

---

## Self-Review

**1. Spec coverage (스펙 §3~§6 대조):**
- §3.1 UI 계층(UIObject/UIWidget/UIWidget<T>/UIView/UIPresenter/UIRoot) → Task 3·4·11·12 ✅
- §3.2 Model: constructor-DI 유지·BaseModel 통일(부분: UI.BaseModel은 Grid 잔존으로 Task13 이월, 근거 명시)·RP 단일 스칼라·struct 래퍼·ForceNotify → Task 5~9 ✅
- §3.3 Bind: (RP,Action) 토큰·OnShow 재반영·Initialize 재진입 가드 → Task 4 ✅
- §3.4 enum(UIDepth/UIInitType) → Task 3 ✅
- §4 HUD 매핑(위젯 6종 중 Health 제외 5종=Arena)·중간 Model 제거·포맷 위젯 소유 → Task 10·11·12 ✅ (HealthWidget은 Grid 전용 → Task 13 후속)
- §5 검증 반영 12항 → 설계 결정 표·각 Task에 반영 ✅
- §6 마이그레이션 순서(베이스→Model RP→위젯/Variant→Presenter/UIRoot→검증) → Task 1~13 순서 일치 ✅

**2. Placeholder scan:** "TBD/적절히/등" 류 없음. 각 코드 스텝 완전 코드 기재 ✅. (Prefab Variant 단계는 코드가 아닌 에디터 작업이라 절차로 기술)

**3. Type consistency:**
- `IReadOnlyReactiveProperty<T>.Subscribe → IDisposable` 일관(Task1) ↔ `Bind`(Task4) ↔ 모델 노출(Task5~9) ✅
- `SetData(T)` 위젯 메서드명 일관(Task3 정의 ↔ Task10 구현 ↔ Task11 호출) ✅
- struct 필드명(`WaveProgress.Current/Total`, `TimerState.Remaining/Duration/Ratio`, `HealthState.Hp/MaxHp/Ratio`, `EnemyState.Alive/Capacity/Ratio`) Task2 정의 ↔ Task7~12 사용 일치 ✅
- `ArenaHudPresenter` ctor 인자 순서(view, economy, score, wave, timer, enemyCapacity) Task12 정의 ↔ UIRoot 호출 일치 ✅

**보정 사항(자기검토 반영):** Task 12에서 `UI.BaseModel` 즉시 삭제가 `HUDModel`을 깨뜨리는 모순을 발견 → `ArenaHudModel`만 삭제하고 `UI.BaseModel` 최종 제거는 Grid HUD 이전(Task 13 후속)으로 이월하도록 정정함.
