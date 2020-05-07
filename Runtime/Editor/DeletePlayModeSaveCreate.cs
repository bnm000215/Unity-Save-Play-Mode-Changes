#if UNITY_EDITOR
using PlayModeSaver;
using UnityEditor;
using UnityEngine;

public class DeletePlayModeSaveCreate : Editor
{
    [MenuItem("PlayMode/Remove Scene Component to Save Play Mode Object",false,20)]
    public static void CreateCustomGameObject(MenuCommand menuCommand) => 
        RemoveComponents();

    public static void RemoveComponents ()
    {
        var components = FindObjectsOfType(typeof(SavePlayModeObject));

        foreach (var c in components) 
            DestroyImmediate(c);

        Debug.Log("Success");
    }
}
#endif
