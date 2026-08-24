#if UNITY_EDITOR
using System.IO;
using System.Collections.Generic;
using System.Linq;
using CoreKeepers;
using Unity.AI.Navigation;
using Unity.Netcode;
using Unity.Netcode.Components;
using UnityEditor;
using UnityEngine;
using UnityEngine.AI;

public static class EnemyPrefabSetup
{
    private const string SetupVersionKey = "CoreKeepers.EnemyPrefabSetup.v1";
    private const string EnemyDirectory = "Assets/CoreKeepers/Resources/Enemies";
    private const string TrapPath = "Assets/CoreKeepers/Resources/Buildings/TrapPlate.prefab";
    private const string NetworkPrefabListPath = "Assets/DefaultNetworkPrefabs.asset";

    [InitializeOnLoadMethod]
    private static void ConfigureOnceAfterCompile()
    {
        if (SessionState.GetBool(SetupVersionKey, false)) return;
        if (EditorApplication.isPlayingOrWillChangePlaymode)
        {
            EditorApplication.playModeStateChanged -= ConfigureWhenEditModeReturns;
            EditorApplication.playModeStateChanged += ConfigureWhenEditModeReturns;
            return;
        }
        EditorApplication.delayCall += ConfigureAndRemember;
    }

    private static void ConfigureWhenEditModeReturns(PlayModeStateChange state)
    {
        if (state != PlayModeStateChange.EnteredEditMode) return;
        EditorApplication.playModeStateChanged -= ConfigureWhenEditModeReturns;
        ConfigureAndRemember();
    }

    private static void ConfigureAndRemember()
    {
        ConfigureAll();
        SessionState.SetBool(SetupVersionKey, true);
    }

    [MenuItem("Core Keepers/Configure Enemy Prefabs")]
    public static void ConfigureAll()
    {
        foreach (var guid in AssetDatabase.FindAssets("t:Prefab", new[] { EnemyDirectory }))
            ConfigureEnemy(AssetDatabase.GUIDToAssetPath(guid));
        ConfigureTrapPlate();
        ConfigureNetworkPrefabList();
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log("Configured all Core Keepers enemy prefabs and the one-shot Trap Plate.");
    }

    public static void ConfigureAllBatch()
    {
        ConfigureAll();
        EditorApplication.Exit(0);
    }

    private static void ConfigureEnemy(string path)
    {
        var root = PrefabUtility.LoadPrefabContents(path);
        try
        {
            AddIfMissing<NetworkObject>(root);
            AddIfMissing<NetworkTransform>(root);
            var agent = AddIfMissing<NavMeshAgent>(root);
            agent.radius = 0.45f;
            agent.height = 1.8f;
            agent.speed = 3.5f;
            agent.acceleration = 10f;
            agent.angularSpeed = 540f;
            agent.obstacleAvoidanceType = ObstacleAvoidanceType.HighQualityObstacleAvoidance;

            var collider = root.GetComponent<Collider>();
            if (collider == null)
                collider = root.AddComponent<CapsuleCollider>();
            if (collider is CapsuleCollider capsule)
            {
                capsule.radius = 0.45f;
                capsule.height = 1.8f;
                capsule.center = Vector3.up * 0.9f;
            }
            collider.isTrigger = false;
            var body = AddIfMissing<Rigidbody>(root);
            body.isKinematic = true;
            body.useGravity = false;

            var brain = AddIfMissing<EnemyBrain>(root);
            var animator = AddIfMissing<EnemyProceduralAnimator>(root);
            var serialized = new SerializedObject(brain);
            var enemyName = Path.GetFileNameWithoutExtension(path);
            serialized.FindProperty("canPassThroughBarricades").boolValue = IsOneOf(enemyName,
                "Banshee", "Ghost", "Water_Slime", "Storm_Elemental", "Poison_Slime");
            serialized.FindProperty("assassin").boolValue = IsOneOf(enemyName,
                "Vampire", "Cursed_Doll", "Warlock", "Poison_Slime", "Chompfin");
            serialized.ApplyModifiedPropertiesWithoutUndo();
            var animatorSerialized = new SerializedObject(animator);
            var attackAngle = animatorSerialized.FindProperty("attackAngle");
            var swooshArc = animatorSerialized.FindProperty("swooshArc");
            var swooshRadius = animatorSerialized.FindProperty("swooshRadius");
            if (attackAngle.floatValue < 165f) attackAngle.floatValue = 165f;
            if (swooshArc.floatValue < 220f) swooshArc.floatValue = 220f;
            if (swooshRadius.floatValue < 0.7f) swooshRadius.floatValue = 0.7f;
            animatorSerialized.ApplyModifiedPropertiesWithoutUndo();
            PrefabUtility.SaveAsPrefabAsset(root, path);
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(root);
        }
    }

    private static void ConfigureTrapPlate()
    {
        var root = PrefabUtility.LoadPrefabContents(TrapPath);
        try
        {
            var collider = root.GetComponent<Collider>();
            if (collider != null) collider.isTrigger = true;
            var obstacle = root.GetComponent<NavMeshObstacle>();
            if (obstacle != null) obstacle.enabled = false;
            AddIfMissing<CoreTrapPlate>(root);
            PrefabUtility.SaveAsPrefabAsset(root, TrapPath);
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(root);
        }
    }

    private static void ConfigureNetworkPrefabList()
    {
        var networkPrefabs = AssetDatabase.LoadAssetAtPath<NetworkPrefabsList>(NetworkPrefabListPath);
        if (networkPrefabs == null)
        {
            Debug.LogError($"Network prefab list is missing at {NetworkPrefabListPath}.");
            return;
        }

        var registeredHashes = new HashSet<uint>();
        foreach (var entry in networkPrefabs.PrefabList.ToArray())
        {
            var networkObject = entry.Prefab != null ? entry.Prefab.GetComponent<NetworkObject>() : null;
            if (networkObject == null || !registeredHashes.Add(networkObject.PrefabIdHash))
                networkPrefabs.Remove(entry);
        }

        foreach (var enemyPrefab in Resources.LoadAll<GameObject>("Enemies"))
        {
            var networkObject = enemyPrefab.GetComponent<NetworkObject>();
            if (networkObject == null || !registeredHashes.Add(networkObject.PrefabIdHash))
                continue;
            networkPrefabs.Add(new NetworkPrefab { Prefab = enemyPrefab });
        }
        EditorUtility.SetDirty(networkPrefabs);
    }

    private static T AddIfMissing<T>(GameObject root) where T : Component
    {
        var component = root.GetComponent<T>();
        if (component == null)
            component = root.AddComponent<T>();
        if (component == null)
            throw new System.InvalidOperationException(
                $"Unity could not add {typeof(T).Name} to prefab root '{root.name}'.");
        return component;
    }

    private static bool IsOneOf(string value, params string[] choices)
    {
        foreach (var choice in choices)
            if (value == choice) return true;
        return false;
    }
}
#endif
