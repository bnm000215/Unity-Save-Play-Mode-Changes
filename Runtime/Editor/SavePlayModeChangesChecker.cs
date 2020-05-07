#if UNITY_EDITOR
using UnityEngine;
using UnityEditor;
using System.Linq;
using System.IO;
using UnityEditor.SceneManagement;
using UnityEngine.SceneManagement;

namespace PlayModeSaver
{
    [InitializeOnLoad]
    public static class SavePlayModeChangesChecker
    {
        private const string ppKey = "PersistSerializationClipboard";

        public delegate void OnRestorePlayModeChangesDelegate(GameObject[] restoredRootGameObjects);

        public static event OnRestorePlayModeChangesDelegate OnRestorePlayModeChanges;

        static SavePlayModeChangesChecker() => 
            EditorApplication.playmodeStateChanged += OnChangePlayModeState;

        private static void OnChangePlayModeState()
        {
            if (!EditorApplication.isPlaying && EditorApplication.isPlayingOrWillChangePlaymode)
            {
                if (AnySceneDirty())
                    EditorSceneManager.SaveOpenScenes();

                for (int i = 0; i < SceneManager.sceneCount; i++)
                {
                    var scene = SceneManager.GetSceneAt(i);
                    string absolutePath = UnityRelativeToAbsolutePath(scene.path);

                    string writePath = System.Environment.GetFolderPath(System.Environment.SpecialFolder.Desktop);
                    writePath = Path.Combine(writePath, "Scene Backups");
                    
                    if (!Directory.Exists(writePath))
                        Directory.CreateDirectory(writePath);
                    
                    var di = new DirectoryInfo(writePath);
                    
                    foreach (FileInfo file in di.GetFiles()) 
                        file.Delete();

                    writePath = Path.Combine(writePath, scene.name + ".unity");
                    File.Copy(absolutePath, writePath, true);
                }
            }
            
            if (EditorApplication.isPlaying && !EditorApplication.isPlayingOrWillChangePlaymode)
                Save();
            else if (!EditorApplication.isPlaying && !EditorApplication.isPlayingOrWillChangePlaymode) 
                Load();
        }

        private static void Save()
        {
            var gameObjectsToPersist = GetGameObjectsToPersist();
            var serializedData = PlayModeSaver.Serialize(gameObjectsToPersist);
            string stringSerializedSelection = EditorJsonUtility.ToJson(serializedData);
            EditorPrefs.SetString(ppKey, stringSerializedSelection);
        }

        private static GameObject[] GetGameObjectsToPersist()
        {
            var persist = Object.FindObjectsOfType<SavePlayModeObject>();
            return persist.Where(x => x.IsValid()).Select(x => x.gameObject).ToArray();
        }

        private static void Load()
        {
            if (!EditorPrefs.HasKey(ppKey)) return;

            string serializedDataString = EditorPrefs.GetString(ppKey);
            EditorPrefs.DeleteKey(ppKey);

            if (!CheckForChanges(serializedDataString)) return;

            var serializedData = new PlayModeSaver.SerializedSelection();
            EditorJsonUtility.FromJsonOverwrite(serializedDataString, serializedData);

            if (!PlayModeSaver.CanDeserialize(serializedData))
            {
                if (serializedData.foundStatic)
                {
                    Debug.LogError(
                        "SavePlayModeChangesChecker SerializedSelection data contains a gameObject with the static flag. The static flag combines meshes on mesh filters, and so cannot properly restore them.\nIf you would like to rescue the data, it has been stored in EditorPrefs at key '" +
                        serializedDataString + "'.");
                    
                    PlayerPrefs.SetString(ppKey, serializedDataString);
                }
                return;
            }

            EditorUtility.DisplayProgressBar("Save Edit Mode Changes", "Restoring Edit Mode GameObjects...", 0);

            try
            {
                var restoredGameObjects = PlayModeSaver.Deserialize(serializedData, true);
                LogRestoredData(restoredGameObjects);
                EditorUtility.ClearProgressBar();
                OnRestorePlayModeChanges?.Invoke(restoredGameObjects);
            }
            catch
            {
                Debug.LogError(
                    "Play mode saver failed to restore data after destroying originals. Scene backups were placed on your desktop which will allow you to recover data.");
                EditorUtility.ClearProgressBar();
            }
        }

        private static void LogRestoredData(GameObject[] restoredGameObjects)
        {
            System.Text.StringBuilder sb = new System.Text.StringBuilder(
                "Save Play Mode Changes restored " + restoredGameObjects.Length + " GameObject hierarchies:");
            foreach (var restoredGameObject in restoredGameObjects)
            {
                sb.Append("\n");
                sb.Append(restoredGameObject.name);
            }

            Debug.Log(sb.ToString());
        }

        /// <summary>
        /// Checks for changes between the saved play mode data and the edit mode data.
        /// </summary>
        /// <returns><c>true</c>, if for changes was checked, <c>false</c> otherwise.</returns>
        /// <param name="serializedDataString"></param>
        private static bool CheckForChanges(string serializedDataString)
        {
            var gameObjectsToPersist = GetGameObjectsToPersist();
            var editModeSerializedData = PlayModeSaver.Serialize(gameObjectsToPersist);

            return serializedDataString != EditorJsonUtility.ToJson(editModeSerializedData);
        }

        private static bool AnySceneDirty()
        {
            for (int i = 0; i < EditorSceneManager.loadedSceneCount; i++)
                if (SceneManager.GetSceneAt(i).isDirty)
                    return true;

            return false;
        }

        private static string UnityRelativeToAbsolutePath(string filePath) => 
            Path.Combine(Application.dataPath, filePath.Substring(7));
    }
}
#endif
