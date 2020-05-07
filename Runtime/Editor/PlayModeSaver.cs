#if UNITY_EDITOR
using UnityEngine;
using UnityEditor;
using System;
using System.Linq;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine.SceneManagement;
using UnityEditor.SceneManagement;
using Object = UnityEngine.Object;

namespace PlayModeSaver
{
    /// <summary>
    /// Play mode saver.
    /// Allows saving and restoring of gameobject hierarchies.
    /// </summary>
    public static class PlayModeSaver
    {
        /// <summary>
        /// Serialize the specified gameObjects and all their children.
        /// </summary>
        /// <param name="gameObjects">Game objects.</param>
        public static SerializedSelection Serialize(IList<GameObject> gameObjects)
        {
            Serializer serializer = new Serializer(gameObjects);
            return serializer.Serialize();
        }

        // Checks if this data can be deserialized
        public static bool CanDeserialize(SerializedSelection serializedSelection)
            => !serializedSelection.foundStatic && serializedSelection.indexOfRootGOs
                .Select(index => serializedSelection.serializedGameObjects[index]).Select(
                    serializedGameObject => SceneManager.GetSceneByPath(serializedGameObject.scenePath))
                .Any(scene => scene.isLoaded);

        /// <summary>
        /// Deserialize the specified serializedSelection and optionally destroy the originals.
        /// </summary>
        /// <param name="serializedSelection">Serialized selection.</param>
        /// <param name="destroyOriginals">If set to <c>true</c> destroy originals.</param>
        /// <returns>Returns the root level restored GameObjects</returns>
        public static GameObject[] Deserialize(SerializedSelection serializedSelection, bool destroyOriginals)
        {
            Deserializer deserializer = new Deserializer(serializedSelection, destroyOriginals);
            var clonedGameObjects = deserializer.Deserialize();
            return clonedGameObjects;
        }


        private class Serializer
        {
            private readonly IList<GameObject> rawGameObjects;
            private SerializedSelection serializedSelection;
            private List<GameObject> rootGameObjectsToCopy;
            private List<GameObject> allGameObjectsToCopy;
            private List<Object> allComponentsInGameObjectsToCopyHierarchy;

            public Serializer(IList<GameObject> rawGameObjects)
                => this.rawGameObjects = rawGameObjects;

            public SerializedSelection Serialize()
            {
                rootGameObjectsToCopy = GetRootGameObjects(rawGameObjects);
                allGameObjectsToCopy = new List<GameObject>();
                rootGameObjectsToCopy.ForEach(x =>
                {
                    var tree = new List<GameObject>();
                    GetTree(x, ref tree);
                    allGameObjectsToCopy.AddRange(tree);
                });
                allComponentsInGameObjectsToCopyHierarchy = GetAllObjects(rootGameObjectsToCopy);

                serializedSelection = new SerializedSelection();
                Serialize(rootGameObjectsToCopy);
                return serializedSelection;
            }

            // Gets all selected gameobjects that aren't parented by another in the selected list
            private List<GameObject> GetRootGameObjects(ICollection<GameObject> gameObjects)
            {
                var rootGameObjects = new List<GameObject>();
                if (gameObjects.Count == 1)
                    rootGameObjects.Add(gameObjects.First());
                else
                    rootGameObjects.AddRange(gameObjects.Where(gameObject =>
                        gameObjects.Any(x => x != gameObject && !gameObject.transform.IsChildOf(x.transform))));

                return rootGameObjects;
            }

            private List<Object> GetAllObjects(List<GameObject> gameObjects)
            {
                var objects = new List<Object>();
                allGameObjectsToCopy.ForEach(x =>
                {
                    objects.Add(x.gameObject);
                    var components = x.GetComponents<Component>().ToList();
                    components.ForEach(y => { objects.Add(y); });
                });
                return objects;
            }

            private void Serialize(IEnumerable<GameObject> gameObjectsToSerialize)
            {
                foreach (GameObject gameObject in gameObjectsToSerialize)
                {
                    serializedSelection.indexOfRootGOs.Add(serializedSelection.serializedGameObjects.Count);
                    serializedSelection.idOfRootGOs.Add(gameObject.GetInstanceID());
                    SerializeGameObject(gameObject);
                }
            }

            private void SerializeGameObject(GameObject gameObject)
            {
                var parent = gameObject.transform.parent;
                SerializedGameObject sgo = new SerializedGameObject
                {
                    serializedData = EditorJsonUtility.ToJson(gameObject, false),
                    savedInstanceIDs = GetInstanceReferenceIDs(gameObject),
                    scenePath = gameObject.scene.path,
                    hasParent = parent != null
                };

                sgo.parentID = sgo.hasParent ? parent.GetInstanceID() : 0;
                sgo.siblingIndex = gameObject.transform.GetSiblingIndex();

                sgo.childCount = gameObject.transform.childCount;
                sgo.indexOfFirstChild = serializedSelection.serializedGameObjects.Count + 1;

                foreach (var component in gameObject.GetComponents<Component>())
                {
                    if (component == null) continue;
                    var serializedComponent = SerializeComponent(component);
                    sgo.serializedComponents.Add(serializedComponent);
                }

                serializedSelection.serializedGameObjects.Add(sgo);

                if (gameObject.isStatic)
                {
                    serializedSelection.foundStatic = true;
                    Debug.LogWarning("PlayModeSaver tried to serialize static GameObject " + gameObject +
                                     ". This is not allowed.");
                }

                foreach (Transform child in gameObject.transform)
                    SerializeGameObject(child.gameObject);
            }

            private SerializedComponent SerializeComponent(Object component)
            {
                SerializedComponent serializedComponent =
                    new SerializedComponent(component.GetType(), EditorJsonUtility.ToJson(component, false))
                    {
                        savedInstanceIDs = GetInstanceReferenceIDs(component)
                    };
                return serializedComponent;
            }

            private List<InstanceReference> GetInstanceReferenceIDs(Object obj)
            {
                var ids = new List<InstanceReference>();
                SerializedObject so = new SerializedObject(obj);
                var prop = so.GetIterator();
                while (prop.NextVisible(true))
                {
                    if (prop.propertyType == SerializedPropertyType.ObjectReference)
                    {
                        if (prop.objectReferenceValue == null)
                        {
                            ids.Add(new InstanceReference());
                        }
                        else if (allComponentsInGameObjectsToCopyHierarchy.Contains(prop.objectReferenceValue))
                        {
                            int index = allComponentsInGameObjectsToCopyHierarchy.IndexOf(prop.objectReferenceValue);
                            ids.Add(new InstanceReference(index, true));
                        }
                        else
                        {
                            ids.Add(new InstanceReference(prop.objectReferenceInstanceIDValue, false));
                        }
                    }
                }

                return ids;
            }

            private static void GetTree(GameObject go, ref List<GameObject> gameObjects)
            {
                gameObjects.Add(go);
                foreach (Transform child in go.transform)
                    GetTree(child.gameObject, ref gameObjects);
            }
        }

        private class Deserializer
        {
            private readonly SerializedSelection serializedSelection;
            private readonly bool destroyOriginals;

            private List<Object> deserializedObjects = new List<Object>();
            private List<DeserializedGameObject> deserializedGameObjects = new List<DeserializedGameObject>();
            private List<DeserializedComponent> deserializedComponents = new List<DeserializedComponent>();
            private Dictionary<string, Assembly> loadedAssemblies = new Dictionary<string, Assembly>();

            private class DeserializedGameObject
            {
                public readonly SerializedGameObject serializedGameObject;
                public readonly GameObject gameObject;

                public DeserializedGameObject(SerializedGameObject serializedGameObject, GameObject gameObject)
                {
                    this.serializedGameObject = serializedGameObject;
                    this.gameObject = gameObject;
                }
            }

            private class DeserializedComponent
            {
                public readonly SerializedComponent serializedComponent;
                public readonly Component component;

                public DeserializedComponent(SerializedComponent serializedComponent, Component component)
                {
                    this.serializedComponent = serializedComponent;
                    this.component = component;
                }
            }

            public Deserializer(SerializedSelection serializedSelection, bool destroyOriginals)
            {
                this.serializedSelection = serializedSelection;
                this.destroyOriginals = destroyOriginals;
            }

            private void Reset()
            {
                deserializedObjects = new List<Object>();
                deserializedGameObjects = new List<DeserializedGameObject>();
                deserializedComponents = new List<DeserializedComponent>();
                loadedAssemblies = new Dictionary<string, Assembly>();
            }

            public GameObject[] Deserialize()
            {
                Reset();

                int undoIndex = Undo.GetCurrentGroup();
                Undo.IncrementCurrentGroup();
                Undo.SetCurrentGroupName("Restore Play Mode Changes");

                // Do this first, since otherwise it can interfere with restoring the sibling indices.
                if (destroyOriginals)
                    DestroyOriginals();

                foreach (var index in from index in serializedSelection.indexOfRootGOs
                    let serializedGameObject = serializedSelection.serializedGameObjects[index]
                    let scene = SceneManager.GetSceneByPath(serializedGameObject.scenePath)
                    where scene.isLoaded
                    select index)
                    ReadNodeFromSerializedNodes(index, out _);

                RestoreInternalObjectReferences();

                var deserializedRootGameObjects = deserializedGameObjects
                    .Where(x =>
                        serializedSelection.indexOfRootGOs.Contains(x.serializedGameObject.indexOfFirstChild - 1))
                    .Select(x => x.gameObject).ToArray();

                // Enforces child index when redoing
                foreach (var g in deserializedRootGameObjects)
                    Undo.SetTransformParent(g.transform, g.transform.parent, "Creat");

                Undo.CollapseUndoOperations(undoIndex);
                return deserializedRootGameObjects;
            }

            private void DestroyOriginals()
            {
                foreach (var originalRootGO in serializedSelection.idOfRootGOs
                    .Select(EditorUtility.InstanceIDToObject).OfType<GameObject>())
                    Undo.DestroyObjectImmediate(originalRootGO);
            }

            private int ReadNodeFromSerializedNodes(int index, out GameObject go)
            {
                var serializedGameObject = serializedSelection.serializedGameObjects[index];
                var newGameObject = RestoreGameObject(serializedGameObject);

                Scene scene = SceneManager.GetSceneByPath(serializedGameObject.scenePath);
                if (!scene.isDirty) EditorSceneManager.MarkSceneDirty(scene);
                Undo.MoveGameObjectToScene(newGameObject, scene, "Move GameObject to scene");
                // The tree needs to be read in depth-first, since that's how we wrote it out.
                for (int i = 0; i != serializedGameObject.childCount; i++)
                {
                    index = ReadNodeFromSerializedNodes(++index, out var childGO);
                    childGO.transform.SetParent(newGameObject.transform, false);
                }

                go = newGameObject;
                return index;
            }

            private GameObject RestoreGameObject(SerializedGameObject serializedGameObject)
            {
                GameObject gameObject = new GameObject();
                Undo.RegisterCreatedObjectUndo(gameObject, "Create");

                deserializedObjects.Add(gameObject);
                deserializedGameObjects.Add(new DeserializedGameObject(serializedGameObject, gameObject));
                EditorJsonUtility.FromJsonOverwrite(serializedGameObject.serializedData, gameObject);
                RestoreObjectReference(serializedGameObject.savedInstanceIDs, gameObject);

                RestoreComponents(gameObject, serializedGameObject.serializedComponents);
                return gameObject;
            }

            private void RestoreComponents(GameObject go, IEnumerable<SerializedComponent> serializedComponents)
            {
                foreach (var serializedComponent in serializedComponents)
                    RestoreComponent(go, serializedComponent);
            }

            private void RestoreComponent(GameObject go, SerializedComponent serializedComponent)
            {
                if (!loadedAssemblies.ContainsKey(serializedComponent.assemblyName))
                    loadedAssemblies.Add(serializedComponent.assemblyName,
                        Assembly.Load(serializedComponent.assemblyName));
                Type type = loadedAssemblies[serializedComponent.assemblyName].GetType(serializedComponent.typeName);
                Debug.Assert(type != null,
                    "Type '" + serializedComponent.typeName + "' not found in assembly '" +
                    serializedComponent.assemblyName + "'");

                var component = type == typeof(Transform) ? go.transform : Undo.AddComponent(go, type);
                
                EditorJsonUtility.FromJsonOverwrite(serializedComponent.serializedData, component);
                RestoreObjectReference(serializedComponent.savedInstanceIDs, component);

                deserializedObjects.Add(component);
                deserializedComponents.Add(new DeserializedComponent(serializedComponent, component));
            }

            private void RestoreObjectReference(List<InstanceReference> savedInstanceIDs, UnityEngine.Object obj)
            {
                SerializedObject so = new SerializedObject(obj);
                var prop = so.GetIterator();
                int i = 0;
                while (prop.NextVisible(true))
                {
                    if (prop.propertyType == SerializedPropertyType.ObjectReference)
                    {
                        if (!savedInstanceIDs[i].isNull && !savedInstanceIDs[i].isInternal)
                        {
                            var refObj = EditorUtility.InstanceIDToObject(savedInstanceIDs[i].id);
                            if (refObj == null)
                                Debug.LogWarning("Object reference with saved id " + savedInstanceIDs[i] + " on " +
                                                 obj + " could not be found. This is likely a bug.");
                            prop.objectReferenceValue = refObj;
                        }
                        i++;
                    }
                }
                so.ApplyModifiedProperties();
            }

            // Some things can't be restored until all the gameobjects and components have been created. Do them now.
            private void RestoreInternalObjectReferences()
            {
                foreach (var deserializedGameObject in deserializedGameObjects)
                {
                    // The root gameobjects need their parents restored
                    if (deserializedGameObject.gameObject.transform.parent == null &&
                        deserializedGameObject.serializedGameObject.hasParent)
                    {
                        Object o = EditorUtility.InstanceIDToObject(deserializedGameObject.serializedGameObject.parentID);
                        if (o == null || (Transform) o == null) return;
                        // Note that this ought to use Undo.SetTransformParent, but you can't currently set worldPositionStays using it.
                        deserializedGameObject.gameObject.transform.SetParent((Transform) o, false);
                    }
                    deserializedGameObject.gameObject.transform.SetSiblingIndex(deserializedGameObject
                        .serializedGameObject.siblingIndex);
                }


                foreach (var deserializedComponent in deserializedComponents)
                {
                    SerializedObject so = new SerializedObject(deserializedComponent.component);
                    var prop = so.GetIterator();
                    int i = 0;
                    while (prop.NextVisible(true))
                    {
                        if (prop.propertyType == SerializedPropertyType.ObjectReference)
                        {
                            if (!deserializedComponent.serializedComponent.savedInstanceIDs[i].isNull &&
                                deserializedComponent.serializedComponent.savedInstanceIDs[i].isInternal)
                            {
                                prop.objectReferenceValue =
                                    deserializedObjects[
                                        deserializedComponent.serializedComponent.savedInstanceIDs[i].id];
                            }
                            i++;
                        }
                    }
                    so.ApplyModifiedProperties();
                }
            }
        }

        [Serializable]
        public class SerializedSelection
        {
            public List<int> indexOfRootGOs = new List<int>();
            public List<int> idOfRootGOs = new List<int>();
            public List<SerializedGameObject> serializedGameObjects = new List<SerializedGameObject>();
            public bool foundStatic;
        }

        [Serializable]
        public class SerializedGameObject
        {
            [TextArea]
            public string serializedData;

            public List<InstanceReference> savedInstanceIDs = new List<InstanceReference>();

            public string scenePath;

            public bool hasParent;
            public int parentID;
            public int siblingIndex;

            public int childCount;
            public int indexOfFirstChild;

            public List<SerializedComponent> serializedComponents = new List<SerializedComponent>();
        }


        [Serializable]
        public class SerializedComponent
        {
            public string assemblyName;
            public string typeName;

            [TextArea]
            public string serializedData;

            public List<InstanceReference> savedInstanceIDs = new List<InstanceReference>();

            public SerializedComponent(Type type, string serializedData)
            {
                assemblyName = type.Assembly.GetName().Name;
                typeName = type.FullName;
                this.serializedData = serializedData;
            }
        }

        // Serializes the instance IDs of any object reference fields. If internal, the index of the object in the serializer list is stored instead.
        [Serializable]
        public class InstanceReference
        {
            public bool isNull;
            public int id;
            public bool isInternal;

            public InstanceReference() => isNull = true;

            public InstanceReference(int id, bool isInternal)
            {
                this.id = id;
                this.isInternal = isInternal;
            }
        }
    }
}
#endif
