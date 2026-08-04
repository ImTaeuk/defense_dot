// ModeVisualBinder 인스펙터에 카메라 각도 프리뷰 버튼을 붙인다
using UnityEngine;
using UnityEditor;
using DefenseDot.Systems.Mode;
using DefenseDot.Systems.Visual.Camera;

namespace DefenseDot.EditorTools
{
    /// <summary>
    /// ModeVisualBinder 인스펙터에 카메라 각도 미리보기 버튼을 추가합니다.
    /// 씬을 편집할 때 그 씬이 열려 있으므로, Config 소유 주체가 프리뷰의 주체가 됩니다.
    /// </summary>
    [CustomEditor(typeof(ModeVisualBinder), true)]
    public class ModeVisualBinderEditor : UnityEditor.Editor
    {
        private const string PREVIEW_BUTTON_LABEL = "카메라 각도 미리보기";

        /// <summary> 기본 인스펙터 아래에 프리뷰 버튼을 그립니다. </summary>
        public override void OnInspectorGUI()
        {
            DrawDefaultInspector();

            EditorGUILayout.Space();
            if (GUILayout.Button(PREVIEW_BUTTON_LABEL))
            {
                MoveSceneViewToCameraPose();
            }
        }

        /// <summary> cameraConfig와 previewCenter로 포즈를 계산해 씬뷰를 옮깁니다. </summary>
        private void MoveSceneViewToCameraPose()
        {
            SerializedProperty configProperty = serializedObject.FindProperty("cameraConfig");
            var config = configProperty != null ? configProperty.objectReferenceValue as CameraRigConfig : null;
            if (config == null)
            {
                Debug.LogError("[ModeVisualBinderEditor] cameraConfig가 할당되지 않았습니다.", target);
                return;
            }

            SceneView view = SceneView.lastActiveSceneView;
            if (view == null)
            {
                Debug.LogError("[ModeVisualBinderEditor] 활성 씬 뷰가 없습니다.", target);
                return;
            }

            SerializedProperty centerProperty = serializedObject.FindProperty("previewCenter");
            var centerTransform = centerProperty != null ? centerProperty.objectReferenceValue as Transform : null;
            Vector3 center = centerTransform != null ? centerTransform.position : Vector3.zero;

            CameraPose pose = CameraRigMath.Solve(
                center, config.pitch, config.yaw, config.distance, config.heightOffset);

            view.LookAt(center + Vector3.up * config.heightOffset, pose.Rotation, config.distance);
            view.Repaint();
        }
    }
}
