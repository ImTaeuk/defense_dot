# Sweeper 적 3D 교체 설계

**작성일**: 2026-06-20
**상태**: 설계 승인 완료
**대상**: Arena 모드 적(원형 공전 몬스터)
**범위**: 2D 스프라이트 적 → Sweeper 3D 모델+애니메이션 전적 교체 (이동방향 회전, 피격 플래시, 사망 dissolve)

---

## 1. 목표 / 성공 기준

- Arena 적 비주얼을 2D 스프라이트(BillboardSprite)에서 Sweeper 3D 모델(BA_ToonLit)로 전적 교체한다.
- 적이 `Move_Ing` 애니메이션으로 이동하고, **이동 방향으로 Y축 회전**한다.
- 데미지 시 **피격 플래시**(흰색 펄스, 기존 기능 유지), 사망 시 **애니 정지 + dissolve(1초)** 후 풀 반환.
- 색상은 스폰마다 Mint/Pink/Yellow 중 랜덤.
- 성공 기준: Play에서 공전 이동·이동방향 회전·피격 플래시·사망 dissolve·풀 재사용(색 재랜덤)이 정상 동작하고, 기존 적 로직(이동 전략·풀링·타겟팅)은 회귀 없음.

## 2. 배경 / 현황 (탐색 결과)

- `MonsterActor : ActorBase<EnemyData>, IMovableActor, ITargetable, IPoolable`. Visual 자식에 SpriteRenderer + Animator + BillboardSprite + ActorAnimatorBinder + Shadow.
- 이동 = `IMovementStrategy` 전략 패턴, Arena = `ArenaOrbitLogic`(원형 공전, `HasReachedGoal` 항상 false). `EnemyBehaviorTree`가 매 프레임 Tick.
- 사망 = `MonsterActor.Resolve(reached)` → `SetState(Dead)` + `spawner.HandleEnemyKilled` → 즉시 `ReturnToPool`(SetActive false).
- `ITargetable.IsActive` = `state != Dead` → 죽으면 능력 타겟에서 자동 제외.
- 풀링 = `EnemySpawner`의 `Dictionary<prefab, Queue<MonsterActor>>`.
- `ActorAnimatorBinder`(2D 전용): StateChanged→State int, 이동 delta→Direction 4방향. **3D엔 부적합** → 신규 비주얼 컴포넌트로 대체.
- Hovl `DissolveNoise.shader`는 파티클(Transparent/CG)용 → 스킨드메시 부적합. **BA_ToonLit 확장**이 정석.
- Sweeper RG: Generic rig, `Move_Ing`/`Longnote_Ing` 2클립(공전엔 Move 1개로 충분). 색상 Mint/Pink/Yellow.

## 3. 아키텍처 / 신규·변경

| 구분 | 자산/파일 | 책임 |
|---|---|---|
| 신규 프리팹 | `BlueArchive/Sweeper/Sweeper_Enemy.prefab` | MonsterActor + Sweeper SkinnedMesh + Animator + SweeperEnemyVisual (+ Shadow) |
| 신규 컨트롤러 | `BlueArchive/Sweeper/AC_SweeperEnemy.controller` | `Move`(`Move_Ing` 루프) 단일 상태 |
| 신규 머티리얼 | `BlueArchive/Sweeper/Sweeper_{Mint,Pink,Yellow}.mat` | BA_ToonLit 색상 3종 |
| 신규 코드 | `Assets/Scripts/Systems/Enemy/SweeperEnemyVisual.cs` | 이동방향 회전 + 피격 플래시 + dissolve |
| 셰이더 확장 | `BlueArchive/Aris/Shaders/BA_ToonLit.shader` | `_DissolveTex`·`_DissolveAmount`·`_DissolveColor`·`_HitFlash` |
| 변경 코드 | `Assets/Scripts/Systems/Enemy/MonsterActor.cs` | 사망 시 dissolve 연동 + 지연 풀 반환, 피격 통지(`OnHit`) |
| 변경 데이터 | Arena `EnemyData.prefab` 필드 | Sweeper_Enemy 프리팹으로 교체 |

- 격리: BA_ToonLit 확장은 dissolve/flash 기본값 0이라 Aris(코어)에 무영향. 신규 컴포넌트/프리팹/머티리얼만 추가.

## 4. SweeperEnemyVisual.cs

`Setup(MonsterActor actor)` 주입. 책임:
- **이동 회전**: `LateUpdate`에서 `actor.Position` delta(Y평면) → `Quaternion.LookRotation` Slerp(`rotateSpeed≈10`). 정지 시 유지.
- **피격 플래시**: `actor.OnHit` 구독 → `HandleHit()` → material `_HitFlash` 0.09초 펄스(Update 감쇠).
- **dissolve**: `PlayDissolve()` (UniTask) — Animator.speed=0 + `_DissolveAmount` 0→1(duration `dissolveDuration≈1`).
- **풀 리셋**: `ResetVisual()` — `_DissolveAmount`=0, `_HitFlash`=0, Animator.speed=1, 랜덤 색 머티리얼 적용.
- material 인스턴스는 SkinnedMeshRenderer 단위로 보관(MaterialPropertyBlock 또는 인스턴스). 이벤트 `On`·핸들러 `Handle` 컨벤션.

## 5. BA_ToonLit 확장

추가 프로퍼티: `_DissolveTex`(노이즈), `_DissolveAmount`(0..1, 기본0), `_DissolveColor`(엣지 발광), `_HitFlash`(0..1, 기본0).
- ForwardLit frag: `half n = SAMPLE(_DissolveTex, uv).r; clip(n - _DissolveAmount);` + 경계 `_DissolveColor` 가산. 최종 `col = lerp(col, 1, _HitFlash)`.
- Outline 패스도 동일 `clip`으로 외곽선 동반 소멸.
- 기본값 0 → 기존 Aris 머티리얼 영향 없음.

## 6. MonsterActor 변경

- `public event System.Action OnHit;` 추가 → `TakeDamage`에서 발화(기존 hitFlashTimer 대체).
- 기존 SpriteRenderer 플래시(`flashRenderers`) 로직은 3D에서 SweeperEnemyVisual로 이관(MonsterActor는 통지만).
- `Resolve(reached)`:
  ```
  SetState(Dead)                     // 타겟 제외·이동 정지
  if (!reached) await visual.PlayDissolve()   // 1초 dissolve
  spawner.HandleEnemyKilled(this)             // 보상 + 풀 반환 (dissolve 후)
  ```
  - 보상/킬 정산은 `HandleEnemyKilled` 내부에서 일괄(약 1초 지연되나 dissolve 동안 적이 보여 자연스러움).
  - 비동기는 UniTask. 도달(reached=true, 코어 접촉)은 즉시 처리(연출 생략).
  - dissolve 중 재정산 방지(`resolved` 가드 유지), 사망 중 풀 반환 전 비활성 가드.

## 7. 색상 랜덤

- `SweeperEnemyVisual.ResetVisual()`에서 3색 머티리얼 중 랜덤 적용. 풀 재사용마다 재랜덤.
- 랜덤: 인덱스 = `spawnIndex % 3` 또는 의사난수(Date 금지 → spawnIndex 기반 분산).

## 8. 디폴트 결정

- 크기: Sweeper globalScale로 적 높이 ≈ 0.8~1.2(기존 적 0.8 기준) 보정
- dissolve: 1.0초, 노이즈 = `tex_terror_dissolve`(sweeper 리소스, 없으면 기본 노이즈)
- 회전: Y축만 Slerp(`rotateSpeed≈10`), BillboardSprite 제거
- 피격 플래시: 0.09초(기존 값)
- 그림자: 기존 Shadow 스프라이트는 유지(발밑 그림자) 또는 단순 blob

## 9. 검증

1. 컴파일(`read_console`) — 셰이더/코드
2. EditMode 회귀 — MonsterActor 변경(`OnHit`, Resolve 비동기) 영향, 기존 테스트 갱신/통과
3. Play: 공전 이동·이동방향 회전·피격 플래시·사망 dissolve·풀 재사용(색 재랜덤)

## 10. 범위 외 (후속)

- Longnote_Ing 등 추가 클립 활용, 적 타입별 색/모델 다양화
- dissolve 파티클 오버레이, 도달(코어 접촉) 전용 연출
- 4종 Sweeper 변형(LongEndDummy 등) 활용
