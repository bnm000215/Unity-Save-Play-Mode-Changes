#if UNITY_EDITOR
using UnityEditor;

namespace PlayModeSaver
{
    [CustomEditor(typeof(SavePlayModeObject))]
    public class SavePlayModeChangesEditor : Editor
    {
        public override void OnInspectorGUI()
        {
            if (target == null || (SavePlayModeObject) target == null) return;
            SavePlayModeObject data = (SavePlayModeObject) target;
            if (data.AnyDescendentIsStatic())
            {
                EditorGUILayout.HelpBox(
                    "A descendent is static.\nCannot properly save or restore statics. This component will be ignored.",
                    MessageType.Warning);
            }
            else if (data.AnyAncestorHasThisComponent())
            {
                EditorGUILayout.HelpBox(
                    "An ancestor has an active SavePlayModeObject component.\nThis one will be ignored since the gameobject will be saved by the ancestor.",
                    MessageType.Warning);
            }
            else
            {
                if (data.enabled)
                {
                    EditorGUILayout.HelpBox("Saves all changes made during play mode.", MessageType.Info);
                }
                else
                {
                    EditorGUILayout.HelpBox("Enable to save play mode changes.", MessageType.Warning);
                }
            }
        }
    }
}
#endif
