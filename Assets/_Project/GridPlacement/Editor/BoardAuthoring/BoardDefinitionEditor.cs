using TowerDefense3D.GridPlacement;
using UnityEditor;
using UnityEngine;

namespace TowerDefense3D.GridPlacement.Editor
{
    [CustomEditor(typeof(BoardDefinition))]
    [CanEditMultipleObjects]
    public sealed class BoardDefinitionEditor : UnityEditor.Editor
    {
        private bool showRawCells;

        public override void OnInspectorGUI()
        {
            serializedObject.Update();
            SerializedProperty dimensions = serializedObject.FindProperty("dimensions");
            SerializedProperty cells = serializedObject.FindProperty("cells");

            EditorGUILayout.LabelField("Board Authoring", EditorStyles.boldLabel);
            EditorGUILayout.PropertyField(serializedObject.FindProperty("visualizeInScene"));
            if (!serializedObject.isEditingMultipleObjects)
            {
                EditorGUILayout.LabelField(
                    "Dimensions",
                    $"{dimensions.FindPropertyRelative("width").intValue} × "
                    + $"{dimensions.FindPropertyRelative("depth").intValue} × "
                    + $"{dimensions.FindPropertyRelative("height").intValue}");
                EditorGUILayout.LabelField("Authored Cells", cells.arraySize.ToString());

                if (GUILayout.Button("Open Board Painter", GUILayout.Height(30f)))
                {
                    BoardPainterWindow.Open((BoardDefinition)target);
                }
            }
            else
            {
                EditorGUILayout.HelpBox(
                    "Board Painter edits one BoardDefinition at a time.",
                    MessageType.Info);
            }

            EditorGUILayout.Space(5f);
            showRawCells = EditorGUILayout.Foldout(
                showRawCells,
                "Advanced / Raw Data",
                true);
            if (showRawCells)
            {
                EditorGUILayout.PropertyField(dimensions, true);
                EditorGUILayout.PropertyField(serializedObject.FindProperty("cellSize"));
                EditorGUILayout.PropertyField(serializedObject.FindProperty("heightUnit"));
                EditorGUILayout.PropertyField(cells, true);
            }

            bool changed = serializedObject.ApplyModifiedProperties();
            if (changed)
            {
                for (int index = 0; index < targets.Length; index++)
                {
                    if (targets[index] is BoardDefinition board)
                    {
                        BoardChangeScheduler.Queue(board);
                    }
                }
            }
        }
    }
}
