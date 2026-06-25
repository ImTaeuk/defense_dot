# 스킬 소유 애니메이션 + 이벤트 발사 설계

**작성일**: 2026-06-21
**상태**: 설계 승인 완료
**대상**: Arena 코어(Aris) 능력 시전 — 스킬별 애니메이션 + AnimationEvent 발사
**범위**: 능력 실행 모델(Tick/Fire 분리) + AnimatorOverrideController 시전 + 발사 프레임 동기화

---

## 1. 목표 / 성공 기준

- 각 스킬(능력)이 **자기 시전 애니메이션**을 소유하고, 시전 시 Tower(Aris) Animator의 Attack 슬롯을 그 클립으로 **오버라이드**해 재생한다.
- 투사체(효과) 발사를 **애니 클립의 AnimationEvent 프레임에 동기화**해, 발사 타이밍과 모션이 정확히 일치한다.
- 애니 클립이 없는 스킬은 애니 없이 즉시 발사한다(현재 동작 유지).
- 성공 기준: Shot 스킬 시전 시 Aris가 전용 모션을 재생하고, 모션의 발사 프레임에 투사체가 정확히 나간다. EditMode 회귀 없음.

## 2. 배경 / 현황 (탐색)

- `AbilityData`(base: id/name/icon/rarity/maxLevel) → `ActiveAbilityData`(baseCooldown, `Tick`, TickCooldown/ResetCooldown) → Projectile/AreaWave.
- 현재: `ProjectileAbilityData.Tick`이 쿨다운→FindNearest→**즉시 발사**+`ctx.OnFired`→`CoreAbilitySystem.OnAbilityActivated`→`ArisTowerVisual.HandleAbilityActivated`→`SetTrigger("Attack")`. 모든 능력이 단일 Attack 트리거 공유.
- `AC_ArisTower` Attack 상태 클립 = `Aris_Original_Normal_Attack_Ing` (오버라이드 키).
- Aris FBX 클립은 default 임포트(31개) — **원본 클립에 AnimationEvent 직접 부착 불가** → 스킬 클립을 `.anim` 복제 후 이벤트 부착.
- `ArisTowerVisual`은 Animator와 같은 GameObject(Aris 인스턴스)에 있음 → AnimationEvent가 이 컴포넌트 메서드를 호출 가능.

## 3. 아키텍처 / 신규·변경

| 구분 | 자산/파일 | 책임 |
|---|---|---|
| 신규 코드 | `Systems/Abilities/ICastHost.cs` | `bool RequestCast(skill, self, target, clip)` + `void NotifyFireFrame()` (CoreAbilitySystem 구현) |
| 신규 코드 | `Systems/Abilities/ICastReceiver.cs` | `void PlayCast(AnimationClip clip)` + `bool IsCasting` (ArisTowerVisual 구현) |
| 변경 코드 | `Systems/Abilities/AbilityContext.cs` | `OnFired` 제거 → `ICastHost Cast` 추가 |
| 변경 코드 | `Systems/Abilities/ActiveAbilityData.cs` | `castAnimation` 필드 + `Tick` 공통화 + `abstract Fire(ctx,self,target)` |
| 변경 코드 | `Definitions/ProjectileAbilityData.cs`·`AreaWaveAbilityData.cs` | `Tick` 본문 → `Fire`로 이동 |
| 변경 코드 | `Systems/Abilities/CoreAbilitySystem.cs` | `ICastHost` 구현, `castReceiver` 보관, pending 발사 보관. `OnAbilityActivated` 제거 |
| 변경 코드 | `Systems/Mode/ArisTowerVisual.cs` | `ICastReceiver` 구현(PlayCast/IsCasting), `OnFireFrame()`, AnimatorOverrideController. `HandleAbilityActivated` 제거 |
| 변경 코드 | `Systems/Mode/ArenaModeBootstrap.cs` | visual ↔ core 시전 연결(`core.SetCastReceiver(visual)`) |
| 신규 자산 | `Sweeper`/`Aris` 영역 — Shot 시전 `.anim`(이벤트 포함) | Aris Attack 클립 복제 + `OnFireFrame` 이벤트 |
| 변경 자산 | `Ability_Shot.asset` | `castAnimation` 할당 |

## 4. 능력 실행 모델 — Tick / Fire 분리 (Template Method)

```
// ActiveAbilityData (base 공통)
public sealed override... 아니라 공통 Tick:
void Tick(ctx, self, dt):
    if (!TickCooldown(self, dt)) return;
    ITargetable target = ctx.Finder?.FindNearest(ctx.Origin, Range);  // Range는 가상
    if (target == null) return;                       // 준비 유지
    if (castAnimation != null && ctx.Cast != null) {
        if (!ctx.Cast.RequestCast(this, self, target, castAnimation)) return;  // 시전 중이면 대기(쿨다운 유지)
    } else {
        Fire(ctx, self, target);                      // 즉시 발사
    }
    ResetCooldown(self, ctx);
protected abstract void Fire(in AbilityContext ctx, AbilityInstance self, ITargetable target);
protected virtual float Range => 30f;   // 서브클래스 재정의
```
- `Projectile.Fire` = 투사체 생성(+머즐), `AreaWave.Fire` = 존 생성. 기존 Tick 본문 이동.
- 상시 능력(Orbital)은 `IAbilityLifecycle`로 별도(Tick 미사용) — 영향 없음.

## 5. 시전 흐름 (CoreAbilitySystem ↔ ArisTowerVisual)

```
Tick → ctx.Cast.RequestCast(skill, self, target, clip)
  CoreAbilitySystem.RequestCast:
    if (castReceiver == null || castReceiver.IsCasting) return false;
    pending = (skill, self, target, ctx);             // ctx는 readonly struct 복사
    castReceiver.PlayCast(clip); return true;
  ArisTowerVisual.PlayCast(clip):
    override["Aris_Original_Normal_Attack_Ing"] = clip;  // AnimatorOverrideController
    animator.SetTrigger("Attack");  IsCasting = true;  castRemaining = clip.length;
애니 진행 → AnimationEvent("OnFireFrame") → ArisTowerVisual.OnFireFrame()
  → core.NotifyFireFrame()
    CoreAbilitySystem.NotifyFireFrame: pending.skill.Fire(pending.ctx, pending.self, pending.target); pending = null;
애니 종료(castRemaining ≤ 0) → IsCasting = false  (다음 시전 허용)
```

## 6. AnimatorOverrideController

- `ArisTowerVisual`이 base `AC_ArisTower`로 `AnimatorOverrideController`를 런타임 생성해 `animator.runtimeAnimatorController`에 지정.
- `PlayCast(clip)`마다 `"Aris_Original_Normal_Attack_Ing"` 키를 `clip`으로 교체. clip이 base와 같으면 기본 모션.

## 7. AnimationEvent 부착

- Aris Attack 클립(`Normal_Attack_Ing`)을 `.anim`으로 복제 → 발사 프레임에 `functionName = "OnFireFrame"` AnimationEvent 추가 → `Ability_Shot.castAnimation`에 할당.
- 복제·이벤트 부착은 execute_code(AssetDatabase). 원본 FBX 무수정.
- 1차: Shot 스킬 1개로 구현·검증. AOE/추가 스킬은 후속.

## 8. 디폴트 결정

- **동시 시전**: 시전 중(IsCasting)이면 다른 *애니* 스킬의 RequestCast는 false → 쿨다운 유지(다음 프레임 재시도). 애니 없는 스킬은 항상 즉시 Fire.
- **타겟**: 시전 시작 시 확정. `Fire`에서 target이 죽었으면(`!IsActive`) `ctx.Finder`로 재탐색, 없으면 전방 발사(투사체 유도).
- **OnFireFrame 누락 안전장치**: 클립에 이벤트가 없으면 발사 안 됨 → PlayCast 시 이벤트 유무를 보장(없으면 즉시 Fire 폴백). 

## 9. 검증

1. 컴파일 0
2. EditMode 회귀 — Tick/Fire 분리, AbilityContext 생성자 변경(테스트 갱신). Fire를 직접 호출하는 단위 테스트 추가 가능
3. Play — Shot 시전 모션 재생 + 발사 프레임에 투사체 정확히 발사, 시전 중 중복 차단, 클립 없는 능력 즉시 발사

## 10. 범위 외 (후속)

- AOE/Beam 등 스킬별 시전 애니, 다중 동시 시전(큐), 시전 중 캔슬, 이동 중 시전 블렌드
