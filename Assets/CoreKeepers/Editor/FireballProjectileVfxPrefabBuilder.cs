#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace CoreKeepers.Editor
{
    [InitializeOnLoad]
    internal static class FireballProjectileVfxPrefabBuilder
    {
        private const string PrefabPath = "Assets/CoreKeepers/Resources/VFX/FireballProjectileVfx.prefab";
        private const string FlamesMaterialPath = "Assets/Materials/Fireball8x5.mat";
        private const string SmokeMaterialPath = "Assets/Materials/Smoke8x5.mat";

        static FireballProjectileVfxPrefabBuilder()
        {
            EditorApplication.delayCall += CreateIfMissing;
        }

        [MenuItem("Core Keepers/VFX/Create Missing Fireball Projectile Prefab")]
        public static void CreateIfMissing()
        {
            if (AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath) != null) return;
            CreatePrefab();
        }

        private static void CreatePrefab()
        {
            EnsureFolder("Assets/CoreKeepers", "Resources");
            EnsureFolder("Assets/CoreKeepers/Resources", "VFX");

            var root = new GameObject("FireballProjectileVfx");
            try
            {
                CreateEmitter(root.transform, "Fireball - Smoke", true,
                    AssetDatabase.LoadAssetAtPath<Material>(SmokeMaterialPath));
                CreateEmitter(root.transform, "Fireball - Flames", false,
                    AssetDatabase.LoadAssetAtPath<Material>(FlamesMaterialPath));
                PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
                AssetDatabase.SaveAssets();
                Debug.Log($"Created editable Fireball VFX prefab at {PrefabPath}");
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }

        private static void CreateEmitter(Transform parent, string objectName, bool smoke, Material material)
        {
            var go = new GameObject(objectName);
            go.transform.SetParent(parent, false);
            go.transform.localRotation = Quaternion.Euler(0f, 180f, 0f);

            var particles = go.AddComponent<ParticleSystem>();
            particles.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

            var main = particles.main;
            main.duration = 1f;
            main.loop = true;
            main.playOnAwake = true;
            main.startLifetime = smoke
                ? new ParticleSystem.MinMaxCurve(0.48f, 0.9f)
                : new ParticleSystem.MinMaxCurve(0.112f, 0.294f);
            main.startSpeed = smoke
                ? new ParticleSystem.MinMaxCurve(0.08f, 0.32f)
                : new ParticleSystem.MinMaxCurve(0.03f, 0.24f);
            main.startSize = smoke
                ? new ParticleSystem.MinMaxCurve(0.6f, 1.36f)
                : new ParticleSystem.MinMaxCurve(0.32f, 0.8f);
            main.startRotation = new ParticleSystem.MinMaxCurve(-Mathf.PI, Mathf.PI);
            main.startColor = smoke
                ? new ParticleSystem.MinMaxGradient(
                    new Color(0.12f, 0.1f, 0.09f, 0.92f),
                    new Color(0.34f, 0.25f, 0.2f, 1f))
                : new ParticleSystem.MinMaxGradient(
                    new Color(1f, 1f, 1f, 1f),
                    new Color(1f, 0.22f, 0.015f, 1f));
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.scalingMode = ParticleSystemScalingMode.Hierarchy;
            main.maxParticles = smoke ? 1200 : 2000;

            var emission = particles.emission;
            emission.rateOverTime = smoke ? 117f : 378f;
            emission.SetBursts(new[]
            {
                new ParticleSystem.Burst(0f, (short)(smoke ? 12 : 32))
            });

            var shape = particles.shape;
            shape.shapeType = ParticleSystemShapeType.Sphere;
            shape.radius = 0.01f;
            shape.radiusThickness = 1f;

            var colorOverLifetime = particles.colorOverLifetime;
            colorOverLifetime.enabled = true;
            colorOverLifetime.color = new ParticleSystem.MinMaxGradient(CreateLifetimeGradient(smoke));

            var noise = particles.noise;
            noise.enabled = true;
            noise.strength = smoke ? 0.3f : 0.2f;
            noise.frequency = smoke ? 0.65f : 1.4f;
            noise.scrollSpeed = smoke ? 0.5f : 1.5f;

            if (smoke)
            {
                var velocity = particles.velocityOverLifetime;
                velocity.enabled = true;
                velocity.space = ParticleSystemSimulationSpace.World;
                velocity.y = 0.25f;
            }

            var sheet = particles.textureSheetAnimation;
            sheet.enabled = true;
            sheet.mode = ParticleSystemAnimationMode.Grid;
            sheet.animation = ParticleSystemAnimationType.WholeSheet;
            sheet.numTilesX = 8;
            sheet.numTilesY = 5;
            sheet.cycleCount = 1;
            sheet.frameOverTime = new ParticleSystem.MinMaxCurve(1f,
                AnimationCurve.Linear(0f, 0f, 1f, 1f));

            var renderer = particles.GetComponent<ParticleSystemRenderer>();
            renderer.renderMode = ParticleSystemRenderMode.Billboard;
            renderer.sortingOrder = smoke ? -1 : 1;
            renderer.sharedMaterial = material;
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = false;
        }

        private static Gradient CreateLifetimeGradient(bool smoke)
        {
            return new Gradient
            {
                colorKeys = smoke
                    ? new[]
                    {
                        new GradientColorKey(new Color(0.28f, 0.2f, 0.16f), 0f),
                        new GradientColorKey(new Color(0.08f, 0.075f, 0.07f), 1f)
                    }
                    : new[]
                    {
                        new GradientColorKey(Color.white, 0f),
                        new GradientColorKey(new Color(1f, 0.38f, 0.025f), 0.4f),
                        new GradientColorKey(new Color(0.35f, 0.025f, 0.005f), 1f)
                    },
                alphaKeys = smoke
                    ? new[]
                    {
                        new GradientAlphaKey(0.15f, 0f),
                        new GradientAlphaKey(1f, 0.08f),
                        new GradientAlphaKey(0.85f, 0.62f),
                        new GradientAlphaKey(0f, 1f)
                    }
                    : new[]
                    {
                        new GradientAlphaKey(0f, 0f),
                        new GradientAlphaKey(1f, 0.08f),
                        new GradientAlphaKey(0f, 1f)
                    }
            };
        }

        private static void EnsureFolder(string parent, string name)
        {
            var path = $"{parent}/{name}";
            if (!AssetDatabase.IsValidFolder(path)) AssetDatabase.CreateFolder(parent, name);
        }
    }
}
#endif
