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
    private const string SetupVersionKey = "CoreKeepers.EnemyPrefabSetup.v6";
    private const string EnemyDirectory = "Assets/CoreKeepers/Resources/Enemies";
    private const string ProjectileDirectory = "Assets/CoreKeepers/Resources/EnemyProjectiles";
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
        ConfigureProjectilePrefabs();
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
            var enemyName = Path.GetFileNameWithoutExtension(path);
            var physicsRoll = enemyName == "Pumpkin_Fiend";
            body.isKinematic = !physicsRoll;
            body.useGravity = physicsRoll;
            body.interpolation = RigidbodyInterpolation.Interpolate;

            if (physicsRoll)
            {
                if (collider is CapsuleCollider oldCapsule)
                    Object.DestroyImmediate(oldCapsule);
                var sphere = root.GetComponent<SphereCollider>() ?? root.AddComponent<SphereCollider>();
                sphere.radius = 0.62f;
                sphere.center = Vector3.up * 0.62f;
                collider = sphere;
            }

            var brain = AddIfMissing<EnemyBrain>(root);
            var animator = AddIfMissing<EnemyProceduralAnimator>(root);
            var serialized = new SerializedObject(brain);
            var combat = CombatProfile(enemyName);
            serialized.FindProperty("maximumHealth").floatValue = combat.Health;
            serialized.FindProperty("damage").floatValue = combat.Damage;
            serialized.FindProperty("lootGoblin").boolValue = enemyName == "Chest";
            if (enemyName == "Chest")
            {
                serialized.FindProperty("movementSpeed").floatValue = 4.8f;
                serialized.FindProperty("coreShardsDropMin").intValue = 50;
                serialized.FindProperty("coreShardsDropMax").intValue = 100;
            }
            serialized.FindProperty("canPassThroughBarricades").boolValue = IsOneOf(enemyName,
                "Banshee", "Ghost", "Water_Slime", "Storm_Elemental", "Poison_Slime");
            serialized.FindProperty("assassin").boolValue = IsOneOf(enemyName,
                "Vampire", "Cursed_Doll", "Warlock", "Poison_Slime", "Chompfin");
            serialized.ApplyModifiedPropertiesWithoutUndo();
            var animatorSerialized = new SerializedObject(animator);
            if (physicsRoll)
            {
                animatorSerialized.FindProperty("movementPreset").enumValueIndex =
                    (int)EnemyMovementAnimationPreset.PhysicsRoll;
                animatorSerialized.FindProperty("attackPreset").enumValueIndex =
                    (int)EnemyAttackAnimationPreset.None;
            }
            var attackAngle = animatorSerialized.FindProperty("attackAngle");
            var attackPreset = animatorSerialized.FindProperty("attackPreset");
            if (attackPreset.enumValueIndex == (int)EnemyAttackAnimationPreset.AlternatingMagicProjectile)
            {
                var attackRange = serialized.FindProperty("attackRange");
                if (attackRange.floatValue < 6.6f) attackRange.floatValue = 6.6f;
                var projectilePath = $"{ProjectileDirectory}/{enemyName}_Projectile.prefab";
                animatorSerialized.FindProperty("projectilePrefab").objectReferenceValue =
                    AssetDatabase.LoadAssetAtPath<GameObject>(projectilePath);
            }
            serialized.ApplyModifiedPropertiesWithoutUndo();
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

    private static void ConfigureProjectilePrefabs()
    {
        if (!AssetDatabase.IsValidFolder(ProjectileDirectory))
            AssetDatabase.CreateFolder("Assets/CoreKeepers/Resources", "EnemyProjectiles");

        var magicEnemies = new[]
        {
            "Mummy", "Dark_Elf", "Book", "Demon", "Storm_Elemental", "Warlock", "Water_Slime", "Witch"
        };
        var colors = new[]
        {
            new Color(0.7f, 0.25f, 1f), new Color(0.3f, 0.8f, 1f), new Color(1f, 0.35f, 0.8f),
            new Color(1f, 0.15f, 0.08f), new Color(0.25f, 0.7f, 1f), new Color(0.6f, 0.1f, 1f),
            new Color(0.1f, 0.9f, 0.85f), new Color(0.75f, 0.2f, 1f)
        };
        for (var index = 0; index < magicEnemies.Length; index++)
            ConfigureProjectilePrefab(magicEnemies[index], colors[index]);
    }

    private static void ConfigureProjectilePrefab(string enemyName, Color color)
    {
        var prefabPath = $"{ProjectileDirectory}/{enemyName}_Projectile.prefab";
        var materialPath = $"{ProjectileDirectory}/{enemyName}_Projectile.mat";
        var material = AssetDatabase.LoadAssetAtPath<Material>(materialPath);
        if (material == null)
        {
            var shader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
            material = new Material(shader) { name = $"{enemyName} Projectile" };
            AssetDatabase.CreateAsset(material, materialPath);
        }
        material.color = color;
        if (material.HasProperty("_BaseColor")) material.SetColor("_BaseColor", color);
        var emission = color * 5f;
        if (material.HasProperty("_EmissionColor")) material.SetColor("_EmissionColor", emission);
        material.EnableKeyword("_EMISSION");
        EditorUtility.SetDirty(material);

        var existing = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
        var root = existing != null ? PrefabUtility.LoadPrefabContents(prefabPath) : GameObject.CreatePrimitive(PrimitiveType.Sphere);
        try
        {
            root.name = $"{enemyName}_Projectile";
            root.transform.localScale = Vector3.one * 0.38f;
            var renderer = root.GetComponent<Renderer>();
            if (renderer != null) renderer.sharedMaterial = material;
            var collider = root.GetComponent<SphereCollider>() ?? root.AddComponent<SphereCollider>();
            collider.isTrigger = true;
            AddIfMissing<NetworkObject>(root);
            AddIfMissing<NetworkTransform>(root);
            AddIfMissing<EnemyProjectile>(root);
            var light = root.GetComponentInChildren<Light>();
            if (light == null)
            {
                var lightObject = new GameObject("Glow");
                lightObject.transform.SetParent(root.transform, false);
                light = lightObject.AddComponent<Light>();
            }
            light.type = LightType.Point;
            light.color = color;
            light.intensity = 3f;
            light.range = 2.5f;
            PrefabUtility.SaveAsPrefabAsset(root, prefabPath);
        }
        finally
        {
            if (existing != null) PrefabUtility.UnloadPrefabContents(root);
            else Object.DestroyImmediate(root);
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
        foreach (var projectilePrefab in Resources.LoadAll<GameObject>("EnemyProjectiles"))
        {
            var networkObject = projectilePrefab.GetComponent<NetworkObject>();
            if (networkObject == null || !registeredHashes.Add(networkObject.PrefabIdHash))
                continue;
            networkPrefabs.Add(new NetworkPrefab { Prefab = projectilePrefab });
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

    private readonly struct EnemyCombatProfile
    {
        public readonly float Health;
        public readonly float Damage;

        public EnemyCombatProfile(float health, float damage)
        {
            Health = health;
            Damage = damage;
        }
    }

    private static EnemyCombatProfile CombatProfile(string enemyName)
    {
        // Damage assumes the shared 1.1 s attack cooldown. Health is expressed in
        // meaningful hero hits: fodder 2-3, regular 3-5, elite 6-10, boss 14+.
        return enemyName switch
        {
            "Bat_Monster" => new EnemyCombatProfile(45f, 7f),
            "Frog" => new EnemyCombatProfile(60f, 8f),
            "Mushroom" => new EnemyCombatProfile(65f, 8f),
            "Kobold" => new EnemyCombatProfile(75f, 10f),
            "Skeleton" => new EnemyCombatProfile(80f, 10f),
            "Book" => new EnemyCombatProfile(70f, 9f),
            "Cursed_Doll" => new EnemyCombatProfile(75f, 14f),
            "Spider" => new EnemyCombatProfile(80f, 11f),
            "Ghost" => new EnemyCombatProfile(85f, 11f),
            "Poison_Slime" => new EnemyCombatProfile(90f, 10f),
            "Dark_Elf" => new EnemyCombatProfile(90f, 11f),
            "Chompfin" => new EnemyCombatProfile(95f, 14f),
            "Ghoul" => new EnemyCombatProfile(95f, 12f),
            "Water_Slime" => new EnemyCombatProfile(100f, 11f),
            "Werewolf" => new EnemyCombatProfile(170f, 18f),
            "Zombie" => new EnemyCombatProfile(105f, 12f),
            "Witch" => new EnemyCombatProfile(2300f, 32f),
            "Vampire" => new EnemyCombatProfile(110f, 16f),
            "Storm_Elemental" => new EnemyCombatProfile(115f, 13f),
            "Rat_Mutant" => new EnemyCombatProfile(120f, 13f),
            "Mummy" => new EnemyCombatProfile(125f, 12f),
            "Fire_Elemental" => new EnemyCombatProfile(130f, 15f),
            "Orc" => new EnemyCombatProfile(135f, 15f),
            "Mossy" => new EnemyCombatProfile(145f, 12f),
            "Frostbite" => new EnemyCombatProfile(150f, 14f),
            "Banshee" => new EnemyCombatProfile(1500f, 28f),
            "Satyr" => new EnemyCombatProfile(155f, 16f),
            "Warlock" => new EnemyCombatProfile(3000f, 40f),
            "Demon" => new EnemyCombatProfile(190f, 18f),
            "Pumpkin_Fiend" => new EnemyCombatProfile(200f, 24f),
            "Brute" => new EnemyCombatProfile(240f, 21f),
            "Dark_Knight" => new EnemyCombatProfile(260f, 22f),
            "Cyclop" => new EnemyCombatProfile(2000f, 35f),
            "Frankenstein" => new EnemyCombatProfile(300f, 23f),
            "Troll" => new EnemyCombatProfile(1200f, 30f),
            "Minotaur" => new EnemyCombatProfile(340f, 27f),
            "Stone_Golem" => new EnemyCombatProfile(400f, 30f),
            "Chest" => new EnemyCombatProfile(180f, 0f),
            "Crystal" => new EnemyCombatProfile(500f, 30f),
            _ => new EnemyCombatProfile(100f, 12f)
        };
    }
}
#endif
