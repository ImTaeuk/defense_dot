# Sweeper 적 3D 교체 Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:executing-plans. Steps use checkbox (`- [ ]`).

**Goal:** Arena 2D 스프라이트 적을 Sweeper 3D 모델로 전적 교체 — 이동방향 회전 + 피격 플래시 + 사망 dissolve.

**Architecture:** BA_ToonLit에 dissolve/hitflash를 더하고, `IDeathVisual` 인터페이스로 사망 연출을 추상화한다. `SweeperEnemyVisual`(IDeathVisual 구현)이 회전·플래시·dissolve를 담당하고, MonsterActor는 인터페이스만 알아 2D 적과 무관하게 유지된다.

**Tech Stack:** Unity 6000.4, URP 17, C#, UniTask, Generic Animator, Unity MCP.

## Global Constraints

- 외부 원본 무수정(신규 프리팹/머티리얼/셰이더 확장만), Aris 코어 무영향(dissolve/flash 기본 0)
- 컨벤션: 명시적 접근제한자, event `On`·핸들러 `Handle`, System 풀패스, UniTask만(코루틴 금지), 임시 컬렉션 풀
- 셰이더/애니 단위테스트 불가 → 컴파일 + EditMode 회귀 + Play 검증
- 커밋은 사용자 명시 요청 시에만

---

### Task 1: BA_ToonLit 셰이더 확장 (dissolve + hitflash)

**Files:** Modify `Assets/ExternalResources/BlueArchive/Aris/Shaders/BA_ToonLit.shader`

- [ ] **Step 1:** Properties에 추가:
```
[NoScaleOffset]_DissolveTex("Dissolve Noise", 2D) = "gray" {}
_DissolveAmount("Dissolve Amount", Range(0,1)) = 0
_DissolveColor("Dissolve Edge", Color) = (1,0.6,0.1,1)
_HitFlash("Hit Flash", Range(0,1)) = 0
```
- [ ] **Step 2:** 두 패스 CBUFFER에 `half _DissolveAmount; half4 _DissolveColor; half _HitFlash;` 추가, ForwardLit에 `TEXTURE2D(_DissolveTex); SAMPLER(sampler_DissolveTex);`
- [ ] **Step 3:** ForwardLit frag 시작부에 `half dn = SAMPLE_TEXTURE2D(_DissolveTex, sampler_DissolveTex, IN.uv).r; clip(dn - _DissolveAmount);`, 반환 직전 `col += _DissolveColor.rgb * step(dn - _DissolveAmount, 0.08) * step(0.001,_DissolveAmount); col = lerp(col, (half3)1, _HitFlash);`
- [ ] **Step 4:** Outline 패스 frag에 동일 `clip(dn - _DissolveAmount)` (UV·샘플러 추가)
- [ ] **Step 5:** refresh + `read_console` 에러 0. Aris 프리뷰 캡처로 기존 룩 무변경 확인(기본값 0)

---

### Task 2: IDeathVisual + MonsterActor 변경

**Files:** Create `Assets/Scripts/Systems/Enemy/IDeathVisual.cs`, Modify `Assets/Scripts/Systems/Enemy/MonsterActor.cs`

**Interfaces:**
- Produces: `interface IDeathVisual { void PlayDeath(System.Action onComplete); }`, `MonsterActor.OnHit` event

- [ ] **Step 1:** IDeathVisual 작성:
```csharp
namespace DefenseDot.Systems.Enemy
{
    /// <summary> 사망 연출을 재생하고 완료 시 콜백하는 비주얼 계약입니다. </summary>
    public interface IDeathVisual
    {
        void PlayDeath(System.Action onComplete);
    }
}
```
- [ ] **Step 2:** MonsterActor에 `public event System.Action OnHit;` 추가, `private IDeathVisual deathVisual;`, Awake/OnSpawn에서 `deathVisual = GetComponentInChildren<IDeathVisual>();`
- [ ] **Step 3:** `TakeDamage`에서 데미지 적용 후 `OnHit?.Invoke();` (기존 hitFlashTimer는 유지하되 2D 폴백)
- [ ] **Step 4:** `Resolve(reached)` 변경 — 즉시 `HandleEnemyKilled` 대신:
```csharp
SetState(ActorState.Dead);
if (!reached && deathVisual != null)
    deathVisual.PlayDeath(() => spawner?.HandleEnemyKilled(this));
else
    { if (reached) spawner?.HandleEnemyReached(this); else spawner?.HandleEnemyKilled(this); }
```
- [ ] **Step 5:** OnDespawn에서 deathVisual null 안전. 컴파일 확인

---

### Task 3: SweeperEnemyVisual.cs (IDeathVisual)

**Files:** Create `Assets/Scripts/Systems/Enemy/SweeperEnemyVisual.cs`

- [ ] **Step 1:** MonsterActor 자식에 붙는 MonoBehaviour, IDeathVisual 구현:
  - `[SF] Animator animator; SkinnedMeshRenderer[] renderers; Material[] colorMats(3); float rotateSpeed=10; float dissolveDuration=1f; float hitFlashDuration=0.09f`
  - 해시: `_DissolveAmount, _HitFlash`
  - `Setup(MonsterActor actor)`: actor.OnHit += HandleHit. `OnEnable`/스폰 시 ResetVisual.
  - `LateUpdate`: 위치 delta(Y평면) → LookRotation Slerp (locked면 skip)
  - `HandleHit()`: hitTimer=hitFlashDuration. Update에서 `_HitFlash` 감쇠 적용(MaterialPropertyBlock)
  - `PlayDeath(Action onComplete)`: locked=true, animator.speed=0, UniTask로 `_DissolveAmount` 0→1, 완료 후 onComplete()
  - `ResetVisual()`: locked=false, animator.speed=1, `_DissolveAmount`=0, `_HitFlash`=0, 랜덤 색 material 적용
  - 컨벤션: `On`/`Handle`, UniTask, System 풀패스
- [ ] **Step 2:** 컴파일 확인

---

### Task 4: Sweeper 머티리얼·컨트롤러·프리팹 (MCP)

**Files:** Create `Sweeper_{Mint,Pink,Yellow}.mat`, `AC_SweeperEnemy.controller`, `Sweeper_Enemy.prefab`

- [ ] **Step 1:** execute_code — RG_Sweeper FBX 클립으로 AnimatorController 생성: `Move`(`Sweeper_Decagram_Taser_RG_Move_Ing`, loop) 단일 상태(default)
- [ ] **Step 2:** execute_code — 3색 머티리얼: BA_ToonLit + 각 색 텍스처(`Sweeper_Decagram_Taser_{Mint,Pink,Yellow}` albedo, `_DissolveTex`=tex_terror_dissolve 임포트 후 or 기본)
- [ ] **Step 3:** execute_code — Sweeper_Enemy.prefab 조립: 기존 Enemy 프리팹 복제 기반(MonsterActor 유지) → Visual 자식의 SpriteRenderer/BillboardSprite/ActorAnimatorBinder 제거 → Sweeper FBX SkinnedMesh 인스턴스 + Animator(AC_SweeperEnemy) + SweeperEnemyVisual(renderers·colorMats 연결) 부착 → globalScale로 적 크기(≈0.8~1.2) 보정 → SaveAsPrefabAsset
- [ ] **Step 4:** 컴파일/에러 확인

---

### Task 5: EnemyData 프리팹 교체 + 배선

**Files:** Modify Arena EnemyData asset (prefab 필드)

- [ ] **Step 1:** execute_code — Arena가 쓰는 EnemyData(들)의 `prefab` 필드를 Sweeper_Enemy.prefab으로 교체. EnemySpawner가 SweeperEnemyVisual.Setup을 호출하도록(또는 MonsterActor.OnSpawn에서 자체 Setup) 배선 확인
- [ ] **Step 2:** SweeperEnemyVisual.Setup 호출 지점 확정 — MonsterActor.OnSpawn에서 `GetComponentInChildren<SweeperEnemyVisual>()?.Setup(this)` (또는 IDeathVisual 캐싱 시 함께)
- [ ] **Step 3:** 씬/데이터 저장

---

### Task 6: 검증

- [ ] **Step 1:** 전체 컴파일 `read_console` 에러 0
- [ ] **Step 2:** EditMode 테스트 — MonsterActor 변경 회귀(전 테스트 PASS)
- [ ] **Step 3:** Play(사용자) — 공전 이동·이동방향 회전·피격 플래시·사망 dissolve·풀 재사용(색 재랜덤)

---

## 완료 기준
- 컴파일 0, EditMode 회귀 통과
- Arena 적이 Sweeper 3D로 이동(방향 회전)·피격 플래시·사망 dissolve
- Aris 코어·기존 2D 경로 무영향
