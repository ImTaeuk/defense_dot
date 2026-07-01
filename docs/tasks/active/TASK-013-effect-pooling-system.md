# TASK-013: 공용 풀링 시스템 (효과 엔티티 풀링 → 범용 확장)

**작성일**: 2026-06-16 (갱신: 2026-07-02 — 코어 설계 확정, TASK-015 선행 완료)
**상태**: 코어 설계 확정 (구현 대기) — **TASK-015(Addressables) 완료됨 → 구현 착수 가능**
**우선순위**: 높음 (상)

> 설계·핸드오프 전문: `docs/superpowers/specs/2026-07-01-pooling-addressables-design.md`
> **코어 확정 설계(정련본)**: `docs/superpowers/specs/2026-07-02-pooling-core-design.md`(+HTML)
> 선행: **TASK-015 (Addressables 인프라) — 완료** / 후속 소비자: **TASK-014 B-3 (피격 VFX)**

> **코어 설계 확정(2026-07-02, 브레인스토밍)**: 인터페이스 3분리(`IPoolable` 리셋 / `IActivatable` Activate·Deactivate+OnActivated·OnDeactivated / `IPooledObject:IDisposable` 반환). `Pool<T> where T:class,IPoolable,IActivatable`(as 없이 위임, 값 타입 차단). `IPoolFactory<T>`+`PrefabFactory`(AssetLoader)/`PocoFactory`(Creator→Factory 개명). `PoolManager`(GameContext DI, IDisposable): `PoolAsync(data)` 예열 / `Get<T>(ref, owner)` 동기 / `Return`(자식 연쇄) / `Dispose`(뿌리 절단=OUT 장부 전량 회수 누수 안전망). 반환 타이밍은 도메인이(구체 타입 OnDied/OnFinished → Dispose 연결), 메커니즘은 PoolManager 가. 편의 베이스 `PooledBehaviour`/`PooledObject` 로 14곳 마이그레이션 경감. **범위=코어만**(마이그레이션은 후속 계획).

---

## 1. 배경 / 목적

A2(코어 자동전투)에서 능력 효과 엔티티(`ProjectileEffect`·`OrbiterSetEffect` 등)와 피격 VFX(`VfxPlayer.SpawnOneShot`)가 **매번 `Instantiate`/`Destroy`** 로 동작해 GC 부담이 있다. 현재 범용 GameObject 풀링이 없고(`EnemySpawner` 자체 Dictionary 풀만 존재), 기존 `ObjectPool<T>` 는 MonoBehaviour 전용·단순하다.

**이 TASK 는 "피격 이펙트 안 보임"(TASK-014) → 풀링 필요라는 흐름에서 범위가 확장**됐다. 단순 효과 풀링이 아니라, **MonoBehaviour·POCO 를 모두 포괄하는 공용 풀링 시스템**을 신설하고 기존 풀링 자산을 흡수한다.

## 2. 확정 설계 (사용자 협의 완료)

### 2.1 인터페이스 계층 (관심사 3분리)
- `IPoolable { OnSpawn(); OnDespawn(); }` — 풀 생명주기 훅. **기존 IPoolable 흡수(시그니처 유지)**.
- `IActivatable { bool IsActive; void SetActive(bool); }` — 활성/비활성 추상(MB=SetActive, POCO=논리 플래그). 옵션 구현.
- `IPooledObject : System.IDisposable` — 반환 강제(`Dispose()`=풀 반환, 파괴 아님). `using` 자동 반납.

### 2.2 생성 — Creator 캡슐화
- `IPoolCreator<T> { T Create(); }`
- `PrefabCreator<T>`(MB) → **Addressables `AssetReferenceGameObject` 로드 후 Instantiate**.
- `PocoCreator<T>`(POCO) → `new T()`.
- 풀은 `creator.Create()` 만 호출 → MB/POCO 분기를 모름(개방-폐쇄).

### 2.3 Repository / Pool / 진입점
- `PoolRepository` : 타입 → Creator + 풀 매핑·보유. **프리팹 강한참조/SerializeField 금지**(약한참조는 Creator 뒤 Addressables).
- `Pool<T>` : `Queue<T>` + Creator 주입. `Get`/`Return`.
- `Pull<T>()` : Repository 진입점 → `IPooledObject` 반환.
- **보유/접근**: `PoolRepository` 를 **GameContext 가 보유·DI 주입**(전역 정적 상태 없음).

### 2.4 프리팹 참조 (TASK-015 의존)
- `AssetReferenceGameObject` 약한참조 → lazy 비동기 로드. **`Pull<T>()` 가 `UniTask<T>` 가 됨**(프리워밍 후 동기 핫패스 여부는 §TODO C 에서 결정).

## 3. 구조적 변경 / 마이그레이션

| 대상 | 변경 |
|---|---|
| `Assets/Scripts/Core/Interfaces/IPoolable.cs` | 계층 확장(IActivatable·IPooledObject 신설) |
| `Assets/Scripts/Core/ObjectPool.cs` | 제거 → 신 `Pool<T>` 로 대체 |
| 기존 `IPoolable` 사용처 14곳 | 신 계약으로 마이그레이션(시그니처 유지 → 본문 무변경, 풀 호출부만 교체) |
| `Assets/Scripts/Systems/Enemy/EnemySpawner.cs` | 자체 Dictionary 풀 → 공용 풀 일원화 **검토**(§TODO) |
| `Assets/Scripts/Systems/Abilities/Effects/VfxPlayer.cs` | `Instantiate`/`Destroy` → `Pull`/`Dispose` (TASK-014 B-3) |
| 외부 Hovl `HS_ParticleCollisionInstance`/`HS_DemoShooting` | 외부 에셋 — 교체 제외 검토 |

## 4. TODO

- **A. 코어 계층 (TDD 선작성)**
  - A-1. `IPoolable`/`IActivatable`/`IPooledObject` 정의.
  - A-2. `IPoolCreator<T>` + `PrefabCreator<T>`(Addressables) + `PocoCreator<T>`.
  - A-3. `Pool<T>` + `PoolRepository` + `Pull<T>()`.
  - A-4. EditMode 단위 테스트: Get/Return 재사용, Creator 분기, Dispose=반환, OnSpawn/OnDespawn 순서.
- **B. GameContext 통합**
  - B-1. `PoolRepository` 를 GameContext 가 보유·주입.
    - **정합(2026-07-01)**: `GameContext`(POCO, DI 주입)가 **UIRoot 자동배선 작업에서 이미 신설**됨(`Assets/Scripts/Domain/GameContext.cs`, 현재 13개 모델/설정 보유). 주입 경로(`GameManager`→`UIRoot.Inject(ctx)`)도 완성 → 본 항목은 `PoolRepository` 필드/생성자 인자 추가만 하면 됨. 전역 정적 상태 없는 설계와 일치.
- **C. 비동기 정책**
  - C-1. `Pull<T>()` 전부 `UniTask<T>` vs 프리워밍 후 동기 핫패스 결정.
- **D. 마이그레이션**
  - D-1. 14곳 신 계약 전환 + 기존 `ObjectPool<T>` 제거.
  - D-2. `EnemySpawner` 풀 일원화 여부 결정·적용.
  - D-3. 회귀: `EnemyBehaviorTreeTests`·`TowerBehaviorTreeTests` 그린 유지.

## 5. 검증

- 다수 효과 동시 스폰/소멸 시 GC Alloc 감소(Profiler).
- 풀 재사용 시 잔여 상태 누수 없음(히트 추적·수명·트랜스폼 초기화).
- 기존 EditMode 스위트 그린 유지(회귀).

## 6. 설계 패턴: Anti-pattern vs Target

| 구분 | Current (Anti-pattern) | Target |
|---|---|---|
| 생성/파괴 | `SpawnOneShot` = `Instantiate`+`Destroy` (GC) | 풀 `Pull`/`Dispose` 재사용 |
| 프리팹 참조 | 데이터 에셋 분산 강참조(상주) | Addressables 약한참조 lazy |
| MB/POCO 분기 | 풀 내부 타입 검사 | Creator 주입 (분기 캡슐화) |
| 반환 | 호출자 책임(누락 위험) | `IPooledObject.Dispose()`/`using` 강제 |
| 풀 접근 | (없음/자체 Dictionary 산재) | GameContext DI 단일 출처 |
