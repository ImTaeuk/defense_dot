# TASK-017: 합성 계보(Fusion Lineage) 비주얼 에디터

**작성일**: 2026-07-13
**상태**: 구현 완료 (GraphView 에디터 · 로드·편집·저장 검증)
**우선순위**: 낮음 (A5-1 플레이 검증·안정화 이후)

> **구현 (2026-07-15)**: `Assets/Scripts/Editor/` 에 GraphView 기반 에디터 4파일 —
> `FusionLineageEditorWindow`(메뉴 `DefenseDot/Fusion Lineage Editor` · 계보 드롭다운) ·
> `FusionLineageGraphView`(로드·저장·연결) · `AbilityNode`(이름·아이콘·포트 입력2·출력1) ·
> `FusionLineageLayout`(깊이별 자동 배치). "결과 노드=2입력 포트" 모델로 다단 체인 표현.
> 노드 추가=캔버스 우클릭, 레시피 추가/삭제=포트 연결/엣지 삭제, 편집 시 자동 저장.
> 검증: Aris 계보 로드(10노드/8엣지), 편집(추가 4→5·제거 5→4) 에셋 반영 확인.

---

## 1. 문제 정의

A5-1 능력 합성(Fusion)에서 각 타워(캐릭터)는 `FusionRecipeSet`(족보/계보 데이터) 하나를 가진다.
현재 이 계보는 기본 인스펙터의 `List<FusionRecipe>`(재료A · 재료B · 결과) 목록으로만 편집·조회된다.

- **불편**: 레시피가 늘어나면(예: 재료→결과가 다시 다른 합성의 재료가 되는 다단 계보) 목록만으로는
  "무엇이 무엇으로 합쳐지고, 그 결과가 또 어디로 이어지는지" 전체 그림이 보이지 않는다.
- **요구**: 계보 데이터 하나당 **어떤 능력들이 어떻게 이어지는지**를 한눈에 보여주는 비주얼 에디터.

## 2. 목표

- `FusionRecipeSet` 1개를 **노드 그래프**로 시각화한다.
  - 노드 = 능력(`AbilityData`) — 재료·결과
  - 엣지 = 레시피 `재료A + 재료B → 결과`
- 다단 계보(결과가 다음 합성의 재료가 되는 경우)의 **연쇄를 트리/그래프로 표현**.
- 가능하면 편집도 지원(노드 연결로 레시피 추가/수정) — 최소 목표는 **읽기 전용 시각화**.

## 3. 접근 후보 (구현 시 브레인스토밍 대상)

| 방식 | 장점 | 단점 |
|---|---|---|
| **UI Toolkit `GraphView`** (Shader Graph·VFX Graph 계열) | Unity 표준 노드 에디터 인프라, 드래그·연결 기본 제공 | 학습 곡선, 에디터 전용 의존 |
| 커스텀 `EditorWindow` + IMGUI 트리 | 단순·가벼움, 읽기 전용에 충분 | 자유 배치·연결 편집은 수작업 |
| 커스텀 `PropertyDrawer` 확장(인스펙터 내 미니 그래프) | 별도 창 없이 인스펙터에서 즉시 | 표현 공간 제약 |

> **Unity 내장 우선**: `GraphView`(UnityEditor.Experimental.GraphView)가 이 요구에 가장 적합한 표준 후보.
> 착수 시 GraphView vs IMGUI 트리를 읽기전용/편집 목표에 맞춰 결정.

## 4. 범위 (Scope)

- **In**: `FusionRecipeSet` 단일 계보의 노드 그래프 시각화(에디터 전용). 능력 아이콘/이름 표시.
- **Out(초기)**: 런타임 UI(플레이어용), 여러 계보 동시 비교, 밸런스 수치 오버레이.
- **선행**: A5-1 플레이 검증 완료 + 계보 데이터가 다단으로 실제 늘어나는 시점.

## 5. 참고

- 데이터: `Assets/Scripts/Systems/Cards/FusionRecipeSet.cs` (`List<FusionRecipe>` · `materialA/materialB/result`)
- 판정 로직: `Assets/Scripts/Systems/Cards/FusionResolver.cs`
- 배선/설계: `docs/tasks/active/A5-1-fusion-implementation-report.html`, `docs/superpowers/specs/2026-07-10-a5-1-ability-fusion-design.md`
- 에디터 자동화 레시피: `~/.claude/skills/unity-editor-recipes/SKILL.md`
