# Aris Core Tower Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:executing-plans. Steps use checkbox (`- [ ]`).

**Goal:** Arena 코어 타워 비주얼을 Aris 3D 모델로 대체하고, 능력 발동·코어 상태·적 방향을 Generic Animator/회전에 연동한다.

**Architecture:** 능력 발동 통지 경로(AbilityContext 콜백 → CoreAbilitySystem 이벤트)를 추가하고, 전용 `ArisTowerVisual` 컴포넌트가 그 이벤트 + 코어 HP + 타겟을 구독해 Animator와 Y축 회전을 구동한다. Aris 프리팹/컨트롤러는 별도 자산으로 생성해 코어 타워에만 격리.

**Tech Stack:** Unity 6000.4, URP 17, C#, Generic Animator, Unity MCP.

## Global Constraints

- 외부 원본 FBX·텍스처·타 모델·HD-2D 적 렌더링 무수정
- 코드 컨벤션: 명시적 접근제한자, event `On`·핸들러 `Handle`, System 풀패스, UniTask만(코루틴 금지)
- 셰이더/애니는 단위테스트 불가 → 컴파일 + EditMode 회귀 + Play 시각 검증
- 커밋은 사용자 명시 요청 시에만

---

### Task 1: AbilityContext 발사 통지 콜백 + 생성처 갱신

**Files:**
- Modify: `Assets/Scripts/Systems/Abilities/AbilityContext.cs`
- Modify: `Assets/Tests/EditMode/AbilityRunnerTests.cs:24`, `Assets/Tests/EditMode/CooldownHelperTests.cs:24`

- [ ] **Step 1:** `AbilityContext`에 `public readonly System.Action OnFired;` 필드 추가, 생성자에 6번째 인자 `System.Action onFired` 추가 → `OnFired = onFired;`
- [ ] **Step 2:** 테스트 2곳의 `new AbilityContext(...)` 호출에 마지막 인자 `null` 추가
- [ ] **Step 3:** 컴파일 확인 (`read_console`) — 에러 0

---

### Task 2: 이산 능력 발사 시 콜백 호출

**Files:**
- Modify: `Assets/Scripts/Systems/Abilities/Definitions/ProjectileAbilityData.cs`
- Modify: `Assets/Scripts/Systems/Abilities/Definitions/AreaWaveAbilityData.cs`

- [ ] **Step 1:** 두 능력의 `Tick`에서 실제 발사(투사체/존 스폰 + ResetCooldown) 직후 `ctx.OnFired?.Invoke();` 호출. (OrbitalAbilityData 상시는 호출 안 함)
- [ ] **Step 2:** 컴파일 확인

---

### Task 3: CoreAbilitySystem.OnAbilityActivated 이벤트

**Files:**
- Modify: `Assets/Scripts/Systems/Abilities/CoreAbilitySystem.cs`

- [ ] **Step 1:** `public event System.Action OnAbilityActivated;` 추가
- [ ] **Step 2:** `Setup`의 `new AbilityContext(...)`에 `() => OnAbilityActivated?.Invoke()` 를 6번째 인자로 전달
- [ ] **Step 3:** `Setup` 시그니처는 유지(스타터 주입). 컴파일 확인

---

### Task 4: ArisTowerVisual.cs 작성

**Files:**
- Create: `Assets/Scripts/Systems/Mode/ArisTowerVisual.cs`

**Interfaces:**
- Consumes: `CoreAbilitySystem.OnAbilityActivated`, `TargetFinder.FindNearest`, `GameFlowModel`, `CoreModel`(HP 이벤트)
- Produces: `void Setup(CoreAbilitySystem core, TargetFinder finder, GameFlowModel flow, CoreModel coreHp)`

- [ ] **Step 1:** MonoBehaviour 작성:
  - `[SerializeField] private Animator animator;` `_rotSpeed`, `_targetRange=30`, `_lowHpRatio=0.3f`
  - Animator 파라미터 해시: `Attack`(trigger), `LowHP`(bool), `Death`(trigger), `Victory`(trigger)
  - `Setup`에서 의존성 저장 + `core.OnAbilityActivated += HandleAbilityActivated` + `coreHp.OnCurrentChanged += HandleCoreHpChanged` (실제 이벤트명은 CoreModel 확인 후 매핑)
  - `HandleAbilityActivated()` → `animator.SetTrigger(Attack)`
  - `HandleCoreHpChanged(cur,max)` → `animator.SetBool(LowHP, cur/max < _lowHpRatio)`; `if(cur<=0) animator.SetTrigger(Death)`
  - `Update()` → 최근접 적 방향 Y축 Slerp 회전 (적 없으면 카메라 정면 yaw)
  - `OnDestroy`에서 구독 해제
- [ ] **Step 2:** 컴파일 확인

---

### Task 5: AC_ArisTower.controller + Aris_CoreTower.prefab 생성 (MCP)

**Files:**
- Create: `Assets/ExternalResources/BlueArchive/Aris/AC_ArisTower.controller`
- Create: `Assets/ExternalResources/BlueArchive/Aris/Aris_CoreTower.prefab`

- [ ] **Step 1:** execute_code로 AnimatorController 생성 — 상태: Idle(`Normal_Idle`,loop) / Attack(`Normal_Attack_Ing`) / Panic(`Vital_Panic`,loop) / Death(`Vital_Death`) / Victory(`Victory_Start`). 파라미터: Attack(Trigger), LowHP(Bool), Death(Trigger), Victory(Trigger). 전이: AnyState→Attack(Attack), Attack→Idle(exit), Idle↔Panic(LowHP), AnyState→Death(Death), AnyState→Victory(Victory). 클립은 FBX 서브에셋 로드.
- [ ] **Step 2:** execute_code로 프리팹 생성 — Aris FBX Instantiate → Animator.runtimeAnimatorController=AC_ArisTower → ArisTowerVisual 추가(animator 참조 연결) → SaveAsPrefabAsset. scale 보정.
- [ ] **Step 3:** 컴파일/에러 확인

---

### Task 6: ArenaModeBootstrap 배선

**Files:**
- Modify: `Assets/Scripts/Systems/Mode/ArenaModeBootstrap.cs`

- [ ] **Step 1:** `[SerializeField] private GameObject arisTowerPrefab;` 추가 (또는 Resources/직접 참조)
- [ ] **Step 2:** SpawnCenterTower에서 기존 코어 2D 스프라이트 비주얼 비활성화 + `arisTowerPrefab` Instantiate(코어 자식) + `GetComponent<ArisTowerVisual>().Setup(coreAbility, finder, flow, coreModel)`
- [ ] **Step 3:** 인스펙터에서 arisTowerPrefab 연결 (execute_code 또는 수동)

---

### Task 7: 검증

- [ ] **Step 1:** 전체 컴파일 (`read_console` 에러 0)
- [ ] **Step 2:** EditMode 테스트 실행 — AbilityContext 변경 회귀 확인 (전 테스트 PASS)
- [ ] **Step 3:** Play 또는 프리뷰로 Idle/Attack/적추적/HP상태 시각 확인 (사용자 직접 가능)

---

## 완료 기준

- 컴파일 0 에러, EditMode 회귀 통과
- 코어 타워가 Aris로 렌더, 능력 발동 시 Attack, 적 추적 회전, 코어 HP/승리 상태 전환
- 타 모델·적 렌더링·외부 원본 무영향
