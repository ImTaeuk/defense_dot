using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using DefenseDot.UI.Views;
using DefenseDot.UI.InGame;
using DefenseDot.Systems.Management;

namespace DefenseDot.EditorTools
{
    /// <summary>
    /// 통합 HUD 의 프리팹 결선과 활성 씬 구성을 자동화하는 에디터 도구입니다.
    /// </summary>
    public static class HudSetupTool
    {
        private const string PrefabPath = "Assets/UI/Prefabs/Panel_Grid.prefab";

        /// <summary>
        /// Panel_Grid 프리팹에 하위 View 4종을 부착·결선하고 EnemyBar_Fill 을 Filled 로 보정합니다.
        /// </summary>
        [MenuItem("Tools/HUD/1. Panel_Grid 프리팹 결선")]
        public static void WirePanelGridPrefab()
        {
            GameObject root = PrefabUtility.LoadPrefabContents(PrefabPath);
            try
            {
                HUDView hud = GetOrAdd<HUDView>(root);

                GoldView gold = WireValueNode<GoldView>(root, "Grid_Gold", "goldText");
                HealthView health = WireGaugeNode<HealthView>(root, "Grid_Health", "healthText", "HealthBar_Fill", "fillBar");
                RoundView round = WireValueNode<RoundView>(root, "Grid_Round", "roundText");
                EnemyCountView enemy = WireGaugeNode<EnemyCountView>(root, "Grid_EnemyCount", "countText", "EnemyBar_Fill", "fillBar");

                SetRef(hud, "goldView", gold);
                SetRef(hud, "healthView", health);
                SetRef(hud, "roundView", round);
                SetRef(hud, "enemyCountView", enemy);

                FixFill(root, "EnemyBar_Fill");

                PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
                Debug.Log("[HudSetupTool] Panel_Grid 프리팹 결선 완료");
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }

        /// <summary>
        /// 활성 씬에 통합 HUD(Canvas + Panel_Grid + UIRoot)를 구성하고 GameManager 에 결선합니다.
        /// </summary>
        [MenuItem("Tools/HUD/2. 활성 씬에 HUD 구성")]
        public static void SetupHudInActiveScene()
        {
            Scene scene = SceneManager.GetActiveScene();

            // 구 미싱 스크립트 제거
            int removed = 0;
            foreach (GameObject go in scene.GetRootGameObjects())
                removed += RemoveMissingRecursively(go);
            if (removed > 0) Debug.Log($"[HudSetupTool] 미싱 스크립트 {removed}개 제거");

            // 구성됐으면 결선만 보정
            UIRoot uiRoot = Object.FindFirstObjectByType<UIRoot>();
            if (uiRoot == null)
            {
                Canvas canvas = Object.FindFirstObjectByType<Canvas>();
                if (canvas == null)
                {
                    GameObject cgo = new GameObject("Canvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
                    canvas = cgo.GetComponent<Canvas>();
                    canvas.renderMode = RenderMode.ScreenSpaceOverlay;
                    Debug.Log("[HudSetupTool] Canvas 생성");
                }

                foreach (HUDView ex in Object.FindObjectsByType<HUDView>(FindObjectsSortMode.None))
                    Debug.LogWarning($"[HudSetupTool] 기존 HUDView 발견 — 중복 시 삭제 필요: {ex.name}", ex);

                GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
                GameObject panel = (GameObject)PrefabUtility.InstantiatePrefab(prefab, canvas.transform);
                panel.name = "Panel_Grid";
                HUDView hud = panel.GetComponent<HUDView>();

                GameObject rootGo = new GameObject("UIRoot");
                rootGo.transform.SetParent(canvas.transform, false);
                uiRoot = rootGo.AddComponent<UIRoot>();
                SetRef(uiRoot, "hudView", hud);
                Debug.Log("[HudSetupTool] Canvas/Panel_Grid/UIRoot 구성 완료");
            }
            else
            {
                Debug.Log("[HudSetupTool] UIRoot 이미 존재 — GameManager 결선만 보정");
            }

            GameManager gm = Object.FindFirstObjectByType<GameManager>();
            if (gm != null) SetRef(gm, "uiRoot", uiRoot);
            else Debug.LogWarning("[HudSetupTool] GameManager 미발견 — uiRoot 수동 결선 필요");

            // 구 UI Toolkit 오브젝트 경고
            WarnObsolete(scene, "Wave Hud");
            WarnObsolete(scene, "WaveHUD");

            EditorSceneManager.MarkSceneDirty(scene);
            Debug.Log("[HudSetupTool] 활성 씬 HUD 구성 완료 — 경고된 구 오브젝트 삭제 후 씬을 저장하세요");
        }

        private static T GetOrAdd<T>(GameObject go) where T : Component
        {
            T c = go.GetComponent<T>();
            if (c == null) c = go.AddComponent<T>();
            return c;
        }

        private static TView WireValueNode<TView>(GameObject root, string nodeName, string textProp) where TView : Component
        {
            Transform node = FindDeep(root.transform, nodeName);
            if (node == null) { Debug.LogError($"[HudSetupTool] 노드 없음: {nodeName}"); return null; }
            TView view = GetOrAdd<TView>(node.gameObject);
            SetRef(view, textProp, node.GetComponentInChildren<TextMeshProUGUI>(true));
            return view;
        }

        private static TView WireGaugeNode<TView>(GameObject root, string nodeName, string textProp, string fillName, string fillProp) where TView : Component
        {
            Transform node = FindDeep(root.transform, nodeName);
            if (node == null) { Debug.LogError($"[HudSetupTool] 노드 없음: {nodeName}"); return null; }
            TView view = GetOrAdd<TView>(node.gameObject);
            SetRef(view, textProp, node.GetComponentInChildren<TextMeshProUGUI>(true));
            Transform fill = FindDeep(node, fillName);
            SetRef(view, fillProp, fill != null ? fill.GetComponent<Image>() : null);
            return view;
        }

        private static void FixFill(GameObject root, string fillName)
        {
            Transform fill = FindDeep(root.transform, fillName);
            Image img = fill != null ? fill.GetComponent<Image>() : null;
            if (img == null) { Debug.LogWarning($"[HudSetupTool] {fillName} Image 없음"); return; }
            img.type = Image.Type.Filled;
            img.fillMethod = Image.FillMethod.Horizontal;
        }

        private static void SetRef(Component target, string propName, Object value)
        {
            if (target == null) return;
            SerializedObject so = new SerializedObject(target);
            SerializedProperty p = so.FindProperty(propName);
            if (p == null) { Debug.LogError($"[HudSetupTool] 프로퍼티 없음: {target.GetType().Name}.{propName}"); return; }
            p.objectReferenceValue = value;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        private static int RemoveMissingRecursively(GameObject go)
        {
            int count = GameObjectUtility.RemoveMonoBehavioursWithMissingScript(go);
            foreach (Transform child in go.transform)
                count += RemoveMissingRecursively(child.gameObject);
            return count;
        }

        private static void WarnObsolete(Scene scene, string nodeName)
        {
            foreach (GameObject go in scene.GetRootGameObjects())
            {
                Transform found = FindDeep(go.transform, nodeName);
                if (found != null) Debug.LogWarning($"[HudSetupTool] 구 오브젝트 '{nodeName}' 발견 — 삭제 필요", found);
            }
        }

        private static Transform FindDeep(Transform parent, string name)
        {
            if (parent.name == name) return parent;
            for (int i = 0; i < parent.childCount; i++)
            {
                Transform found = FindDeep(parent.GetChild(i), name);
                if (found != null) return found;
            }
            return null;
        }
    }
}
