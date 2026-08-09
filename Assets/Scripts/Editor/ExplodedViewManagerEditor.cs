using UnityEditor;
using UnityEngine;

namespace F1AR.Editor
{
    [CustomEditor(typeof(ExplodedView.ExplodedViewManager))]
    public class ExplodedViewManagerEditor : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            ExplodedView.ExplodedViewManager manager = (ExplodedView.ExplodedViewManager)target;

            EditorGUILayout.Space(10);
            EditorGUILayout.HelpBox("Use the slider below to live-preview the CAD Explosion in the Scene View!", MessageType.Info);

            serializedObject.Update();

            SerializedProperty progressProp = serializedObject.FindProperty("explosionProgress");
            
            EditorGUI.BeginChangeCheck();
            EditorGUILayout.PropertyField(progressProp, new GUIContent("Explosion Progress Bar"));
            if (EditorGUI.EndChangeCheck())
            {
                serializedObject.ApplyModifiedProperties();
                manager.SetExplosionProgress(progressProp.floatValue);
                EditorUtility.SetDirty(manager);
            }

            EditorGUILayout.Space(10);

            // Action Buttons
            EditorGUILayout.BeginHorizontal();
            
            if (GUILayout.Button("💥 Explode", GUILayout.Height(30)))
            {
                manager.Explode();
            }

            if (GUILayout.Button("🧩 Assemble", GUILayout.Height(30)))
            {
                manager.Assemble();
            }

            if (GUILayout.Button(manager.IsSpinning ? "⏸ Stop Spin" : "🔄 Start Spin", GUILayout.Height(30)))
            {
                manager.ToggleSpin();
            }

            EditorGUILayout.EndHorizontal();

            EditorGUILayout.Space(5);

            if (GUILayout.Button("🔄 Re-Cache Model Components", GUILayout.Height(25)))
            {
                manager.CacheParts();
            }

            EditorGUILayout.Space(10);

            DrawDefaultInspector();
        }
    }
}
