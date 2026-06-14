# TASK-012: Arena 모드 로드맵 (원작 standard 모드 포팅)

**작성일**: 2026-06-12
**상태**: 분석 완료 (A0 착수 예정)
**우선순위**: 높음 (Arena 우선 개발)
**근거**: 원작 `Assets/Reference/dot-defense-main/index.html`(정본, =`standard` 모드) 분석

---

## 1. 정의 (원작 분석 기반 · 교정 포함)

Arena = 원작의 **`standard`(클래식) 모드**. 원형 경기장에서 중앙 코어에 붙은 **능력(satellite)들이 자동 전투**하고, 플레이어는 **카드 선택·골드 강화·삭제**로 빌드를 키우는 **로그라이트 생존**.

### 1.1 확정 규칙
- **라운드/웨이브 = 유한, 데이터 주도.** 라운드 수는 **100 고정 아님** — 디자이너가 `WaveSequence`/`WaveData`(SO)로 기획·등록. 기존 `EnemySpawner.waveSequence` 구조 그대로 사용.
- **승리** = 등록된 웨이브 전부 클리어(디자이너 정의 종료). (엔드리스는 선택적 후속)
- **패배** = 동시 생존 적 수 ≥ `maxAlive`. 기존 `mode.CheckDefeat(ActiveEnemyCount)` wired.
- **플레이어 직접 조작 없음** — 코어 능력 자동 전투. 입력 = 카드 선택 / 골드 강화 / 능력 삭제.
- **성장 2축 (독립)**: 라운드(시간) 진행 ↔ **kills 기반 레벨업·카드**.

### 1.2 교정 (이전 가정 폐기)
- ❌ "Arena = 무한 생존" → ✅ **유한 등록 웨이브 + 선택적 엔드리스**.
- ❌ "라운드 100 고정" → ✅ **디자이너 데이터 주도(WaveSequence)**.
- ❌ "동적 아레나 크기" → 원작에 **대응물 없음**(반경 고정, `maxAlive` 숫자만 가변). 우리 독자기능으로 **보류**.
- `defense`(타워디펜스) 모드 = 원작에서 하드코딩 비활성 죽은 코드 → 무시.

---

## 2. 시스템 목록 (원작 매핑 · Unity 재사용 현황)

| 시스템 | 역할 | 원작 file:line | Unity |
|---|---|---|---|
| 스폰·웨이브 | 라운드별 적 산출·스폰계획 | `roundConfig:15691`, `planRound:19255` | **있음**(EnemySpawner·Wave) |
| 패배(수용한계) | 적≥maxAlive 패배 | `getMaxAlive:4830`, 체크 `:20996` | **있음**(CheckDefeat) |
| 종료(승/엔드리스) | 웨이브 소진 종료, 엔드리스 선택 | `advanceRound:16021` | **없음**(가짜 Victory 차단 필요) |
| 코어 자동전투 | 능력 satellite tick 공격 | `ABILITY_DEFS:15680` | **부분**(ArenaOrbitLogic 공전) |
| 레벨업·카드 | kills→레벨업→3장 선택 ★허브 | `triggerLevelUp:20159`, `generateLevelUpChoices:19952` | **없음** |
| 능력 효과 실행 | def init/tick/draw, 액티브6+패시브6 | `ABILITY_DEFS:15680`, 슬롯 `:4838` | **없음** |
| 능력 조합 | 재료2→상위 조합 | `applyLevelUpChoice combo:20290` | **없음** |
| 인게임 강화 | 골드로 능력 레벨업/삭제 | `actEnhance:20403`, `actDelete:20448` | **부분**(Economy 있음) |
| 보스 | 5R마다, 보상 | `roundConfig isBoss:15706`, `bossIntro:15816` | **없음** |
| 메타(가챠·유물·강화소·승천) | 런에 보정 주입 | `gacha:1387`, `RELICS:14844`, `buyUpgrade:18571`, `asc:15699` | **없음** |

---

## 3. TODO (빌드 가능 순서 = Phase)

> 각 단계가 "플레이 가능한 증분". 의존 적은 것부터.

### A0. 종료 규칙 정합성 ✅ 완료
> 페이싱 결정: **연속 스폰 + 유한**(원작 충실). `WinsOnWaveClear=false` 유지(연속성), Arena 분기를 "무한루프 → 유한 연속 + 전멸 시 승리"로 수정.
- [x] **A0-1.** `EnemySpawner`에 `allWavesSpawned` 추가, Arena 분기 수정 — 마지막 등록 웨이브 스폰 후 중단, `CheckWaveComplete`에서 전멸 시 `MarkWaveCleared`(승리). Grid 경로 무변경. *(검증: Play 리플렉션 테스트 Playing→Victory)*
- [x] **A0-2.** ArenaScene `EnemySpawner.waveSequence` = `Sample_Sequence`(2웨이브·15마리) 등록 확인.
- [x] **A0-3.** 검증 — 승리 경로 결정적 확인(전멸→Victory), 패배=`ArenaMode.CheckDefeat`(적≥maxAlive) 기존 wired, 결과UI·재시작·정지 기존 동작. 컴파일 0.
> 잔여(디자이너): 실제 밸런싱된 Arena `WaveSequence` 콘텐츠 등록(Sample은 임시).

### A1. 능력 데이터 아키텍처
- [ ] **A1-1.** `AbilityDef`(ScriptableObject): id·이름·아이콘·쿨다운·레벨곡선·tick 동작 분류
- [ ] **A1-2.** 런타임 능력 인스턴스(id/level/cooldown/state)
- [ ] **A1-3.** 코어 능력 슬롯(액티브6+패시브6)

### A2. 코어 자동전투 (능력 실행 최소판)
- [ ] **A2-1.** 능력 1~2종 tick 자동공격 (BT·풀링·투사체[디버그공격 유산] 재사용)
- [ ] **A2-2.** 능력별 타게팅·발사 연동 (TargetFinder 재사용)

### A3. 레벨업·카드 선택 ★핵심 허브
- [ ] **A3-1.** kills 누적→레벨업 트리거(`killsToNextLevel` 곡선)
- [ ] **A3-2.** 카드 3장 생성(신규/레벨업) + `timeScale=0` 선택 모달(GameManager 정지·HUD 재사용)
- [ ] **A3-3.** 선택 적용 → 슬롯/레벨 반영

### A4. 인게임 강화
- [ ] **A4-1.** 골드(Economy)로 능력 레벨업/삭제 UI·로직 (`enhanceCost` 라운드 비례)

### A5. 능력 조합
- [ ] **A5-1.** COMBO 레시피(재료2 소진→상위), 카드 풀에 combo 액션 추가

### A6. 보스
- [ ] **A6-1.** N라운드마다 보스(roundConfig isBoss 이식), 보스 보상(A3/A5 재사용)

### A7. 메타층 (런과 느슨결합 · 가장 마지막)
- [ ] **A7-1.** 가챠(스타더스트→캐릭터/유물)
- [ ] **A7-2.** 유물 효과(런 시작 보정)
- [ ] **A7-3.** 영구강화(강화소) · 승천(난이도/보상 배수)

---

## 4. Phase 권장 순서

A0 → A1 → A2 → A3(여기까지 = "성장하는 자동전투 생존" = Arena 정체성) → A4 → A5 → A6 → A7.
- **A3가 허브**: A2·A4·A5가 A3에 매달림.
- A0는 즉시 가능(기존 코드 정리 수준).
- A7은 런에 보정만 주입 → 단독 추가 가능, 최후순위.

---

## 5. 기존 태스크 매핑 (흡수/대체)

| 기존 | 처리 |
|---|---|
| TASK-002 B-1 (Arena 무한화) | **A0로 교정·흡수** (무한 아님 = 데이터주도 유한+엔드리스) |
| TASK-001 B-2 (동적 아레나 크기) | **보류** (원작 무, 우리 독자기능) |
| TASK-001 A-1 (Arena HUD 결선) | A0 검증 단계에 포함 |
| TASK-010 (Skill 시스템) | **A1~A5로 흡수** |
| TASK-011 (메타 상점) | **A7로 흡수** |

---

## 6. 참고
- 정본: `Assets/Reference/dot-defense-main/index.html` (index-v2=VER2 샌드박스, 비정본)
- 원작 핵심 위치: `roundConfig:15691` `planRound:19255` `getMaxAlive:4830`(MAX_ALIVE_BASE=80 `:4829`) `advanceRound:16021` `triggerLevelUp:20159` `generateLevelUpChoices:19952` `applyLevelUpChoice:20188` `ABILITY_DEFS:15680` `actEnhance:20403` `actDelete:20448` `bossIntro:15816` `buyUpgrade:18571` `RELICS:14844` `gacha UI:1387`
- 우리 자산: `EnemySpawner`·`WaveModel`·`ArenaModeBootstrap`·`ArenaOrbitLogic`·`GameManager`(승패·정지·재시작)·`ActorBehaviorTree`·`ActorAnimatorBinder`·`Economy/Core/Wave/Flow`·uGUI HUD
