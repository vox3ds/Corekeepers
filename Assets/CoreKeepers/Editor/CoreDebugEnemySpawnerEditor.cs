#if UNITY_EDITOR
using System;
using System.Linq;
using CoreKeepers;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

[CustomEditor(typeof(CoreDebugEnemySpawner))]
public sealed class CoreDebugEnemySpawnerEditor : Editor
{
    public override void OnInspectorGUI()
    {
        serializedObject.Update();
        var prefabs = Resources.LoadAll<GameObject>("Enemies")
            .OrderBy(prefab => prefab.name, StringComparer.Ordinal)
            .ToArray();
        var indexProperty = serializedObject.FindProperty("enemyIndex");
        if (prefabs.Length == 0)
            EditorGUILayout.HelpBox("No enemy prefabs found in Resources/Enemies.", MessageType.Warning);
        else
        {
            var names = prefabs.Select(prefab => prefab.name).ToArray();
            indexProperty.intValue = EditorGUILayout.Popup("Enemy Type", 
                Mathf.Clamp(indexProperty.intValue, 0, names.Length - 1), names);
        }

        EditorGUILayout.Space();
        DrawPropertiesExcluding(serializedObject, "m_Script", "enemyIndex");
        EditorGUILayout.Space();
        EditorGUILayout.HelpBox("V: spawn enemy    =: next prefab    -: previous prefab", MessageType.Info);
        serializedObject.ApplyModifiedProperties();

        using (new EditorGUI.DisabledScope(!Application.isPlaying))
            if (GUILayout.Button("Spawn Selected Enemy"))
                ((CoreDebugEnemySpawner)target).SpawnSelected();
    }
}

public static class CoreDebugEnemySpawnerSceneSetup
{
    private const string DebugScenePath = "Assets/Scenes/DebugScene.unity";

    [MenuItem("Core Keepers/Configure Debug Enemy Spawner")]
    public static void ConfigureSpawner()
    {
        var scene = SceneManager.GetActiveScene().path == DebugScenePath
            ? SceneManager.GetActiveScene()
            : EditorSceneManager.OpenScene(DebugScenePath, OpenSceneMode.Single);
        EnsureSpawner(scene);
    }

    public static CoreDebugEnemySpawner EnsureSpawner(Scene scene)
    {
        var spawner = UnityEngine.Object.FindFirstObjectByType<CoreDebugEnemySpawner>();
        if (spawner == null)
        {
            var root = new GameObject("Debug Enemy Spawner");
            spawner = root.AddComponent<CoreDebugEnemySpawner>();
            root.transform.position = new Vector3(0f, 0f, -8f);
        }
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        Selection.activeGameObject = spawner.gameObject;
        Debug.Log("Debug enemy spawner configured. Use V, = and - in Play Mode.", spawner);
        return spawner;
    }
}
#endif
