# A4 인게임 강화 — 설계 스펙

> **네이밍·구조 최종 반영 (2026-07-09)** — 구현 확정본은 본문과 아래가 다르다(본문은 설계 시점 이름 유지). 최종 상태는 코드 및 `docs/tasks/active/TASK-012` 참조.
> - 비용 계산: `EnhanceCostCalculator`(정적) → **`AbilityCostExtensions`(확장 메서드 `ability.UpgradeCost(config)`)**
> - 서비스: `AbilityEnhancer` → **`AbilityUpgradeService`** (메서드 `TryUpgrade`/`Dismiss`)
> - SO: `EnhanceCostConfig` → **`AbilityUpgradeConfig`** (필드 `deleteRefundRatio` → `refundRatio`)
> - 인터페이스: `ICardCommandTarget` → **`IAbilityCommandTarget`**
> - UI 행: `AbilityUpgradeRow`는 **`UIWidget<AbilityUpgradeRowData>`(SetData)** 로 승격

**작성일**: 2026-07-08
**상태**: 설계 승인됨 (구현 계획 대기)
**출처**: TASK-012 A4 · 원작 `dot-defense` v608 `actEnhance`/`actDelete`
**짝 HTML 보고서**: `scratchpad/A4-design.html` (텔레그램 전송본, message_id 743)

---

## 1. 목표 · 범위

원작 성장 3단(획득 → 강화 → 조합) 중 **②강화 축**을 채운다. 골드로 보유 능력을 레벨업(강화)하거나, 불필요한 능력을 삭제(강화분 일부 환불)한다.

**확정된 결정**
- **UI 범위 (b)** — 로직 + "실제 작동하는" 최소 UI. 도크 치장·상시 레이아웃은 B(HUD 리스킨)로 미룸.
- **로직 배치 (B)** — POCO 서비스 `AbilityEnhancer` (애플리케이션/도메인 서비스 + UI Facade). Mediator 아님.
- **비용 파라미터 SO 2군데** — 능력별 `baseCost`(AbilityData) + 아레나 전역 `EnhanceCostConfig`(4필드).

**비범위(이번 제외)**
- 도크 비주얼/네온/칩 그리드/상시 표시 레이아웃 (→ B)
- 할인원(scholar 패시브·유물) 실제 구현 (→ A7). `maxDiscountRate`는 손잡이만 심어둠.
- 랜덤 획득 비용(`randomAcquireCost`) 재현 (→ 별도, A4 범위 아님)

---

## 2. 아키텍처

기존 경제·능력 시스템 위에 **서비스 1 + SO 1 + 순수함수 1 + UI 1쌍**을 얹는다. 새 도메인 모델 없음.

```
        최소 UI (b):  AbilityEnhanceView  ◀─bind─  AbilityEnhancePresenter
                                    │ 질의/명령 (Facade)
                         ┌──────────▼───────────┐
                         │     AbilityEnhancer    │  ← 서비스 계층 (POCO)
                         └───┬─────────┬──────┬───┘
              TrySpend/AddGold│         │      │LevelUpAbility/RemoveAbility
                 ┌────────────▼┐ ┌──────▼────┐ ▼─────────────────┐
                 │ EconomyModel │ │EnhanceCost│ │ CoreAbilitySystem │
                 │  (골드 RP)   │ │Config(SO) │ │ (Loadout/Runner)  │
                 └─────────────┘ └───────────┘ └───────────────────┘
                        비용 계산 = EnhanceCostCalculator(순수함수) · baseCost ← AbilityData
```

| 레이어 | 책임 | 신규/기존 |
|---|---|---|
| UI | 슬롯 목록·강화/삭제 버튼·골드 반응 | 신규 |
| 서비스 | 비용·골드·레벨업/삭제 유스케이스 조율 | 신규 |
| SO/데이터 | 비용 파라미터·baseCost·acquiredRound | 신규 + 필드 |
| 경제/능력 | 골드 차감·능력 레벨업/제거 | 기존 재사용 |

---

## 3. SO · 데이터 변경

### 3.1 `AbilityData.baseCost` (수정)
`AbilityData`에 `public int baseCost = 30;` 추가 (`<summary>` 필수). 능력별 강화 기본 비용.

### 3.2 `AbilityInstance.acquiredRound` (수정)
`public int acquiredRound = 1;` 추가. 획득 라운드(강화비 스케일 기준). 스타터는 기본값 1.

### 3.3 `EnhanceCostConfig` (신규 SO)
`Assets/Scripts/Systems/Economy/EnhanceCostConfig.cs` — `[CreateAssetMenu]`, 아레나 모드 전역 1개.

| 필드 | 기본값 | 의미 |
|---|---|---|
| `levelSlope` | 0.10 | 레벨당 가격 배율 가산 |
| `roundInflation` | 0.05 | 획득 라운드당 배율 가산 |
| `maxDiscountRate` | 0.55 | 누적 최대 할인 상한 (할인원 붙기 전 비활성) |
| `deleteRefundRatio` | 0.40 | 삭제 시 강화비 환급률 |

에셋 1개 생성: `Assets/Data/.../EnhanceCostConfig.asset` (경로는 구현 시 확정).

---

## 4. 비용 · 환불 공식

`Assets/Scripts/Systems/Economy/EnhanceCostCalculator.cs` (신규, 순수 함수). 원작 `enhanceCost`(index.html:21900) 이식.

```
lvScale  = (level + 1) + level × levelSlope
roundMul = 1 + (acquiredRound - 1) × roundInflation
costMul  = max(1 - maxDiscountRate, discountStack)     // 현재 discountStack = 1
Cost     = ceil( baseCost × lvScale × roundMul × costMul )

Refund   = Σ (lv = 1 .. level-1) ceil( CostAtLevel(lv) × deleteRefundRatio )
```

**핵심 단순화**: 비용은 인스턴스에 **박제된 `acquiredRound`**로 계산된다(현재 라운드 아님). 따라서 계산기·서비스는 `ICombatState`가 필요 없다 — 라운드는 획득 시점에만 참조.

---

## 5. 서비스 계층 — AbilityEnhancer

`Assets/Scripts/Systems/Economy/AbilityEnhancer.cs` (신규). 본질 = 유스케이스 조율자 + UI Facade.

```
ctor(ICardCommandTarget core, EconomyModel economy, EnhanceCostConfig config)

// 질의 (UI 바인딩용)
int  GetEnhanceCost(AbilityInstance a)
bool IsMaxLevel(AbilityInstance a)      // a.level >= a.data.maxLevel
bool CanEnhance(AbilityInstance a)      // !Max && economy.CanAfford(cost)
int  GetRefund(AbilityInstance a)

// 명령
bool TryEnhance(AbilityInstance a):
    if (IsMaxLevel(a)) return false;                 // ★ 차감 전 MAX 가드
    if (!economy.TrySpend(GetEnhanceCost(a))) return false;
    core.LevelUpAbility(a); return true;
void Delete(AbilityInstance a):
    economy.AddGold(GetRefund(a));
    core.RemoveAbility(a);
```

**MAX 가드 순서**: `LevelUpAbility`는 MAX에서 조용히 무효화되므로 반드시 **차감 전에** `IsMaxLevel`을 검사해 헛돈 차감을 막는다.

---

## 6. 커맨드 대상 확장 + 삭제 동기화

`ICardCommandTarget`에 `void RemoveAbility(AbilityInstance instance);` 추가. `CoreAbilitySystem` 구현:

```
public void RemoveAbility(AbilityInstance inst):
    if (inst?.data is ActiveAbilityData) runner?.Unequip(inst);   // 액티브 언장착
    loadout?.Remove(inst);                                        // 패시브면 내부 보정 재계산
```

`AddAbility`가 `runner.Equip`하는 것의 **대칭**. 액티브 삭제 시 러너 언장착을 빠뜨리면 라이프사이클이 어긋난다.

---

## 7. acquiredRound 기록 + 로드아웃 변경 통지

**(가) 획득 라운드 박제** — `CoreAbilitySystem.AddAbility`에서 현재 라운드(`loadout.Modifiers.combatState?.Round ?? 1`)를 추가된 인스턴스에 기록. 스타터는 `Setup`의 `TryAdd` 경로라 기본값 1 유지.

**(나) UI 갱신 트리거** — `AbilityLoadout`에 `public event System.Action OnChanged;` 추가, `TryAdd`/`LevelUp`/`Remove` 말미에서 발화. 카드로 레벨업돼도 강화 패널이 즉시 반영되도록.

**UI 2채널 구독** — 구조 변경은 `Loadout.OnChanged`(행 재구성), 골드 변화는 `EconomyModel.Gold` RP(버튼 활성/라벨). 채널 분리로 과도 갱신 방지.

---

## 8. 최소 UI 구성 (b)

기존 UI 아키텍처(UIView/Presenter + ReactiveProperty) 위에 얹는다.

- **AbilityEnhanceView** — 능력 슬롯 세로 목록. 행 = 아이콘 + 이름 + `Lv` + [강화 (비용)G] + [삭제]. 폰트 **neodgm SDF**.
- **AbilityEnhancePresenter** — `Loadout.OnChanged`→행 재구성, `Gold`→버튼 활성/라벨. 버튼 클릭→`enhancer.TryEnhance`/`Delete`.
- **MAX 표시** — `IsMaxLevel`이면 강화 버튼 라벨 "MAX"(조합 대기), 비활성.
- **상호작용 게이트** — 카드 선택 모달 활성 중·비플레이 상태에선 강화/삭제 비활성 (원작 꼼수 방지 대응).

---

## 9. baseCost 매핑 (기존 7 에셋)

| 에셋 | 타입 | 원작 대응 | baseCost(초안) |
|---|---|---|---|
| Ability_Shot | 액티브 | 샷 | 30 |
| Ability_Orbital | 액티브 | 오비탈 | 60 |
| Ability_AreaWave | 액티브 | 노바/포격 계열 | 55 * |
| Passive_Onslaught / Press / Cull / Awaken | 패시브 | 순수 데미지 패시브(분노 계열) | 60 * |

`*` = 원작 1:1 대응 없어 유사 능력 기준 초안. 구현 단계 플레이 밸런싱으로 확정.

---

## 10. 테스트 시나리오 (EditMode · TDD)

| # | 대상 | 시나리오 · 기대 |
|---|---|---|
| 1 | Calculator | 레벨↑→lvScale 증가 / 획득라운드↑→roundMul 증가 (공식대로) |
| 2 | Calculator | 환불 = 레벨1~직전 강화비 합 × 0.40 (레벨별 ceil) 일치 |
| 3 | Enhancer | 골드 충분 → 차감 + level+1 성공 |
| 4 | Enhancer | 골드 부족 → 차감·레벨업 없음, false |
| 5 | Enhancer | MAX에서 강화 → 헛돈 차감 없이 차단, false |
| 6 | Enhancer | 삭제 → 정확한 환불 가산 + 로드아웃 제거 |
| 7 | CoreAbilitySystem | 액티브 삭제 → 러너 `Unequip` 호출(모의) |
| 8 | AddAbility | 늦게 획득한 능력일수록 강화비 큼(acquiredRound 반영, 단조 증가) |

Calculator·Enhancer·Loadout은 순수 C#/모의 주입으로 **EditMode 자동 테스트**. UI 반응·게이트는 PlayMode 수동 검증.

---

## 11. 신규 · 수정 파일 요약

**신규**
- `Systems/Economy/EnhanceCostConfig.cs` — 4필드 SO
- `Systems/Economy/EnhanceCostCalculator.cs` — 비용·환불 순수 함수
- `Systems/Economy/AbilityEnhancer.cs` — 서비스(질의/명령)
- `UI/.../AbilityEnhanceView.cs` · `AbilityEnhancePresenter.cs` — 최소 UI 1쌍
- `Data/.../EnhanceCostConfig.asset` — 설정 에셋

**수정**
- `Abilities/AbilityData.cs` — `baseCost` 필드
- `Abilities/AbilityInstance.cs` — `acquiredRound` 필드
- `Abilities/ICardCommandTarget.cs` — `RemoveAbility` 추가
- `Abilities/CoreAbilitySystem.cs` — `RemoveAbility` 구현 + `AddAbility`에서 acquiredRound 박제
- `Abilities/AbilityLoadout.cs` — `OnChanged` 이벤트
- `Mode/ArenaBootstrap(합성 루트)` — Enhancer·Presenter 배선 + Config 참조
- `Data/Abilities/*.asset (7)` — baseCost 값 설정

**변경 없음**: `EconomyModel`·`CoreAbilitySystem` 핵심 로직(호출/필드만 추가). 새 도메인 모델 없음.

---

## 12. 리스크 · 유의

- **밸런스**: `baseCost` 실값·환불율은 플레이 밸런싱 필요 (구현 단계).
- **A5-1 조합 접점**: MAX 처리 방식을 조합 설계와 맞춰둘 것("MAX(조합 대기)" 표기 유지).
- **합성 루트**: `EnhanceCostConfig` 에셋 참조를 ArenaBootstrap 직렬화 필드로 주입.
- **세이브/로드 무관**: 인런 상태라 런 종료 시 소멸.
