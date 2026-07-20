# TASK-021: 합성(계보) UI 가독성 — 플레이어가 계보를 읽을 수 없음

**작성일**: 2026-07-20
**상태**: 문제 등록 (설계 전)
**우선순위**: 높음 (계보 시스템이 작동해도 플레이어에게 전달되지 않음)

---

## 1. 문제 정의

계보 합성 시스템은 코드·데이터상 정상 작동하나(TASK-017·018 검증 완료), **플레이어가 그 존재와 규칙을 알 방법이 없다.**

사용자 제기 3가지:

| # | 모르는 것 | 결과 |
|---|---|---|
| 1 | 이 카드가 **합성으로 얻는 능력**이라는 사실 | 일반 신규 카드와 구분되지 않음 |
| 2 | 지금 가진 능력이 **무엇의 재료**인지 | 어떤 능력을 모아야 할지 판단 불가 → 계보 등반이 우연에 의존 |
| 3 | 합성 카드를 고르면 **재료 2개가 사라진다**는 사실 | 예상 못 한 능력 상실. 강해지려고 골랐는데 보유 능력이 줄어듦 |

---

## 2. 현재 상태 (코드 근거)

### 2.1 카드 표시가 만드는 정보

`Assets/Scripts/Systems/Cards/CardPresentation.cs:19-38`

```csharp
string kind = passive ? "[ 패시브 ]" : "[ 액티브 ]";
string body = c.applyType == CardApplyType.Level
    ? $"Lv{c.fromLevel} > Lv{c.toLevel}"
    : (string.IsNullOrEmpty(d.description) ? d.displayName : d.description);
```

- **합성 카드도 `[ 액티브 ]`로만 표시**된다. `CardApplyType.Fuse` 를 구분하지 않는다.
- 본문은 결과 능력의 `description` 뿐 — **재료가 무엇인지, 소진된다는 사실이 어디에도 없다.**

### 2.2 데이터는 이미 있으나 쓰이지 않음

`Assets/Scripts/Systems/Cards/Card.cs`

`Card` 구조체는 합성 카드일 때 `materialA`·`materialB` 를 이미 들고 있다(`FusionSystem.CollectOffers` 가 채움). 즉 **표시에 필요한 정보는 이미 카드 안에 있고, `CardPresentation` 이 쓰지 않을 뿐이다.**

### 2.3 유일한 시각적 구분

`Assets/Scripts/UI/Widgets/CardSlotWidget.cs:34` — `SetTierStyle(CardTierSet.TierStyle)`

`CardTier.Fusion` 이 티어 스타일(색·테두리 수준)로만 전달된다. 그 스타일이 실제로 얼마나 구분되는지, 플레이어가 "합성"이라는 의미로 읽을 수 있는지는 미확인.

### 2.4 계보 전체를 보는 화면 없음

플레이 중 계보 구조(무엇 + 무엇 → 무엇)를 확인할 수 있는 UI가 없다. 계보는 `FusionRecipeSet` 에셋과 에디터 툴(`DefenseDot/Fusion Lineage Editor`)에만 존재하며 **런타임 노출 경로가 전무하다.**

---

## 3. 결정이 필요한 항목 (설계 전 — 미결)

아래는 답을 정하지 않고 질문만 기록한다.

- **카드에 얼마나 담을 것인가** — 재료 이름 표기 / 아이콘 표기 / 소진 경고 문구 / 셋 다
- **재료 힌트를 어디서 줄 것인가** — 능력 슬롯(HUD)에 "○○의 재료" 표시 / 카드에만 / 별도 화면
- **계보 화면을 만들 것인가** — 만든다면 진입 지점(일시정지 중? 카드 선택 중?)
- **소진 고지의 강도** — 문구만 / 재료 아이콘에 취소선 / 선택 시 확인 단계
- **원작은 어떻게 하는가** — `Assets/Reference/dot-defense-main/index.html` 의 조합 카드 표시(`action: 'combo'`, `parts` 보유)를 먼저 대조할 것

---

## 4. 참고

- 합성 판정·생성·적용: `Assets/Scripts/Systems/Cards/FusionSystem.cs`
- 카드 표시 변환: `Assets/Scripts/Systems/Cards/CardPresentation.cs`
- 카드 슬롯 위젯: `Assets/Scripts/UI/Widgets/CardSlotWidget.cs`
- 계보 데이터: `Assets/Data/Fusion/Aris_FusionLineage.asset` (레시피 4건)
- 원작 조합 카드 생성: `index.html:21524-21538` (`choices.push({ action: 'combo', id, parts, tier })`)
- 관련: TASK-017 계보 비주얼 에디터(에디터 전용), TASK-018 계보 능력 세부(스탯·VFX)

---

## 5. 비고

능력별 VFX·아이콘이 아직 복제본이라(TASK-018 잔여) **카드 아이콘으로도 구분이 안 되는 상태**다. 이 TASK와 TASK-018은 "플레이어가 능력을 식별할 수 있는가"라는 같은 문제의 양면이므로, 착수 시 함께 볼 것.
