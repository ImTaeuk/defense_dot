# A5-1 능력 합성(Fusion) — 설계 스펙

**작성일**: 2026-07-10
**상태**: 설계 승인됨 (구현 계획 대기)
**출처**: TASK-012 A5-1 · 원작 `dot-defense` `COMBO_RECIPES`/`findAvailableCombos`
**용어**: 코드 어휘 = **Fusion(융합)**, 플레이어 노출 텍스트 = **"합성"**. (원작은 "조합/combo" — 재료 소진→생성 의미상 합성이 정확)

---

## 1. 목표 · 범위

원작 성장 3단(획득 → 강화 → 합성) 중 **③ 합성**을 추가한다. 능력을 **MAX까지 키운 뒤 재료 2개를 소진해 상위 능력 1개로 합친다**. A4에서 남긴 "MAX(합성 대기)" 상태가 실제 의미를 갖는다(합성 = 맥스된 능력의 소비처).

**확정된 결정**
- **범위**: 합성 **메커니즘 + 타워별 계보 구조 + 소수 데모 레시피**. 능력 대량 확장·다단계 진화(트리플/8돌파)는 후속.
- **캐릭터층 = 기존 타워 분리**: `ArenaModeBootstrap.centerTowerData`("추후 선택 UI 주입점")를 활용. A7 가챠 없이 **타워(캐릭터)별 합성 루트** 성립. 원작 가챠 캐릭터 시그니처 라인 ↔ 우리 타워별 계보 테이블(동형).
- **계보 데이터 위치**: `TowerData` 확장(**데이터만** 보유). **합성 로직은 전부 분리된 서비스**(SRP — TowerData엔 메서드 없음).
- **레시피 데이터**: 신규 `FusionRecipeSet` SO. 능력을 **`AbilityData` 오브젝트 참조**로(원작 문자열 id → 오타 위험 제거).
- **디자이너 도구**: 커스텀 PropertyDrawer("A + B → C" 아이콘·이름 표시) + `OnValidate` 검증.
- **결과 능력**: 기존 효과 타입 재사용(강한 파라미터 `AbilityData` 자산). 일반 카드 풀에서 제외.
- **starterAbilities 이관**: `ArenaModeBootstrap`에서 `TowerData`로 이관("타워=캐릭터 정의" 완성).

**비범위(이번 제외)**
- 다단계 진화(트리플 tier3 / 8돌파 tier4) — `FusionRecipe` 구조에 확장 여지만 남김
- 타워 선택 UI (계보는 데이터로 준비, 선택 UI는 별도 트랙)
- 능력 로스터 대량 신규 제작

---

## 2. 원작 근거 (index.html)

- **합성은 자동 아님 — 카드 옵션** (`:16991`): "조합 자동 형성은 제거됨. 두 재료가 모두 max 도달 시 다음 카드 모달에 '조합' 옵션으로 등장."
- **등장 조건** `findAvailableCombos` (`:21391`): 재료 2개 보유 + **둘 다 isAtMax** + 결과 미보유.
- **적용** (`:21837`): 재료 2개 소진(splice) → 결과 능력 Lv1 생성(획득 라운드 기록).
- **캐릭터별 고정 루트** (`COMBO_RECIPES :16884~`): 전역 베이스 조합 + 가챠 캐릭터 시그니처 라인(기본조합→4돌파 tier3→8돌파 tier4).

---

## 3. 데이터 구조

### 3.1 `TowerData` (수정 · 데이터만)
```csharp
public FusionRecipeSet fusionLineage;          // 이 타워의 합성 계보
public List<AbilityData> starterAbilities;     // 이관: 이 타워의 시작 능력
```
메서드 없음(순수 데이터 SO 유지).

### 3.2 `FusionRecipeSet` (신규 SO)
`Assets/Scripts/Systems/Cards/FusionRecipeSet.cs`
```csharp
[CreateAssetMenu(menuName = "DefenseDot/Fusion Recipe Set")]
public sealed class FusionRecipeSet : ScriptableObject
{
    public List<FusionRecipe> recipes;
    // OnValidate(): null · 자기합성(A==B) · 결과==재료 · 중복 레시피 경고
}

[System.Serializable]
public struct FusionRecipe
{
    public AbilityData materialA;   // 재료 A
    public AbilityData materialB;   // 재료 B
    public AbilityData result;      // 결과 (일반 풀 제외)
}
```

### 3.3 `FusionRecipeSetDrawer` (신규 Editor)
`Assets/Scripts/Systems/Cards/Editor/FusionRecipeSetDrawer.cs`
- 각 레시피를 한 줄 `[아이콘 A] materialA + [아이콘 B] materialB → [아이콘 C] result`로 렌더(`AbilityData.icon`/`displayName`).
- ReorderableList 요소 헤더도 "A + B → C".

---

## 4. 로직 (분리된 서비스)

### 4.1 `FusionResolver` (신규 POCO)
`Assets/Scripts/Systems/Cards/FusionResolver.cs`
```csharp
// (loadout, FusionRecipeSet) → 가용 합성 목록
//   재료 A·B 모두 보유 + 둘 다 IsMaxLevel + 결과 미보유
List<FusionRecipe> Available(AbilityLoadout loadout, FusionRecipeSet lineage)
```
- MAX 판정: `inst.level >= inst.data.maxLevel` (A4 `AbilityUpgradeService.IsMaxLevel`과 동일 기준).
- 데이터(lineage)만 읽고 판정. TowerData는 관여 안 함.

### 4.2 카드 시스템 확장
| 대상 | 변경 |
|---|---|
| `CardAction` | `Fuse` 값 추가 (New/Level/Fuse) |
| `CardTier` | `Combo` → **`Fusion`** 개명 (A3 파급: `CardTierSet` 자산·`CardPresentation`) |
| `CardChoice` | 합성 카드 필드(결과 `AbilityData` + 소진할 재료 인스턴스 2개) + `FusionCard(...)` 팩토리 |
| `CardChoiceGenerator` | `FusionResolver`로 가용 합성 있으면 합성 카드 우선 제시 |
| `CardChoiceApplier` | `Fuse` 처리: `core.RemoveAbility(재료A)` + `RemoveAbility(재료B)` + `AddAbility(결과)`(Lv1, 획득 라운드) |

**A4 시너지**: 재료 소진은 A4 `IAbilityCommandTarget.RemoveAbility` 재사용.

### 4.3 합성 루트 주입
`ArenaModeBootstrap`: `centerTowerData.fusionLineage`(+ `.starterAbilities`)를 카드 생성 경로에 주입 → 합성은 **선택된 타워의 계보 안에서만** 성립. (GameContext 또는 CardSelectionPresenter 조립부에 lineage 전달)

---

## 5. 데모 레시피 (현재 타워 Aris 계보)

| # | 패턴 | 재료 A | 재료 B | → 결과(신규 자산) |
|---|---|---|---|---|
| 1 | 액티브+패시브 | 샷 | 맹공(패시브) | **정밀사격** — 강한 `ProjectileAbilityData` |
| 2 | 액티브+액티브 | 오비탈 | 에어리어웨이브 | **폭풍궤도** — 강한 `OrbitalAbilityData` |

결과 자산 2개 신규(기존 효과 타입·프리팹 재사용, 강한 수치 + 합성 baseCost). 능력명·수치는 구현 밸런싱 확정. Aris `TowerData.fusionLineage` = 이 레시피 셋.

---

## 6. 테스트 (EditMode · TDD)

| # | 대상 | 시나리오 · 기대 |
|---|---|---|
| 1 | FusionResolver | 재료2 보유 + 둘 다 MAX + 결과 미보유 → 가용 포함 |
| 2 | FusionResolver | 재료 하나라도 비MAX → 제외 |
| 3 | FusionResolver | 결과 이미 보유 / 계보에 없는 재료 → 제외 |
| 4 | CardChoiceApplier | Fuse 적용 → 재료2 제거 + 결과 추가(Lv1) 일치 |
| 5 | CardChoiceGenerator | 가용 합성 존재 시 합성 카드 제시 포함 |

Resolver·Applier·Generator는 순수 C#/스텁 주입으로 EditMode 자동 테스트(계보는 스텁 `FusionRecipeSet`). 카드 표시·소진 연출은 PlayMode 수동.

---

## 7. 신규 · 수정 파일 요약

**신규**
- `Systems/Cards/FusionRecipeSet.cs` (SO + FusionRecipe struct)
- `Systems/Cards/Editor/FusionRecipeSetDrawer.cs` (커스텀 드로어)
- `Systems/Cards/FusionResolver.cs` (POCO)
- `Data/.../*_FusionLineage.asset` (Aris 계보), 결과 능력 자산 2

**수정**
- `Data/TowerData.cs` — `fusionLineage` + `starterAbilities`
- `Systems/Cards/CardEnums.cs` — `CardAction.Fuse`, `CardTier.Combo`→`Fusion`
- `Systems/Cards/CardChoice.cs` — 합성 카드 필드/팩토리
- `Systems/Cards/CardChoiceGenerator.cs` — 합성 카드 생성
- `Systems/Cards/CardChoiceApplier.cs` — Fuse 적용
- `Systems/Mode/ArenaModeBootstrap.cs` — 계보·스타터 주입(타워 데이터에서)
- `UI/CardPresentation.cs`·`CardTierSet` 자산 — Fusion 티어 표시(개명 반영)

**변경 없음**: A4 `AbilityUpgradeService`·`IAbilityCommandTarget`(호출만 재사용).

---

## 8. 리스크

- **결과 능력 밸런싱**: 수치·효과 재사용 범위(구현 시).
- **CardTier 개명 파급**: `CardTier.Combo`→`Fusion`이 A3 카드 티어 자산/표시에 소폭 영향(퍼플 유지).
- **다단계 진화**: 이번 제외. `FusionRecipe`에 향후 `tier` 필드 추가로 확장.
- **기존 카드 회귀**: New/Level 경로 불변, Fuse 분기만 추가 → 안전.
