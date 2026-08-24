#if UNITY_EDITOR
using System.IO;
using CoreKeepers;
using Unity.Netcode;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

public static class CoreLootPrefabSetup
{
    private const string PrefabPath = "Assets/CoreKeepers/Resources/CoreLootPickup.prefab";
    private const string MaterialDirectory = "Assets/CoreKeepers/Materials";
    private const string OreMaterialPath = MaterialDirectory + "/LootOre.mat";
    private const string ShardsMaterialPath = MaterialDirectory + "/LootCoreShards.mat";
    private const string NetworkPrefabListPath = "Assets/DefaultNetworkPrefabs.asset";

    [InitializeOnLoadMethod]
    private static void ConfigureOnce()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
        {
            EditorApplication.playModeStateChanged -= ConfigureAfterPlayMode;
            EditorApplication.playModeStateChanged += ConfigureAfterPlayMode;
            return;
        }
        EditorApplication.delayCall += ConfigureIfMissing;
    }

    private static void ConfigureAfterPlayMode(PlayModeStateChange state)
    {
        if (state != PlayModeStateChange.EnteredEditMode) return;
        EditorApplication.playModeStateChanged -= ConfigureAfterPlayMode;
        ConfigureIfMissing();
    }

    private static void ConfigureIfMissing()
    {
        if (!EditorApplication.isCompiling && !File.Exists(PrefabPath))
            ConfigureLootPrefab();
    }

    [MenuItem("Core Keepers/Configure Loot Prefab")]
    public static void ConfigureLootPrefab()
    {
        Directory.CreateDirectory(MaterialDirectory);
        var ore = CreateMaterial(OreMaterialPath, "Assets/UI/OreIcon.png");
        var shards = CreateMaterial(ShardsMaterialPath, "Assets/UI/CoreShardsIcon.png");

        var root = new GameObject("CoreLootPickup");
        root.AddComponent<NetworkObject>();
        var collider = root.AddComponent<SphereCollider>();
        collider.isTrigger = true;
        collider.radius = 0.45f;
        var body = root.AddComponent<Rigidbody>();
        body.isKinematic = true;
        body.useGravity = false;
        var pickup = root.AddComponent<CoreLootPickup>();

        var visual = GameObject.CreatePrimitive(PrimitiveType.Quad);
        visual.name = "Floating Resource Icon";
        visual.transform.SetParent(root.transform, false);
        visual.transform.localScale = Vector3.one * 0.65f;
        Object.DestroyImmediate(visual.GetComponent<Collider>());
        visual.GetComponent<Renderer>().sharedMaterial = ore;

        var serialized = new SerializedObject(pickup);
        serialized.FindProperty("visualRenderer").objectReferenceValue = visual.GetComponent<Renderer>();
        serialized.FindProperty("oreMaterial").objectReferenceValue = ore;
        serialized.FindProperty("coreShardsMaterial").objectReferenceValue = shards;
        serialized.ApplyModifiedPropertiesWithoutUndo();
        PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
        Object.DestroyImmediate(root);

        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
        var list = AssetDatabase.LoadAssetAtPath<NetworkPrefabsList>(NetworkPrefabListPath);
        if (prefab != null && list != null && !list.Contains(prefab))
        {
            list.Add(new NetworkPrefab { Prefab = prefab });
            EditorUtility.SetDirty(list);
        }
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log("Configured networked Ore/CoreShards loot prefab.", prefab);
    }

    private static Material CreateMaterial(string materialPath, string texturePath)
    {
        var material = AssetDatabase.LoadAssetAtPath<Material>(materialPath);
        var shader = Shader.Find("Universal Render Pipeline/Unlit") ?? Shader.Find("Unlit/Transparent");
        if (material == null)
        {
            material = new Material(shader);
            AssetDatabase.CreateAsset(material, materialPath);
        }
        else if (shader != null)
            material.shader = shader;
        var texture = AssetDatabase.LoadAssetAtPath<Texture2D>(texturePath);
        if (material.HasProperty("_BaseMap")) material.SetTexture("_BaseMap", texture);
        if (material.HasProperty("_MainTex")) material.SetTexture("_MainTex", texture);
        if (material.HasProperty("_BaseColor")) material.SetColor("_BaseColor", Color.white);
        if (material.HasProperty("_Surface")) material.SetFloat("_Surface", 1f);
        if (material.HasProperty("_ZWrite")) material.SetFloat("_ZWrite", 0f);
        if (material.HasProperty("_Cull")) material.SetFloat("_Cull", (float)CullMode.Off);
        if (material.HasProperty("_SrcBlend")) material.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha);
        if (material.HasProperty("_DstBlend")) material.SetFloat("_DstBlend", (float)BlendMode.OneMinusSrcAlpha);
        material.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
        material.renderQueue = (int)RenderQueue.Transparent;
        EditorUtility.SetDirty(material);
        return material;
    }
}
#endif
