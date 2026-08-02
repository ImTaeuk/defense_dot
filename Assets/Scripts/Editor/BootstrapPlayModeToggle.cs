// 어느 씬에서 Play 하든 Bootstrap 을 먼저 거치게 하는 토글
using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;

namespace DefenseDot.EditorTools
{
    /// <summary>
    /// Play 시작 시 BootstrapScene 을 먼저 거치도록 강제하는 토글입니다.
    /// 켜져 있으면 열린 씬 대신 Bootstrap 이 로드되므로, 특정 씬만 따로 확인할 때는 꺼야 합니다.
    /// </summary>
    [InitializeOnLoad]
    public static class BootstrapPlayModeToggle
    {
        private const string MENU_PATH = "Tools/Bootstrap 경유 Play";

        private const string PREF_KEY = "DefenseDot.BootstrapPlayMode";

        private const string SCENE_PATH = "Assets/Scenes/BootstrapScene.unity";

        /// <summary> 에디터가 열릴 때와 스크립트 재컴파일 때 저장된 설정을 반영합니다. </summary>
        static BootstrapPlayModeToggle()
        {
            Apply(EditorPrefs.GetBool(PREF_KEY, false));
        }

        /// <summary> 토글을 뒤집고 즉시 반영합니다. </summary>
        [MenuItem(MENU_PATH)]
        private static void Toggle()
        {
            bool next = !EditorPrefs.GetBool(PREF_KEY, false);
            EditorPrefs.SetBool(PREF_KEY, next);
            Apply(next);
        }

        /// <summary> 메뉴에 현재 켜짐 상태를 체크 표시로 보여줍니다. </summary>
        [MenuItem(MENU_PATH, true)]
        private static bool ToggleValidate()
        {
            Menu.SetChecked(MENU_PATH, EditorPrefs.GetBool(PREF_KEY, false));
            return true;
        }

        /// <summary> 켜짐이면 Bootstrap 을 시작 씬으로 걸고, 꺼짐이면 해제합니다. </summary>
        /// <param name="isEnabled">Bootstrap 경유 여부</param>
        private static void Apply(bool isEnabled)
        {
            if (!isEnabled)
            {
                EditorSceneManager.playModeStartScene = null;
                return;
            }

            SceneAsset scene = AssetDatabase.LoadAssetAtPath<SceneAsset>(SCENE_PATH);
            if (scene == null)
            {
                Debug.LogError($"[BootstrapPlayModeToggle] 씬을 찾을 수 없습니다: {SCENE_PATH}");
                EditorSceneManager.playModeStartScene = null;
                return;
            }

            EditorSceneManager.playModeStartScene = scene;
        }
    }
}