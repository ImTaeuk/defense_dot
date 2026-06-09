# TASK-006: Actor 기본 Animator 재설계

**작성일**: 2026-06-09
**상태**: 보류 (Phase 3에서 분리, 추후 재설계)
**우선순위**: 중간
**관련**: HD-2D Phase 3 (빌보드), `docs/superpowers/plans/2026-06-09-hd2d-phase3-billboard.md`

---

## 1. 문제 정의

### 1.1 증상
HD-2D Phase 3에서 적 비주얼을 Cainos `PF Player`(스프라이트 + Animator `AC Player`)로 구성하고, `ActorAnimatorView`(속도 기반 브리지)로 Animator 파라미터를 구동했으나 **이동 시 방향/걷기 애니메이션이 의도대로 재생되지 않음**.

- 빌보드(`BillboardSprite`, **CameraPlane** 모드)와 카메라 중앙 주시는 정상 작동 확인됨.
- 애니메이션(방향 N/S/E/W, walk)만 문제.

### 1.2 추정 근본 원인
1. **AC Player 2-레이어 구조**: `Direction` 레이어(기본 weight **0**) + `Movement` 레이어(weight 1). Direction 레이어 weight가 0이라 방향 클립이 화면에 반영되지 않음. Cainos 데모는 자체 플레이어 스크립트로 레이어 weight/파라미터를 제어했을 가능성 — 그 스크립트를 제거하면서 방향 구동이 끊김.
2. **velocity 기반 브리지의 한계**: `ActorAnimatorView`는 `actor.Position` 델타로 `IsMoving`/`Direction`을 산출. MonsterActor가 이동 중 `ActorState.Moving`으로 전환하지 않아 상태 기반이 아닌 속도 기반으로 갔으나, 이동량/스레숄드·실행 순서에 따라 IsMoving 판정이 불안정할 수 있음.
3. **Animator vs 경량 플레이어 결정 재검토 필요**: Phase 3 설계는 원래 경량 프레임 플레이어였다가 Cainos 활용 위해 Animator로 전환. "Actor 기본 Animator"를 어떤 방식(Cainos AC Player 재활용 / 프로젝트 전용 컨트롤러 / 경량 플레이어 복귀)으로 표준화할지 미결.

---

## 2. 현재 상태 (Phase 3에서 유지되는 것)

| 항목 | 상태 |
|---|---|
| `BillboardSprite` (CameraPlane) | ✅ 정상 — 적이 카메라 정면 |
| 카메라 중앙 주시 (Grid 맵 중심 계산) | ✅ 정상 |
| `BillboardMath.DirectionIndex` (순수, 0=S/1=N/2=E/3=W) | ✅ 구현·테스트 |
| `ActorAnimatorView` (속도 기반 IsMoving/Direction 브리지) | ⚠️ 구동되나 방향/걷기 애니 미반영 |
| `ActorBase.StateChanged` 이벤트 | ✅ 유지(범용 API) |
| Cainos `AC Player` 파라미터 | `Direction`(Int 0=S/1=N/2=E/3=W), `IsMoving`(Bool). `Direction` 레이어 기본 weight 0, `Movement` 레이어 weight 1 |

---

## 3. TODO (재설계 시)

**A. 방식 결정**
- A-1. "Actor 기본 Animator" 표준 방식 확정: (a) Cainos AC Player 재활용 + 레이어 weight 제어, (b) 프로젝트 전용 AnimatorController 신규, (c) 경량 프레임 플레이어 복귀 중 선택.

**B. Cainos 재활용 시(옵션 a)**
- B-1. `ActorAnimatorView.OnEnable`에서 `Direction` 레이어 weight를 1로 설정(`animator.SetLayerWeight`)해 방향 클립 반영 확인.
- B-2. 2-레이어(Direction/Movement) 결합 방식 검증 — 둘 다 full-body override 시 충돌 여부, 의도된 블렌딩 확인.
- B-3. IsMoving 판정 안정화(스레숄드/실행 순서/상태 연동) — 필요 시 MonsterActor가 이동 중 `ActorState.Moving` 전환하도록 보강하고 상태 기반으로 전환.

**C. 표준화·확장**
- C-1. 타워 등 다른 액터에도 동일 브리지 적용 가능하게 일반화.
- C-2. 좌우 플립/8방향 등 확장 여지 정리.

---

## 4. 영향도 및 위험도

- 영향 범위: `Assets/Scripts/Systems/Visual/Billboard/ActorAnimatorView.cs`, 적/타워 프리팹의 Animator 셋업, (옵션 b 시) 신규 AnimatorController 에셋.
- 위험: 낮음 — 빌보드·카메라는 독립적으로 정상 동작하므로, 애니메이션 재설계는 격리된 추가 작업.
- 회귀: `BillboardMath`/`DirectionIndex` 단위 테스트는 유지. 재설계가 빌보드·카메라에 영향 없음.

---

## 5. 참고

- Cainos AC Player: `Assets/Cainos/Pixel Art Top Down - Basic/Animation/AC Player.controller`
- 적 프리팹: `Assets/Prefabs/Game/Enemies/Enemy_Placeholder.prefab` (Visual = SpriteRenderer + Animator(AC Player) + BillboardSprite(CameraPlane) + ActorAnimatorView + Shadow)
- 브리지: `Assets/Scripts/Systems/Visual/Billboard/ActorAnimatorView.cs`
