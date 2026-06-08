# 플레이모드 타워 배치 치트 툴 Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** 플레이 중 에디터 윈도우에서 타워 슬롯에 타워를 생성/삭제(스탯 조절 포함)해 디버그 공격 behavior를 관찰하는 치트 툴을 만든다.

**Architecture:** 런타임 `GameManager`에 공유 `TargetFinder` 읽기전용 게터 1줄을 노출하고, 에디터 어셈블리에 IMGUI `EditorWindow`(`TowerCheatWindow`)를 추가한다. 윈도우는 Play 중에만 동작하며, `TowerData`를 런타임 복제해 스탯을 오버라이드한 뒤 `TowerPlacementController.TryPlace`와 동일한 시퀀스로 타워를 생성한다. 의존 방향은 에디터→런타임 단방향.

**Tech Stack:** Unity 6000.x / URP / C# / UnityEditor(EditorWindow, IMGUI, AssetDatabase). 비동기·테스트 프레임워크 미사용.

**선행 스펙:** [2026-06-09-tower-cheat-tool-design.md](../specs/2026-06-09-tower-cheat-tool-design.md)

---

## 이 계획 공통 규칙 (모든 태스크 적용)

- **브랜치 `main`에서 작업.**
- **Unity 실행 불가**: 컴파일/PlayMode를 이 세션에서 돌릴 수 없다. 코드 작성 후 정적 참조 검증만 하고, **"컴파일됨/동작함"을 주장하지 말 것.** 컴파일·PlayMode 검증은 사용자 Unity에서.
- **커밋 정책**: **구현만 — 태스크별 커밋 금지.** 전부 끝나면 diff 제시 → 사용자 **명시 승인** → `commit` 스킬로 scoped 일괄 커밋(아래 Task 4). (CLAUDE.md "명시적 요청 없이 커밋 금지" + 하네스 무단커밋 경고 이력)
- **scoped staging**: `git add .`/`-A` 금지 — 만든 파일 경로만 명시.
- **신규 `.cs`**: Unity 미실행이므로 `.cs.meta`를 직접 생성(충돌 검사한 32-hex GUID). 생성: `head -c16 /dev/urandom | od -An -tx1 | tr -d ' \n'`. 스크립트 `.cs.meta` 형식은 기존 `Assets/Scripts/Editor/HudSetupTool.cs.meta` 참고(`MonoImporter` 블록).
- **에디터 코드 위치**: 신규 `.cs`는 반드시 `Assets/Scripts/Editor/` 하위(= `DefenseDot.Editor.asmdef` 범위). 네임스페이스 `DefenseDot.EditorTools`(기존 `HudSetupTool`과 동일).

---

## File Structure

**Modify**
- `Assets/Scripts/Systems/Management/GameManager.cs` — 공유 `TargetFinder` 읽기전용 게터 1줄 (`// DEBUG`)

**Create**
- `Assets/Scripts/Editor/TowerCheatWindow.cs` (+`.meta`) — 치트 EditorWindow 전체

---

## Task 1: GameManager 에 TargetFinder 게터 노출 (런타임 훅)

**Files:**
- Modify: `Assets/Scripts/Systems/Management/GameManager.cs`

- [ ] **Step 1: 게터 추가** — `private TargetFinder targetFinder;` 선언 바로 아래에 추가. (현재 `// 서비스 (합성 루트가 생성·주입)` 주석 영역, `private EnemyRegistry registry;` / `private TargetFinder targetFinder;` 다음)

```csharp
        // 서비스 (합성 루트가 생성·주입)
        private EnemyRegistry registry;
        private TargetFinder targetFinder;

        // DEBUG: 치트 도구 접근용 — 실제 타워 등장 시스템 구현 시 삭제
        /// <summary>적 타겟 탐색기입니다. Start 이후 non-null. (DEBUG)</summary>
        public TargetFinder TargetFinder => targetFinder;
```

> `TargetFinder` 타입은 `DefenseDot.Systems.Tower` 소속이며 `GameManager.cs`는 이미 `using DefenseDot.Systems.Tower;`를 가지므로 추가 using 불필요.

- [ ] **Step 2: 정적 확인** — `GameManager.cs`를 읽어 `targetFinder` 필드가 `GameManager.Start()`에서 `new TargetFinder(registry)`로 채워지는지(따라서 Play 이후 non-null) 재확인. 컴파일은 사용자 Unity에서.

---

## Task 2: TowerCheatWindow 에디터 윈도우

**Files:**
- Create: `Assets/Scripts/Editor/TowerCheatWindow.cs`
- Create: `Assets/Scripts/Editor/TowerCheatWindow.cs.meta`

- [ ] **Step 1: MapVisualizer 네임스페이스 확인** — `Assets/Scripts/Systems/Grid/MapVisualizer.cs`를 읽어 네임스페이스를 확인한다. 아래 코드는 `DefenseDot.Systems.Grid`로 가정하므로, 다르면 Step 2의 `using DefenseDot.Systems.Grid;`를 실제 네임스페이스로 교체한다.

- [ ] **Step 2: 윈도우 작성** — `Assets/Scripts/Editor/TowerCheatWindow.cs` 에 EXACTLY:

```csharp
// DEBUG: 플레이모드 타워 배치 치트 — 실제 타워 등장 시스템 구현 시 삭제
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using DefenseDot.Data;
using DefenseDot.Systems.Tower;
using DefenseDot.Systems.Management;
using DefenseDot.Systems.Grid;

namespace DefenseDot.EditorTools
{
    /// <summary> 플레이 중 타워 슬롯에 타워를 생성/삭제하는 치트 윈도우입니다. (DEBUG) </summary>
    public class TowerCheatWindow : EditorWindow
    {
        private MapData mapData;
        private Vector3 origin;
        private TowerData[] towerDatas = new TowerData[0];
        private string[] towerNames = new string[0];
        private int selectedTower;
        private float rangeOverride = 3f;
        private float dmgOverride = 5f;
        private float spdOverride = 1f;
        private readonly List<Vector2Int> slots = new List<Vector2Int>();
        private int selectedSlot = -1;
        private readonly Dictionary<Vector2Int, GameObject> placed = new Dictionary<Vector2Int, GameObject>();
        private readonly Dictionary<Vector2Int, TowerData> clones = new Dictionary<Vector2Int, TowerData>();

        [MenuItem("DefenseDot/Tower Cheat")]
        private static void Open() => GetWindow<TowerCheatWindow>("Tower Cheat");

        private void OnEnable()
        {
            EditorApplication.playModeStateChanged += OnPlayModeChanged;
            LoadTowerDatas();
            LoadDefaultMap();
        }

        private void OnDisable()
        {
            EditorApplication.playModeStateChanged -= OnPlayModeChanged;
        }

        private void OnPlayModeChanged(PlayModeStateChange state)
        {
            if (state == PlayModeStateChange.ExitingPlayMode)
            {
                placed.Clear();
                clones.Clear();
            }
            Repaint();
        }

        private void OnGUI()
        {
            if (!EditorApplication.isPlaying)
            {
                EditorGUILayout.HelpBox("플레이 중에만 사용할 수 있습니다.", MessageType.Info);
                return;
            }

            mapData = (MapData)EditorGUILayout.ObjectField("Map Data", mapData, typeof(MapData), false);

            EditorGUILayout.BeginHorizontal();
            origin = EditorGUILayout.Vector3Field("Origin", origin);
            if (GUILayout.Button("씬에서", GUILayout.Width(60)))
            {
                MapVisualizer vis = FindFirstObjectByType<MapVisualizer>();
                if (vis != null) origin = vis.transform.position;
                else ShowNotification(new GUIContent("씬에 MapVisualizer 없음"));
            }
            EditorGUILayout.EndHorizontal();

            if (towerDatas.Length == 0)
            {
                EditorGUILayout.HelpBox("TowerData 에셋이 없습니다.", MessageType.Warning);
                return;
            }
            int newSel = EditorGUILayout.Popup("Tower", selectedTower, towerNames);
            if (newSel != selectedTower) { selectedTower = newSel; PullStats(); }

            rangeOverride = EditorGUILayout.FloatField("Attack Range", rangeOverride);
            dmgOverride = EditorGUILayout.FloatField("Attack Damage", dmgOverride);
            spdOverride = EditorGUILayout.FloatField("Attack Speed", spdOverride);

            if (GUILayout.Button("슬롯 새로고침")) RebuildSlots();
            EditorGUILayout.LabelField($"Tower Slots ({slots.Count})");
            for (int i = 0; i < slots.Count; i++)
            {
                Vector2Int c = slots[i];
                bool occupied = placed.ContainsKey(c);
                bool sel = i == selectedSlot;
                string label = $"({c.x}, {c.y}){(occupied ? "  ●" : "")}";
                if (GUILayout.Toggle(sel, label, "Button") && !sel) selectedSlot = i;
            }

            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("생성")) PlaceTower();
            if (GUILayout.Button("삭제")) DeleteTower();
            EditorGUILayout.EndHorizontal();
        }

        private void LoadTowerDatas()
        {
            string[] guids = AssetDatabase.FindAssets("t:TowerData");
            towerDatas = new TowerData[guids.Length];
            towerNames = new string[guids.Length];
            for (int i = 0; i < guids.Length; i++)
            {
                towerDatas[i] = AssetDatabase.LoadAssetAtPath<TowerData>(AssetDatabase.GUIDToAssetPath(guids[i]));
                towerNames[i] = towerDatas[i] != null ? towerDatas[i].towerName : "(null)";
            }
            PullStats();
        }

        private void LoadDefaultMap()
        {
            if (mapData == null)
            {
                string[] guids = AssetDatabase.FindAssets("t:MapData");
                if (guids.Length > 0)
                    mapData = AssetDatabase.LoadAssetAtPath<MapData>(AssetDatabase.GUIDToAssetPath(guids[0]));
            }
            RebuildSlots();
        }

        private void PullStats()
        {
            if (selectedTower < 0 || selectedTower >= towerDatas.Length) return;
            TowerData t = towerDatas[selectedTower];
            if (t == null) return;
            rangeOverride = t.attackRange;
            dmgOverride = t.attackDamage;
            spdOverride = t.attackSpeed;
        }

        private void RebuildSlots()
        {
            slots.Clear();
            selectedSlot = -1;
            if (mapData == null) return;
            for (int y = 0; y < mapData.height; y++)
                for (int x = 0; x < mapData.width; x++)
                    if (mapData.GetCellType(x, y) == CellType.TowerSlot)
                        slots.Add(new Vector2Int(x, y));
        }

        private void PlaceTower()
        {
            if (selectedSlot < 0 || selectedSlot >= slots.Count) { ShowNotification(new GUIContent("슬롯을 선택하세요")); return; }
            Vector2Int cell = slots[selectedSlot];
            if (placed.ContainsKey(cell)) { ShowNotification(new GUIContent("이미 점유된 슬롯")); return; }

            GameManager gm = FindFirstObjectByType<GameManager>();
            TargetFinder finder = gm != null ? gm.TargetFinder : null;
            if (finder == null) { ShowNotification(new GUIContent("TargetFinder 없음 (Play 직후 1프레임 후 재시도)")); return; }

            TowerData src = towerDatas[selectedTower];
            if (src == null || src.prefab == null) { ShowNotification(new GUIContent("TowerData/prefab 없음")); return; }

            TowerData data = Instantiate(src);
            data.attackRange = rangeOverride;
            data.attackDamage = dmgOverride;
            data.attackSpeed = spdOverride;

            GameObject go = Instantiate(data.prefab);
            go.name = $"CheatTower_{cell.x}_{cell.y}";
            TowerActor tower = go.GetComponent<TowerActor>();
            if (tower == null) tower = go.AddComponent<TowerActor>();
            go.transform.position = origin + new Vector3(cell.x + 0.5f, 0.8f, cell.y + 0.5f);
            tower.Initialize(data);
            tower.SetTargetFinder(finder);

            placed[cell] = go;
            clones[cell] = data;
        }

        private void DeleteTower()
        {
            if (selectedSlot < 0 || selectedSlot >= slots.Count) return;
            Vector2Int cell = slots[selectedSlot];
            if (placed.TryGetValue(cell, out GameObject go))
            {
                if (go != null) Destroy(go);
                placed.Remove(cell);
            }
            if (clones.TryGetValue(cell, out TowerData so))
            {
                if (so != null) Destroy(so);
                clones.Remove(cell);
            }
        }
    }
}
```

- [ ] **Step 3: .meta 직접 생성** — `Assets/Scripts/Editor/TowerCheatWindow.cs.meta` 를 충돌 검사한 32-hex GUID로 작성:

```
fileFormatVersion: 2
guid: <생성한 32-hex>
MonoImporter:
  externalObjects: {}
  serializedVersion: 2
  defaultReferences: []
  executionOrder: 0
  icon: {instanceID: 0}
  userData:
  assetBundleName:
  assetBundleVariant:
```

- [ ] **Step 4: 정적 참조 검증** (읽기 전용, Unity 미실행) — 다음을 실제 코드로 확인:
  - `MapData`에 public `width`/`height`/`GetCellType(int,int)` 와 `CellType.TowerSlot` 존재 (`Assets/Scripts/Data/MapData.cs`)
  - `TowerData`에 public `towerName`/`attackRange`/`attackDamage`/`attackSpeed`/`prefab` (`Assets/Scripts/Data/TowerData.cs`)
  - `TowerActor`에 public `Initialize(TowerData)`/`SetTargetFinder(TargetFinder)` (`Assets/Scripts/Systems/Tower/TowerActor.cs`)
  - `GameManager.TargetFinder` 게터(Task 1) 존재
  - `DefenseDot.Editor.asmdef`가 `DefenseDot` 참조(이미 확인됨)
  - 불일치 시 멈추고 보고. 컴파일은 사용자 Unity에서.

---

## Task 3: PlayMode 수동 검증 (커밋 없음 · 사용자 Unity)

- [ ] **V1** 메뉴 `DefenseDot > Tower Cheat` 로 윈도우 열기 → 미플레이 시 "플레이 중에만" 안내 표시
- [ ] **V2** Play 진입 → MapData 자동 로드(TestMapData), 슬롯 9개 리스트, Tower 드롭다운(Tower Test) 표시
- [ ] **V3** Origin "씬에서"(GridScene) 또는 수동으로 적 공전 근처 설정 → 슬롯 선택 → Attack Range 크게(예: 10) → **생성** → 타워가 공전 적을 공격(cyan 라인/처치/골드)
- [ ] **V4** 생성된 타워 인스펙터에서 `debugAoe`/`debugProjectile` 토글 → 범위/투사체 공격 전환 관찰
- [ ] **V5** **삭제** → 타워·복제 SO 제거, 슬롯 점유 해제. Play 종료 → 추적 초기화, 재진입 정상

---

## Task 4: 일괄 커밋 (V 검증 후 · 사용자 명시 승인 하)

- [ ] **Step 1: lint** — `lint` 스킬을 **본 작업 파일만** 범위로(`GameManager.cs`, `TowerCheatWindow.cs`) 수행.
- [ ] **Step 2: scoped 커밋** (사용자 "커밋해줘" 후)

```bash
git add Assets/Scripts/Systems/Management/GameManager.cs Assets/Scripts/Systems/Management/GameManager.cs.meta \
        "Assets/Scripts/Editor/TowerCheatWindow.cs" "Assets/Scripts/Editor/TowerCheatWindow.cs.meta"
git commit -m "feat: 플레이모드 타워 배치 치트 에디터 윈도우 추가"
```

---

## Self-Review (작성자 점검)

- **스펙 커버리지**: D1 게터(Task1), D2 IMGUI+MenuItem+플레이게이트(Task2 OnGUI/Open), D3 MapData ObjectField+슬롯스캔(Task2 LoadDefaultMap/RebuildSlots), D4 origin+씬에서버튼(Task2), D5 공격타입은 타워토글(Task3 V4), D6 placed/clones 자체추적(Task2), D8 스탯오버라이드+SO복제(Task2 PlaceTower) — 전부 태스크 대응.
- **플레이스홀더 스캔**: 모든 코드 단계 완전 코드 포함. "TBD/적절히" 없음.
- **타입 일관성**: `placed`/`clones`(Vector2Int 키), `rangeOverride/dmgOverride/spdOverride`, `GameManager.TargetFinder`, `RebuildSlots/PullStats/PlaceTower/DeleteTower` 명칭이 선언부와 호출부 일치. `Instantiate`/`Destroy`/`FindFirstObjectByType`는 `EditorWindow`(→Object) 상속 정적 멤버라 무자격 호출 가능.
