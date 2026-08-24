#if UNITY_EDITOR
using System.IO;
using Unity.AI.Navigation;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.SceneManagement;

public static class DebugNavMeshSetup
{
    private const string DebugScenePath = "Assets/Scenes/DebugScene.unity";

    [InitializeOnLoadMethod]
    private static void BakeWhenDebugSceneIsAlreadyOpen()
    {
        EditorApplication.delayCall += () =>
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling)
                return;
            var scene = SceneManager.GetActiveScene();
            if (scene.path == DebugScenePath)
                ConfigureAndBake(scene);
        };
    }

    [MenuItem("Core Keepers/Configure Debug NavMesh")]
    public static void ConfigureDebugNavMesh()
    {
        if (!File.Exists(DebugScenePath))
        {
            Debug.LogError($"Debug scene is missing at {DebugScenePath}.");
            return;
        }

        var scene = SceneManager.GetActiveScene().path == DebugScenePath
            ? SceneManager.GetActiveScene()
            : EditorSceneManager.OpenScene(DebugScenePath, OpenSceneMode.Single);
        ConfigureAndBake(scene);
    }

    private static void ConfigureAndBake(Scene scene)
    {
        var surface = Object.FindFirstObjectByType<NavMeshSurface>();
        if (surface == null)
        {
            var navigation = new GameObject("Core Navigation");
            surface = navigation.AddComponent<NavMeshSurface>();
        }

        surface.collectObjects = CollectObjects.All;
        surface.useGeometry = NavMeshCollectGeometry.PhysicsColliders;
        surface.layerMask = ~0;
        surface.RemoveData();
        surface.BuildNavMesh();
        var spawner = CoreDebugEnemySpawnerSceneSetup.EnsureSpawner(scene);
        if (NavMesh.SamplePosition(spawner.transform.position, out var spawnHit, 10f, NavMesh.AllAreas))
            spawner.transform.position = spawnHit.position;
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        Debug.Log($"DebugScene NavMesh baked successfully ({surface.navMeshData != null}).", surface);
    }
}
#endif
