# 이펙트 풀링 마이그레이션 — 세션 핸드오프

**작성**: 2026-07-03
**목적**: 다른 세션에서 Task 3부터 이어가기 위한 상태·컨텍스트·함정 인수인계.

---

## 0. 30초 요약

- **Task 1(코드 마이그레이션) 완료·검증·커밋** (`24d67711`, EditMode 136/136, 컴파일 클린).
- **Task 2(EditMode 테스트) 스킵** — 스포너가 얇은 어댑터고 핵심 경로가 Addressables 실자산 필요 → PlayMode(Task 5)로 통합.
- **Task 3~5 남음**: ③ 에디터 Addressable화(프리팹/SO 수정) → ④ 예열 배선 → ⑤ PlayMode 검증(명중 VFX).
- **재개점**: 이 문서 + 계획 `docs/superpowers/plans/2026-07-03-effect-pooling-migration.md` 읽고 **Task 3**부터.

## 1. 근거 문서

| 문서 | 경로 |
|---|---|
| 설계 스펙 | `docs/superpowers/specs/2026-07-03-effect-pooling-migration-design.md` (+.html) |
| 구현 계획 | `docs/superpowers/plans/2026-07-03-effect-pooling-migration.md` |
| 코어 API 레퍼런스 | `docs/superpowers/specs/2026-07-02-pooling-api-reference.html` |
| 메모리 | `pooling-addressables-architecture`(설계 확정), `html-report-send-via-telegram`, `communication-explain-fully` |

## 2. 커밋 상태 (이번 세션, 이펙트 마이그레이션)

로컬 `main`, origin/main 은 `68b99482`(풀링 코어). 아래 **3커밋 미push**:
- `067b94e5` docs: 이펙트 마이그레이션 설계 스펙
- `5984c44c` docs: 구현 계획
- `24d67711` refactor: 이펙트 스폰을 공용 풀로 전환 (**Task 1 코드**)

> push는 사용자 승인 필요. (풀링 코어 `68b99482`까지는 이미 push됨.)

## 3. Task 1에서 실제로 바뀐 것 (완료분)

- `IEffectSpawner`: `Spawn<T>(AssetReferenceGameObject)` + `PlayOneShot(asset,pos,rot)` + `Release(fx)`.
- `PooledEffectSpawner`(신규): PoolManager 어댑터. `Spawn`→`Get<T>`+`Bind`, `PlayOneShot`→`Get<VfxPlayer>`+`PlayThenReturn`, `Release`→`Dispose`.
- `SimpleEffectSpawner` **삭제**.
- `AbilityEffect : PooledBehaviour`, `Bind`(중첩 VFX 스폰용 유지)·`Spawner`·`ReturnToPool()=>Dispose()`.
- `VfxPlayer : PooledBehaviour` + `PlayThenReturn()`(재생→UniTask.Delay(ResolveDuration)→Dispose, `OnDespawn`에서 lifeCts 취소). 정적 `SpawnOneShot` 제거, `EnsurePlay` 유지.
- `ProjectileEffect`: `hitVfxPrefab` 필드 제거→`hitVfxAsset`(런타임 주입, `Activate(...,hitVfx)`), 명중 시 `Spawner.PlayOneShot(hitVfxAsset,...)`, `Release()`→`ReturnToPool()`.
- `AreaZoneEffect`: `Release()`→`ReturnToPool()`.
- `AbilityData.EffectAssets`(virtual, 기본 빈 열거). `ProjectileAbilityData`·`OrbitalAbilityData`·`AreaWaveAbilityData`: 프리팹 필드→`AssetReferenceGameObject`, `EffectAssets` override, `Spawn<T>(asset)`.
- 배선: `CoreAbilitySystem.Setup(..., PoolManager)` + `pool` 필드 보관 + `PooledEffectSpawner` 생성. `ModeContext.Pooling` 추가. `GameManager`: poolManager 생성을 `CreateMode` **앞으로** 이동 + `ModeContext` 생성자에 전달. `ArenaModeBootstrap:90` Setup 호출에 `ctx.Pooling`.

## 4. Task 3 조사 결과 (재조사 불필요 — 이대로 진행)

**대상 프리팹 5개** (SO는 구 필드 참조를 아직 YAML에 보존 중 → 복구 가능):

| 능력 SO | 새 필드 | 프리팹 | GUID |
|---|---|---|---|
| `Assets/Data/Abilities/Ability_Shot.asset` | `projectileAsset` | `Assets/Prefabs/Abilities/Projectile_Water.prefab` | `6a69eecf8d11bfc43a543e8ac90745bc` |
| Ability_Shot | `muzzleAsset` | `.../MasterStylizedProjectiles/.../Par_YellowSwordBeam_Muzzle.prefab` | `aa7f91c6190ce6540a0b18ab3483498d` |
| Ability_Shot | `hitVfxAsset` | (Projectile_Water 의 구 hitVfxPrefab) | `3428211ee08e9f443a44710e9689fe26` |
| `Ability_Orbital.asset` | `orbiterAsset` | `Assets/Prefabs/Abilities/OrbiterSetEffect.prefab` | `c41eb85840eeb8a45aeda2d220e6c31b` |
| `Ability_AreaWave.asset` | `zoneAsset` | `Assets/Prefabs/Abilities/AreaZoneEffect.prefab` | `6c364e6e955529f42bf87330ff72afd5` |

**Task 3 할 일 (Unity MCP `execute_code` 에디터 스크립트, 자산 수정)**:
1. **VFX 프리팹 2개(muzzle `aa7f91…`, hit `3428211e…`) 루트에 `VfxPlayer` 컴포넌트 추가** — 풀링은 `GetComponent<VfxPlayer>`로 찾으므로 사전 부착 필수(과거엔 런타임 AddComponent였음).
   - 엔티티 3개(Projectile_Water/Orbiter/AreaZone)는 이미 각 `AbilityEffect` 서브클래스가 루트에 있음(확인만).
   - D-6 규약: 루트에 poolable 컴포넌트 1개.
2. **5개 프리팹을 Addressable 등록**(주소=안정 키, 예: 프리팹명). `AddressableAssetSettingsDefaultObject.Settings` + `CreateOrMoveEntry(guid, group)`.
3. **3개 SO의 새 AssetReference 필드 배선**: `SerializedObject(so).FindProperty("projectileAsset").FindPropertyRelative("m_AssetGUID").stringValue = guid;` 식으로 각 필드에 대응 GUID 설정 후 `ApplyModifiedProperties`+`SaveAssets`.
   - Ability_Shot: projectileAsset=6a69…, muzzleAsset=aa7f91…, hitVfxAsset=3428211e…
   - Ability_Orbital: orbiterAsset=c41e…
   - Ability_AreaWave: zoneAsset=6c36…

## 5. Task 4 (예열 배선) — ⚠️ 순서 함정 주의

**핵심 버그 주의**: `CoreAbilitySystem.Setup`이 끝에서 `runner.EquipAll()`를 즉시 호출한다. Orbital 능력은 `OnEquip`에서 `ctx.Effects.Spawn<OrbiterSetEffect>(orbiterAsset)`→`PoolManager.Get`을 부른다 → **예열 전이면 KeyNotFound**. 따라서:

- **Setup을 재구성**: 로드아웃 구성(동기)과 `EquipAll`을 분리. 아레나 진입 시 `await WarmupStartersAsync()`(스타터 전부의 `EffectAssets` 예열) **후** `EquipAll`. 즉 `Setup`에서 `EquipAll` 제거하고, 별도 `async UniTask WarmupAndEquipAsync()` 신설 → `ArenaModeBootstrap`가 Setup 직후 await.
  - 단, `ArenaModeBootstrap.CreateMode`(동기)에서 await 필요 → 호출 경로 async화 or fire-and-forget+게이트. `GameManager.Start→CreateMode` 흐름과 `Flow.SetPhase(Playing)` 시점 조정 필요(예열 완료 전 발동 방지).
- **카드 획득 예열**: `CardChoiceApplier.Apply`(static·sync) → `ApplyAsync(core, choice, PoolManager)`로. New면 `await pool.WarmupAsync(choice.data.EffectAssets)` 후 `AddAbility`. 레벨업은 예열 스킵(코어가 ContainsKey no-op). **호출부**를 grep으로 찾아 await로 전환(카드 모달 닫힘 핸들러).
- 스타터 예열 헬퍼: `EffectAssets`를 `HashSetPool`로 모아 `pool.WarmupAsync(set)`.

## 6. Task 5 (PlayMode 검증) + 코어 가드

- **코어 가드(D-5/6/7)**를 이 단계 직전에 최소 추가(계약·로직 불변):
  - D-5: `PoolManager.Get<T>`의 `pools[key]`→`TryGetValue` 실패 시 `InvalidOperationException("WarmupAsync 예열 안 됨: "+RuntimeKey)`.
  - D-6: `Pool.Get`의 `GetComponent<IPoolableObject>()` null→프리팹명 담은 명시 예외; `PoolManager.Get<T>`의 `GetComponent<T>()` null→인스턴스 풀 반납 후 명시 예외(누수 방지).
- **검증**: 발사체 스타터 아레나 PlayMode 진입 → 적 명중 → **명중 VFX 가시**(스크린샷, 원래 "피격 이펙트 안 보임" 동기 닫기) + 반복 발사 시 재사용(Instantiate 최초만) + 투사체 수명 소진 시 풀 복귀 + 아레나 종료 `poolManager.Dispose()` 정리(콘솔 에러 0).
- 종료 시 TASK-013 문서 D-4~D-7 상태 및 이 마이그레이션 완료 반영.

## 7. 이번 세션에서 발견한 함정 (반복 방지)

- **OrbiterSetEffect는 자가반납 안 함** — `OnUnequip`에서 외부 `ctx.Effects.Release(fx)`로 반납. `ReturnToPool` 대상 아님(계획 초안과 다름). 자가반납은 Projectile·AreaZone만.
- **ModeContext에 Pooling 없었음** → 추가함. **GameManager의 poolManager 생성이 CreateMode보다 뒤였음** → 앞으로 이동함(둘 다 Task 1에서 처리 완료).
- **재개 세션 Read-before-Write 가드**: 오래전 읽은 파일도 Write/Edit 전 재Read 필요(하네스).
- CRLF 경고는 무해(정상).
- VfxPlayer의 `EnsurePlay`(AreaZone/Orbiter 자식 지속 VFX)는 PlayThenReturn을 안 부르므로 자동 Dispose 안 됨(효과 엔티티가 관리) — 그대로 두면 됨.

## 8. 재개 절차

1. 이 문서 + 계획 파일 읽기.
2. `superpowers:executing-plans`로 **Task 3**부터. Unity MCP 필요(에디터·PlayMode) → 인라인 실행.
3. Task 3 자산 수정은 git 추적이라 문제 시 `git checkout -- <asset>`로 복구.
4. 각 태스크 종료 시 커밋(lint→commit) + 텔레그램 체크포인트.
