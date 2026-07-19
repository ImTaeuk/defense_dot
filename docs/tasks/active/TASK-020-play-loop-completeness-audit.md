# TASK-020: 인게임 플레이 루프 완성도 감사

**작성일**: 2026-07-19
**상태**: 분석 완료
**우선순위**: 높음

---

## 1. 문제 정의

"플레이 루프가 완성됐는가"에 대해, 문서 주장이 아닌 **코드·씬·데이터 근거**로 전수 감사를 수행했다.
구간 10개를 병렬 감사한 뒤 각 판정을 적대적으로 반증했다(에이전트 21).

### 1.1 결론

**배선은 완성, 게임은 미완성.**

ArenaScene 한정으로 `씬 실행 → 웨이브1~5 → 레벨업 카드 → 골드 강화 → 전멸 → 승리 → 결과창 → 재시작` 사슬이 **미배선 지점 없이 닫혀 있다**(EventSystem·GraphicRaycaster·버튼 interactable까지 확인).
그러나 **승패 판정·전투 밸런스·성장 반영**이라는 규칙 층이 비어 있어, 플레이 경험으로는 성립하지 않는다.

### 1.2 "코드 정합성 ≠ 데이터 정합성"

이번 결함 다수는 **개별 값은 타당한데 값들의 관계가 게임을 무너뜨리는** 형태다.
컴파일러·단위 테스트(621/621 통과)가 검출할 수 없는 층이다.

| 값 A | 값 B | 각각은 | 관계가 만드는 결과 |
|---|---|---|---|
| `maxAlive: 80` | 총 스폰 80 | 타당 | 패배 도달 불가 |
| `baseCooldown: 0.4` | 시전 클립 2.667초 | 타당 | 실발사 주기 6.7배 |

---

## 2. 구간별 판정

| 구간 | 상태 | 근거 | 빠진 것 |
|---|---|---|---|
| 진입/시작 | 부분 | `EditorBuildSettings.asset:8-13`, `GameManager.cs:137-138` | 타이틀·모드선택·일시정지·종료 전무. `GamePhase.cs:9-12`에 Paused 없음 |
| 웨이브·라운드 | 완성 | `EnemySpawner.cs:104-136, 216-231` | 라운드 전환 연출 없음. `WaveData.cs:25 nextWaveDelay` 참조처 0건 |
| 적 스폰·전투·처치 | 완성 | `EnemySpawner.cs:163-182, 207-214` | 처치 집계가 사망 연출 뒤(0.6초 지연) |
| 코어 능력 실행 | 부분 | `CoreAbilitySystem.cs:107,113-121`, `ActiveAbilityData.cs:25-39` | 시전 채널 병목(§3-1) |
| 성장① 카드 획득 | 완성 | `CardSelectionPresenter.cs:58-89` | 없음(배선 기준) |
| 성장② 골드 강화 | 완성 | `AbilityUpgradeService.cs:37,46`, `ArenaScene.unity:155043` | ScrollRect 부재 — 12행 오버플로 |
| 성장③ 합성(계보) | 부분 | `Aris_FusionLineage.asset:15-27`, `FusionSystem.cs:23-46` | 결과 4종 데이터 동일. 합성 고지 UI 없음 |
| 승패·결과·재시작 | 부분 | `GameManager.cs:108-109,181-185`, `GameResultPresenter.cs:36-43` | **패배 도달 불가**(§3-1). 종료 버튼 없음 |
| HUD·피드백 | 부분 | `ArenaHudView.cs:14-19` | 피격 플래시 무효, `OnHit` 구독자 0건, 데미지 숫자·셰이크 없음 |
| 페이싱·콘텐츠 | 부분 | `Wave_01~05`(5/10/15/20/30), `EnemyData` 1종 | 적 1종·보스 없음. 라운드 스케일링 0건. 총 약 150초 |
| GridScene | 파손 | `GridScene.unity:11553-11557` | UIRoot에 구 필드만, `views`·레이어 키 전무 → Presenter 0개. 빌드에는 포함 |

---

## 3. 확정 결함 (직접 검증 완료)

### 3-1. 패배가 데이터상 도달 불가능 — **치명**

- `Assets/Settings/Arena_Default.asset:19` — `maxAlive: 80`
- 총 스폰 = 5+10+15+20+30 = **정확히 80** (`Assets/Data/Waves/Sample Wave/Wave_01~05.asset`)
- `ArenaMode.cs:46` — `CheckDefeat(activeEnemyCount) => activeEnemyCount >= arena.MaxAlive`
- `ArenaMode.cs:41-44` — 코어 도달 패배 **없음**(빈 구현). 적 감소 요인은 처치뿐
- `ArenaMode.cs:51-55` — 코어 HP = `MaxAlive − activeEnemyCount` (헤드룸 모델)

**귀결**: 한 마리도 처치하지 않아야만, 그것도 80번째 스폰 순간에만 패배한다.
1킬 발생 시 생존 상한이 79로 고정되어 **남은 판 내내 패배가 영구 불가능**하다.
패배 판정·결과 UI는 모두 구현돼 있으나 실플레이로 도달할 수 없다.

### 3-2. 투사체 계열 실발사 주기 6.7배 — **치명**

- `Ability_Shot.asset:22` `baseCooldown: 0.4` / `:23` castAnimation = `Aris_Cast_Shot.anim`
- `Aris_Cast_Shot.anim:38650` — `m_StopTime: 2.6666667`
- `ActiveAbilityData.cs:30-33` — `RequestCast` 실패 시 `return` → `:38 ResetCooldown` **미도달**
- `CoreAbilitySystem.cs:113-115` — `if (castReceiver.IsCasting) return false`. 시전 채널은 **코어 전역 1개**
- `ArisTowerVisual.cs:114-115` — `isCasting = true; castRemaining = clip.length`

**귀결**: 실발사 주기 ≈ 2.67초(설계 0.4초).

**범위 정정**: 병목 대상은 **castAnimation 보유 3종 한정**이다.

| 능력 | castAnimation | baseCooldown | 병목 |
|---|---|---|---|
| Shot / Railgun / StormBrand | 있음(동일 클립) | 0.4 | **채널 경합** |
| AreaWave / LevinLash / TempestVeil / StormSovereign / StunBomb | 없음 | 3 | 무관(즉시 발사) |
| Orbital / Tornado | 없음 | 0.2 | 무관 |

투사체 3종이 **같은 클립·같은 단일 채널**을 공유하므로, 3개를 모두 보유해도 총 발사 빈도는 1개일 때와 동일하다.
(선두 능력이 채널을 계속 선점해 후순위가 아예 발사되지 않을 가능성 — `AbilityRunner` 순회 순서 확인 필요. **미검증**)

### 3-3. 패시브 보정 2채널 사망

- `AbilityModifiers.cs:10,12` — `damageBonus`, `cooldownReduction`
- 생산자 **0건**. 존재하는 참조는 선언·`Reset(=0)`(`:26-27`)·소비처 2곳(`ActiveAbilityData.cs:60`, `DamageSource.cs:29`)뿐
- **귀결**: 두 값은 항상 0. 해당 패시브는 수치로 반영되지 않는다

### 3-4. `isSpawning` 누수 — 잠복

- `EnemySpawner.cs:142` `isSpawning = true` → `:154-156` `catch(OperationCanceledException) { return; }` → **`:159` `isSpawning = false`에 도달하지 못함**
- **귀결**: 스폰 도중 라운드 전환(취소) 시 플래그가 영구 true. `:220`·`:236`의 `!isSpawning` 가드가 영구 거짓 → **승리 판정과 라운드 진행이 둘 다 정지**
- 현재 미발현: 웨이브별 스폰 소요(5/5/7.5/10/15초) < duration 30초
- **물량·간격을 키우는 순간 발현**한다. 페이싱 튜닝 착수 전 선행 수정 필요

### 3-5. 웨이브 에셋 구버전 직렬화

`Wave_01`·`Wave_02`에 `duration` 키 없음. 이니셜라이저 `WaveData.cs:26 = 30f`로 동작상 문제는 없으나 에셋 미갱신 상태.

---

## 4. 미검증 (감사 주장 · 재확인 필요)

| 항목 | 감사 근거 |
|---|---|
| GridScene UIRoot 파손 | `GridScene.unity:11553-11557`, `UIRoot.cs:29`. 복구 도구 `HudSetupTool.cs:88`도 구 필드명 사용 |
| 피격 플래시 무효 | `MonsterActor.cs:110,114` Shadow 필터가 유일 잔존 렌더러 배제 → `flashRenderers.Length == 0` |
| 처치 보상 0.6초 지연·유실 | `MonsterActor.cs:164` → `SweeperEnemyVisual.cs:85,87`. `GetCancellationTokenOnDestroy()`로 연출 중 파괴 시 `onComplete` 미호출 |
| 합성 결과 4종 데이터 동일 | AreaWave/LevinLash/TempestVeil/StormSovereign — GUID·아이콘·설명·수치 동일, `id`·`displayName`만 상이 (baseCooldown 3 동일은 확인됨) |
| 강화 패널 오버플로 | ScrollRect 부재, 최대 12행 |

---

## 5. TODO

### A. 게임 성립 (필수)
- **A-1. 패배 조건 재설계** — `maxAlive`(80)가 총 스폰(80)과 같아 도달 불가. 임계값 조정 또는 코어 도달 시 실피해 경로 신설. **밸런스 설계 판단 필요** (`ArenaMode.cs:41-44`, `Arena_Default.asset:19`)
- **A-2. 시전 채널 병목 해소** — 능력별 독립 시전 또는 `Ability_Shot` 짧은 클립 교체. **설계 판단 필요** (`ActiveAbilityData.cs:30-33`, `CoreAbilitySystem.cs:113-115`, `ArisTowerVisual.cs:114-115`)
- **A-3. `isSpawning` 누수 수정** — `catch` 내에서도 false 복원. 기계적 수정 (`EnemySpawner.cs:154-159`)
- **A-4. GridScene 처리 결정** — 빌드 제외 또는 UIRoot 재배선. 후자 시 `HudSetupTool.cs:88` 동반 수정

### B. 성장 체감
- B-1. `damageBonus`·`cooldownReduction` 생산자 구현 (`AbilityModifiers.cs:10,12`)
- B-2. 오비탈 레벨업 시 위성 수 갱신 — `OnEquip` 1회만 실행됨 (`OrbitalAbilityData.cs:39`, `AbilityLoadout.cs:68`)
- B-3. 합성 결과 4종 차별화 (아이콘·설명·수치·효과) → TASK-018 A·B·D와 통합
- B-4. 계보 진입점 재설계 — 레시피1(Shot+Orbital→AreaWave)이 막다른 가지. 실제 체인은 스타터와 무관한 잎 4종을 각각 MAX까지 요구

### C. 피드백
- C-1. 피격 표현 복구 (`MonsterActor.cs:110-114`)
- C-2. 처치 집계를 사망 연출과 분리 — `Resolve` 시점 즉시 반영, 회수만 지연
- C-3. 데미지 숫자·라운드 전환·승패 연출 최소 1종

### D. 제품 형태
- D-1. 일시정지 (`GamePhase`에 Paused 추가 + 입력)
- D-2. 타이틀 또는 결과창 종료 버튼 (`Application.Quit` 참조 0건)
- D-3. 강화 패널 ScrollRect

### E. 콘텐츠 ('완성' 판정 밖)
- E-1. 적 2종 이상 + 보스 (`MonsterActor.cs:70 IsBoss => false` 하드코딩)
- E-2. 라운드별 적 스탯 스케일링 (코드 0건)
- E-3. `Wave_01`·`Wave_02` 재저장, `nextWaveDelay` 死데이터 제거

---

## 6. 적용 순서

| Phase | 내용 | 성격 |
|---|---|---|
| 1 | A-3(누수), A-4(GridScene) | 기계적 — 판단 불요 |
| 2 | A-1(패배 조건), A-2(시전 병목) | **설계 판단 필요** |
| 3 | B-1, B-2 | 기계적 |
| 4 | C-1, C-2 | 기계적 |
| 5 | B-3, B-4 + TASK-018 | 콘텐츠 설계 |
| 6 | D, E | 제품화·콘텐츠 |

Phase 2 이전에는 페이싱·밸런스 튜닝을 시작하지 않는다(A-3 미수정 상태에서 물량 증가 시 진행 정지).

---

## 7. 감사 방법

- 구간 10개 병렬 감사 → 구간별 적대적 반증 → 종합 (에이전트 21, 도구 호출 756)
- 각 감사자 제약: 문서(`docs/`) 주장을 근거로 사용 금지, "코드 존재"와 "플레이에서 접근 가능"을 구분, `file:line` 근거 필수
- 반증자 제약: 기본자세 회의(skeptical), complete 판정에 대해 미배선·미구독·씬 부재를 탐색
- 감사 결과 중 §3-2 범위는 본 문서 작성 시 직접 재확인하여 정정(전체 → 투사체 3종 한정)

---

## 8. 관련

- TASK-017 계보 비주얼 에디터, TASK-018 계보 능력 세부(B-3·B-4와 통합 대상)
- 계보 실플레이 검증(2026-07-19)은 치트로 재료를 급조한 경로였으며, **자연 획득 곡선으로 정점 도달이 가능한지는 미검증**
