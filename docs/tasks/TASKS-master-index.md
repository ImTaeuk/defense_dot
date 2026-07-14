# defense_dot — 마스터 TASK 인덱스 & 원작 갭 로드맵

**최초 작성**: 2026-07-08
**최종 갱신**: 2026-07-08
**성격**: 크로스-세션 단일 진실 공급원(SSOT) — 남은 작업의 마스터 인덱스
**근거**: 원작 `Assets/Reference/dot-defense-main/index.html` (APP_VERSION 608, 33,187줄) + 현재 `Assets/Scripts/Systems/` 구현 상태

---

## 0. 사용법 (다른 세션 필독)

> 이 문서는 **여러 세션이 함께 읽고 갱신하는 마스터 인덱스**다. 개별 TASK 문서(`active/TASK-*.md`)는 상세를, 이 문서는 **전체 우선순위·원작 갭·진행 상태**를 담는다.

**갱신 규칙**
1. TASK를 **완료**하면 → 이 문서 §2 갭 표의 상태(`❌ 없음`→`🔶 부분`→`✅ 완료`)를 갱신하고, §7 갱신 이력에 한 줄 추가.
2. TASK를 **신설/분리**하면 → §4 상세와 §6 인덱스에 항목 추가.
3. **상태 판단은 코드 기준.** 문서 기술이 아니라 `Assets/Scripts/`의 실제 구현으로 확인한다. (과거에 문서의 "미커밋" 기술을 그대로 믿어 오판한 이력 있음 — 반드시 git·코드 대조)
4. 원작 대비는 **standard 모드 = Arena** 기준. Grid 모드는 원작 비정본 갈래이므로 갭 분석에서 분리한다.

**상태 범례**: `✅ 완료` · `🔶 부분(코어만/미완)` · `❌ 없음` · `⏸ 보류(설계·결정 대기)`

---

## 1. 현재 위치 — 완료된 기반 (코드로 확인됨)

원작 standard 모드의 **"성장하는 자동전투 생존"** 골격은 서 있다. 아래는 `Assets/Scripts/Systems/`에 실제 존재하는 시스템이다.

| 영역 | 구현 | 핵심 파일 |
|---|---|---|
| 모드 합성 루트 | Arena/Grid 분리, ModeBootstrap | `Systems/Mode/` (ArenaMode·GridDefenseMode·*Bootstrap) |
| 종료 규칙 (유한+승리) | 웨이브 소진→전멸 시 승리 | `EnemySpawner` (A0) |
| 플레이 루프 | 결과 패널·재시작·정지 | `GameManager`, GameResult (TASK-002 ✅ done) |
| 능력 데이터 아키텍처 | AbilityData/Instance/Loadout/Modifiers | `Systems/Abilities/` (A1) |
| 코어 자동전투 | 능력 실행 3종(투사체/궤도/영역) | `Systems/Abilities/Definitions·Effects/` (A2) |
| 레벨업·카드 선택 | kills→레벨업→3장 선택 모달 | `Systems/Cards/` (A3) |
| 순수 패시브 4종 | 맹공·토벌·쇄도·각성 조건부 배수 | `PureDamagePassiveData` (A5-2) |
| Actor BT 프레임워크 | POCO BT (Node/Selector/Sequence/Blackboard) | `Systems/Actor/` (Phase 1) |
| 풀링 인프라 | Pool/PoolManager, Addressables | TASK-013·015 ✅ done |
| 전투 VFX | 명중·머즐 이펙트, unscaled time | `Systems/Abilities/Effects/`, Rendering (TASK-014 🔶) |

---

## 2. 원작 대비 갭 분석 (핵심)

> "원작에 가까워지기 위해 무엇이 핵심인데 비어 있는가." 각 시스템의 **원작 근거(키워드·빈도)**, **우리 상태**, **매핑 TASK**.

### 2.1 성장 루프 — 원작 3단 성장 중 우리는 1단만

원작의 코어 성장은 **① 획득(레벨업 카드) → ② 강화(골드) → ③ 조합(재료)** 3단이다. 우리는 ①만 있다.

| 단계 | 원작 근거 | 우리 상태 | TASK |
|---|---|---|---|
| ① 획득 (레벨업→카드) | `triggerLevelUp`, `generateLevelUpChoices` | ✅ 완료 | A3 |
| ② **강화 (골드로 능력 레벨업/삭제)** | `actEnhance`, `actDelete` | ✅ **완료(로직·UI·PlayMode 검증)** | **A4** |
| ③ **조합 (재료2→상위)** | `COMBO_RECIPES`(14) | 🔶 **구현 완료(로직·배선·데이터·EditMode 152/152) · 플레이 검증/커밋 대기** | **A5-1** |

→ **①②③ 모두 코드상 갖춰짐.** A4 강화 완료 + A5-1 합성 구현 완료(검증·커밋 잔여)로 원작 빌드 성장 3단이 코드상 완성 — 남은 것은 A5-1 플레이 검증뿐.

### 2.2 페이싱·콘텐츠

| 시스템 | 원작 근거 | 우리 상태 | TASK |
|---|---|---|---|
| 보스 (N라운드마다) | `isBoss`(100), `bossIntro` | ❌ 없음 | A6 |
| 계보 공명 (SR+SSR 조합 시너지, v608) | `LINEAGE_RESONANCE` (23계보×5축) | ❌ 없음 | A8 |
| HUD/UI 리스킨 (메카닉 HUD·능력 도크) | v575~v606, `ABILITY_DOCK` | 🔶 부분(ArenaHud) | B |

### 2.3 메타층 (런간 진행 = 로그라이트 정체성)

| 시스템 | 원작 근거 | 우리 상태 | TASK |
|---|---|---|---|
| 가챠 (스타더스트→캐릭터/유물) | `gacha` | ❌ 없음 | A7-1 |
| 유물 (런 시작 보정) | `RELICS`(60) | ❌ 없음 | A7-2 |
| 강화소·승천 (영구강화·난이도 배수) | `buyUpgrade`, `ascension`(81) | ❌ 없음 | A7-3 |
| 행성 난이도 (3티어·31단계, v608) | `PLANET_TIERS` | ❌ 없음 | A7-4 |
| 계정 레벨·계급 (8계급, v608) | `RANKS` | ❌ 없음 | A7-5 |
| 업적 해금 (v608) | `checkUnlockAchievements` | ❌ 없음 | A7-6 |
| 기록·블랙박스 분석 (v608) | v510/v511 | ❌ 없음 | A7-7 |
| 글로벌 랭킹 (서버, v608) | `submitGlobalScore` | ❌ 없음 | A7-8 |

### 2.4 원작 재현과 분리된 트랙 (Arena 갭 아님)

Grid 모드·품질·구조 작업. 원작 standard 재현과 독립적이나 프로젝트 완성도에 필요.

| 작업 | 우리 상태 | TASK |
|---|---|---|
| Grid 타워 관리 모달(업그레이드·판매)·다종 | 🔶 SP1만 완료 | 005 |
| Actor Animator 방향/걷기 애니 | ⏸ 보류(방식 미결) | 006 |
| 진입 흐름·타이틀·일시정지 | ❌ 없음 | 008 |
| 코드 정리(빈 껍데기·리네임) | ❌ 없음 | 009 |
| 코어 전용 프리팹 정리 | ❌ 없음(분석 완료) | 016 |
| 디버그 공격 폐기 (능력 시스템이 대체함) | 🔶 폐기 대상 | 004 |

---

## 3. 우선도 × 개발비용 매트릭스 (2축)

> **우선도** = 원작 standard 정체성 재현 필수도. **개발비용** = Unity 재구현 공수.
> 왼쪽 위(고우선·저비용)일수록 **즉시 가성비**, 오른쪽 위(고우선·고비용)는 **전략 투자**.

### 3.0 개발비용(공수) 등급 정의

> 1인 작업(코드 + Unity 검증 포함) 기준. TASK 문서에 시간 명시가 없어 **남은 항목 수 × 작업 성격**으로 추정. `*` = 선행 결정·불확실 변수로 공수 변동 큼.

| 등급 | 절대 시간 | 성격 |
|---|---|---|
| **XS** | ≤ 0.5일 (2~4h) | 에디터 검증만 / 저위험 리팩토링 |
| **S** | ~1일 | 계획 완비된 C# 구현 + 테스트 |
| **M** | ~2일 | C# + UI/프리팹 배선, 다중 항목 |
| **L** | ~3~4일 | 여러 시스템 연계 / 디자인 결정 변수 |
| **XL** | 5일 ~ 2주+ | 대형 스코프(다수 시스템) / 장기 메타 |

매트릭스의 **비용 낮음 = XS~S · 중간 = M · 높음 = L~XL** 에 대응한다.

| | 비용: 낮음 | 비용: 중간 | 비용: 높음 |
|---|---|---|---|
| **우선도 ★★★**<br>(코어 루프) | **A4 인게임 강화** ⭐즉시 | **A5-1 능력 조합** | — |
| **우선도 ★★**<br>(페이싱·메타·UX) | A7-6 업적 · A7-3 승천수식 | A6 보스 · A7-1 가챠 · A7-2 유물 · A7-3 강화소 | **B HUD 리스킨** · A8 계보 공명 |
| **우선도 ★**<br>(진행감·확장) | 009 코드정리 | A7-4 행성난이도 · A7-5 계정계급 · 005 Grid SP2 · 016 코어프리팹 · 008 진입흐름 · 006 Animator | — |
| **우선도 ☆**<br>(정보성·서버) | 004 디버그 폐기(정리) | A7-7 기록 · A7-8 랭킹(서버) | — |

**해석**
- **⭐ 최우선 1건 = A4 인게임 강화** — 고우선·저비용. Economy가 이미 있어 강화/삭제 UI·로직만 얹으면 원작 성장 2축이 완성된다.
- **다음 = A5-1 능력 조합** — 고우선·중비용. 빌드 깊이의 마지막 조각.
- **A6 보스 · B HUD** — 페이싱·체감 향상. 중~고비용.
- **메타층(A7)** — Arena 방향이 (a)메타 진행형으로 확정되면 우선도 ★★로 상승. 아니면 후순위.
- **A8 계보 공명** — 콘텐츠 깊이지만 가챠/진화(A7) 선행 필요 + 고비용 → 후미.

---

## 4. 누락 시스템 상세 (원작 근거·우리 상태·착수 메모)

### A4. 인게임 강화 ★★★ · 비용 낮음 · ⭐최우선
- **원작**: `actEnhance`(골드로 능력 레벨업, 라운드 비례 `enhanceCost`) / `actDelete`(능력 삭제·환불).
- **우리**: `EconomyController` 존재. 능력은 `AbilityInstance.level`·`CoreAbilitySystem.LevelUpAbility` 존재. **UI + 골드 차감 로직만 없음.**
- **착수**: 능력 슬롯 클릭 → 강화/삭제 액션 UI. `superpowers:brainstorming` 선행(신규 기능).

### A5-1. 능력 합성(Fusion) ★★★ · 비용 중간 · 🔶 구현 완료(플레이 검증/커밋 대기)
- **원작**: `COMBO_RECIPES`(14) — 재료 능력 2개 소진 → 상위 능력. 카드 풀에 combo 액션 추가.
- **우리**: 타워별 계보 테이블(`FusionRecipeSet`)로 재해석. 데이터(`TowerData.fusionLineage`)와 로직(`FusionResolver`·`CardChoiceGenerator`·`CardChoiceApplier`) 분리 배선 완료.
- **상태**: 로직·배선·데모 데이터·EditMode 152/152 통과. 남은 것: 플레이(PlayMode) 체감 검증 + 커밋. 상세: `docs/tasks/active/A5-1-fusion-implementation-report.html`, 계획: `docs/superpowers/plans/2026-07-10-a5-1-ability-fusion.md`.

### A6. 보스 ★★ · 비용 중간
- **원작**: `isBoss`(100), `bossIntro`, `roundConfig`. 5라운드마다 보스 + 보상.
- **우리**: 웨이브/스폰 있음. 보스 플래그·연출·보상 없음. A3/A5 보상 재사용 가능.

### A7. 메타층 ★★~★ · 비용 중~높음 (Arena 방향 a 시 우선도↑)
- 가챠(A7-1)·유물(A7-2, `RELICS` 60)·강화소/승천(A7-3, `ascension` 81)·행성난이도(A7-4)·계정계급(A7-5)·업적(A7-6)·기록(A7-7)·랭킹(A7-8).
- 런에 보정만 주입 → 코어와 느슨결합, 단독 추가 가능. **최후 트랙.**

### A8. 계보 공명 ★★ · 비용 높음 (v608 신규)
- **원작**: `LINEAGE_RESONANCE` (23계보×5축: blast/pierce/shock/echo/ice), SR+SSR 캐릭터 조합 발동, 온히트/틱 비주얼.
- **선행**: 가챠/캐릭터(A7) 인프라. A2 능력 실행계 위에 얹힘.

### B. HUD/UI 리스킨 ★★ · 비용 높음 (v575~v608)
- 메카닉 HUD(네온·코너 꺾쇠)·능력 도크(칩 그리드)·세로 상단바·배속/일시정지 컨트롤.
- UI 아키텍처 베이스(UIView/Widget/Presenter+ReactiveProperty) 완료 → 그 위에 올림. `arena-hud-replacement` 진행분과 통합.

---

## 5. 권장 진행 순서

```
1. A4 인게임 강화        ← ⭐ 고우선·저비용, 원작 성장 2축 완성
2. A5-1 능력 조합        ← 빌드 깊이 마지막 조각 (여기까지 = 원작 코어 루프 완성)
3. A6 보스 + B HUD 리스킨 ← 페이싱·체감 (병행 가능)
   ─── Arena 방향(a/b) 결정 게이트 ───
4. A7 메타층 (방향 a 확정 시)  ← 로그라이트 런간 진행
5. A8 계보 공명 (가챠 선행 후)
(병행 트랙) 004 폐기 · 016 코어프리팹 · 009 정리 · 005 Grid · 006 Animator · 008 진입
```

**게이트**: A7·A8은 **Arena 정체성 방향** 결정이 선행 — (a) 메타 진행형 오토배틀러 / (b) 인런 조작형. 이 결정 전까지 메타층은 착수 보류.

---

## 6. 활성 TASK 인덱스

| TASK | 제목 | 우선순위 | 남은 공수 | 매핑 |
|---|---|---|---|---|
| [012](active/TASK-012-arena-mode-roadmap.md) | Arena 모드 로드맵 (마스터) | 높음 | **XL** (2주+) | 위 A0~A8·B 전체의 정본 |
| [014](active/TASK-014-combat-vfx-restore.md) | 전투 VFX 복구 | 높음 | XS~S | 🔶 B-1/B-2 연출만 남음 |
| [004](active/TASK-004-debug-attack-types-resume.md) | 디버그 공격 | 높음→폐기후보 | XS (폐기 시) | 능력 시스템이 대체 |
| [015](active/TASK-015-addressables-asset-infra.md) | Addressables 인프라 | 높음 | XS | ✅ 사실상 완료(배포시점만) |
| [005](active/TASK-005-tower-placement-system.md) | Grid 타워 배치 | 중간 | L | SP2/SP3 |
| [010](active/TASK-010-skill-system.md) | Skill 시스템 | 중간 | — | 012 A1~A5로 흡수 |
| [006](active/TASK-006-actor-animator-redesign.md) | Actor Animator | 중간(보류) | L* | 방식 결정 선행 |
| [016](active/TASK-016-core-tower-prefab.md) | 코어 전용 프리팹 | 중간 | S~M | 독립 |
| [017](active/TASK-017-fusion-lineage-visual-editor.md) | 합성 계보 비주얼 에디터 | 낮음 | M | A5-1 후속(에디터 툴) |
| [018](active/TASK-018-levina-storm-lineage-ability-details.md) | 레비나 폭풍 계보 능력 세부 | 중간 | M | 계보 구조 검증 완료·세부(이펙트·스탯·풀) 미완 |
| [008](active/TASK-008-entry-flow-pause.md) | 진입 흐름·일시정지 | 중간 | S | 독립 |
| [001](active/TASK-001-arena-map-remaining.md) | 아레나 맵 남은 작업 | 중간 | XS | 대부분 012 흡수 |
| [009](active/TASK-009-code-cleanup.md) | 코드 정리 | 낮음 | XS | ⚠️ PathfindingService는 보류(미완성 인프라) |
| [011](active/TASK-011-meta-shop.md) | 메타 상점 | 낮음 | XL* | 012 A7로 흡수 |

> 공수는 6/29 `priority-effort-matrix` 추정을 계승·갱신(013·002 done 반영, 014 B-0/B-3 완료로 하향). `*` = 선행 결정으로 변동.

**완료(done)**: [TASK-002](done/TASK-002-game-end-result-restart.md) 플레이 루프 · [TASK-013](done/TASK-013-effect-pooling-system.md) 풀링 시스템

---

## 7. 갱신 이력

- **2026-07-14** — **계보 다단(높이3) 구조 검증**. 원작 레비나 시그니처 라인을 레퍼런스해 Aris 계보를 높이 3 트리로 구축(데이터만, 코드 0). 신규 능력 7개(기존 타입/프리팹 복제)·레시피 3개 추가 → 1층 StormBrand+StunBomb→LevinLash / 2층 +Tornado→TempestVeil / 3층 +Railgun→StormSovereign 다단 등반 + 기존 분기 공존을 런타임 검증(PASS). FusionSystem 평면 리스트가 다단+분기 지원 확인. 능력 세부(이펙트·스탯·밸런스·풀 편입)는 **TASK-018** 신규 등록으로 이연.

- **2026-07-13** — **A5-1 능력 합성(Fusion) 구현 완료**(플레이 검증/커밋 대기)로 §2.1 조합 상태 `❌→🔶`, §4 A5-1 상세 갱신. 타워별 계보(`FusionRecipeSet`) 데이터/로직 분리 설계로 구현: `FusionResolver`·`CardChoiceGenerator`·`CardChoiceApplier`·`TowerData.fusionLineage` 배선, 데모 계보(Shot+Orbital→AreaWave) 에셋, EditMode 152/152 통과. 보고서 `active/A5-1-fusion-implementation-report.html`. → 원작 성장 3단(획득 A3·강화 A4·조합 A5-1) 코드상 모두 갖춰짐(A5-1 검증·커밋만 잔여). **TASK-017**(합성 계보 비주얼 에디터) 신규 등록 — A5-1 후속 에디터 툴.
- **2026-07-08** — 문서 신설. 원작 v608(33,187줄) 갭 재검증, 우선도×개발비용 2축 매트릭스 작성. 선행 3개 정리 문서 통합·대체: `TASKS-remaining-overview.html`(6/11 낡음)·`task-backlog-triage`(7/7 우선도×볼륨)·`TASKS-priority-effort-matrix.html`(6/29 우선도×소요시간 — 공수 등급 XS~XL 정의·TASK별 공수 추정을 §3.0·§6에 흡수). 핵심 결론: 원작 성장 3단(획득/강화/조합) 중 강화(A4)·조합(A5-1) 누락이 최상위 갭.
