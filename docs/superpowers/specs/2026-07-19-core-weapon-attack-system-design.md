# CoreWeapon 공격 체계 설계

**작성일**: 2026-07-19
**상태**: 설계 확정
**범위**: 아레나 코어(아리스)의 공격 주기·발사 묶음 체계
**별도 스펙**: 라운드 이어가기 + 적 체력 스케일링 (본 스펙 범위 밖)

---

## 1. 배경

플레이 루프 완성도 감사(TASK-020)에서 확정된 결함이다.

- `Ability_Shot`의 설계 쿨다운은 **0.4초**인데, 시전 애니메이션 클립(`Aris_Cast_Shot.anim`)이 **2.667초**다.
- `ActiveAbilityData.cs:30-33` — `RequestCast` 실패 시 `return`하여 `:38 ResetCooldown`에 도달하지 못한다.
- `CoreAbilitySystem.cs:113-115` — 시전 채널이 **코어 전역 1개**(`castReceiver.IsCasting`)다.
- `ArisTowerVisual.cs:114-115` — `castRemaining = clip.length`로 2.667초간 채널을 점유한다.

**귀결 1**: 투사체 실발사 주기 ≈ 2.67초 (설계 대비 6.7배).
**귀결 2**: `Ability_Shot`·`Ability_Railgun`·`Ability_StormBrand` 3종이 **같은 클립·같은 단일 채널**을 공유하므로, 3개를 모두 보유해도 총 발사 빈도가 1개일 때와 같다. 로그라이트의 핵심 보상인 "능력을 모을수록 강해진다"가 투사체 계열에서 무효화된다.

### 1.1 원작 대조

원작 dot-defense(`Assets/Reference/dot-defense-main/index.html`)에는 **이 문제가 존재하지 않는다**.

- `:23710 drawTower()` — 캐릭터는 정지 이미지 한 장. 발사 모션이 없다.
- `isCasting`·`recoil`·`attackAnim` 검색 결과 **0건** — 발사를 막는 개념 자체가 없다.
- `:22726` — `for (const ab of state.tower.abilities) { def.tick(ab); }` 로 모든 능력이 **각자 독립적으로** 자기 쿨다운만 보고 발사한다.

즉 본 병목은 아리스 3D 모델에 실제 시전 애니메이션을 도입하면서 **우리가 추가한 구조**이며, 원작의 밸런스 전제("능력은 각자 독립 발사")를 깨뜨렸다. 원작이 답을 주지 않는 영역이므로 우리 게임 고유의 설계가 필요하다.

---

## 2. 목표 / 비목표

### 목표
- 발사 주기를 데이터로 제어 가능하게 하고, 그 **단일 수정 원천**을 만든다.
- 공격 능력을 여러 개 모을수록 실제로 화력이 늘어나게 한다.
- 캐릭터마다 주공격 형태가 달라도(투사체·범위 장판·레이저) 같은 체계로 표현되게 한다.

### 비목표
- 라운드 이어가기·적 체력 스케일링 (별도 스펙)
- `AbilityModifiers.damageBonus`·`cooldownReduction`의 **생산자** 구현 (연결 지점만 만들어 둔다)
- 계보 결과 능력의 스탯·VFX 차별화 (TASK-018)
- 메인 교체 전용 카드 UI (메인이 둘 이상 실재할 때 다룬다)

---

## 3. 확정된 설계 결정

| # | 결정 | 근거 |
|---|---|---|
| D1 | 능력을 **타이밍 축 하나**로만 분류한다 (`Main` / `Sub` / `Auto`) | "무엇이 나가는가"를 축에 섞으면 범위 장판형 주공격 캐릭터가 들어올 자리가 없어진다 |
| D2 | 발사 주기는 **`CoreWeapon`이 단독 소유**한다 | 공격 속도를 고칠 때 볼 곳이 항상 한 곳 |
| D3 | 서브는 메인과 **한 묶음으로 동시 발사**한다 (한 모션 = 한 발사 묶음) | 총에 총신을 더하는 체감. 부가를 쌓을수록 한 발이 무거워지고 느려진다 |
| D4 | 메인은 **1개만 보유**하며, 새 메인 획득 시 **교체**된다 | 계보 정점에서 "주 무기가 바뀐다"는 강한 체감 |
| D5 | 모션 재생 속도를 **발사 주기에 자동으로 맞춘다** | 주기라는 숫자 하나가 발사와 연출을 동시에 결정 |
| D6 | 타워 기본 공격 속도는 **`TowerData`에 둔다** | 타워 능력치 데이터가 제자리. 이미 있는 `attackSpeed` 필드를 되살린다 |
| D7 | 형태가 겹치는 로직은 **상속이 아니라 공용 조각 호출**로 공유한다 | 상속으로 묶으면 다시 형태가 축이 된다 |

---

## 4. 능력 타입 계층

```
AbilityData
├── ActiveAbilityData (추상)
│   ├── MainAbilityData (추상)   주기의 주인. 공격 모션 보유
│   ├── SubAbilityData  (추상)   메인 발사에 동반. 주기 가감값 보유
│   └── AutoAbilityData (추상)   주기와 무관. 자기 쿨다운으로 동작
└── PassiveAbilityData (추상)
```

**이 셋은 "언제 발동하는가"만 규정한다.** 무엇이 나가는지는 구현체가 정한다.

### 4.1 현재 능력 배치

| 능력 | 타입 | 나가는 것 |
|---|---|---|
| 샷 | `MainAbilityData` | 투사체 |
| 레일건 · 폭풍낙인 | `SubAbilityData` | 투사체 |
| 오비탈 · 회오리 | `AutoAbilityData` | 궤도 위성 |
| 범위파동 · 기절폭탄 · 번개채찍 · 폭풍의장막 · 폭풍의군주 | `AutoAbilityData` | 범위 파동 |
| 순수 데미지 패시브 4종 | `PassiveAbilityData` | — |

### 4.2 확장 예시 (미래)

범위 마법진을 주공격으로 쓰는 캐릭터는 `MainAbilityData`를 상속해 `Fire()`에서 장판을 깔면 된다. `CoreWeapon`은 무엇이 나가는지 모르며 코드 변경이 없다.

### 4.3 형태 로직 공유

샷(Main)과 레일건(Sub)이 둘 다 투사체를 만들지만 **상속으로 묶지 않는다**. 공용 조각을 호출한다.

```csharp
// 각 능력의 Fire 안에서 호출
ProjectileLauncher.Launch(in ctx, target, speed, damage, pierce);
```

새 형태(마법진·레이저)가 생기면 계층을 건드리지 않고 조각만 추가한다.

---

## 5. CoreWeapon

### 5.1 형태

**MonoBehaviour가 아닌 순수 C# 클래스**로 만든다. 주기 계산을 EditMode 테스트로 검증하기 위함이다. `CoreAbilitySystem`이 소유하고 매 프레임 `Tick`을 호출한다.

### 5.2 책임

| 한다 | 하지 않는다 |
|---|---|
| 메인 1개·서브 N개를 보유 | 무엇이 나가는지 판단 |
| 발사 주기 계산 | 능력 장착·해제 결정 (`CoreAbilitySystem`) |
| 타겟 선정 | `AutoAbilityData` 구동 |
| 모션 재생·속도 지시 | 데미지 계산 |
| 발사 프레임에 메인+서브 전부 발사 | |

### 5.3 인터페이스 (개형)

```csharp
public sealed class CoreWeapon
{
    /// <summary> 주축 공격 능력을 장착합니다(기존 메인은 해제). </summary>
    /// <param name="main">새로 장착할 주축 능력</param>
    /// <param name="instance">해당 능력의 런타임 인스턴스</param>
    public void SetMain(MainAbilityData main, AbilityInstance instance);

    /// <summary> 동반 공격 능력을 추가합니다. </summary>
    public void AddSub(SubAbilityData sub, AbilityInstance instance);

    /// <summary> 동반 공격 능력을 제거합니다. </summary>
    public void RemoveSub(SubAbilityData sub);

    /// <summary> 주기를 진행시키고 준비되면 공격 모션을 시작합니다. </summary>
    /// <param name="deltaTime">경과 시간(초)</param>
    public void Tick(in AbilityContext ctx, float deltaTime);

    /// <summary> 모션의 발사 프레임에서 호출되어 메인·서브를 한 번에 발사합니다. </summary>
    public void FireAll(in AbilityContext ctx);

    /// <summary> 현재 발사 주기(초)입니다. </summary>
    public float Cycle { get; }
}
```

---

## 6. 발사 주기 계산

```
기본 주기 = 1 ÷ TowerData.attackSpeed
발사 주기 = 기본 주기 + 메인 가감 + Σ(서브 가감) − 쿨다운 감소 보정
하한      = 0.05초
```

- `TowerData.attackSpeed`(초당 공격 횟수)는 이미 존재하나 아레나에서 배선이 끊겨 있다. `ArenaModeBootstrap.cs:92-93`이 `TowerBehaviorTree`를 `Destroy`하여 `CombatLogic`이 구동되지 않기 때문이다. 값 자체는 `TowerData` 에셋에서 조정한다.
- **가감값은 선택 사항**이다. 값이 0이면 주기에 영향을 주지 않는다.
- `쿨다운 감소 보정`은 `AbilityModifiers.cooldownReduction`이 들어올 자리다. 현재 생산자가 없어 항상 0이지만, **연결 지점을 이 한 곳으로 고정**한다.

### 6.1 계산 예시

`TowerData.attackSpeed = 1.0`(기본 주기 1.0초) 가정.

| 진행 | 보유 | 발사 주기 | 한 번에 나가는 것 | 모션 속도 |
|---|---|---|---|---|
| 시작 | 샷 | 1.0초 | 샷 | 2.7배 |
| 레일건(+0.5) 획득 | 샷 · 레일건 | 1.5초 | 샷 + 레일건 | 1.8배 |
| 폭풍낙인(+0.2) 획득 | 샷 · 레일건 · 낙인 | 1.7초 | 셋 다 | 1.6배 |
| 오비탈 획득 | 위 + 오비탈 | **1.7초 (불변)** | 셋 다 | 1.6배 |

부가를 쌓을수록 주기가 늘어나 **모션이 오히려 자연스러워진다**.

---

## 7. 발사 흐름

```
CoreWeapon.Tick(ctx, dt)
  ① 남은시간 -= dt
  ② 남은시간 > 0 → 대기
  ③ 메인 없음 → 대기
  ④ 타겟 탐색(메인의 사거리 기준)
     없으면 대기 — 남은시간을 주기로 되돌리지 않으므로, 타겟이 잡히는 즉시 발사된다
  ⑤ 주기 = 계산()
  ⑥ 모션 재생 지시 (속도 = 클립 길이 ÷ 주기)
  ⑦ 남은시간 = 주기

ArisTowerVisual.OnFireFrame()   ← 애니메이션 이벤트(클립의 32% 지점)
  → CoreWeapon.FireAll(ctx)
      메인.Fire(ctx, 타겟)
      각 서브.Fire(ctx, 타겟)
```

### 7.1 타겟 공유

한 묶음이므로 **타겟 하나를 공유**한다. 메인의 사거리로 탐색한 대상에 메인·서브가 모두 발사한다. 서브별 개별 사거리는 두지 않는다.

### 7.2 모션 속도 지시

기존 `ICastReceiver`를 역할에 맞게 바꾼다.

```csharp
public interface IAttackMotion
{
    /// <summary> 공격 모션을 지정 속도로 재생합니다. </summary>
    /// <param name="clip">재생할 공격 모션</param>
    /// <param name="target">조준 대상</param>
    /// <param name="speed">재생 속도 배수(클립 길이 ÷ 발사 주기)</param>
    void PlayAttack(AnimationClip clip, ITargetable target, float speed);
}
```

`IsCasting`(거부 판단)은 제거한다. 발사 가부는 `CoreWeapon`의 주기가 단독으로 결정한다.

속도 적용은 Animator의 상태 속도 배수 파라미터로 한다. `Animator.speed`를 직접 바꾸면 대기 모션 등 다른 상태까지 영향을 받으므로 사용하지 않는다.

---

## 8. 메인 교체 규칙

```
새 MainAbilityData 획득
  → 기존 메인 해제 (레벨도 함께 사라짐)
  → 새 메인 장착
  → 서브들은 유지되어 새 메인에 그대로 얹힘
```

현재 메인은 샷 하나뿐이라 교체 상황이 실재하지 않는다. 계보 정점이 새 주 무기가 되는 경우를 위해 규칙만 정의해 둔다. 교체를 알리는 카드 UI는 메인이 둘 이상 실재할 때 다룬다.

---

## 9. 데이터 이관

`.asset` 파일의 `m_Script` GUID를 새 클래스의 GUID로 교체한다. **이름이 같은 필드 값은 보존**된다.

에셋을 재생성하면 안 된다. 이 능력 에셋들은 아래에서 참조되고 있어 GUID가 바뀌면 참조가 끊어진다.

- `Assets/Data/Cards/AbilityPool.asset` (카드 풀 11종)
- `Assets/Data/Fusion/Aris_FusionLineage.asset` (레시피 4건)
- `ArenaModeBootstrap`의 `starterAbilities`

### 9.1 신설 필드

| 타입 | 필드 | 의미 |
|---|---|---|
| `MainAbilityData` | `castAnimation` | `ActiveAbilityData`에서 이동 |
| `MainAbilityData` | `cycleDelta` | 기본 주기에 대한 가감(초). 기본 0 |
| `SubAbilityData` | `cycleDelta` | 기본 주기에 대한 가감(초). 기본 0 |

---

## 10. 걷어내는 것

| 대상 | 처리 | 이유 |
|---|---|---|
| `CoreAbilitySystem.RequestCast` · `ICastHost` · 시전 채널 | 삭제 | 경합의 직접 원인 |
| `ArisTowerVisual.IsCasting` 거부 로직 | 삭제 | 속도 지정 재생으로 대체 |
| `ActiveAbilityData.castAnimation` | `MainAbilityData`로 이동 | 아무 능력이나 모션을 갖지 못하게 |
| `ActiveAbilityData.Tick`의 시전 분기 | 삭제 | 발사 판단이 `CoreWeapon`으로 이동 |
| `AbilityInstance.cooldownRemaining` | `AutoAbilityData` 전용으로 축소 | 공격 주기는 `CoreWeapon` 독점 |

---

## 11. 테스트

### 11.1 자동 (EditMode)

| # | 시나리오 | 기대 |
|---|---|---|
| 1 | 메인만 장착, 가감 0 | 주기 = 기본 주기 |
| 2 | 메인 가감 +0.3 | 주기 = 기본 + 0.3 |
| 3 | 서브 2개(+0.5, +0.2) | 주기 = 기본 + 0.7 |
| 4 | 가감 합이 음수로 커짐 | 주기 = 0.05초 (하한) |
| 5 | `cooldownReduction` 0.2 | 주기에서 0.2 차감 |
| 6 | 새 메인 장착 | 기존 메인 제거, 서브 유지 |
| 7 | `AutoAbilityData` N개 장착 | 주기 불변 |
| 8 | `FireAll` 1회 호출 | 메인 1회 + 각 서브 1회 발사 |
| 9 | 타겟 없음 상태로 Tick | 주기를 소모하지 않고 준비 유지 |

### 11.2 수동 (PlayMode)

| # | 시나리오 | 기대 |
|---|---|---|
| 10 | 서브 획득 전후 모션 관찰 | 주기가 늘면 모션이 느려짐 |
| 11 | 투사체 3종 보유 | 한 모션에 3발이 함께 나감 |
| 12 | 오비탈 보유 상태 | 오비탈이 공격 주기와 무관하게 동작 |

---

## 12. 영향 범위

| 항목 | 영향 |
|---|---|
| `AutoAbilityData` 계열 7종(오비탈·회오리·범위파동·기절폭탄·번개채찍·폭풍의장막·폭풍의군주) | 동작 변화 없음 |
| 계보 합성 시스템(`FusionSystem`) | 변화 없음. 결과 능력이 전부 `AutoAbilityData`라 무기와 무관 |
| 카드 생성·적용 | 메인 교체 규칙만 추가 |
| 그리드 배치 타워(`TowerActor`·`CombatLogic`) | 변화 없음. `attackSpeed`의 의미가 같아 필드 공유에 충돌 없음 |
| 기존 EditMode 테스트 621건 | 시전 채널 관련 테스트가 있으면 삭제·대체 필요 |

---

## 13. 미결 사항

| # | 항목 | 처리 |
|---|---|---|
| U1 | `cycleDelta` 필드명 | 구현 착수 전 확정. 대안: `attackCycleDelta`, `cycleAdd` |
| U2 | `TowerData.attackSpeed` 초기값 | 에셋에서 플레이하며 조정 |
| U3 | 서브별 `cycleDelta` 초기값 | 에셋에서 플레이하며 조정 |
| U4 | 기존 시전 채널 관련 테스트 존재 여부 | 구현 시 확인 후 정리 |

---

## 14. 후속 (별도 스펙)

- **라운드 이어가기 + 적 체력 스케일링** — 원작 기준 라운드당 적 수 고정·체력 지수 증가(`3 × 1.35^(n−1)`). 현재 총 스폰 80 = 수용 한계 80이라 패배 도달이 불가능한 문제(TASK-020 §3-1)를 해결한다.
- **검증은 두 스펙을 함께** 수행한다. 화력이 정상화되지 않으면 체력 곡선을 넣어도 몇 라운드 만에 무너지고, 체력 곡선이 없으면 패배가 불가능해 판단 기준이 없다.
