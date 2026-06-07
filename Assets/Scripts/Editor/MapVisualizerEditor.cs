using UnityEngine;
using UnityEditor;
using DefenseDot.Systems.Grid;

namespace DefenseDot.Editor
{
    [CustomEditor(typeof(MapVisualizer))]
    public class MapVisualizerEditor : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            DrawDefaultInspector();

            MapVisualizer visualizer = (MapVisualizer)target;

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Map Controls", EditorStyles.boldLabel);

            if (GUILayout.Button("Generate 3D Map"))
            {
                visualizer.GenerateMap();
            }

            if (GUILayout.Button("Clear Map Tiles"))
            {
                visualizer.ClearExistingMap();
            }
        }
    }
}
