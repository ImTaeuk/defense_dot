# Arena 능력 데이터 아키텍처 (A1) 설계

**작성일**: 2026-06-12
**상태**: 설계 확정 (구현 전)
**범위**: TASK-012 Arena 로드맵의 **A1** — 능력의 데이터 구조·슬롯·실행 계약. *능력 콘텐츠·tick 루프·카드 UI는 비범위(A2~A3).*
**근거**: 원작 `Assets/Reference/dot-defense-main/index.html` (능력 인스턴스 `{id,level,cooldown,...}`, 액티브6+패시브6 슬롯, def `init/tick/draw`)

---

## 0. 범위 / 비범위

| 범위 (A1) | 비범위 (후속) |
|---|---|
| `AbilityData` SO 계층(추상+Active/Passive) | 실제 능력 콘텐츠(에셋·서브클래스 구현) → A2+ |
| `AbilityInstance` 런타임 상태 | 능력 tick·발동 루프 → **A2** |
| `AbilityLoadout`(active6+passive6) + API | 카드 선택 UI/로직 → **A3** |
| `AbilityContext` 실행 입력 계약 | 강화·조합 로직 → **A4·A5** |
| `AbilityModifiers` 패시브 합산 보정 | — |

> **네이밍**: SO 정의는 프로젝트 컨벤션(`EnemyData`/`TowerData`)에 맞춰 **`~Data`** 접미사. `Def`는 방어력(defense)과 혼동되어 사용 금지.

---

## 1. 아키텍처 (4개 단위)

```
AbilityData (추상 ScriptableObject)     ← 공통: id, displayName, icon, rarity/tier, maxLevel
├─ ActiveAbilityData (추상)             ← 발동형: baseCooldown + Execute()
│   ├─ ProjectileAbilityData            ← 투사체 (예: 화염구·얼음창)
│   ├─ AoeAbilityData                   ← 범위 폭발
│   └─ OrbitalAbilityData ...           ← 동작 계열별로 추가
└─ PassiveAbilityData (추상)            ← 보정형: ApplyModifiers()
    └─ StatBoostAbilityData             ← 공격력/쿨다운 % 등
```

- **능력 1종 = 에셋 1개**(`EnemyData` 에셋과 동일 방식). 동작 코드는 소수 서브클래스, 능력은 다수 에셋.
- **정적 설계도 = `AbilityData`(SO, 불변)** ↔ **런타임 상태 = `AbilityInstance`**.

---

## 2. `AbilityData` 계층 상세

### 2.1 공통 베이스 (추상)
```csharp
public abstract class AbilityData : ScriptableObject
{
    public string id;
    public string displayName;
    public Sprite icon;
    public int rarity;        // 등급/티어
    public int maxLevel = 5;
}
```
> 액티브/패시브 구분은 **별도 enum 없이** `ActiveAbilityData`/`PassiveAbilityData` 서브클래스로 표현(중복 제거). 로드아웃은 `data is ActiveAbilityData` / `is PassiveAbilityData`로 라우팅.

### 2.2 액티브 (추상)
```csharp
public abstract class ActiveAbilityData : AbilityData
{
    public float baseCooldown = 1f;
    /// <summary> 레벨별 스케일 값(데미지 등). 디자이너 조정 — 기본 공식, 서브클래스 재정의 가능. </summary>
    public virtual float ValueAtLevel(int level) { return /* 기본 선형/곡선 */ 0f; }
    /// <summary> 1회 발동. 투사체 생성·데미지 등 동작 수행. (가동은 A2) </summary>
    public abstract void Execute(in AbilityContext ctx, AbilityInstance self);
    /// <summary> 레벨별 쿨다운(기본 baseCooldown, 보정 적용은 호출부). </summary>
    public virtual float CooldownAtLevel(int level) { return baseCooldown; }
}
```

### 2.3 패시브 (추상)
```csharp
public abstract class PassiveAbilityData : AbilityData
{
    /// <summary> 보유/레벨에 따라 합산 보정에 기여. </summary>
    public abstract void ApplyModifiers(AbilityModifiers mods, int level);
}
```

> 구체 서브클래스(`ProjectileAbilityData` 등)와 실제 에셋은 **A2 이후** 생성. A1은 계약(추상)만.

---

## 3. 런타임

### 3.1 `AbilityInstance` (POCO)
```csharp
public sealed class AbilityInstance
{
    public AbilityData data;       // 설계도 참조
    public int level = 1;
    public float cooldownRemaining; // 액티브용
    // 능력별 추가 상태는 필요 시 확장
}
```
원작 `{id, level, cooldown, ...}` 대응.

### 3.2 `AbilityLoadout` (POCO, 코어 보유)
- `IReadOnlyList<AbilityInstance> Actives` (≤6), `Passives` (≤6)
- API (A3·A4·A5의 공통 데이터원):
  - `bool TryAdd(AbilityData)` — 슬롯 여유 시 인스턴스 추가(카드 신규 획득)
  - `void LevelUp(AbilityInstance)` — 레벨↑ (카드 레벨업·강화)
  - `void Remove(AbilityInstance)` — 제거(삭제·조합 재료 소모)
  - `bool CanAdd(AbilityData)` — 서브클래스로 active/passive 판별 + 슬롯 여유 확인, `bool Contains(AbilityData)`
  - `AbilityModifiers Modifiers` — 패시브 합산 캐시(패시브 변경 시 재계산)
- 코어 액터(현재 중앙 `TowerActor`)가 보유. **tick(액티브 발동)·보정 적용은 A2.**

### 3.3 `AbilityModifiers` (POCO)
- 패시브들이 누적하는 합산 보정 컨테이너(예: `damageMul`, `cooldownMul`, `maxAliveAdd` 등). 패시브 변경 시 `AbilityLoadout`이 재계산해 캐시.

---

## 4. 실행 계약 — `AbilityContext`

기존 디버그 `AttackContext`(throwaway)를 일반화:
```csharp
public readonly struct AbilityContext
{
    public readonly MonoBehaviour Host;      // 투사체 생성 등
    public readonly Vector3 Origin;          // 코어 위치
    public readonly TargetFinder Finder;     // 적 질의
    public readonly AbilityModifiers Modifiers; // 패시브 보정
    // 생성자 …
}
```
`ActiveAbilityData.Execute(in AbilityContext, AbilityInstance)`가 입력으로 받음. **A2의 동작 구현이 사용** — 디버그 공격 3종(Single/Aoe/Projectile)을 이 계약 위 정식 `ActiveAbilityData`로 이식.

---

## 5. 데이터 흐름 (가동은 A2)

```
코어 tick → 각 active AbilityInstance.cooldownRemaining -= dt
          → 0 이하면 data.Execute(ctx, instance); cooldownRemaining = data.CooldownAtLevel(level)
패시브 변경(추가/레벨/삭제) → Loadout이 ApplyModifiers 합산 → AbilityModifiers 캐시 → 액티브·코어가 참조
```

---

## 6. 후속 이음새 (A1은 자리만)

| 후속 | A1이 제공하는 진입점 |
|---|---|
| A3 카드 선택 | `AbilityData` 풀에서 후보 생성 → `Loadout.TryAdd` / `LevelUp` |
| A4 인게임 강화 | `Loadout.LevelUp` / `Remove` |
| A5 능력 조합 | 재료 `Remove` + 결과 `TryAdd`. 콤보 메타(`comboResult`/재료)는 별도 `ComboRecipe` SO 또는 `AbilityData` 필드로 **추후** 추가 |

---

## 7. 테스트 전략 (EditMode)

- **순수 POCO 검증**: `AbilityLoadout` — `TryAdd`(슬롯한계 6 초과 거부), `LevelUp`(maxLevel 클램프), `Remove`, `HasFreeSlot`, 패시브 합산 보정(`AbilityModifiers`) 정확성.
- `AbilityData`는 추상 SO → 테스트용 스텁 서브클래스(`ScriptableObject.CreateInstance`)로 주입.
- 가동(tick·Execute)·실제 능력은 A2에서 검증.

---

## 8. 우리 자산 매핑 / 참조

- 재사용: `ScriptableObject` 데이터 idiom(`EnemyData`/`TowerData`), `TargetFinder`, 디버그 공격(`SingleTargetAttack`/`AoeAttack`/`ProjectileAttack`·`AttackContext`)을 A2에서 정식 이식.
- 코어 보유 주체: 현재 Arena 중앙 `TowerActor`(A-1 자동배치). A2에서 `AbilityLoadout` 부착·tick 연결.
- 원작: `state.tower.abilities=[{id,level,cooldown,...}]`(index.html:15789), 액티브/패시브 슬롯(`:4838~`), `ABILITY_DEFS`(:15680 영역).

---

## 9. 비범위·향후
- 능력별 구체 동작·에셋(A2+), tick 루프(A2), 카드 UI(A3), 강화/조합(A4/A5), 메타 보정 주입(A7).
- enum/필드 명은 구현 시 컨벤션 확정(접근제한자 명시·camelCase 필드).
