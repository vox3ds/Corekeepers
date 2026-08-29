#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Linq;
using CoreKeepers;
using UnityEditor;
using UnityEngine;

namespace CoreKeepers.Editor
{
    public static class SharedCharacterAnimationSetup
    {
        private const string SetupVersionKey = "CoreKeepers.SharedCharacterAnimationSetup.v4";
        private const string AnimationModelPath = "Assets/Animation/HeroEnemyAnimations.fbx";
        private const string EnemyPrefabDirectory = "Assets/CoreKeepers/Resources/Enemies";
        private const string GeneratedVariantDirectory = "Assets/CoreKeepers/Animations/Generated";
        private static readonly Dictionary<string, NamedCharacterClip[]> VariantCache = new(StringComparer.Ordinal);
        private static readonly string[] HeroPrefabPaths =
        {
            "Assets/CoreKeepers/Resources/CoreWarrior.prefab",
            "Assets/CoreKeepers/Resources/CoreMage.prefab",
            "Assets/CoreKeepers/Resources/CoreBuilder.prefab",
            "Assets/CoreKeepers/Resources/CoreHealer.prefab"
        };

        private static readonly string[] RequiredClipNames =
        {
            "AttackRHand", "AttackLHand", "Smash", "ShieldSmash",
            "CastProjectileLHand", "CastProjectileRHand", "CastSpellUp", "CastSpellAround",
            "TwirilStart", "TwirilLoop", "TwirilEnd", "DeadStart", "DeadLoop", "Resurected",
            "ThrowRock", "Hit", "ReviveStart", "ReviveLoop", "ReviveEnd", "Mine", "Build",
            "Deposit", "Idle", "RunStart", "RunLoop", "RunStop", "WalkStart", "WalkLoop",
            "WalkStop", "Float", "HeadAttack"
        };

        [InitializeOnLoadMethod]
        private static void ConfigureOnceAfterCompile()
        {
            if (SessionState.GetBool(SetupVersionKey, false))
                return;
            EditorApplication.delayCall += ConfigureAndRemember;
        }

        private static void ConfigureAndRemember()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling)
            {
                EditorApplication.delayCall += ConfigureAndRemember;
                return;
            }

            if (TryConfigureAll())
                SessionState.SetBool(SetupVersionKey, true);
        }

        [MenuItem("Core Keepers/Animations/Apply Shared Animations To Prefabs")]
        public static void ConfigureAllFromMenu()
        {
            if (!TryConfigureAll())
                throw new InvalidOperationException("Shared character animations could not be configured. See Console errors.");
        }

        [MenuItem("Core Keepers/Animations/Validate Shared Animation Prefabs")]
        public static void ValidateAllFromMenu()
        {
            var errors = new List<string>();
            foreach (var path in EnumerateCharacterPrefabPaths())
            {
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                if (prefab == null)
                {
                    errors.Add($"Missing prefab: {path}");
                    continue;
                }

                var player = prefab.GetComponent<SharedCharacterClipAnimator>();
                if (player == null)
                    errors.Add($"{path} has no {nameof(SharedCharacterClipAnimator)}.");
                else if (player.enabled && player.TargetAnimator == null)
                    errors.Add($"{path} has no target Animator.");
            }

            if (errors.Count > 0)
                throw new InvalidOperationException(string.Join("\n", errors));
            Debug.Log("Shared character animation validation passed for all hero and enemy prefabs.");
        }

        private static bool TryConfigureAll()
        {
            AssetDatabase.ImportAsset(AnimationModelPath,
                ImportAssetOptions.ForceSynchronousImport | ImportAssetOptions.ForceUpdate);

            var clips = LoadClips();
            var missing = RequiredClipNames.Where(name => !clips.ContainsKey(name)).ToArray();
            if (missing.Length > 0)
            {
                Debug.LogError($"Shared animation FBX is missing imported clips: {string.Join(", ", missing)}.");
                return false;
            }

            VariantCache.Clear();

            var configuredHeroes = 0;
            foreach (var path in HeroPrefabPaths)
                if (ConfigurePrefab(path, clips, false)) configuredHeroes++;

            var configuredEnemies = 0;
            foreach (var guid in AssetDatabase.FindAssets("t:Prefab", new[] { EnemyPrefabDirectory }))
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                if (ConfigurePrefab(path, clips, true)) configuredEnemies++;
            }

            AssetDatabase.SaveAssets();
            Debug.Log($"Applied shared animation library to {configuredHeroes} hero and {configuredEnemies} enemy prefabs.");
            return configuredHeroes == HeroPrefabPaths.Length && configuredEnemies > 0;
        }

        private static Dictionary<string, AnimationClip> LoadClips()
        {
            return AssetDatabase.LoadAllAssetsAtPath(AnimationModelPath)
                .OfType<AnimationClip>()
                .Where(clip => !clip.name.StartsWith("__preview__", StringComparison.Ordinal))
                .GroupBy(clip => clip.name, StringComparer.Ordinal)
                .ToDictionary(group => group.Key, group => group.First(), StringComparer.Ordinal);
        }

        private static bool ConfigurePrefab(string path, Dictionary<string, AnimationClip> sourceClips, bool isEnemy)
        {
            var prefabRoot = PrefabUtility.LoadPrefabContents(path);
            try
            {
                if (!TryFindRig(prefabRoot.transform, out var rigHost, out var pathRemapping, out var hasHead))
                {
                    Debug.LogError($"Cannot find both hand transforms and their common rig host in '{path}'.");
                    return false;
                }
                if (!hasHead)
                    Debug.LogWarning($"'{path}' has no separate Head transform. Head curves will be ignored.");

                var clips = GetClipEntries(sourceClips, pathRemapping);

                var animator = rigHost.GetComponent<Animator>();
                if (animator == null)
                    animator = rigHost.gameObject.AddComponent<Animator>();
                animator.applyRootMotion = false;
                animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
                animator.runtimeAnimatorController = null;
                if (animator.avatar == null)
                    animator.avatar = FindSourceAvatar(rigHost.gameObject);

                var player = prefabRoot.GetComponent<SharedCharacterClipAnimator>();
                if (player == null)
                    player = prefabRoot.AddComponent<SharedCharacterClipAnimator>();
                player.Configure(animator, clips);

                var useImportedAnimation = true;
                if (isEnemy)
                {
                    var procedural = prefabRoot.GetComponent<EnemyProceduralAnimator>();
                    if (procedural == null || prefabRoot.GetComponent<EnemyBrain>() == null)
                    {
                        Debug.LogError($"Enemy prefab '{path}' is missing its animation or brain component.");
                        return false;
                    }
                    useImportedAnimation = !procedural.UsesPhysicsRolling;
                    procedural.ConfigureImportedAnimation(useImportedAnimation);
                }
                else
                {
                    var procedural = prefabRoot.GetComponent<WarriorProceduralAnimator>();
                    if (procedural == null || prefabRoot.GetComponent<NetworkWarrior>() == null)
                    {
                        Debug.LogError($"Hero prefab '{path}' is missing its animation or network component.");
                        return false;
                    }
                    procedural.ConfigureImportedAnimation(true);
                }

                player.enabled = useImportedAnimation;
                EditorUtility.SetDirty(animator);
                EditorUtility.SetDirty(player);
                PrefabUtility.SaveAsPrefabAsset(prefabRoot, path);
                return true;
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(prefabRoot);
            }
        }

        private static bool TryFindRig(Transform prefabRoot, out Transform rigHost,
            out Dictionary<string, string> pathRemapping, out bool hasHead)
        {
            var head = FindDeepChildIgnoreCase(prefabRoot, "Head");
            var leftHand = FindDeepChildIgnoreCase(prefabRoot, "LHand");
            var rightHand = FindDeepChildIgnoreCase(prefabRoot, "RHand");
            rigHost = null;
            pathRemapping = new Dictionary<string, string>(StringComparer.Ordinal);
            hasHead = head != null;
            if (leftHand == null || rightHand == null)
                return false;

            if (!string.Equals(leftHand.name, "LHand", StringComparison.Ordinal))
                pathRemapping["LHand"] = leftHand.name;
            if (!string.Equals(rightHand.name, "RHand", StringComparison.Ordinal))
                pathRemapping["RHand"] = rightHand.name;
            if (head != null && !string.Equals(head.name, "Head", StringComparison.Ordinal))
                pathRemapping["Head"] = head.name;

            var rigParts = head != null
                ? new[] { head, leftHand, rightHand }
                : new[] { leftHand, rightHand };
            for (var candidate = leftHand; candidate != null; candidate = candidate.parent)
            {
                if (!rigParts.All(part => part == candidate || part.IsChildOf(candidate)))
                    continue;
                rigHost = candidate;
                return true;
            }

            return false;
        }

        private static NamedCharacterClip[] GetClipEntries(Dictionary<string, AnimationClip> sourceClips,
            Dictionary<string, string> pathRemapping)
        {
            if (pathRemapping.Count == 0)
                return CreateEntries(sourceClips);

            var key = string.Join("_", pathRemapping.OrderBy(pair => pair.Key)
                .Select(pair => $"{pair.Key}-{pair.Value}"));
            if (VariantCache.TryGetValue(key, out var cached))
                return cached;

            EnsureGeneratedVariantDirectory();
            var assetPath = $"{GeneratedVariantDirectory}/HeroEnemyAnimations_{key}.asset";
            var remappedClips = LoadClipsAtPath(assetPath);
            if (RequiredClipNames.Any(name => !remappedClips.ContainsKey(name)))
            {
                if (AssetDatabase.LoadMainAssetAtPath(assetPath) != null)
                    AssetDatabase.DeleteAsset(assetPath);
                remappedClips = CreateRemappedLibrary(assetPath, sourceClips, pathRemapping);
            }

            cached = CreateEntries(remappedClips);
            VariantCache[key] = cached;
            return cached;
        }

        private static Dictionary<string, AnimationClip> CreateRemappedLibrary(string assetPath,
            Dictionary<string, AnimationClip> sourceClips, Dictionary<string, string> pathRemapping)
        {
            var container = new AnimationClip { name = "GeneratedVariantContainer" };
            AssetDatabase.CreateAsset(container, assetPath);
            foreach (var clipName in RequiredClipNames)
            {
                var clone = new AnimationClip();
                EditorUtility.CopySerialized(sourceClips[clipName], clone);
                clone.name = clipName;
                RemapClipPaths(clone, pathRemapping);
                AssetDatabase.AddObjectToAsset(clone, container);
            }

            EditorUtility.SetDirty(container);
            AssetDatabase.ImportAsset(assetPath, ImportAssetOptions.ForceSynchronousImport);
            return LoadClipsAtPath(assetPath);
        }

        private static void RemapClipPaths(AnimationClip clip, Dictionary<string, string> pathRemapping)
        {
            foreach (var binding in AnimationUtility.GetCurveBindings(clip))
            {
                var remappedPath = RemapPath(binding.path, pathRemapping);
                if (string.Equals(remappedPath, binding.path, StringComparison.Ordinal))
                    continue;
                var curve = AnimationUtility.GetEditorCurve(clip, binding);
                AnimationUtility.SetEditorCurve(clip, binding, null);
                var remappedBinding = binding;
                remappedBinding.path = remappedPath;
                AnimationUtility.SetEditorCurve(clip, remappedBinding, curve);
            }

            foreach (var binding in AnimationUtility.GetObjectReferenceCurveBindings(clip))
            {
                var remappedPath = RemapPath(binding.path, pathRemapping);
                if (string.Equals(remappedPath, binding.path, StringComparison.Ordinal))
                    continue;
                var keyframes = AnimationUtility.GetObjectReferenceCurve(clip, binding);
                AnimationUtility.SetObjectReferenceCurve(clip, binding, null);
                var remappedBinding = binding;
                remappedBinding.path = remappedPath;
                AnimationUtility.SetObjectReferenceCurve(clip, remappedBinding, keyframes);
            }
        }

        private static string RemapPath(string path, Dictionary<string, string> pathRemapping)
        {
            var segments = path.Split('/');
            for (var index = 0; index < segments.Length; index++)
                if (pathRemapping.TryGetValue(segments[index], out var replacement))
                    segments[index] = replacement;
            return string.Join("/", segments);
        }

        private static NamedCharacterClip[] CreateEntries(Dictionary<string, AnimationClip> clips)
        {
            var missing = RequiredClipNames.Where(name => !clips.ContainsKey(name)).ToArray();
            if (missing.Length > 0)
                throw new InvalidOperationException(
                    $"Generated animation variant is incomplete. Missing: {string.Join(", ", missing)}.");

            return RequiredClipNames.Select(name => new NamedCharacterClip
            {
                name = name,
                clip = clips[name]
            }).ToArray();
        }

        private static Dictionary<string, AnimationClip> LoadClipsAtPath(string path)
        {
            return AssetDatabase.LoadAllAssetsAtPath(path)
                .OfType<AnimationClip>()
                .GroupBy(clip => clip.name, StringComparer.Ordinal)
                .ToDictionary(group => group.Key, group => group.First(), StringComparer.Ordinal);
        }

        private static void EnsureGeneratedVariantDirectory()
        {
            if (!AssetDatabase.IsValidFolder("Assets/CoreKeepers/Animations"))
                AssetDatabase.CreateFolder("Assets/CoreKeepers", "Animations");
            if (!AssetDatabase.IsValidFolder(GeneratedVariantDirectory))
                AssetDatabase.CreateFolder("Assets/CoreKeepers/Animations", "Generated");
        }

        private static Avatar FindSourceAvatar(GameObject rigHost)
        {
            var source = PrefabUtility.GetCorrespondingObjectFromOriginalSource(rigHost);
            var sourcePath = source != null ? AssetDatabase.GetAssetPath(source) : null;
            if (string.IsNullOrEmpty(sourcePath))
                return null;
            return AssetDatabase.LoadAllAssetsAtPath(sourcePath).OfType<Avatar>().FirstOrDefault();
        }

        private static Transform FindDeepChildIgnoreCase(Transform root, string childName)
        {
            foreach (var child in root.GetComponentsInChildren<Transform>(true))
                if (string.Equals(child.name, childName, StringComparison.OrdinalIgnoreCase)) return child;
            return null;
        }

        private static IEnumerable<string> EnumerateCharacterPrefabPaths()
        {
            foreach (var path in HeroPrefabPaths)
                yield return path;
            foreach (var guid in AssetDatabase.FindAssets("t:Prefab", new[] { EnemyPrefabDirectory }))
                yield return AssetDatabase.GUIDToAssetPath(guid);
        }
    }
}
#endif
