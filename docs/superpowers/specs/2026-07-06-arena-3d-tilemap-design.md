# 아레나 3D 타일맵 장식 설계 (TileWorldCreator 4)

**작성일**: 2026-07-06
**상태**: 설계 확정 (구현 대기 — TWC4 임포트 완료)
**대상 씬**: `Assets/Scenes/ArenaScene.unity`
**도구**: TileWorldCreator 4 (Giant Grey, `Assets/TileWorldCreator/`)

---

## 1. 목적

HD-2D 원형 아레나 바닥을 3D 타일로 깔아 **시각 장식 + 입체감 + 배경 환경**을 연출한다.
동시에 이 타일 저작 파이프라인을 **추후 그리드 맵 모드에서 재사용**할 수 있도록 확장성 있게 구성한다.

- 즉시 목표: 아레나 바닥·경계·주변 환경을 3D 타일로 장식
- 미래 목표: 그리드 기반 새 게임 모드가 동일 타일 데이터를 활용

## 2. 확정된 결정

| 항목 | 결정 | 근거 |
|---|---|---|
| 저작 도구 | **TileWorldCreator 4** ($25, 임포트 완료) | 절차적+수작업 저작, 인접 타일 자동 방향, URP 대응, 성능(Cluster Merging) |
| 도구의 역할 | **비주얼/저작 레이어 전용** | 게임 로직과 분리해 벤더 종속 회피 |
| 테마 | 🌲 숲 / 🌊 바다 / 🌌 우주 3종 (교체형) | 사용자 요구 — 여러 환경 실험 |
| 첫 검증 테마 | **🌲 숲** | 포함 타일(Grass/Cliff)로 즉시 가능, 톤 궁합 안전 |

## 3. 아키텍처 — 비주얼 레이어와 게임 로직 분리

```
ArenaScene
├─ [게임 로직 레이어]  ← 절대 변경 안 함
│   └─ 반경 29 원형 스폰 링 · 코어 사거리 · ArenaModel (기존 그대로)
│
└─ [비주얼 바닥 레이어]  ← 신규, 게임 로직과 완전 분리
    └─ ArenaFloor (TileWorldCreatorManager)
       └─ Configuration (테마별 교체 지점)
          ├─ Forest  Config
          ├─ Sea     Config
          └─ Space   Config
```

**핵심 원칙**: 게임플레이(반경 29 원형 로직)는 TWC 에 의존하지 않는다. TWC 는 "보이는 배경"만 담당한다. 나중에 TWC 를 다른 툴로 교체해도 게임은 깨지지 않는다. 그리드 맵 모드(미래)의 게임플레이 데이터는 우리 자체 자료구조로 소유하며, 필요 시 TWC 의 조회 API 를 어댑터 뒤에 감싸 사용한다.

## 4. TileWorldCreator 4 워크플로우 (검증 완료)

TWC4 는 **Blueprint(설계) → Build(출력)** 2단계 레이어 스택 구조다.

1. **Configuration** ScriptableObject 에 `cellSize`, `width`, `height` 와 레이어 스택을 담는다.
2. **Blueprint Layer** — 2D 그리드 셀 데이터(`HashSet<Vector2>`). 채우는 방법:
   - **Generators** (절차적): `Shapes(Circle)`, `Maze`, `CellularAutomata`, `RandomWalkDungeon` 등
   - **Modifiers** (변형): `Expand`, `Shrink`, `SelectByRule`, `SelectBasedOnNeighbour`, `Invert` 등
   - **씬 페인팅** (수작업): PaintSceneOverlay 브러시로 직접 칠하기
3. **Build Layer** — Blueprint 셀을 실제 3D 로 변환:
   - **TilesBuildLayer** — 타일 프리팹을 인접 셀 기반 자동 선택(`TilePreset`)으로 배치 → 벽·모서리 자동
   - **ObjectBuildLayer** — 프리팹 산포(나무 등), 인접 타일 기반 방향 결정
4. **`GenerateCompleteMap()`** = `ExecuteBlueprintLayers()` + `ExecuteBuildLayers(FromScratch)`. 에디터·런타임 모두 실행 가능.

### 4.1 원형 아레나에 매핑

- **Shapes 제너레이터의 `Circle` 셰이프**(`radius` 지정)로 반경에 맞는 원형 Blueprint 를 절차적으로 생성 → 원 vs 격자 충돌을 도구가 해결.
- **CliffTiles** 를 자동 모서리 타일로 지정 → 원형 경계에 절벽/단차가 자동 배치.
- **ObjectBuildLayer** 에 나무 프리팹 → 숲 경계.
- 그리드 원점은 Manager transform. 아레나 중심(코어)에 정렬하고, `cellSize`·`radius` 를 반경 29 유닛에 맞춰 캘리브레이션.

## 5. 그리드 데이터 API (그리드 모드 재사용 근거 — 리스크 해소)

당초 "그리드 모드용 데이터 접근 API 불명확" 리스크는 **해소됨**. `TileWorldCreatorManager` 가 공개 API 를 제공한다:

| API | 용도 |
|---|---|
| `GetRelativeGridPosition(Vector3)` | 월드 → 그리드 셀 좌표 |
| `IsRelativePositionOverGrid(Vector2)` | 좌표가 그리드 내부인지 |
| `CellPositionExists(layer, Vector2)` | 특정 셀 점유 여부 |
| `GetCellPositionsInRadius(layer, worldPos, r)` | 반경 내 셀 목록 |
| `GetBuildLayerTileDataFromPosition(layer, Vector2)` | 셀의 TileData |
| `SampleLayerHeight(Vector3)` | 월드 위치의 높이 |
| `AddCellsToLayer / RemoveCellsFromLayer / FillLayer / ClearLayer` | 런타임 셀 변형 |
| 이벤트: `OnMapReady`, `OnBlueprintLayersReady`, `OnBuildLayersReady`, `OnMapProgress` | 생성 단계 훅 |

→ 그리드 맵 모드는 이 API 로 셀 데이터를 읽을 수 있다. 단, 아키텍처 원칙에 따라 **우리 어댑터 인터페이스 뒤에 감싼다** (직접 의존 금지).

## 6. 3테마 정의

| 테마 | 바닥 | 경계 연출 | 바깥 | 아트 소스 |
|---|---|---|---|---|
| 🌲 숲 | Grass 타일 | 나무 라인으로 감싼 빈터 | 숲(벽 역할) | TWC 포함 Grass/Cliff + **나무 프리팹 외부 소싱** |
| 🌊 바다 | 섬/모래 타일 | 물 위에 뜬 원형 섬 | 바다(물 셰이더) | TWC 포함 Sand/GrassToBeach + 물 셰이더 |
| 🌌 우주 | 플랫폼 타일 | 떠있는 플랫폼 + 가장자리 페이드 | 공허(스타필드) | **sci-fi 타일 외부 소싱** + 스카이박스 |

## 7. 진행 단계 (Phase)

| Phase | 내용 | 선행 조건 | 산출물 |
|---|---|---|---|
| **0** | TWC4 설치·컴파일·URP 확인, Samples_URP 추출로 예제 학습 | 완료(임포트됨) | 정상 컴파일, 예제 씬 확인 |
| **1** | 🌲숲 1테마 end-to-end: Circle Blueprint → Grass 바닥 + Cliff 경계 + 나무, ArenaScene 배치, 틸트시프트 카메라 톤·스프라이트 궁합·성능 검증 | Phase 0, 나무 아트 소싱 | 아레나에 깔린 숲 바닥 + 검증 스크린샷 |
| **2** | 검증된 파이프라인으로 🌊바다·🌌우주 Configuration 복제, (선택) 런타임 테마 교체 | Phase 1 | 3테마 Configuration |

## 8. 열린 항목 / 리스크

| # | 항목 | 상태 | 대응 |
|---|---|---|---|
| 1 | 숲 **나무/프롭 아트** 미포함 (TWC 포함 타일은 지형만) | 열림 | Phase 1 착수 시 무료 스타일라이즈드 자연 킷(KayKit Nature 등) 조사·소싱 |
| 2 | 틸트시프트 카메라 + 애니 스프라이트와 3D 바닥 **톤 궁합** | 미검증 | Phase 1 에서 실제 카메라로 스크린샷 검증, 필요 시 포스트 FX 조정 |
| 3 | `cellSize`·`radius` **스케일 캘리브레이션** (반경 29 유닛) | 미정 | Phase 1 초반 실측 조정 |
| 4 | 그리드 데이터 API | **해소** | 섹션 5 참조 |
| 5 | Samples_URP 미추출 (예제 씬 없음) | 열림 | Phase 0 에서 `Samples_URP.unitypackage` 추출 |

## 9. 참고 — TWC4 진입점

- 환영 창: `Tools > TileWorldCreator > Welcome`
- Configuration 생성: `Assets > Create > TileWorldCreator/Configuration`
- 타일 프리셋: `Assets/TileWorldCreator/Tiles URP/` (Grass, Cliff, River, Sand, BaseBlock, 2.5D)
- 공식 문서: https://giantgrey.gitbook.io/tileworldcreator-v4-documentation
