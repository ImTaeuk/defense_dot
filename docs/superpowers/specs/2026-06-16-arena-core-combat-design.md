# Arena A2 — 코어 자동전투(능력 실행) 설계

**작성일**: 2026-06-16
**상태**: 설계 확정
**관련 로드맵**: TASK-012 Arena (A1 능력 데이터 골격 후속), 풀링은 TASK-013로 분리

---

## 1. 목적 / 배경

A1에서 능력 데이터 골격만 구축되었고(실제 능력·전투 연결 없음), A2는 **Arena 코어가 능력으로 자동 전투**하도록 실행 시스템과 스타터 능력을 만든다.

원작(reference `index.html`)의 능력은 매 프레임 `tick(ab)` 모델이며 **이산형**(쿨다운마다 발동: 샷·체인·레이저)과 **지속형**(매 프레임 상태 갱신: 노바 `ab.waves` 확장, 오비탈 `ab.angle` 회전)이 섞여 있다. A1의 `Execute`(1회 발동)만으로는 지속형을 표현할 수 없다.

### 확정된 설계 결정

1. **능력 스케줄 = 합성(composition)**: 상위 단일 진입점 `ActiveAbilityData.Tick(ctx, self, dt)` + 쿨다운 발동은 재사용 헬퍼 `TickCooldown`/`ResetCooldown`, 상시 수명은 `IAbilityLifecycle`(OnEquip/OnUnequip) 인터페이스. **상호배타 서브클래스 금지** — 한 능력이 쿨다운+상시를 모두 가질 수 있어야 함(예: 터렛=상시 소환 + 주기 발사). (사용자 지적)
2. **지속성은 효과 엔티티에 위치**: 시간축 거동(이동·확장·회전)은 능력이 아니라 **자가 구동 효과 엔티티**(`AbilityEffect`)가 자기 Update로 수행. 능력은 "쿨다운마다 무엇을 스폰/상시 무엇을 유지"만 책임(거의 무상태). 그래서 노바 같은 "지속형"도 **이산 능력 + 지속 효과**로 표현됨.
3. **원작 6종류 수용, A2는 2 파운데이션**: 종류 = Projectile·Beam·Chain·Strike·AreaWave·Orbital. A2는 **샷(Projectile, 쿨다운 경로)** + **오비탈(Orbital, IAbilityLifecycle 경로)** 템플릿으로 두 경로를 모두 검증. 나머지 4종류는 추후 같은 추상화로 추가.
4. **효과 비주얼 = ExternalResources/Hovl Studio 조합**: 직접 VFX 제작 X. 우리 효과 MB가 로직·이동·데미지, Hovl 프리팹이 외형·트레일. **MCP 비주얼 검증 결과**: 샷=`AAA Projectiles Vol1/Projectiles(transform)/Projectile 14 blue rapid`(가독성 우수, 대안 `Projectile 2 electro`) — 색은 SO 교체로 조정 가능. 오비탈 위성 오브=Small*Bullet은 길쭉·방향성이라 부적합 → **컴팩트 글로우 오브로 배선 시 확정**(프리팹은 SO 필드라 교체 자유). 효과 프리팹 참조는 능력 SO가 보유.
5. **풀링 분리(TASK-013)**: 효과는 빈번 스폰되나 범용 풀이 없어 A2 범위를 키우므로 분리. A2는 교체 심(`IEffectSpawner`)을 두고 단순 구현(Instantiate/Destroy)으로 시작.
6. **코어 한정**: 능력계는 Arena 코어에만. Grid 배치 타워는 단일공격(디버그계) 유지 — Grid 회귀 없음.

### 기각된 대안

- **상호배타 서브클래스**(`CooldownAbilityData`/`PersistentAbilityData`): 단일 상속이라 "둘 다 가진 능력"에서 막힘 → 합성으로 대체.
- **원작 충실 매프레임 Tick + AbilityInstance 외부 상태만**(접근 B): SO에 외부 상태 결합·매프레임 능력 로직이 덜 Unity스러움. 지속성을 효과 엔티티로 내리는 접근 A가 관심사 분리·풀링 친화에 유리 → A 채택(단 단일 Tick 진입점은 B에서 차용).
- **A2에서 6종류 전부 구현**: 지속형 FX·상태 4종 추가로 A2 과대 → 2 파운데이션으로 축소(추상화는 6종 수용).

---

## 2. 컴포넌트

### 2.1 능력 레이어 (`DefenseDot.Systems.Abilities`, A1 정교화)

```
ActiveAbilityData : AbilityData (추상 SO)
  float baseCooldown
  virtual float ValueAtLevel(int lv)
  virtual float CooldownAtLevel(int lv)
  abstract void Tick(in AbilityContext ctx, AbilityInstance self, float dt)   // A1 Execute 대체
  protected bool TickCooldown(AbilityInstance self, float dt)
      // self.cooldownRemaining -= dt; return self.cooldownRemaining <= 0   // 감소·준비여부만, 리셋 X
  protected void ResetCooldown(AbilityInstance self, in AbilityContext ctx)
      // self.cooldownRemaining = Max(0.05, CooldownAtLevel(self.level) - ctx.Modifiers.cooldownReduction)
      // 발동 성공 후에만 호출 → 타겟 없으면 준비상태 유지(원작 충실)

interface IAbilityLifecycle           // 상시 수명 능력만 구현 (러너가 add/remove 시 호출)
  void OnEquip(in AbilityContext ctx, AbilityInstance self)
  void OnUnequip(in AbilityContext ctx, AbilityInstance self)

AbilityInstance (sealed)              // A1 + 런타임 상태 추가
  readonly AbilityData data; int level; float cooldownRemaining;
  object runtimeState                 // 효과 핸들·커스텀 상태 (상시형이 사용)

AbilityContext (readonly struct)     // A1 + 효과 스포너 추가
  MonoBehaviour Host; Vector3 Origin; TargetFinder Finder; AbilityModifiers Modifiers;
  IEffectSpawner Effects
```

**구체 템플릿 2종**:
```
ProjectileAbilityData : ActiveAbilityData
  [SF] ProjectileEffect projectilePrefab; [SF] float speed; [SF] int pierce; [SF] float range
  Tick: if (!TickCooldown(self, dt)) return;
        var t = ctx.Finder.FindNearest(ctx.Origin, range); if (t==null) return;   // 준비상태 유지·재시도
        var fx = ctx.Effects.Spawn(projectilePrefab);
        fx.Activate(ctx.Origin, t, ValueAtLevel(self.level)+ctx.Modifiers.damageBonus, speed, pierce, ctx.Finder);
        ResetCooldown(self, ctx);

OrbitalAbilityData : ActiveAbilityData, IAbilityLifecycle
  [SF] OrbiterSetEffect orbiterPrefab; [SF] float rotSpeed
  OnEquip: var fx = ctx.Effects.Spawn(orbiterPrefab);
           fx.Activate(ctx.Origin, 1+self.level, ValueAtLevel(self.level)+ctx.Modifiers.damageBonus, rotSpeed, ctx.Finder);
           self.runtimeState = fx;
  OnUnequip: if (self.runtimeState is AbilityEffect fx) ctx.Effects.Release(fx); self.runtimeState = null;
  Tick: (선택) 레벨/타겟 피드. 회전·데미지는 효과가 자가 수행 → 기본 no-op
```

### 2.2 효과 레이어 (`DefenseDot.Systems.Abilities.Effects`)

```
AbilityEffect : MonoBehaviour, IPoolable          // 자가구동 효과 베이스
  protected IEffectSpawner spawner; void Bind(IEffectSpawner s)
  protected void Release()  => spawner.Release(this)   // Destroy 대신 (풀링 심)
  virtual void OnSpawn(){} virtual void OnDespawn(){}

interface IEffectSpawner                          // A2 단순 구현, TASK-013서 풀로 교체
  T Spawn<T>(T prefab) where T : AbilityEffect
  void Release(AbilityEffect fx)

ProjectileEffect : AbilityEffect                  // DebugProjectile 로직 승격 + Hovl 비주얼
  void Activate(Vector3 origin, ITargetable target, float dmg, float speed, int pierce, TargetFinder finder)
  Update: 유도·이동·명중 시 IDamageable.TakeDamage·관통(미명중 최근접) / 수명·관통 소진 → Release()

OrbiterSetEffect : AbilityEffect                  // 상시 회전 위성
  void Activate(Vector3 center, int count, float dmg, float rotSpeed, TargetFinder finder)
  Update: angle += rotSpeed*dt; 반경=최근접 적 거리 추종; 위성 접촉 적에 데미지(재타격 쿨다운 Map)
  // 외부(OnUnequip)에서 Release
```

> 데미지=`IDamageable.TakeDamage`(MonsterActor 구현), 질의=`TargetFinder.FindNearest/FindAllInRange`. 히트추적(관통 Set·재타격 Map)은 **효과 인스턴스가 보유**. 비주얼은 Hovl 프리팹을 효과 프리팹의 자식/본체로 구성.

### 2.3 러너 + 코어 배선

```
AbilityRunner (순수 C#)
  ctor(AbilityLoadout loadout, AbilityContext ctx)   // 또는 ctx 팩토리
  void Tick(float dt): for each loadout.Actives → inst.data is ActiveAbilityData a → a.Tick(ctx, inst, dt)
  void Equip(AbilityInstance inst):   if data is IAbilityLifecycle l → l.OnEquip(ctx, inst)
  void Unequip(AbilityInstance inst): if data is IAbilityLifecycle l → l.OnUnequip(ctx, inst)

CoreAbilitySystem : MonoBehaviour    // Arena 중앙 코어
  AbilityLoadout loadout; AbilityRunner runner; GameFlowModel flow(주입)
  void Setup(TargetFinder finder, Vector3 origin, IEffectSpawner effects, IReadOnlyList<AbilityData> starters, GameFlowModel flow)
      → loadout 구성(TryAdd starters) + ctx 구성 + runner.Equip(각 active)
  Update: if (flow.IsPlaying) runner.Tick(Time.deltaTime)

ArenaModeBootstrap (수정)
  [SF] List<AbilityData> starterAbilities   // 샷·오비탈
  CreateMode/스폰 시: 코어 GO에 CoreAbilitySystem 부착·Setup 호출(finder=ctx.TargetFinder, origin=코어 중심,
      effects=단순 IEffectSpawner, starters, flow). 코어는 디버그 단일공격 경로 미사용.
```

> `IEffectSpawner` 단순 구현체(`SimpleEffectSpawner`): `Spawn`=`Instantiate(prefab)` 후 `Bind(this)`, `Release`=`Destroy`. TASK-013서 풀 구현으로 교체.

---

## 3. 데이터 흐름

```
CoreAbilitySystem.Update (flow.IsPlaying)
  → AbilityRunner.Tick(dt)
     → 각 액티브 ability.Tick(ctx, inst, dt)
        → [이산] TickCooldown 준비 → 타겟 확인 → Fire(ctx.Effects.Spawn(prefab).Activate(...)) → ResetCooldown
        → [상시] (OnEquip에서 이미 스폰) Tick은 피드/no-op
  → 효과 엔티티 자가 Update → IDamageable.TakeDamage(적)
  → 적 사망 → EnemySpawner.HandleEnemyKilled → CombatModel/ScoreModel (기존 A0/HUD 흐름)
```

---

## 4. 엣지 / 에러 처리

| 상황 | 처리 |
|---|---|
| 타겟 없음(이산) | `TickCooldown`이 준비(true) 반환해도 타겟 없으면 `ResetCooldown` 미호출 → **준비상태 유지, 다음 프레임 재시도**(원작 `if(!target) return` 충실) |
| 효과 핸들 분실(상시) | OnUnequip에서 `runtimeState is AbilityEffect` 확인 후 Release, null 처리 |
| 일시정지/게임오버 | `CoreAbilitySystem.Update`의 `flow.IsPlaying` 게이트 → 러너 미틱 |
| 풀 없음(A2) | `SimpleEffectSpawner` Instantiate/Destroy로 동작. 성능 최적화는 TASK-013 |
| Grid 모드 | CoreAbilitySystem 미부착, 디버그 단일공격 유지 — 무영향 |
| 능력 레벨 변경(상시) | A2: 재장착/스폰 재생성 또는 효과에 SetLevel 피드(템플릿 단순화 위해 후속) |

---

## 5. 테스트 / 검증

### 5.1 EditMode 순수 단위 (스텁 능력 = ScriptableObject.CreateInstance)
- `CooldownHelperTests`: `TickCooldown` dt 누적으로 0 도달 시 true, `ResetCooldown` 리셋값=CooldownAtLevel−cooldownReduction(0.05 클램프), 타겟없음 시 미리셋→연속 준비 유지.
- `AbilityRunnerTests`: 스텁 액티브 N개 Tick 호출 횟수, `IAbilityLifecycle` 스텁의 OnEquip/OnUnequip 호출, loadout 연동.

### 5.2 Play 검증 (효과·코어 통합 — MonoBehaviour)
| # | 시나리오 | 기대 |
|---|---|---|
| 1 | Arena 진입(샷·오비탈 스타터) | 코어가 자동으로 샷 발사 + 위성 회전 |
| 2 | 샷 | 쿨다운마다 최근접 적 유도 투사체 → 명중 데미지·관통 |
| 3 | 오비탈 | 위성 회전·반경 추종·접촉 데미지 |
| 4 | 처치 연동 | 적 사망 시 골드·점수 반영(기존 HUD) |
| 5 | 일시정지/게임오버 | 능력 정지 |
| 6 | Grid 회귀 | Grid 타워 단일공격 정상(능력계 미개입) |

---

## 6. 영향도 / 위험도

| 항목 | 내용 |
|---|---|
| 신규 | `IAbilityLifecycle`, `AbilityEffect`, `IEffectSpawner`, `SimpleEffectSpawner`, `ProjectileEffect`, `OrbiterSetEffect`, `ProjectileAbilityData`, `OrbitalAbilityData`, `AbilityRunner`, `CoreAbilitySystem`, 효과 프리팹(Hovl 조합), 능력 SO 에셋(샷·오비탈), 테스트 2종 |
| 수정 | `ActiveAbilityData`(Execute→Tick+TickCooldown/ResetCooldown), `AbilityInstance`(runtimeState), `AbilityContext`(Effects), `ArenaModeBootstrap`(starter·CoreAbilitySystem 배선) |
| 회귀 위험 | A1 `Execute`는 미사용이라 교체 안전. 코어 전투 신설 — Grid 무영향(디버그계 잔존). 효과 비주얼/스케일은 Play 튜닝 |
| 비고 | 디버그 공격계(IAttackBehavior/DebugProjectile/AttackContext/토글)는 코어 경로 미사용. Grid용 잔존 → Grid 실타워 도입 시 별도 정리 |

---

## 7. 설계 패턴 메모

- **합성 > 상속**: 능력 종류를 서브클래스로 가르지 않고 단일 Tick + 헬퍼(`TickCooldown`/`ResetCooldown`) + 인터페이스(`IAbilityLifecycle`)로 조합 → 쿨다운·상시 동시 보유 가능.
- **관심사 분리**: 능력=스케줄/스폰, 효과 엔티티=시간축 거동·데미지. 지속성은 효과에 캡슐화.
- **Context Object**: `AbilityContext`로 입력 묶음(A1 `AbilityContext`·HUD `HudContext`와 동일 패턴), `Effects` 추가.
- **교체 심**: `IEffectSpawner`로 풀링 구현을 무수정 교체(TASK-013).
- **자산 재사용**: VFX는 ExternalResources/Hovl 조합 — 로직 MB + 외형 프리팹 분리.
