# 디버그용 공격 타입 3종 Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** 타워에 단일/범위/투사체 공격을 디버그용으로 붙여, 플레이 중 인스펙터 토글로 add/remove 하며 플레이 루프를 눈으로 검증한다.

**Architecture:** 기존 `IMovementStrategy`(이동)와 동형의 `IAttackBehavior` 전략 패턴. `TowerActor`가 활성 behavior 리스트를 순회 실행. 3 behavior + 투사체는 전부 throwaway(`// DEBUG`), 실제 능력 시스템 구현 시 삭제.

**Tech Stack:** Unity 6000.2 / URP 17 / C# / UniTask(미사용 — 본 기능은 `Update` 기반) / NUnit EditMode 테스트.

**선행 스펙:** [2026-06-08-debug-attack-types-design.md](../specs/2026-06-08-debug-attack-types-design.md)

---

## 이 계획 공통 규칙 (모든 태스크에 적용)

- **네임스페이스**: 신규 파일은 `DefenseDot.Systems.Tower.Debugging` — **`.Debug` 금지**(`UnityEngine.Debug` 와 충돌). 폴더는 `Assets/Scripts/Systems/Tower/Debug/`.
- **파일명 = 첫 타입명** (PostToolUse `sync_cs_filename.py` 훅) → 파일당 타입 1개.
- 모든 신규 throwaway 파일·필드 상단/옆에 `// DEBUG` 표식.
- **현재 브랜치 `feature/arena-map-system`** 작업 트리에 무관한 HD2D 미추적 파일(`Assets/Scripts/Systems/Visual/`, `Assets/Tests/EditMode/Camera*Tests.cs`, `docs/superpowers/{plans,specs}/2026-06-07-hd2d-*`, `docs/tasks/active/TASK-003-*`)이 있다. **절대 `git add .`/`git add -A` 금지** — 커밋 시 본 계획이 만든 파일 경로만 명시 staging.
- 커밋 전 `lint` 스킬은 **이번에 만든/수정한 `.cs` 파일 경로만 범위로** 수행(HD2D 미추적 `.cs` 미접촉).
- `.cs` 신규/수정 시 `.meta` 동반 커밋.

---

## File Structure

**Create**
- `Assets/Scripts/Systems/Tower/Debug/AttackContext.cs` — 공격 1회 입력 묶음(struct)
- `Assets/Scripts/Systems/Tower/Debug/IAttackBehavior.cs` — 전략 인터페이스
- `Assets/Scripts/Systems/Tower/Debug/SingleTargetAttack.cs` — 단일 타겟 + 라인
- `Assets/Scripts/Systems/Tower/Debug/AoeAttack.cs` — 반경 전체 + 원
- `Assets/Scripts/Systems/Tower/Debug/ProjectileAttack.cs` — 투사체 발사
- `Assets/Scripts/Systems/Tower/Debug/DebugProjectile.cs` — 투사체 mover(코드 생성 구)
- `Assets/Tests/EditMode/AttackBehaviorTests.cs` — FindAllInRange / AoE 선택 테스트

**Modify**
- `Assets/Scripts/Systems/Tower/TargetFinder.cs` — `FindAllInRange` 추가
- `Assets/Scripts/Systems/Tower/TowerActor.cs` — behavior 리스트 위임 + 토글

---

## Task 1: TargetFinder.FindAllInRange (TDD)

**Files:**
- Modify: `Assets/Scripts/Systems/Tower/TargetFinder.cs`
- Test: `Assets/Tests/EditMode/AttackBehaviorTests.cs`

- [ ] **Step 1: 실패 테스트 작성** — `Assets/Tests/EditMode/AttackBehaviorTests.cs` 생성

```csharp
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using DefenseDot.Core;
using DefenseDot.Data;
using DefenseDot.Systems.Enemy;
using DefenseDot.Systems.Tower;

public class AttackBehaviorTests
{
    private readonly List<GameObject> created = new List<GameObject>();

    [TearDown]
    public void TearDown()
    {
        foreach (GameObject go in created)
            if (go != null) Object.DestroyImmediate(go);
        created.Clear();
    }

    private MonsterActor MakeEnemy(EnemyRegistry reg, Vector3 pos, float health)
    {
        GameObject go = new GameObject("TestEnemy");
        go.transform.position = pos;
        created.Add(go);
        MonsterActor actor = go.AddComponent<MonsterActor>();
        EnemyData data = ScriptableObject.CreateInstance<EnemyData>();
        data.health = health;
        actor.Initialize(data);   // currentHealth = health, 기본 상태 Idle → IsActive true
        reg.Register(actor);
        return actor;
    }

    [Test]
    public void FindAllInRange_ReturnsOnlyEnemiesWithinRadius()
    {
        EnemyRegistry reg = new EnemyRegistry();
        MonsterActor inside  = MakeEnemy(reg, new Vector3(1f, 0f, 0f), 10f);
        MonsterActor edge    = MakeEnemy(reg, new Vector3(3f, 0f, 0f), 10f); // 거리 3 == 반경
        MonsterActor outside = MakeEnemy(reg, new Vector3(5f, 0f, 0f), 10f);
        TargetFinder finder = new TargetFinder(reg);

        List<ITargetable> results = new List<ITargetable>();
        finder.FindAllInRange(Vector3.zero, 3f, results);

        Assert.Contains(inside, results);
        Assert.Contains(edge, results);                 // 경계 포함(<=)
        Assert.IsFalse(results.Contains(outside));
        Assert.AreEqual(2, results.Count);
    }
}
```

- [ ] **Step 2: 컴파일 실패 확인** — Unity 콘솔에서 `TargetFinder.FindAllInRange` 미정의로 컴파일 에러 확인 (테스트 실행 불가 = 실패).

- [ ] **Step 3: 최소 구현** — `TargetFinder.cs` 수정. 상단 using에 `using System.Collections.Generic;` 추가(없으면). `FindNearest` 아래에 추가:

```csharp
        /// <summary>
        /// 원점에서 사거리 내 모든 활성 적을 results에 채웁니다. (제곱거리 비교)
        /// </summary>
        public void FindAllInRange(Vector3 origin, float range, List<ITargetable> results)
        {
            if (registry == null || results == null) return;

            float rangeSqr = range * range;
            var actors = registry.Actors;
            for (int i = 0; i < actors.Count; i++)
            {
                MonsterActor actor = actors[i];
                if (actor == null || !actor.IsActive) continue;
                if ((actor.Position - origin).sqrMagnitude <= rangeSqr) results.Add(actor);
            }
        }
```

- [ ] **Step 4: 테스트 통과 확인** — Unity Test Runner(EditMode) → `FindAllInRange_ReturnsOnlyEnemiesWithinRadius` PASS, 기존 9개 회귀 0.

- [ ] **Step 5: lint + 커밋** — `lint` 스킬을 `TargetFinder.cs`, `AttackBehaviorTests.cs` 범위로 수행 후:

```bash
git add Assets/Scripts/Systems/Tower/TargetFinder.cs Assets/Scripts/Systems/Tower/TargetFinder.cs.meta \
        "Assets/Tests/EditMode/AttackBehaviorTests.cs" "Assets/Tests/EditMode/AttackBehaviorTests.cs.meta"
git commit -m "feat: TargetFinder 에 반경 내 전체 적 질의(FindAllInRange) 추가"
```

---

## Task 2: AttackContext + IAttackBehavior (계약)

**Files:**
- Create: `Assets/Scripts/Systems/Tower/Debug/AttackContext.cs`
- Create: `Assets/Scripts/Systems/Tower/Debug/IAttackBehavior.cs`

- [ ] **Step 1: AttackContext.cs 작성**

```csharp
// DEBUG: 공격 타입 테스트용 — 실제 능력 시스템 구현 시 삭제
using UnityEngine;
using DefenseDot.Data;

namespace DefenseDot.Systems.Tower.Debugging
{
    /// <summary>
    /// 공격 1회 수행에 필요한 입력 묶음입니다. (DEBUG)
    /// </summary>
    public readonly struct AttackContext
    {
        /// <summary>투사체 생성 등에 쓰는 호스트 MonoBehaviour입니다.</summary>
        public readonly MonoBehaviour Host;
        /// <summary>공격 시작점(타워 위치)입니다.</summary>
        public readonly Vector3 Origin;
        /// <summary>적 질의 수단입니다.</summary>
        public readonly TargetFinder Finder;
        /// <summary>타워 능력치 데이터입니다.</summary>
        public readonly TowerData Data;

        public AttackContext(MonoBehaviour host, Vector3 origin, TargetFinder finder, TowerData data)
        {
            Host = host;
            Origin = origin;
            Finder = finder;
            Data = data;
        }
    }
}
```

- [ ] **Step 2: IAttackBehavior.cs 작성**

```csharp
// DEBUG: 공격 타입 테스트용 — 실제 능력 시스템 구현 시 삭제
namespace DefenseDot.Systems.Tower.Debugging
{
    /// <summary>
    /// 공격 타입 전략입니다. 1회 공격을 수행하고 디버그 비주얼을 그립니다. (DEBUG)
    /// </summary>
    public interface IAttackBehavior
    {
        /// <summary> 주어진 컨텍스트로 공격 1회를 수행합니다. </summary>
        void Execute(in AttackContext ctx);
    }
}
```

- [ ] **Step 3: 컴파일 확인** — Unity 콘솔 에러 0.

- [ ] **Step 4: 커밋** (테스트 없는 계약 파일)

```bash
git add "Assets/Scripts/Systems/Tower/Debug/AttackContext.cs" "Assets/Scripts/Systems/Tower/Debug/AttackContext.cs.meta" \
        "Assets/Scripts/Systems/Tower/Debug/IAttackBehavior.cs" "Assets/Scripts/Systems/Tower/Debug/IAttackBehavior.cs.meta" \
        "Assets/Scripts/Systems/Tower/Debug.meta"
git commit -m "feat: 공격 타입 전략 계약(IAttackBehavior/AttackContext) 추가"
```

---

## Task 3: SingleTargetAttack

**Files:**
- Create: `Assets/Scripts/Systems/Tower/Debug/SingleTargetAttack.cs`

- [ ] **Step 1: 구현 작성**

```csharp
// DEBUG: 공격 타입 테스트용 — 실제 능력 시스템 구현 시 삭제
using UnityEngine;
using DefenseDot.Core;

namespace DefenseDot.Systems.Tower.Debugging
{
    /// <summary> 최근접 적 1체에 즉시 데미지 + 타워→타겟 라인. (DEBUG) </summary>
    public class SingleTargetAttack : IAttackBehavior
    {
        public void Execute(in AttackContext ctx)
        {
            if (ctx.Finder == null || ctx.Data == null) return;
            ITargetable target = ctx.Finder.FindNearest(ctx.Origin, ctx.Data.attackRange);
            if (target == null) return;
            if (target is IDamageable damageable) damageable.TakeDamage(ctx.Data.attackDamage);
            UnityEngine.Debug.DrawLine(ctx.Origin, target.Position, Color.cyan, 0.08f);
        }
    }
}
```

- [ ] **Step 2: 컴파일 확인** — 에러 0.

- [ ] **Step 3: lint + 커밋**

```bash
git add "Assets/Scripts/Systems/Tower/Debug/SingleTargetAttack.cs" "Assets/Scripts/Systems/Tower/Debug/SingleTargetAttack.cs.meta"
git commit -m "feat: 단일 타겟 공격 behavior 추가"
```

---

## Task 4: AoeAttack (TDD)

**Files:**
- Create: `Assets/Scripts/Systems/Tower/Debug/AoeAttack.cs`
- Test: `Assets/Tests/EditMode/AttackBehaviorTests.cs` (테스트 추가)

- [ ] **Step 1: 실패 테스트 추가** — `AttackBehaviorTests` 클래스 안에 메서드 추가. 파일 상단 using에 `using DefenseDot.Systems.Tower.Debugging;` 추가.

```csharp
    [Test]
    public void AoeAttack_KillsEnemiesInRange_SparesOutside()
    {
        EnemyRegistry reg = new EnemyRegistry();
        MonsterActor inside  = MakeEnemy(reg, new Vector3(1f, 0f, 0f), 1f);
        MonsterActor outside = MakeEnemy(reg, new Vector3(5f, 0f, 0f), 1f);
        TargetFinder finder = new TargetFinder(reg);

        TowerData data = ScriptableObject.CreateInstance<TowerData>();
        data.attackDamage = 5f;
        data.attackRange = 3f;
        AttackContext ctx = new AttackContext(null, Vector3.zero, finder, data);

        new AoeAttack().Execute(in ctx);

        Assert.IsFalse(inside.IsActive,  "범위 내 적은 처치되어야 함");
        Assert.IsTrue(outside.IsActive,  "범위 밖 적은 생존해야 함");
    }
```

- [ ] **Step 2: 컴파일 실패 확인** — `AoeAttack` 미정의 컴파일 에러.

- [ ] **Step 3: 구현 작성** — `AoeAttack.cs`

```csharp
// DEBUG: 공격 타입 테스트용 — 실제 능력 시스템 구현 시 삭제
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Pool;
using DefenseDot.Core;

namespace DefenseDot.Systems.Tower.Debugging
{
    /// <summary> 반경(=attackRange) 내 전체 적에 즉시 데미지 + 원 비주얼. (DEBUG) </summary>
    public class AoeAttack : IAttackBehavior
    {
        public void Execute(in AttackContext ctx)
        {
            if (ctx.Finder == null || ctx.Data == null) return;
            float radius = ctx.Data.attackRange;

            List<ITargetable> hits = ListPool<ITargetable>.Get();
            ctx.Finder.FindAllInRange(ctx.Origin, radius, hits);
            for (int i = 0; i < hits.Count; i++)
                if (hits[i] is IDamageable damageable) damageable.TakeDamage(ctx.Data.attackDamage);
            ListPool<ITargetable>.Release(hits);

            DrawCircle(ctx.Origin, radius, Color.magenta);
        }

        private static void DrawCircle(Vector3 center, float radius, Color color)
        {
            const int seg = 24;
            Vector3 prev = center + new Vector3(radius, 0f, 0f);
            for (int i = 1; i <= seg; i++)
            {
                float a = (i / (float)seg) * Mathf.PI * 2f;
                Vector3 next = center + new Vector3(Mathf.Cos(a) * radius, 0f, Mathf.Sin(a) * radius);
                UnityEngine.Debug.DrawLine(prev, next, color, 0.08f);
                prev = next;
            }
        }
    }
}
```

- [ ] **Step 4: 테스트 통과 확인** — Test Runner: `AoeAttack_KillsEnemiesInRange_SparesOutside` + 기존 전부 PASS.

- [ ] **Step 5: lint + 커밋**

```bash
git add "Assets/Scripts/Systems/Tower/Debug/AoeAttack.cs" "Assets/Scripts/Systems/Tower/Debug/AoeAttack.cs.meta" \
        "Assets/Tests/EditMode/AttackBehaviorTests.cs"
git commit -m "feat: 범위(AoE) 공격 behavior 추가"
```

---

## Task 5: ProjectileAttack + DebugProjectile

**Files:**
- Create: `Assets/Scripts/Systems/Tower/Debug/DebugProjectile.cs`
- Create: `Assets/Scripts/Systems/Tower/Debug/ProjectileAttack.cs`

> MonoBehaviour 이동·GameObject 생성이라 단위 테스트 대신 Task 7 PlayMode 검증.

- [ ] **Step 1: DebugProjectile.cs 작성**

```csharp
// DEBUG: 공격 타입 테스트용 — 실제 능력 시스템 구현 시 삭제
using UnityEngine;
using DefenseDot.Core;

namespace DefenseDot.Systems.Tower.Debugging
{
    /// <summary> 코드 생성 디버그 투사체. 타겟으로 이동, 근접 시 데미지, 관통/수명 종료 시 파괴. (DEBUG) </summary>
    public class DebugProjectile : MonoBehaviour
    {
        private ITargetable target;
        private float damage;
        private float speed;
        private int pierceRemaining;
        private float life;

        /// <summary> 디버그 투사체를 생성해 발사합니다. </summary>
        public static void Spawn(Vector3 origin, ITargetable target, float damage, float speed, int pierce)
        {
            GameObject go = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            go.name = "DebugProjectile";
            go.transform.position = origin;
            go.transform.localScale = Vector3.one * 0.25f;
            Object.Destroy(go.GetComponent<Collider>());

            DebugProjectile p = go.AddComponent<DebugProjectile>();
            p.target = target;
            p.damage = damage;
            p.speed = speed;
            p.pierceRemaining = pierce;
            p.life = 3f;
        }

        private void Update()
        {
            life -= Time.deltaTime;
            if (life <= 0f) { Destroy(gameObject); return; }

            Vector3 aim = (target != null && target.IsActive)
                ? target.Position
                : transform.position + transform.forward;
            transform.position = Vector3.MoveTowards(transform.position, aim, speed * Time.deltaTime);

            if (target != null && target.IsActive &&
                (transform.position - target.Position).sqrMagnitude < 0.09f)
            {
                if (target is IDamageable damageable) damageable.TakeDamage(damage);
                pierceRemaining--;
                if (pierceRemaining <= 0) { Destroy(gameObject); return; }
                target = null;   // 관통 단순화: 이후 직진하다 수명 종료
            }
        }
    }
}
```

- [ ] **Step 2: ProjectileAttack.cs 작성**

```csharp
// DEBUG: 공격 타입 테스트용 — 실제 능력 시스템 구현 시 삭제
using DefenseDot.Core;

namespace DefenseDot.Systems.Tower.Debugging
{
    /// <summary> 최근접 적을 향해 디버그 투사체를 발사. (DEBUG) </summary>
    public class ProjectileAttack : IAttackBehavior
    {
        private const float Speed = 12f;
        private const int PierceMax = 3;

        public void Execute(in AttackContext ctx)
        {
            if (ctx.Finder == null || ctx.Data == null || ctx.Host == null) return;
            ITargetable target = ctx.Finder.FindNearest(ctx.Origin, ctx.Data.attackRange);
            if (target == null) return;
            DebugProjectile.Spawn(ctx.Origin, target, ctx.Data.attackDamage, Speed, PierceMax);
        }
    }
}
```

- [ ] **Step 3: 컴파일 확인** — 에러 0.

- [ ] **Step 4: lint + 커밋**

```bash
git add "Assets/Scripts/Systems/Tower/Debug/DebugProjectile.cs" "Assets/Scripts/Systems/Tower/Debug/DebugProjectile.cs.meta" \
        "Assets/Scripts/Systems/Tower/Debug/ProjectileAttack.cs" "Assets/Scripts/Systems/Tower/Debug/ProjectileAttack.cs.meta"
git commit -m "feat: 투사체 공격 behavior 및 디버그 투사체 추가"
```

---

## Task 6: TowerActor 통합 (토글 + 위임)

**Files:**
- Modify: `Assets/Scripts/Systems/Tower/TowerActor.cs`

- [ ] **Step 1: TowerActor 수정** — using에 `using System.Collections.Generic;` 와 `using DefenseDot.Systems.Tower.Debugging;` 추가. 필드 영역(`targetFinder` 아래)에 추가:

```csharp
        // DEBUG: 공격 타입 테스트 — 실제 능력 시스템 구현 시 삭제
        [Header("DEBUG Attack Toggles")]
        [SerializeField] private bool debugSingle = true;
        [SerializeField] private bool debugAoe = false;
        [SerializeField] private bool debugProjectile = false;

        private readonly IAttackBehavior singleBehavior = new SingleTargetAttack();
        private readonly IAttackBehavior aoeBehavior = new AoeAttack();
        private readonly IAttackBehavior projectileBehavior = new ProjectileAttack();
        private readonly List<IAttackBehavior> activeBehaviors = new List<IAttackBehavior>();
```

- [ ] **Step 2: PerformAttack 교체** — 기존 단일 타겟 본문을 위임으로 교체:

```csharp
        public void PerformAttack()
        {
            if (currentTarget == null || !currentTarget.IsActive)
            {
                SetState(ActorState.Idle);
                return;
            }

            SetState(ActorState.Attacking);

            // DEBUG: 토글에서 활성 behavior 구성 후 순회 실행
            activeBehaviors.Clear();
            if (debugSingle) activeBehaviors.Add(singleBehavior);
            if (debugAoe) activeBehaviors.Add(aoeBehavior);
            if (debugProjectile) activeBehaviors.Add(projectileBehavior);

            if (activeBehaviors.Count == 0) return;
            AttackContext ctx = new AttackContext(this, Position, targetFinder, data);
            for (int i = 0; i < activeBehaviors.Count; i++) activeBehaviors[i].Execute(in ctx);
        }
```

> 비고: `Update`의 `IsTargetValid`/`SearchTarget` 게이팅은 그대로 둔다 — "사거리 내 적 존재" 트리거 역할(behavior가 자체 재질의). 단일 토글 off + 범위/투사체 on 이어도 최근접 적이 사거리 내면 트리거된다.

- [ ] **Step 3: 컴파일 확인 + 회귀** — 에러 0, 기존 EditMode 9 + 신규 2 PASS.

- [ ] **Step 4: lint + 커밋**

```bash
git add Assets/Scripts/Systems/Tower/TowerActor.cs Assets/Scripts/Systems/Tower/TowerActor.cs.meta
git commit -m "refactor: TowerActor 공격을 behavior 리스트 위임으로 전환하고 디버그 토글 추가"
```

---

## Task 7: PlayMode 수동 검증 (커밋 없음)

**전제:** Unity 에디터에서 Grid 씬에 타워 설치 가능 상태(앞서 만든 `Tower_Test` 프리팹 결선). 본 세션의 Claude는 Unity 실행 불가 → 사용자가 수행.

- [ ] **V1 단일** — `debugSingle`만 on. 플레이 → 타워가 최근접 적에 cyan 라인, 적 처치, 골드 증가(HUD).
- [ ] **V2 범위** — `debugAoe`만 on. 반경(magenta 원) 내 여러 적 동시 처치. (Game 뷰 우상단 Gizmos 토글 필요)
- [ ] **V3 투사체** — `debugProjectile`만 on. 작은 구가 적으로 날아가 도달 시 데미지(최대 3관통).
- [ ] **V4 스택** — 플레이 중 3 토글을 하나씩 켜고/끄며 동시 적용·해제가 즉시 반영되는지.
- [ ] **V5 루프** — 적 처치→골드, 적 코어 도달→체력 감소(HUD), 웨이브 전환, 승/패 도달까지 한 바퀴.

> 라인/원은 Scene 뷰 항상·Game 뷰 Gizmos 토글 시 표시. 투사체는 실제 오브젝트라 토글 무관.

---

## Self-Review (작성자 점검 완료)

- **스펙 커버리지**: 단일/범위/투사체(T3·T4·T5), 토글(T6), FindAllInRange(T1), 디버그 비주얼(각 behavior), TowerData 불변(어느 태스크도 미수정), 테스트(T1·T4 + T7) — §1~§7 전부 태스크 대응.
- **플레이스홀더 스캔**: 모든 코드 단계에 완전한 코드 포함, "TBD/적절히" 없음.
- **타입 일관성**: `AttackContext(host, origin, finder, data)` 생성자 ↔ 사용처 일치. `Execute(in AttackContext)` 시그니처 전 behavior 동일. `FindAllInRange(origin, range, List<ITargetable>)` ↔ 호출처 일치. `DebugProjectile.Spawn(origin, target, damage, speed, pierce)` ↔ 호출 일치.
