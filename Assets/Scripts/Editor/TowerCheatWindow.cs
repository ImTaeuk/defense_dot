// DEBUG: 플레이모드 타워 배치 치트 — 실제 타워 등장 시스템 구현 시 삭제
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using DefenseDot.Data;
using DefenseDot.Systems.Tower;
using DefenseDot.Systems.Management;
using DefenseDot.Systems.Grid;
using DefenseDot.Systems.Mode;
using DefenseDot.Systems.Cards;
using DefenseDot.Systems.Abilities;

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
        private int goldAmount = 1000;
        private float rangeOverride = 3f;
        private float dmgOverride = 5f;
        private float spdOverride = 1f;
        private float aoeRadiusOverride = 3f;
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

            // DEBUG: 게임 속도(배속) — 테스트용
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField($"게임 속도  x{Time.timeScale:0.##}", GUILayout.Width(150));
            if (GUILayout.Button("0.5x")) Time.timeScale = 0.5f;
            if (GUILayout.Button("1x")) Time.timeScale = 1f;
            if (GUILayout.Button("2x")) Time.timeScale = 2f;
            if (GUILayout.Button("4x")) Time.timeScale = 4f;
            if (GUILayout.Button("8x")) Time.timeScale = 8f;
            EditorGUILayout.EndHorizontal();
            EditorGUILayout.Space();

            // DEBUG: 타워 공격속도 — 애니·발사 반영 확인용
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("공격속도(회/s)", GUILayout.Width(150));
            if (GUILayout.Button("1")) SetAttackSpeed(1f);
            if (GUILayout.Button("2")) SetAttackSpeed(2f);
            if (GUILayout.Button("3")) SetAttackSpeed(3f);
            if (GUILayout.Button("5")) SetAttackSpeed(5f);
            EditorGUILayout.EndHorizontal();
            EditorGUILayout.Space();

            // DEBUG: 합성 검증 치트 — 골드·재료MAX·카드팝업
            EditorGUILayout.LabelField("── 합성 검증 ──", EditorStyles.boldLabel);
            EditorGUILayout.BeginHorizontal();
            goldAmount = EditorGUILayout.IntField("골드", goldAmount);
            if (GUILayout.Button("골드 지급", GUILayout.Width(90)))
                GiveGold();
            EditorGUILayout.EndHorizontal();
            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("합성 재료 MAX"))
                MaxFusionMaterials();
            if (GUILayout.Button("레벨업 카드 팝업"))
                ForceLevelUpCard();
            EditorGUILayout.EndHorizontal();
            EditorGUILayout.Space();

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
            aoeRadiusOverride = EditorGUILayout.FloatField("AoE Radius", aoeRadiusOverride);

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
            aoeRadiusOverride = t.aoeRadius;
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
            data.aoeRadius = aoeRadiusOverride;

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

        /// <summary> 현재 골드에 설정액을 지급합니다. </summary>
        private void GiveGold()
        {
            GameManager gm = FindFirstObjectByType<GameManager>();
            if (gm == null || gm.Economy == null)
            {
                ShowNotification(new GUIContent("GameManager/Economy 없음"));
                return;
            }
            gm.Economy.AddGold(goldAmount);
        }

        /// <summary> 대기 레벨업이 생길 때까지 처치를 집계해 레벨업 카드 팝업을 강제로 띄웁니다. </summary>
        private void ForceLevelUpCard()
        {
            GameManager gm = FindFirstObjectByType<GameManager>();
            if (gm == null || gm.Level == null)
            {
                ShowNotification(new GUIContent("GameManager/Level 없음"));
                return;
            }
            int before = gm.Level.PendingLevelUps;
            int guard = 0;
            while (gm.Level.PendingLevelUps == before && guard++ < 100000) gm.Level.RegisterKill();
        }

        /// <summary> 타워 기본 공격속도(초당 횟수)를 즉시 설정합니다(애니·발사 반영 확인용). </summary>
        /// <param name="attacksPerSecond">초당 공격 횟수</param>
        private void SetAttackSpeed(float attacksPerSecond)
        {
            ArenaModeSystem boot = FindFirstObjectByType<ArenaModeSystem>();
            if (boot == null || boot.TowerAbility == null)
            {
                ShowNotification(new GUIContent("TowerAbility 없음"));
                return;
            }

            boot.TowerAbility.SetBaseAttackSpeed(attacksPerSecond);
            ShowNotification(new GUIContent($"공격속도 {attacksPerSecond:0.##}회/s"));
        }

        /// <summary> 계보 첫 레시피의 재료 2개를 코어에 확보하고 MAX 레벨로 올립니다. </summary>
        private void MaxFusionMaterials()
        {
            ArenaModeSystem boot = FindFirstObjectByType<ArenaModeSystem>();
            if (boot == null)
            {
                ShowNotification(new GUIContent("ArenaModeSystem 없음"));
                return;
            }
            FusionRecipeSet lineage = boot.CharacterLineage;
            if (lineage == null || lineage.recipes.Count == 0)
            {
                ShowNotification(new GUIContent("계보/레시피 없음"));
                return;
            }
            IAbilityCommandTarget core = boot.TowerAbility;
            if (core == null)
            {
                ShowNotification(new GUIContent("TowerAbility 없음"));
                return;
            }

            FusionRecipe recipe = lineage.recipes[0];
            MaxAbility(core, recipe.materialA);
            MaxAbility(core, recipe.materialB);
        }

        /// <summary> 코어 로드아웃에서 능력을 찾아(없으면 추가) MAX 레벨로 올립니다. </summary>
        private void MaxAbility(IAbilityCommandTarget core, AbilityData data)
        {
            if (data == null)
                return;
            AbilityInstance inst = Find(core.Loadout, data);
            if (inst == null)
                inst = core.AddAbility(data);
            if (inst == null)
                return;
            while (inst.level < data.maxLevel)
                core.LevelUpAbility(inst);
        }

        /// <summary> 로드아웃의 액티브·패시브에서 설계도 일치 인스턴스를 찾습니다. </summary>
        private static AbilityInstance Find(AbilityLoadout loadout, AbilityData data)
        {
            foreach (AbilityInstance i in loadout.Actives)
                if (i.data == data)
                    return i;
            foreach (AbilityInstance i in loadout.Passives)
                if (i.data == data)
                    return i;
            return null;
        }
    }
}
