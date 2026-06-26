# 카드 선택 허브 (Arena A3 코어) 설계

**작성일**: 2026-06-26
**상태**: 설계 승인됨 (사용자 리뷰 대기)
**범위**: TASK-012 Arena 로드맵 A3 (레벨업·카드 선택 ★핵심 허브) — 코어(①)만
**근거**: 원작 `Assets/Reference/dot-defense-main/index.html` (APP_VERSION 608) 카드 선택 시스템 분석

---

## 1. 목적 / 배경

Arena 모드의 정체성은 "성장하는 자동전투 생존"이다. 코어 능력이 자동 전투하고, 플레이어는 **kills 누적 → 레벨업 → 카드 3장 선택**으로 빌드를 키운다. 이 카드 선택 허브(A3)는 A2(능력 실행)·A4(강화)·A5(조합)가 매달리는 중심 허브다.

원작 카드 시스템은 5겹(코어 / 럭키 / 조합 / 보너스 / 부가)이지만, 본 설계는 **코어(①)만** 다룬다. 나머지 겹은 로드맵 A5(조합)·A7(메타)·후속으로 분리하되, 본 설계의 데이터/연출 구조가 그 확장을 **플래그·데이터 추가만으로** 수용하도록 한다.

---

## 2. 범위 (스코프)

### 2.1 포함 (이번 구현)
- kills 누적 → 레벨업 트리거 (`killsToNextLevel` 곡선, 데이터 주도)
- 레벨업 시 카드 **3장 생성**: `신규 능력` 또는 `기존 능력 레벨업`
- 액티브/패시브 **슬롯 가용성** 반영 (슬롯 가득 → 레벨업 카드만)
- 카드 선택 적용 → `AbilityLoadout` 반영 (신규 추가 / 레벨업) + 러너 동기화
- 카드 선택 중 **정지(timeScale=0)** — 설정 토글
- 카드 모달 UI(uGUI 프리팹) — 암전 오버레이 + 3카드(아이콘/이름/종류태그/설명/희귀도 색) + 셰이더 글로우 + 파티클 + fade-in
- 설정/콘텐츠 SO 분리: `ArenaCardConfig`(설정) + `AbilityPool`(풀) + `CardTierSet`(티어 색/연출)

### 2.2 제외 (후속 — 뼈대만 마련)
- ② 럭키/슈퍼럭키 (+2/+3) — `enableLucky` 플래그 자리만, 기본 off
- ③ 조합 카드 (재료2 MAX 소진→상위) — 로드맵 A5
- ④ 보너스 카드 (점수/스타더스트/어센던트) — 로드맵 A7 메타
- ⑤ 부가 (리롤/취소·골드/자동선택 타이머) — 후속
- 메타 보정(`levelUpMul`/`levelUpFast`/`luckyChanceBonus` 등) — A7 연결 시

---

## 3. 설계 결정 로그 (브레인스토밍 합의)

| # | 결정 | 선택 | 근거 |
|---|---|---|---|
| D1 | A3 스코프 | 코어(①)만 + 토글 | A5/A7 단계와 비중첩, 플레이 가능 최소 증분 |
| D2 | 설정 관리 | 단일 `ArenaCardConfig` SO | 토글·파라미터 한곳 집약, 원작 const 플래그의 SO 이관 |
| D3 | 능력 풀 | 별도 `AbilityPool` SO (지금 분리) | 분리가 확정된 일을 미룰 이유 없음(콘텐츠/설정 분리) |
| D4 | 정지 | `pauseOnCardSelect` 토글 (기본 on) | 자동전투 — 멈추고 고르는 게 명확. 원작은 비정지지만 의도적 분기 |
| D5 | UI 충실도 | 최대(C) — 셰이더+파티클 | "유니티 구현이 웹보다 연출 구린 건 불가" |
| D6 | 연출 구현 | 방식1 — 셰이더 주력 + Shuriken 파티클(C# 구성), VFX Graph 미사용 | 손코딩 셰이더는 MCP 저작 가능, 노드 그래프는 불가 |

---

## 4. 아키텍처 개요

```
[적 처치]
   │ CombatModel.OnEnemyKilled(reward)
   ▼
LevelModel (kills++, 곡선 도달 시 level++, pendingLevelUps++)
   │ OnLevelUp
   ▼
CardSelectionPresenter (IPresenter)
   │ 1) CardChoiceGenerator.Generate(loadout, pool, config) → List<CardChoice>
   │ 2) choices 비면 무모달(레벨만 부여), 아니면 ↓
   │ 3) View.Show(choices) + (config.pause면) Time.timeScale=0
   ▼
CardSelectionView (uGUI 프리팹, 셰이더+파티클)
   │ OnCardSelected(index)
   ▼
CardSelectionPresenter
   │ 4) CoreAbilitySystem.AddAbility(data) / LevelUpAbility(instance)  ※러너 동기화
   │ 5) View.Hide() + Time.timeScale=1 (게임이 Playing일 때만)
   │ 6) pendingLevelUps>0 면 3)으로 재진입
```

설계 원칙: **로직(생성·적용)과 표현(View·셰이더·파티클) 분리**, **티어 색/연출은 데이터(CardTierSet)로 구동** → 원작의 하드코딩 9티어 분기를 데이터 한곳에서 관리, 후속 겹은 데이터/플래그 추가로 확장.

---

## 5. 컴포넌트 명세

### 5.1 `ArenaCardConfig` (ScriptableObject) — 신규
설정 단일 진실. 디자이너가 인스펙터에서 토글.
- `bool pauseOnCardSelect = true`
- `int choiceCount = 3`
- `int curveBase = 8`, `int curvePerLevel = 4` — `killsToNextLevel(level) = max(3, curveBase + level*curvePerLevel)`
- `float newCardChanceEarly = 0.75f`, `float newCardChanceLate = 0.45f`, `int earlyLevelThreshold = 4` — 신규 vs 레벨업 비율(원작 대응)
- `[향후] bool enableLucky=false, enableCombo=false, enableBonus=false`
- `[향후] float luckyChance=0.12f, superLuckyChance=0.03f`
- `CardTierSet tierSet` 참조

### 5.2 `AbilityPool` (ScriptableObject) — 신규
- `List<AbilityData> abilities` — "신규 능력" 카드 후보. 시작은 기존 3종(Shot/Orbital/AreaWave). 능력 추가는 별도 콘텐츠 작업.
- 조회 헬퍼: 미보유 + 슬롯 가용 필터링은 Generator가 수행.

### 5.3 `CardTierSet` (ScriptableObject) — 신규
티어별 색/연출 데이터. 원작 `CARD_TIERS` 이관.
- 항목: `CardTier`(enum: New, Upgrade, [향후 Lucky/SuperLucky/Combo/Triple/...])
- 각 티어: `Color borderColor`, `Color bgTop`, `Color bgBottom`, `Color glowColor`, `float glowIntensity`, `bool useParticle`, `ParticlePreset preset`
- 코어는 New/Upgrade만 채움. 나머지 티어 항목은 후속 겹 활성 시 채움.

### 5.4 `LevelModel` (BaseModel) — 신규
플레이어 레벨/킬 추적.
- 필드: `int Level`(시작 1), `int Kills`, `int KillsToNextLevel`, `int PendingLevelUps`
- `event Action OnLevelUp`
- 생성 시 `ArenaCardConfig` 곡선 파라미터로 `KillsToNextLevel` 초기화
- `CombatModel.OnEnemyKilled` 구독 → `RegisterKill()`:
  - `Kills++`; `while (Kills >= KillsToNextLevel) { Kills -= KillsToNextLevel; Level++; KillsToNextLevel = curve(Level); PendingLevelUps++; }`
  - `PendingLevelUps>0`면 `OnLevelUp` 발화

### 5.5 `CardChoiceGenerator` — 신규 (순수 로직, 비 MonoBehaviour)
- 입력: `AbilityLoadout`(보유/레벨/슬롯), `AbilityPool`, `ArenaCardConfig`, 현재 `Level`
- 출력: `List<CardChoice>` (0 ~ choiceCount장)
- 절차:
  1. `levelPool` = 보유 중 maxLevel 미만 인스턴스
  2. `newPool` = 풀 중 미보유 & 해당 종류 슬롯 가용(액티브면 active 슬롯 빈 칸, 패시브면 passive 슬롯)
  3. choiceCount회 반복: `newPool`/`levelPool` 가용 + 확률(`Level<earlyThreshold? early:late`)로 한쪽 선택, 중복 회피하며 카드 1장 push
  4. 후보 고갈 시 가능한 만큼만 생성 (코어는 보너스 카드 폴백 없음)
- `CardChoice` 구조: `{ CardAction action(New|Level), AbilityData data, AbilityInstance instance(level용), int fromLevel, int toLevel, CardTier tier }`
  - tier: New→`New`, Level→`Upgrade` (코어). 럭키는 후속.

### 5.6 `CardSelectionView` (uGUI 프리팹 + MonoBehaviour) — 신규
- 위치: `Assets/Prefabs/UI/CardSelection_Panel.prefab`, Canvas 하위 전체화면 오버레이
- 구성: 암전 Image(알파) · 타이틀 TMP(`LEVEL n // UP`) · 카드 슬롯 3 (카드 아이템: bg Image[셰이더 머티리얼]·border Image·icon Image·name TMP·kind tag TMP·desc TMP·Button) · 파티클(스크린스페이스)
- 폰트: **neodgm SDF** 강제
- API: `void Show(IReadOnlyList<CardChoice>)`, `void Hide()`, `event Action<int> OnCardSelected`
- fade-in(약 300ms) — 캔버스그룹 알파 트윈 + 카드 등장 스케일

### 5.7 `CardSelectionPresenter` (IPresenter) — 신규
- 의존: `CardSelectionView`, `LevelModel`, `CardChoiceGenerator`, `CoreAbilitySystem`, `ArenaCardConfig`, `AbilityPool`, `GameFlowModel`
- `Initialize`: `level.OnLevelUp += HandleLevelUp`; `view.OnCardSelected += HandleSelected`; `view.Hide()`
- `HandleLevelUp`: choices 생성 → 비면 `ConsumePending()`(모달 없이 레벨 소진) ; 아니면 `view.Show(choices)` + (config.pause면 `Time.timeScale=0`)
- `HandleSelected(idx)`: 선택 적용(New→`core.AddAbility(data)`, Level→`core.LevelUpAbility(instance)`) → `view.Hide()` → 게임이 Playing이면 `Time.timeScale=1` → `PendingLevelUps>0`면 `HandleLevelUp` 재진입
- `Dispose`: 구독 해제 + `Time.timeScale=1` 안전 복구

### 5.8 카드 셰이더 (ShaderLab/HLSL) — 신규
- `Assets/Shaders/UI/CardBackground.shader` (URP, UI 호환)
- 프로퍼티: `_ColorTop/_ColorBottom`(그라데이션), `_GlowColor/_GlowIntensity`, `_Pulse`, `[향후] _RainbowOn`
- 티어 머티리얼 인스턴스에 `CardTierSet` 값 주입(프레젠터/뷰가 MaterialPropertyBlock로 설정)

### 5.9 기존 코드 수정 (연동)
- **`CoreAbilitySystem`**: `AbilityLoadout` 접근/변경 façade 신설 (현재 internal).
  - `bool AddAbility(AbilityData)` → `loadout.TryAdd` + 성공 시 `runner.Equip(newInstance)` (틱 동기화 ★중요)
  - `void LevelUpAbility(AbilityInstance)` → `loadout.LevelUp`
  - `AbilityLoadout Loadout { get; }` — 읽기 전용 getter 노출. Generator는 이걸로 보유/레벨/슬롯 질의 (변경은 위 두 메서드로만)
- **`GameManager`**: `LevelModel` 생성(config 곡선) + `CombatModel` 연결 + Inject에 전달
- **`UIRoot.Inject`**: `CardContext` 구조체(`LevelModel`·`ArenaCardConfig`·`AbilityPool`·`CoreAbilitySystem`·`GameFlowModel`)를 추가 인자로 받아 `CardSelectionPresenter` 등록. (개별 인자 나열 대신 구조체로 묶어 시그니처 비대화 방지)
- **`ArenaModeBootstrap`**: `[SerializeField] ArenaCardConfig cardConfig`, `AbilityPool abilityPool` 보유·노출

---

## 6. 데이터 흐름 (상세)

1. 적 사망 → `CombatModel.RegisterKill(reward)` → `OnEnemyKilled` 발화
2. `LevelModel`이 수신 → `Kills++` → 곡선 도달 시 `Level++`, `PendingLevelUps++` → `OnLevelUp`
3. `CardSelectionPresenter.HandleLevelUp` → `CardChoiceGenerator.Generate(...)`
4. choices 있으면 `View.Show` + 정지(설정 시)
5. 사용자 카드 클릭 → `OnCardSelected(idx)`
6. 적용: New→`CoreAbilitySystem.AddAbility` (러너 Equip 포함), Level→`LevelUpAbility`
7. `View.Hide` + 정지 해제(Playing 시) + `PendingLevelUps` 남으면 재진입

---

## 7. 정지(Pause) 처리

- `config.pauseOnCardSelect == true` → 모달 표시 시 `Time.timeScale = 0`, 선택 후 `1` 복구
- **소유권 충돌 방지**: `GameResultPresenter`도 timeScale을 만진다. 규칙 — 카드 프레젠터는 **게임이 Playing일 때만** `1`로 복구. 게임 종료(GameOver/Victory) 발생 시 결과 프레젠터가 0으로 잡으므로, 카드 프레젠터는 복구하지 않고 모달만 숨긴다.
- `GamePhase`에 `Paused` 추가하지 않음 (기존 합의 — timeScale 방식)

---

## 8. 연출(VFX) 구현 방식 (방식1)

- **셰이더(주력)**: `CardBackground.shader` 손코딩 — 그라데이션·네온 글로우, 향후 무지개/펄스. MCP `manage_shader`/머티리얼로 저작.
- **파티클**: Shuriken `ParticleSystem`을 `execute_code`(C# 에디터 스크립트)로 모듈 구성 — 스크린스페이스 카메라 캔버스 또는 오버레이에 배치.
- **VFX Graph 미사용** (MCP 저작 불가). 코어 연출은 셰이더+파티클로 충분.
- **검증**: 빌드 후 스크린샷(`SceneView.Capture`/게임뷰)으로 확인하며 반복 튜닝.
- 코어 티어(New/Upgrade)부터 풀 폴리시. 럭키/조합 전용 연출(스파클/무지개/셰이크)은 `CardTierSet` 항목으로 자리만, 해당 겹 활성 시 채움.

---

## 9. 오류 처리 / 엣지 케이스

| 케이스 | 처리 |
|---|---|
| 후보 0장 (전부 보유+maxLevel, 풀 소진) | 모달 미표시, 레벨만 부여(`ConsumePending`). 경고 로그 |
| 후보 < choiceCount | 가능한 장수만 표시 (3 미만 카드) |
| 레벨업 다중 큐(비정지 중 연속) | `PendingLevelUps` 카운터로 순차 처리(선택 후 재진입) |
| 신규 액티브 추가 | 러너 `Equip` 호출로 즉시 틱 반영 (누락 시 동작 안 함 — ★) |
| 게임 종료 중 모달 열림 | 모달 숨김, timeScale 복구는 결과 프레젠터에 위임 |
| Dispose/씬 종료 | 구독 해제 + timeScale=1 안전 복구 |

---

## 10. 테스트 전략

- **EditMode 단위**
  - `LevelModel`: 곡선 계산(레벨별 KillsToNextLevel), 다중 레벨업/PendingLevelUps 누적
  - `CardChoiceGenerator`: 슬롯 가득→레벨업 카드만, 풀 소진→fewer/empty, 3장 내 중복 없음, New/Level 비율 경계(earlyThreshold)
- **PlayMode 통합** (기존 리플렉션 패턴)
  - kills 주입 → `OnLevelUp` → 모달 표시 → 카드 선택 → `AbilityLoadout` 반영(신규 추가&Equip / 레벨 증가), `Time.timeScale` 정지·복구 확인

---

## 11. 후속 / 확장 지점 (이번 미포함)

- ② 럭키: `enableLucky` on + Generator에 lucky roll + `CardChoice.tier=Lucky/SuperLucky` + 셰이더 무지개/파티클 채움
- ③ 조합(A5): Generator에 combo 후보(재료2 MAX) + `CardAction.Combo` 적용 경로(재료 소진→상위)
- ④ 보너스(A7): `CardAction.Bonus*` + 메타(스타더스트/점수/어센던트) 연결
- ⑤ 부가: 리롤/취소(골드 환불·다음 비용↑)/자동선택 타이머
- 메타 보정: `levelUpMul/levelUpFast/luckyChanceBonus`를 `ArenaCardConfig`/런 상태에 연결
