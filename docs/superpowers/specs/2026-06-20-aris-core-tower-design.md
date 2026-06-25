# Aris 코어 타워 통합 설계

**작성일**: 2026-06-20
**상태**: 설계 승인 완료
**대상**: Arena 모드 중앙 코어 타워
**범위**: Aris 3D 모델을 코어 타워 비주얼로 사용 + Generic Animator로 능력/게임상태 연동 + 최근접 적 추적 회전

---

## 1. 목표 / 성공 기준

- Arena 코어 타워의 비주얼을 Aris 3D 모델(BA_ToonLit 적용)로 대체한다.
- Aris FBX 내장 애니메이션을 활용해 **대기·공격·위기·파괴·승리** 상태를 연동한다.
- 코어 타워가 **최근접 적을 향해 Y축 회전**하여 "조준" 연출을 낸다.
- 이 코어 타워에만 격리 적용한다. 외부 원본 FBX·타 모델·HD-2D 적 렌더링 무영향.
- 성공 기준: Play에서 ① Idle 재생 ② 능력 발동 시 Attack ③ 적 추적 회전 ④ 코어 HP 저하 시 Panic·파괴 시 Death·웨이브 클리어 시 Victory 전환.

## 2. 배경 / 현황 (탐색 결과)

- 코어 타워 = `ArenaModeBootstrap.SpawnCenterTower`가 TowerData 프리팹 Instantiate + TowerActor + CoreAbilitySystem. 현재 비주얼은 2D 스프라이트 빌보드.
- 적/액터 = `BillboardSprite`(Y축 빌보드) + `ActorAnimatorBinder`(ActorState int + 4방향). **2D 스프라이트 기반** — Aris(3D)에는 부적합.
- `CoreAbilitySystem`은 능력 발동 외부 이벤트가 **없음** → 추가 필요.
- Aris FBX: rig=**Generic**, 내장 클립 31개. 사용 클립: `Normal_Idle`, `Normal_Attack_Ing`, `Vital_Panic`, `Vital_Death`, `Victory_Start/End`, (선택) `Normal_Callsign`/`Normal_Reload`.
- Generic rig → Humanoid 리타겟 불필요, FBX 자체 본으로 클립 직접 재생.

## 3. 아키텍처 / 컴포넌트 구조

| 구분 | 자산/파일 | 책임 |
|---|---|---|
| 신규 프리팹 | `BlueArchive/Aris/Aris_CoreTower.prefab` | Aris FBX + BA_ToonLit 머티리얼 + Animator(Generic) + ArisTowerVisual |
| 신규 컨트롤러 | `BlueArchive/Aris/AC_ArisTower.controller` | Idle/Attack/Panic/Death/Victory 상태머신 |
| 신규 코드 | `Assets/Scripts/Systems/Mode/ArisTowerVisual.cs` | 능력/게임상태/타겟 구독 → Animator 구동 + Y축 회전 |
| 수정 코드 | `Assets/Scripts/Systems/Abilities/CoreAbilitySystem.cs` | `OnAbilityActivated` 이벤트 추가 |
| 수정 코드 | `Assets/Scripts/Systems/Abilities/AbilityContext.cs` | `onAbilityFired` 콜백 추가 (발사 시점 통지) |
| 수정 코드 | `Assets/Scripts/Systems/Mode/ArenaModeBootstrap.cs` | SpawnCenterTower에서 Aris 프리팹 생성 + Setup |

- 격리: 코어 타워에만 적용. 기존 적 렌더링/빌보드/타 모델 무변경.

## 4. AC_ArisTower.controller (Generic)

| 상태 | 클립 | 전이 조건 |
|---|---|---|
| Idle (기본) | `Normal_Idle` (loop) | 기본, 다른 상태 종료 후 복귀 |
| Attack | `Normal_Attack_Ing` | 트리거 `Attack` → 종료 시 Idle |
| Panic | `Vital_Panic` (loop) | bool `LowHP` true |
| Death | `Vital_Death` | 트리거 `Death` (복귀 없음) |
| Victory | `Victory_Start`→`Victory_End` | 트리거 `Victory` |
| (선택) 변주 | `Normal_Callsign`/`Normal_Reload` | Idle 중 랜덤 (후순위) |

- 클립은 모두 loop=False(원본) → Idle/Panic만 컨트롤러에서 Loop 설정.

## 5. ArisTowerVisual.cs 인터페이스

```
public void Setup(CoreAbilitySystem core, TargetFinder finder, GameFlowModel flow, CoreModel coreHp)
```
- `core.OnAbilityActivated += () => animator.SetTrigger("Attack")`
- Update: `finder.FindNearest(origin, range)` → 적 방향 Y축 회전(Quaternion.Slerp, _rotSpeed). 적 없으면 카메라 정면 기준.
- `coreHp.OnCurrentChanged` → `LowHP = current/max < _lowHpRatio`. 0 도달 → `SetTrigger("Death")`
- `flow` 승리 상태 → `SetTrigger("Victory")`
- 핸들러는 `Handle` 접두사, 이벤트 `On` 접두사 (컨벤션)

## 6. CoreAbilitySystem / AbilityContext 변경

- `AbilityContext`에 `public readonly System.Action onAbilityFired;` 추가 (생성자 인자). 이산 능력이 실제 발사 직후 호출.
- `ProjectileAbilityData.Tick`·`AreaWaveAbilityData.Tick`이 발사 성공 시 `ctx.onAbilityFired?.Invoke()` 호출. (상시 오비탈은 호출 안 함)
- `CoreAbilitySystem`: `public event System.Action OnAbilityActivated;`. Setup에서 AbilityContext의 onAbilityFired를 `() => OnAbilityActivated?.Invoke()`로 연결.

## 7. ArenaModeBootstrap 변경

- SpawnCenterTower: 기존 코어 2D 스프라이트 렌더러 비활성화(또는 비주얼 루트 숨김).
- `Aris_CoreTower.prefab`을 코어 타워 자식으로 Instantiate.
- `ArisTowerVisual.Setup(coreAbilitySystem, targetFinder, flow, coreModel)` 호출.

## 8. 디폴트 결정

- 스케일: globalScale 170(~1.8m) 유지, 코어 위치 프리뷰로 미세조정
- Attack 트리거: 이산 능력 발동마다(연속 시 crossfade). 상시 오비탈 제외
- 회전: Y축만, Slerp 보간, 적 없으면 카메라 정면
- 빌보드: Aris 3D → BillboardSprite 미사용, 기존 코어 스프라이트 숨김

## 9. 검증

1. 컴파일 (`read_console`) — ArisTowerVisual·CoreAbilitySystem·AbilityContext 변경 후
2. EditMode 회귀 (AbilityContext 생성자 변경 영향 — 기존 테스트 갱신)
3. Play: Idle/Attack/적추적/Panic/Death/Victory 시각 확인
4. 격리: 타 모델·적 렌더링 무영향 확인

## 10. 범위 외 (후속)

- 4방향/세밀한 조준 블렌드, 능력 종류별 다른 Attack 모션
- 대기 변주(Callsign/Reload) 랜덤 재생 정교화
- 이동/소환 등 기타 클립 활용
