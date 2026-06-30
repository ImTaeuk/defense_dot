# TASK-012: Arena 모드 로드맵 (원작 standard 모드 포팅)

**작성일**: 2026-06-12
**갱신일**: 2026-06-26 (원작 v480→v608 재분석 반영, §7 신설)
**상태**: 진행 중 (A0·A1·A2 완료 · A3 착수 예정)
**우선순위**: 높음 (Arena 우선 개발)
**근거**: 원작 `Assets/Reference/dot-defense-main/index.html`(정본, =`standard` 모드, **APP_VERSION 608**) 분석

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
| **계보 공명** (v608 신규) | SR+SSR 캐릭 조합 시 능력 강화·온히트/틱 비주얼 | `LINEAGE_RESONANCE:28644` (23계보×5축) | **없음** |
| **순수 패시브 4종** (v607 신규) | 조건부 데미지 배수(맹공/토벌/쇄도/각성) | `onslaughtBonus:11028~`, 적용 `:21082` | **없음** |
| **행성 난이도 선택** (v608 신규) | 3티어(테라/이그니스/아비스) 진입 + 31단계 래더 | `PLANET_TIERS`, `planetSvg`, 진입 `:v518` | **없음** |
| **계정 레벨·계급** (v608 신규) | 계정XP·8계급(생도→총사령관)·진급 연출·ID카드 | `RANKS`, `accountLevelInfo`, `xpForLevelUp` | **없음** |
| **업적 해금** (v608 신규/확장) | 조건 달성 해금·연대기 훈장소 | `checkUnlockAchievements`, 업적 63→69 | **없음** |
| **기록·블랙박스 분석** (v608 신규) | 성과 추이 차트·난이도 포함 항해 진행도 | v510/v511 | **없음** |
| **글로벌 랭킹 강화** (v608 변경) | 명예의 전당 리워크·계급장·프로필 모달·시즌최고점 | `submitGlobalScore` 리워크, v514/515/574 | **없음** |
| **HUD/UI 리스킨** (v608 변경) | 메카닉 HUD·능력 도크·상단바·배속/일시정지·트레이 | v575~v606, `ABILITY_DOCK` | **부분**(ArenaHUD 진행 중) |

> §2 추가분(계보공명~HUD)은 모두 원작 **v481~v608** 갱신에서 신규/대폭변경된 시스템. 상세·우선순위는 **§7** 참조.

---

## 3. TODO (빌드 가능 순서 = Phase)

> 각 단계가 "플레이 가능한 증분". 의존 적은 것부터.

### A0. 종료 규칙 정합성 ✅ 완료
> 페이싱 결정: **연속 스폰 + 유한**(원작 충실). `WinsOnWaveClear=false` 유지(연속성), Arena 분기를 "무한루프 → 유한 연속 + 전멸 시 승리"로 수정.
- [x] **A0-1.** `EnemySpawner`에 `allWavesSpawned` 추가, Arena 분기 수정 — 마지막 등록 웨이브 스폰 후 중단, `CheckWaveComplete`에서 전멸 시 `MarkWaveCleared`(승리). Grid 경로 무변경. *(검증: Play 리플렉션 테스트 Playing→Victory)*
- [x] **A0-2.** ArenaScene `EnemySpawner.waveSequence` = `Sample_Sequence`(2웨이브·15마리) 등록 확인.
- [x] **A0-3.** 검증 — 승리 경로 결정적 확인(전멸→Victory), 패배=`ArenaMode.CheckDefeat`(적≥maxAlive) 기존 wired, 결과UI·재시작·정지 기존 동작. 컴파일 0.
> 잔여(디자이너): 실제 밸런싱된 Arena `WaveSequence` 콘텐츠 등록(Sample은 임시).

### A1. 능력 데이터 아키텍처 ✅ 완료
> `AbilityData` 계층·`AbilityInstance`·`AbilityLoadout`·`AbilityContext` 구현·커밋(9016abda·8b7932d8).
- [x] **A1-1.** `AbilityData`(ScriptableObject): id·이름·쿨다운·tick 동작 분류
- [x] **A1-2.** 런타임 능력 인스턴스(`AbilityInstance` — level/cooldown/state)
- [x] **A1-3.** 코어 능력 슬롯(`AbilityLoadout` — Actives/Modifiers)

### A2. 코어 자동전투 (능력 실행 최소판) ✅ 완료
> `AbilityRunner`·`CoreAbilitySystem`(ICastHost) + 정의 3종(Projectile/Orbital/AreaWave)·Effects 구현·커밋(8b7932d8). Aris 시전 애니 연동(ICastReceiver·06a39d48), 명중·이펙트 프리팹 포함.
- [x] **A2-1.** 능력 3종(투사체/궤도/영역) tick 자동공격 — 투사체·이펙트 풀링 프리팹 포함
- [x] **A2-2.** 능력별 타게팅·발사 연동 (`AbilityContext`+`TargetFinder`, 시전 발사 프레임 `NotifyFireFrame`)

### A3. 레벨업·카드 선택 ★핵심 허브 ✅ 코어 완료 (디자인 후속 대기 · 미커밋)
> 신규: `Systems/Cards/*`(ArenaCardConfig·AbilityPool·CardTierSet·CardChoice·CardChoiceGenerator·CardPresentation)·`LevelModel`·`CardSelectionView`/`CardSelectionPresenter`·`ICardCommandTarget`·`ICardSelectionView`·`CardContext`. 배선: GameManager(LevelModel 생성·CombatModel 구독)·UIRoot.Inject·ArenaModeBootstrap(CardConfig/AbilityPool/CoreAbility 노출). 플레이 검증(처치→레벨업→모달+정지→선택→능력추가+러너장착→복귀), EditMode 90/90.
- [x] **A3-1.** kills 누적→레벨업 트리거(`killsToNextLevel` 곡선, `LevelModel`)
- [x] **A3-2.** 카드 3장 생성(신규/레벨업) + `timeScale=0` 선택 모달(`CardSelectionPresenter`·`pauseOnCardSelect` 토글)
- [x] **A3-3.** 선택 적용 → 슬롯/레벨 반영(`CoreAbilitySystem.AddAbility`/`LevelUpAbility`, 러너 동기화)
- [x] **A3-D (디자인).** 등급별 카드 비주얼 — 블랭크 스킬 카드 스프라이트(등급색) + `HologramFoilTinted` 셰이더 홀로그램 포일. `CardTier` 5등급 확장 + `CardTierSet`(스프라이트+머티리얼) 데이터 주도, `CardSelectionView.Bind` 자동 적용. 매핑: 신규=블루/강화=그린/조합=퍼플/럭키=옐로우/슈퍼=레드. (코어는 신규·레벨업만 등장, 나머지는 향후 겹 활성 시 자동)

### A4. 인게임 강화
- [ ] **A4-1.** 골드(Economy)로 능력 레벨업/삭제 UI·로직 (`enhanceCost` 라운드 비례)

### A5. 능력 조합
- [ ] **A5-1.** COMBO 레시피(재료2 소진→상위), 카드 풀에 combo 액션 추가
- [x] **A5-2.** (v607) **순수 패시브 4종** — 맹공(>50%HP)/토벌(비보스)/쇄도(적수)/각성(라운드) 조건부 데미지 배수. `PureDamagePassiveData`(kind) + `AbilityModifiers.ConditionalMultiplier`(소스당 +500% cap), 명중 시점 적용(`DamageSource`). 적 `ICombatTargetInfo`(보스·HP) + `ICombatState`(라운드·적수) 주입. *(선행: 데미지 산출을 발사→피격 시점으로 리팩토링)*

### A6. 보스
- [ ] **A6-1.** N라운드마다 보스(roundConfig isBoss 이식), 보스 보상(A3/A5 재사용)

### A7. 메타층 (런과 느슨결합 · 가장 마지막)
- [ ] **A7-1.** 가챠(스타더스트→캐릭터/유물)
- [ ] **A7-2.** 유물 효과(런 시작 보정)
- [ ] **A7-3.** 영구강화(강화소) · 승천(난이도/보상 배수)
- [ ] **A7-4.** (v608) **행성 난이도 선택** — 3티어(테라/이그니스/아비스) 진입 화면 + 31단계 래더 + (밸런스) 승천/초월 ≤30R 점진 완화(R1≈15%→R30 100%)
- [ ] **A7-5.** (v608) **계정 레벨·계급** — 계정XP(`xpForLevelUp`)·8계급·진급 연출·함장 ID카드
- [ ] **A7-6.** (v608) **업적 해금** — 조건 달성 해금 + 연대기 훈장소(데이터+조건)
- [ ] **A7-7.** (v608) **기록·블랙박스 분석** — 성과 추이 차트 + 난이도 포함 항해 진행도
- [ ] **A7-8.** (v608) **글로벌 랭킹 강화** — 명예의 전당·계급장·프로필 모달·기기로컬 시즌최고점 *(서버 연동 필요·MVP 후순위)*

### A8. 계보 공명 (v608 신규 · 캐릭터/가챠 의존)
- [ ] **A8-1.** `LINEAGE_RESONANCE`(23계보×5축: blast/pierce/shock/echo/ice) 데이터 이관 + SR+SSR 조합 판정
- [ ] **A8-2.** 축별 온히트/틱 비주얼 메커닉(★★★★ 비주얼) — A2 능력 실행계 위에 얹힘. *(A7 가챠/진화 선행 필요)*

### B. UI/HUD 리스킨 (v608 변경 · 횡단 관심사)
- [ ] **B-1.** (v575~v606) 메카닉 HUD(네온 사이언·코너 꺾쇠) — 진행 중 `arena-hud-replacement` 작업과 통합
- [ ] **B-2.** (v598) 능력 도크(칩 그리드·단일 오픈 아코디언) — A3 카드/슬롯 UI에 반영
- [ ] **B-3.** (v599~v606) 세로 상단 HUD 바 + 배속(▶▶)/일시정지 컨트롤 + 보조정보 ▾ 트레이
- [ ] **B-4.** (v608) 진화 힌트에서 미보유 파트너 제외(소소 UX)
- [ ] **B-5.** 레벨업 진척도 HUD 표시 — 다음 레벨업까지 남은 처치 수·진행 게이지를 HUD 위젯으로 노출(`LevelModel`의 현재 kills·`KillsToNextLevel` 기반). 신규 UI 베이스(`UIWidget<T>`)로 `LevelProgressWidget` 추가, `LevelModel` 진행도를 ReactiveProperty 로 노출해 Presenter 가 Bind.

---

## 4. Phase 권장 순서

A0 → A1 → A2 → A3(여기까지 = "성장하는 자동전투 생존" = Arena 정체성) → **A5-2(순수 패시브, 저비용·고중요)** → A4 → A5-1 → A6 → A7 → A8.
- **A3가 허브**: A2·A4·A5가 A3에 매달림.
- A0는 즉시 가능(기존 코드 정리 수준).
- **A5-2(순수 패시브)**: 데이터 4개+곱연산 훅이라 비용 최소·빌드 밸런스 직결 → A3 직후 끼워넣기 권장.
- A7은 런에 보정만 주입 → 단독 추가 가능, 최후순위. **A7-8(글로벌 랭킹)·A8(계보 공명)** 은 각각 서버·가챠/진화 선행이 필요해 최후미.
- **B(HUD 리스킨)** 은 횡단 관심사 — `arena-hud-replacement` 진행분과 합쳐 A3 UI 시점에 함께 처리.

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

---

## 7. 원작 v480 → v608 갱신 반영 (2026-06-26 재분석)

> 직전 커밋본 `dad82a8b`(APP_VERSION **480**) ↔ 현재 워킹본(APP_VERSION **608**) diff = **+3,155 / −599줄**.
> 키워드 빈도 검증: 계보 2→29 · 공명 9→30 · 진화 69→89 · 업적 63→69 · 가챠 132→141 / **유물·승천·조합(COMBO_RECIPES)·MAX_ASCENSION 불변**.

### 7.1 신규/변경 시스템 (원작 근거)

| # | 시스템 | 구분 | 핵심 내용 | 원작 근거 |
|---|---|---|---|---|
| 1 | 순수 패시브 4종 | 신규(v607) | 맹공(>50%HP)·토벌(비보스)·쇄도(적수)·각성(라운드) 조건부 데미지 배수 | `onslaughtBonus:11028`, 적용 `:21082` |
| 2 | 계보 공명 | 신규(v608) | 23계보×5축(blast8·pierce6·shock5·echo3·ice1), SR+SSR 조합 발동, 온히트+틱 비주얼 | `LINEAGE_RESONANCE:28644` |
| 3 | 행성 난이도 선택 | 신규(v500/518/519) | 3티어(테라/이그니스/아비스=기본/승천/초월), 31단계 래더, 캐러셀 | `PLANET_TIERS`, `planetSvg` |
| 4 | 계정 레벨·계급 | 신규(v501/502/504) | 계정XP·8계급(생도→총사령관)·chevron 계급장·진급 시네마틱·함장 ID카드 | `RANKS`, `accountLevelInfo`, `xpForLevelUp` |
| 5 | 업적 해금 | 신규/확장(v504) | 조건 달성 해금 + 연대기 훈장소(63→69) | `checkUnlockAchievements` |
| 6 | 기록·블랙박스 분석 | 신규(v510/511) | 성과 추이 area chart·와이어프레임 함선·난이도 포함 항해 진행도 | v510/511 |
| 7 | 글로벌 랭킹 강화 | 변경(v514/515/574) | 명예의 전당 리워크·계급장·Top3 아바타·프로필 모달·기기로컬 시즌최고점 | `submitGlobalScore` 리워크 |
| 8 | HUD/UI 리스킨 | 변경(v575~606) | 메카닉 HUD(네온/코너꺾쇠)·능력 도크(칩그리드)·세로 상단바·배속/일시정지·보조정보 트레이·타이포(Rajdhani+IBM Plex)·이벤트 이모지 스트립 | v575~v606, `ABILITY_DOCK` |
| 9 | 승천/초월 난이도 완화 | 변경·밸런스(v514) | ≤30R 승천 추가난이도 점진 적용(R1≈15%→R30 100%) | v514 |
| 10 | 진화 힌트 개선 | 변경·소(v608) | 미보유 파트너 힌트 제외(진화 69→89) | v608 |

### 7.2 분류: 개발비용 × 개발중요도 (Unity 이식 기준)

> 비용 = Unity 재구현 공수 / 중요도 = Arena 정체성·플레이어 가치 기여.

| # | 시스템 | 개발비용 | 개발중요도 | 우선순위 | 로드맵 슬롯 |
|---|---|:---:|:---:|---|---|
| 1 | 순수 패시브 4종 | 낮음 | **높음** | ⭐ 최우선(즉시 가성비) | A5-2 |
| 8 | HUD/UI 리스킨 | 높음 | **높음** | ⭐ 높음(진행분 통합) | B-1~B-3 |
| 3 | 행성 난이도 선택 | 중간 | 중간 | 중간 | A7-4 |
| 9 | 승천/초월 완화 | 낮음 | 중간 | 중간(수식 1개) | A7-4 |
| 2 | 계보 공명 | **높음** | 중간 | 중간(가챠/진화 선행) | A8 |
| 4 | 계정 레벨·계급 | 중간 | 낮음 | 낮음(메타 진행감) | A7-5 |
| 5 | 업적 해금 | 낮음 | 낮음 | 낮음(데이터+조건) | A7-6 |
| 10 | 진화 힌트 개선 | 낮음 | 낮음 | 낮음(UX 소소) | B-4 |
| 6 | 기록·블랙박스 | 중간 | 낮음 | 후순위(정보성) | A7-7 |
| 7 | 글로벌 랭킹 강화 | 중간 | 낮음 | 최후순위(서버 필요) | A7-8 |

**해석**: 저비용·고중요 = **순수 패시브 4종**(최우선). 고비용·고중요 = **HUD 리스킨**(이미 진행 중 작업과 합산). 고비용·중요도 보통 = **계보 공명**(콘텐츠 깊이지만 가챠/진화 인프라 선행). 저~중비용·저중요 메타군(계정레벨·업적·기록·랭킹)은 코어 완성 후 일괄 처리.
