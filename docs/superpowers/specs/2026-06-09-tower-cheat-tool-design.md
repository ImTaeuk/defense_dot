# 플레이모드 타워 배치 치트 툴 설계 (Editor Window)

**작성일**: 2026-06-09
**상태**: 설계 승인됨 (스펙 사용자 검토 대기)
**브랜치**: `main`
**선행 문서**: [TASK-004 디버그 공격](../../tasks/active/TASK-004-debug-attack-types-resume.md) · [TASK-005 타워 등장 시스템](../../tasks/active/TASK-005-tower-placement-system.md)

---

## 1. 목표 & 배경

현재 두 씬(GridScene·AreanaScene) 모두 Arena 모드로 돌고 **타워 배치 게임플레이가 미구현**이라, TASK-004의 디버그 공격 behavior(단일/범위/투사체)를 **띄울 타워가 없다**. 정식 타워 등장 시스템(TASK-005)을 기다리지 않고, **플레이모드 전용 에디터 치트 윈도우**로 타워를 임의 배치해 공격 behavior를 즉시 관찰한다.

**확정 결정 (사용자 승인)**
| # | 항목 | 결정 |
|---|---|---|
| D1 | 공유 `TargetFinder` 획득 | `GameManager`에 `public TargetFinder TargetFinder => targetFinder;` **1줄 게터** 추가 (런타임 훅) |
| D2 | 윈도우 구현 | `EditorWindow` + **IMGUI(`OnGUI`)**, `[MenuItem("DefenseDot/Tower Cheat")]` |
| D3 | 슬롯 소스 | `MapData` ObjectField (기본 = 유일 `TestMapData`), `CellType.TowerSlot` 셀 순회 |
| D4 | origin(셀→월드) | 윈도우 Vector3 필드 + "씬에서 자동(MapVisualizer)" 버튼. **수동 조절로 슬롯 격자를 적 공전 경로에 정합** |
| D5 | 공격 타입 전환 | 배치된 타워의 인스펙터 토글(`debugSingle/Aoe/Projectile`, TASK-004)로 — 치트는 "타워 생성"만 |
| D6 | 점유/삭제 | 윈도우 자체 `Dictionary<Vector2Int, GameObject>` 추적 (런타임 `occupied`는 private·씬 인스턴스 없음) |
| D7 | 수명 | throwaway dev 도구. 런타임 게터는 `// DEBUG` 표식 |
| D8 | 스탯 오버라이드 | 윈도우에서 `attackRange`(+ `attackDamage`/`attackSpeed`) 조절. 배치 시 `TowerData`를 `Object.Instantiate`로 **런타임 복제** → 오버라이드 적용 → 복제본으로 `Initialize`. **원본 에셋 불변**, 사거리 3 고정 불편 해소. 복제본은 타워 삭제/플레이 종료 시 정리 |

---

## 2. 아키텍처

```
[런타임] GameManager (합성 루트)
   + public TargetFinder TargetFinder => targetFinder;   // DEBUG 게터 1줄 (Start 이후 non-null)

[에디터] TowerCheatWindow : EditorWindow   (Assets/Scripts/Editor/, DefenseDot.Editor.asmdef)
   OnGUI:
     EditorApplication.isPlaying == false → 안내만 그리고 return
     아니면 → MapData/Origin/타워 드롭다운/슬롯 리스트/생성·삭제 UI
   OnEnable:  playModeStateChanged 구독
   OnDisable: 구독 해제
   ExitingPlayMode: placed 추적 비움
```

**의존 방향**: 에디터 → 런타임(읽기 전용 게터 1개 + public 생성 API). 런타임은 에디터를 모름.

---

## 3. 컴포넌트

### 3.1 런타임 변경 (최소)
| 파일 | 변경 |
|---|---|
| `Systems/Management/GameManager.cs` | `// DEBUG` 읽기전용 프로퍼티 `public TargetFinder TargetFinder => targetFinder;` 1줄 추가. 기존 로직 무변경 |

### 3.2 신규 에디터 (`Assets/Scripts/Editor/TowerCheatWindow.cs`)
**윈도우 상태 필드**: `MapData mapData`, `Vector3 origin`, `TowerData[] towerDatas` + `int selectedTower`, `float rangeOverride / dmgOverride / spdOverride`, `List<Vector2Int> slots`, `int selectedSlot`, `Dictionary<Vector2Int, GameObject> placed`, `Dictionary<Vector2Int, TowerData> clones`.

**OnGUI 레이아웃**:
1. 플레이 게이트 — 미플레이 시 "플레이 중에만 사용 가능" HelpBox + return
2. `MapData` ObjectField (기본값 자동 로드) + "슬롯 새로고침"
3. `origin` Vector3Field + "씬에서 자동" 버튼(`FindFirstObjectByType<MapVisualizer>()?.transform.position` 대입 — `transform`은 public이라 리플렉션 불필요. 없으면 안내)
4. 타워 `Popup`(towerName 목록) — `AssetDatabase.FindAssets("t:TowerData")`로 채움
5. **스탯 오버라이드** (D8) — `attackRange`/`attackDamage`/`attackSpeed` Float 필드. 타워 선택 시 해당 `TowerData` 값으로 자동 채움(이후 자유 수정)
6. 슬롯 선택 리스트 — 각 `(x,y)` + 점유 여부(`placed.ContainsKey`) 표시, 선택형
7. **생성** / **삭제** 버튼

### 3.3 불변
- 도메인 모델, `TowerActor`/behavior(TASK-004), `TowerData`/`MapData`, HUD, 모드 부트스트랩

---

## 4. 데이터 흐름 (생성/삭제)

```
[생성]
  if !isPlaying or mapData==null or selectedSlot 무효 → 무시
  GameManager gm = FindFirstObjectByType<GameManager>()
  TargetFinder finder = gm != null ? gm.TargetFinder : null
  if finder == null → HelpBox "Play 직후 1프레임 대기/재시도"; return        (D1 타이밍 가드)
  TowerData src = towerDatas[selectedTower]
  if src.prefab == null → 경고; return                                    (prefab 가드)
  cell = slots[selectedSlot]
  if placed.ContainsKey(cell) → 경고(이미 점유); return
  TowerData data = Object.Instantiate(src)                               // 런타임 복제 — 원본 불변 (D8)
  data.attackRange = rangeOverride; data.attackDamage = dmgOverride; data.attackSpeed = spdOverride
  GameObject go = Instantiate(data.prefab)                               // prefab 필드는 복제본도 src와 동일
  TowerActor tower = go.GetComponent<TowerActor>() ?? go.AddComponent<TowerActor>()
  go.transform.position = origin + new Vector3(cell.x + 0.5f, 0.8f, cell.y + 0.5f)
  tower.Initialize(data)                                                 // 복제본 주입 → 프리팹 pre-assigned data·combatLogic 덮어씀
  tower.SetTargetFinder(finder)
  placed[cell] = go;  clones[cell] = data                                // 복제 SO 추적

[삭제]
  cell = slots[selectedSlot]
  if placed.TryGetValue(cell, out go) → Object.Destroy(go); placed.Remove(cell)
  if clones.TryGetValue(cell, out so) → Object.Destroy(so); clones.Remove(cell)   // 복제 SO 정리
```

> 생성 시퀀스는 `TowerPlacementController.TryPlace`(:102-110)와 동일하되 **골드 검증 제외**(치트). 적은 `EnemyRegistry`에만 등록되고 타워는 미등록이라 `TargetFinder` 순회에 영향 없음.

---

## 5. 범위 밖 / 연계

- **Arena 중앙 타워(TASK-005 A-1)**: 치트로 관찰하는 "중앙 타워 1개"는 정식 Arena 기능과 동일 → **치트 직후 바로 구현** 예정(별도 작업).
- **공간 정합**: 타워는 사거리(`attackRange`) 내 적만 공격. Arena는 적이 중앙을 공전하므로 `origin`으로 슬롯을 공전 반경 근처에 두거나, **사거리 오버라이드(D8)를 키워** 더 넓게 잡으면 정합이 쉬워짐.
- **TowerData 다종 / 공격 타입별 에셋**: 현재 `Tower Test` 1개. 공격 타입 전환은 타워 토글(D5)로 충분 → 추가 에셋은 보류.
- 정식 타워 등장 시스템(구매 UI·Grid 결선·능력 카드)은 TASK-005.

---

## 6. 오류 처리

- **isPlaying 가드**: 미플레이 시 동작 차단(에디트모드 오작동 방지)
- **finder null(타이밍)**: Play 진입 1프레임 null → 생성 차단 + 재시도 안내 (NRE 방지)
- **prefab null**: `Instantiate(null)` 예외 방지 가드 (현재 `TowerData.prefab`은 Tower_Test 할당됨, 그래도 방어)
- **GameManager 부재**: 씬에 없으면 안내
- **playModeStateChanged 구독 누수**: `OnEnable` 구독 ↔ `OnDisable` 해제 쌍 정확히

---

## 7. 테스트 / 검증

- **EditMode 단위테스트 없음** — 에디터 UI + 런타임 GameObject 상호작용이라 자동화 부적합.
- **PlayMode 수동**:
  1. `DefenseDot/Tower Cheat` 윈도우 열기 → Play → "플레이 중" UI 표시
  2. origin을 적 공전 근처로, 슬롯 선택 → 생성 → 타워가 적 공격(cyan 라인/처치/골드)
  3. 타워 인스펙터 토글로 단일/범위/투사체 전환 관찰
  4. 삭제 → 타워 제거, 슬롯 점유 해제
  5. Play 종료 → 추적 초기화, 다음 Play에서 정상

---

## 8. 컨벤션

- 신규 `.cs`는 `Assets/Scripts/Editor/` 하위(= `DefenseDot.Editor.asmdef` 범위), 네임스페이스 기존 에디터 규칙 일치(`DefenseDot.EditorTools` 등 `HudSetupTool` 참고).
- 명시적 접근 제한자, private 필드 camelCase, `System.*` 풀패스(단 `System.Collections.Generic` using 허용).
- 런타임 게터는 `// DEBUG` 표식.
- 신규 `.cs`는 `.meta` 동반.
