# TASK-013: 효과 엔티티 풀링 시스템

**작성일**: 2026-06-16
**상태**: 등록(보류) — A2 이후 진행
**우선순위**: 중간 (성능 최적화)

---

## 1. 배경 / 목적

A2(코어 자동전투)에서 능력 효과 엔티티(`ProjectileEffect`·`OrbiterSetEffect`·추후 `WaveEffect` 등)를 도입한다. 효과는 쿨다운마다 빈번히 스폰되므로(투사체 등) 풀링이 성능에 유리하나, **현재 프로젝트에 범용 GameObject 풀링 시스템이 없다**(EnemySpawner가 자체 Dictionary 풀을 보유할 뿐). 풀링 시스템 신설은 A2 범위를 키우므로 **별도 Task로 분리**한다.

> 기존 `DebugProjectile`은 매 발 `CreatePrimitive`+`Destroy`로 GC 부담이 있었다. A2의 효과 엔티티는 이 패턴을 답습하지 않도록 **풀링 교체 심(seam)을 미리 둔다**.

## 2. A2에서 준비되는 선결 조건 (seam)

A2 설계에 다음 추상화를 포함해 풀링을 나중에 무수정 교체 가능하게 한다.

- `AbilityEffect : MonoBehaviour, IPoolable` — `Release()`는 직접 `Destroy`가 아니라 `IEffectSpawner.Release(this)` 위임.
- `IEffectSpawner { T Spawn<T>(T prefab); void Release(AbilityEffect fx); }` — A2는 **단순 구현**(Spawn=Instantiate, Release=Destroy)을 제공.
- 능력은 항상 `ctx.Effects.Spawn(prefab)` / `ctx.Effects.Release(handle)`만 호출 → 구현 교체에 능력·효과 코드 불변.

## 3. 본 Task 범위 (A2 이후)

- `IEffectSpawner`의 풀링 구현체 작성 — 프리팹 키 풀(EnemySpawner 패턴) 또는 `UnityEngine.Pool.ObjectPool<T>` 래핑.
- `IPoolable.OnSpawn/OnDespawn`으로 효과 상태 초기화(히트 Set/Map·수명·트랜스폼) 보장.
- 컨테이너·프리워밍·최대치 정책 결정.
- EnemySpawner의 자체 풀과 통합 여부 검토(공용 풀 서비스로 일원화 가능성).
- A2 단순 구현체를 풀링 구현체로 교체 후 회귀 확인.

## 4. 검증

- 다수 효과 동시 스폰/소멸 시 GC Alloc 감소(Profiler) 확인.
- 풀 재사용 시 잔여 상태 누수 없음(히트 추적·수명 초기화) 확인.
