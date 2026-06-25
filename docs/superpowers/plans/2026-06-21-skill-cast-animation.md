# 스킬 소유 애니메이션 + 이벤트 발사 Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:executing-plans. Steps use checkbox (`- [ ]`).

**Goal:** 능력 실행을 Tick(시전 트리거)/Fire(발사) 로 분리하고, 스킬 클립의 AnimationEvent 프레임에서 발사해 모션-발사 타이밍을 일치시킨다.

**Architecture:** `ICastHost`(CoreAbilitySystem)·`ICastReceiver`(ArisTowerVisual) 두 인터페이스로 시전을 중계. `ActiveAbilityData.Tick`은 공통(쿨다운→타겟→시전요청 or 즉시발사), `Fire`는 능력별. AnimatorOverrideController로 Attack 슬롯을 스킬 클립으로 교체, AnimationEvent("OnFireFrame")가 발사를 트리거.

**Tech Stack:** Unity 6000.4, URP 17, C#, AnimatorOverrideController, AnimationEvent, Unity MCP.

## Global Constraints
- 외부 원본 FBX 무수정(.anim 복제), 컨벤션(명시 접근제한자, event `On`/핸들러 `Handle`, System 풀패스, UniTask)
- 코드가 상호의존이라 전체 작성 후 일괄 컴파일, 그 다음 에셋
- 커밋은 사용자 명시 요청 시에만

---

### Task 1: 인터페이스 ICastHost / ICastReceiver

**Files:** Create `Systems/Abilities/ICastHost.cs`, `Systems/Abilities/ICastReceiver.cs`

- [ ] ICastHost: `bool RequestCast(ActiveAbilityData skill, AbilityInstance self, DefenseDot.Core.ITargetable target, UnityEngine.AnimationClip clip);` + `void NotifyFireFrame();`
- [ ] ICastReceiver: `void PlayCast(UnityEngine.AnimationClip clip);` + `bool IsCasting { get; }`

---

### Task 2: AbilityContext — OnFired 제거 → Cast 추가

**Files:** Modify `Systems/Abilities/AbilityContext.cs`, `Tests/EditMode/AbilityRunnerTests.cs`, `CooldownHelperTests.cs`

- [ ] `OnFired` 필드/생성자 인자 제거 → `public readonly ICastHost Cast;` + 생성자 마지막 인자 `ICastHost cast = null`
- [ ] 테스트 2곳 `new AbilityContext(...)` 마지막 `null` 인자 유지(기본값이라 그대로 컴파일)

---

### Task 3: ActiveAbilityData — castAnimation + Tick 공통 + Fire 추상

**Files:** Modify `Systems/Abilities/ActiveAbilityData.cs`

- [ ] 추가:
```csharp
[SerializeField] private AnimationClip castAnimation;
public AnimationClip CastAnimation => castAnimation;
protected virtual float Range => 30f;
protected abstract void Fire(in AbilityContext ctx, AbilityInstance self, DefenseDot.Core.ITargetable target);
public void Tick(in AbilityContext ctx, AbilityInstance self, float deltaTime)
{
    if (!TickCooldown(self, deltaTime)) return;
    DefenseDot.Core.ITargetable target = ctx.Finder != null ? ctx.Finder.FindNearest(ctx.Origin, Range) : null;
    if (target == null) return;
    if (castAnimation != null && ctx.Cast != null)
    {
        if (!ctx.Cast.RequestCast(this, self, target, castAnimation)) return;
    }
    else { Fire(ctx, self, target); }
    ResetCooldown(self, ctx);
}
```
- [ ] 기존 `public abstract void Tick(...)` → 위 비추상 공통 Tick으로 교체 (서브클래스는 Tick 오버라이드 제거)

---

### Task 4: Projectile / AreaWave — Tick 본문 → Fire

**Files:** Modify `Definitions/ProjectileAbilityData.cs`, `AreaWaveAbilityData.cs`

- [ ] Projectile: `Tick` 제거 → `protected override void Fire(in AbilityContext ctx, AbilityInstance self, ITargetable target)` 에 발사 본문(투사체 Spawn+Activate+머즐). `protected override float Range => range;`. 발사 전 `if(target==null||!target.IsActive) target = ctx.Finder?.FindNearest(ctx.Origin, range);` 재확인
- [ ] AreaWave: 동일 — `Fire`에 존 Spawn+Activate, `Range => range`
- [ ] 기존 `ctx.OnFired?.Invoke()` 제거

---

### Task 5: CoreAbilitySystem — ICastHost 구현

**Files:** Modify `Systems/Abilities/CoreAbilitySystem.cs`

- [ ] `OnAbilityActivated` event 제거
- [ ] `ICastHost` 구현: `private ICastReceiver castReceiver;` `public void SetCastReceiver(ICastReceiver r)=>castReceiver=r;`
- [ ] pending 보관 필드: `private ActiveAbilityData pendingSkill; private AbilityInstance pendingSelf; private ITargetable pendingTarget; private AbilityContext pendingCtx;`
- [ ] `RequestCast`: `if(castReceiver==null||castReceiver.IsCasting) return false; pendingSkill=skill; pendingSelf=self; pendingTarget=target; pendingCtx=ctx; castReceiver.PlayCast(clip); return true;`
- [ ] `NotifyFireFrame`: `if(pendingSkill==null) return; var s=pendingSkill; pendingSkill=null; s.FireFromHost(pendingCtx, pendingSelf, pendingTarget);` (FireFromHost = ActiveAbilityData internal 래퍼가 protected Fire 호출)
- [ ] Setup: `new AbilityContext(this, origin, finder, loadout.Modifiers, effects, this)` (Cast=this)

**Note:** Fire가 protected라 외부 호출 불가 → ActiveAbilityData에 `internal void FireFromHost(in AbilityContext ctx, AbilityInstance self, ITargetable target) => Fire(ctx, self, target);` 추가(Task 3에 포함).

---

### Task 6: ArisTowerVisual — ICastReceiver + OnFireFrame + Override

**Files:** Modify `Systems/Mode/ArisTowerVisual.cs`

- [ ] `ICastReceiver` 구현. `HandleAbilityActivated`/OnAbilityActivated 구독 제거
- [ ] `AnimatorOverrideController` 런타임 생성: Setup에서 `var aoc=new AnimatorOverrideController(animator.runtimeAnimatorController); animator.runtimeAnimatorController=aoc;` 보관
- [ ] 상수 `private const string AttackClipKey="Aris_Original_Normal_Attack_Ing";`
- [ ] `PlayCast(clip)`: `aoc[AttackClipKey]=clip; animator.SetTrigger(AttackHash); isCasting=true; castRemaining=clip.length; if(clip 이벤트에 OnFireFrame 없음) → 즉시 OnFireFrame() 폴백` (이벤트 유무는 런타임 판단 어려우면 생략, 8번 안전장치는 클립에 이벤트 보장으로 충족)
- [ ] `public bool IsCasting => isCasting;` `Update`에서 `if(isCasting){ castRemaining-=Time.deltaTime; if(castRemaining<=0) isCasting=false; }`
- [ ] `public void OnFireFrame()` — AnimationEvent 호출 대상: `core?.NotifyFireFrame();`
- [ ] Setup에서 `coreSystem.SetCastReceiver(this);`

---

### Task 7: ArenaModeBootstrap 배선 확인

**Files:** `Systems/Mode/ArenaModeBootstrap.cs`

- [ ] `ArisTowerVisual.Setup`이 `core.SetCastReceiver(this)`를 부르므로 추가 배선 불필요 — 호출 순서만 확인(visual.Setup이 coreAbility.Setup 이후)

---

### Task 8: 전체 컴파일

- [ ] refresh + `read_console` 에러 0. (Fire 미구현/시그니처 불일치 등 일괄 수정)

---

### Task 9: Shot 시전 .anim + AnimationEvent + 에셋 배선

**Files:** Create `BlueArchive/Aris/Anim/Aris_Cast_Shot.anim`, Modify `Ability_Shot.asset`

- [ ] execute_code: Aris `Normal_Attack_Ing` 클립을 `Object.Instantiate` 복제 → `AnimationEvent{ time=클립의 60% 지점, functionName="OnFireFrame" }` 추가 → `AssetDatabase.CreateAsset(.anim)`
- [ ] execute_code: `Ability_Shot.asset`의 `castAnimation` = 위 .anim 할당

---

### Task 10: 검증

- [ ] EditMode 테스트 (Tick/Fire 분리 회귀, AbilityContext 생성자). Fire 단위 호출 테스트 추가 가능
- [ ] Play(사용자/내가) — Shot 시전 모션 + 발사 프레임 투사체 일치, 시전 중 중복 차단, AOE(무클립) 즉시 발사

---

## 완료 기준
- 컴파일 0, EditMode 회귀 통과
- Shot 시전 시 전용 모션의 발사 프레임에 투사체 발사
- 무클립 능력 즉시 발사, 오비탈 무영향
