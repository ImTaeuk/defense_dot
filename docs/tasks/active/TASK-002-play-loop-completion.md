# TASK-002: 플레이 루프 완성 — 갭 분석 및 TODO

**작성일**: 2026-06-07
**상태**: 분석 완료 (코드 직접 검증 반영 · Arena 방향 결정 대기)
**우선순위**: 높음

---

## 1. 문제 정의

공통 HUD(TASK-001 A) 완성 후, "시작 → 플레이 → 다시 시작"까지 **완결된 플레이 루프**가 도는지 점검.

> ⚠ **1차 보고(탐색 에이전트)는 "메서드가 호출 연결됨"을 "완전 동작"으로 과신했다.**
> 코드를 직접 회의적으로 재검증한 결과: **전투 로직 코드는 실재하나, 플레이 루프는 실제로 성립하지 않는다**(설치 블로커·종료 규칙 불일치 등).

```
시작(자동) ─→ 플레이(코드 ○ / 실제 ✗) ─→ 승/패 ─→ ❌ 막다른 길
              └ 타워 설치 불가(프리팹 없음)      └ 결과 UI·재시작 없음
                Arena 잘못된 '승리'
```

---

## 2. 현황 (코드 직접 검증)

### 2.1 ✅ 실제 구현 확인 (검증 통과)
- **타워 공격 로직**: `TowerActor.PerformAttack` → `IDamageable.TakeDamage(attackDamage)` (`TowerActor.cs:42-55`), `CombatLogic` 쿨타임 (`CombatLogic.cs:30-44`)
- **적 체력/처치/보상**: `MonsterActor.OnSpawn`/`Initialize`에서 `currentHealth=data.health` (`MonsterActor.cs:60,74`), `TakeDamage`→`Resolve`→`HandleEnemyKilled` (`:86-105`)
- **적 코어 피해(Grid)**: `GridDefenseMode.OnEnemyReachedGoal`→`CoreModel.ApplyDamage` (`GridDefenseMode.cs:49`)
- **경로 추종**: `PathFollowerLogic`이 `mapData.bakedPaths`의 baked path 사용 (`GridDefenseMode.cs:28-47`). **JPS 스텁이 아니라 baked path 사용** (2.3-F5 참고)
- **패배 조건 — 원본과 일치 확인**: Arena=`enemies>=maxAlive`(원본 `index.html:13378`) ↔ `ArenaMode.CheckDefeat:46`. Grid=`towerHP<=0`(원본 `:13476`) ↔ `CoreModel.OnCoreDestroyed`. **개념·값(본진 40) 일치.**
- **경제·HUD**: 골드 획득/소비, HUD 4종 갱신

### 2.2 🟡 부분/스텁 (껍데기만)
- `ActorBase.Initialize`(체력 미설정 주석만)·`Die`(빈 본문 "추가 로직" 주석만) — 파생 클래스가 보완하나 기반 클래스는 빈 껍데기 (`ActorBase.cs:45-58`)
- `GameFlowModel.OnPhaseChanged` **발행만, 구독자 0** (`GameFlowModel.cs:35`)
- `WaveData.nextWaveDelay` 정의만, 미사용 (고정 2초)
- `MonsterActor.CoreDamage => 1f` **하드코딩** (`EnemyData`에 coreDamage 필드 없음)

### 2.3 🔴 누락·블로커 (플레이 루프 불성립)
- **F1. 타워 프리팹 부재 → Grid 타워 설치 런타임 예외**: 프로젝트에 타워 prefab 0개(`Default_TowerSlot`만 존재), `TowerData.asset.prefab = null`. `TowerPlacementController.TryPlace:102` `Instantiate(null)` → 예외. **타워를 못 놓음 → Grid 방어 불가 → 즉시 패배.** (최우선 블로커)
- **F2. 결과 UI / 재시작 / 타이틀 / 일시정지 전무**: `SceneManager.LoadScene` 호출 0, 결과 패널 0, Pause·`Time.timeScale` 0
- **F3. Arena 유한-웨이브 '승리' 오발생**: `EnemySpawner`가 양 모드 공통으로 `waveSequence` 소진 시 `MarkWaveCleared`→`HandleVictory`. **원본 Arena는 무한 생존(승리 없음)** → 불일치
- **F4. 공격 비주얼/투사체 없음**: `PerformAttack`이 즉시 무형 데미지. 타격 피드백 0
- **F5. JPS `PathfindingService`/`JPSJob` 스텁 (dead code)**: `Execute()`가 start/end만 반환(`PathfindingService.cs:103-117` "TODO: 실제 JPS 구현"), **호출처 없음** → 게임플레이엔 영향 없으나 혼란 요소
- **F6. 원본 메타 시스템 전무**: 가챠·렐릭·승천(asc)·다중맵(basic/twin/triple)·레벨업 선택 등 원본의 핵심 깊이. Arena (a)안과 직결, 대형 스코프

---

## 3. TODO (검증 반영 · 우선순위)

### P0. 플레이 가능 블로커 — 해소 전엔 게임 성립 안 함
- [ ] **P0-1.** 타워 프리팹 제작(메시 + `TowerActor`) + `TowerData.asset.prefab` 할당 → Grid 타워 설치 복구
- [ ] **P0-2.** Grid 맵 점검: `NewMapData.bakedPaths`(현재 spawn 1개)·타워슬롯 12개가 실제 방어 가능한 미로 구성인지 검증·보강

### P1. 루프 클로징 (결과 + 모드별 종료 규칙)
- [ ] **P1-1.** `GameResultPresenter`(POCO, `IPresenter`) → `GameFlowModel.OnPhaseChanged` 구독 → 승/패 패널. **이벤트 이미 존재, 소비자만 추가**. `UIRoot`에 한 줄(HUDPresenter 패턴)
- [ ] **P1-2.** 결과 패널 프리팹(승/패 메시지 + 재시작 버튼) — uGUI, 공통 HUD 컨벤션
- [ ] **P1-3.** 재시작 = `SceneManager.LoadScene(현재 씬)` (또는 도메인 리셋)
- [ ] **P1-4.** **Arena 무한화**: Arena에서 `OnWaveCleared`→Victory 미발생하도록 모드별 분기(원본=무한 생존). Arena 전용 무한 스폰 검토
- [ ] **P1-5.** (선택) 게임오버 후 스폰/입력 정지 — `Flow.IsPlaying` 체크가 `GameManager.Update` 1곳뿐

### P2. 전투 완성도
- [ ] **P2-1.** 공격 비주얼/투사체(타격 피드백)
- [ ] **P2-2.** `CoreDamage`를 `EnemyData` 필드로 (하드코딩 1f 제거)

### P3. 진입/폴리시
- [ ] **P3-1.** 타이틀 + 모드 선택(Arena/Grid) → 씬 로드
- [ ] **P3-2.** 일시정지(Pause 상태 + `Time.timeScale`)
- [ ] **P3-3.** `WaveData.nextWaveDelay` 적용

### C. 정리/일관성
- [ ] **C-1.** JPS `PathfindingService`/`JPSJob` 스텁 — 제거 또는 실제 구현 결정 (현재 dead code)
- [ ] **C-2.** `ActorBase.Initialize`/`Die` 빈 껍데기 — 보완 또는 추상화 정리
- [ ] **C-3.** `Panel_Grid` → `Panel_HUD` 리네임(공통 HUD), `TowerData.asset` prefab 할당(P0-1과 동시)

---

## 4. 설계 결정 필요 — "Arena의 플레이란 무엇인가" (선행)

Arena는 현재 **플레이어 입력이 전혀 없는 자동 전투**다. 이 분기가 Arena 쪽 TODO 전체를 좌우한다:

| 안 | 방향 | 플레이 루프 형태 | 연계 |
|---|---|---|---|
| **(a)** | 메타 진행형 오토배틀러 | 런 자동 → 사망 → **코어 강화/가챠/렐릭** → 재런 | 원본의 가챠·승천(F6) + TASK-001 **B-1**이 곧 핵심 플레이. P1 결과화면에 "강화" 부착 |
| **(b)** | 인런 조작형 | 런 중 코어 이동/스킬/조준 등 플레이어 행동 추가 | 입력 시스템 + Arena 상호작용 **신규 설계(대형)** |

> Grid 모드는 이미 인런 조작(타워 배치) 코드 완성 → **P0(타워 프리팹) + P1(결과·재시작)** 이면 루프 완결.
> Arena는 (a)/(b) 결정 후 F3·F6·B-1 우선순위가 확정된다.

---

## 5. 원본 대조 요약 (Assets/Reference/dot-defense-main)

| 항목 | 원본 | 현재 구현 | 판정 |
|---|---|---|---|
| Grid(defense) 패배 | `towerHP<=0` (`:13476`), 본진 40 | `CoreModel` HP 0, 코어 40 | ✅ 일치 |
| Arena 패배 | `enemies.length>=maxAlive` (`:13378`) | `activeEnemyCount>=MaxAlive` | ✅ 일치 |
| Arena 종료 | 무한 생존(승리 없음, 점수제) | 유한 웨이브 후 Victory | ❌ 불일치(F3) |
| 메타(가챠·렐릭·승천·다중맵) | 풍부 | 전무 | ❌ 누락(F6) |
| 타워 실체 | 다수 타워·조합 | 프리팹 0개 | ❌ 블로커(F1) |

> 원본은 가챠·렐릭·승천·레벨업 선택을 갖춘 큰 게임. 현재는 **코어 전투 루프의 스켈레톤** 단계.
