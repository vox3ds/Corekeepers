#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace CoreKeepers.Editor
{
    [InitializeOnLoad]
    internal static class MeteorStrikeVfxPrefabBuilder
    {
        private const string PrefabPath = "Assets/CoreKeepers/Resources/VFX/MeteorStrikeVfx.prefab";
        private const string ShockwaveMaterialPath = "Assets/CoreKeepers/Materials/MeteorShockwave.mat";
        private const string CoreMaterialPath = "Assets/CoreKeepers/Materials/MeteorCore.mat";

        static MeteorStrikeVfxPrefabBuilder()
        {
            EditorApplication.delayCall += CreateIfMissing;
        }

        [MenuItem("Core Keepers/VFX/Create Missing Meteor Strike Prefab")]
        public static void CreateIfMissing()
        {
            if (AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath) != null) return;
            CreatePrefab();
        }

        private static void CreatePrefab()
        {
            EnsureFolder("Assets/CoreKeepers", "Resources");
            EnsureFolder("Assets/CoreKeepers/Resources", "VFX");
            EnsureFolder("Assets/CoreKeepers", "Materials");

            var fireball = AssetDatabase.LoadAssetAtPath<Material>("Assets/Materials/Fireball8x5.mat");
            var smoke = AssetDatabase.LoadAssetAtPath<Material>("Assets/Materials/Smoke8x5.mat");
            var explosionMaterial = AssetDatabase.LoadAssetAtPath<Material>("Assets/Materials/explosion9x9.mat");
            var flame = AssetDatabase.LoadAssetAtPath<Material>("Assets/Materials/Flame8x4.mat");
            var crack = AssetDatabase.LoadAssetAtPath<Material>("Assets/Materials/groundFireCrack.mat");
            var shockwaveMaterial = GetOrCreateGlowMaterial(ShockwaveMaterialPath,
                new Color(8f, 1.2f, 0.08f, 1f));
            var coreMaterial = GetOrCreateGlowMaterial(CoreMaterialPath,
                new Color(4f, 0.24f, 0.015f, 1f));

            var root = new GameObject("MeteorStrikeVfx");
            try
            {
                var controller = root.AddComponent<MeteorStrikeVfx>();

                var marker = CreateQuad(root.transform, "Meteor - Warning Marker", crack, 0);
                marker.transform.localPosition = Vector3.up * 0.025f;
                marker.transform.localRotation = Quaternion.Euler(-90f, 0f, 0f);

                var meteor = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                meteor.name = "Meteor";
                meteor.transform.SetParent(root.transform, false);
                meteor.transform.localPosition = new Vector3(-6f, 11f, -4f);
                meteor.transform.localScale = Vector3.one * 0.9f;
                Object.DestroyImmediate(meteor.GetComponent<Collider>());
                var meteorRenderer = meteor.GetComponent<Renderer>();
                meteorRenderer.sharedMaterial = coreMaterial;
                meteorRenderer.shadowCastingMode = ShadowCastingMode.Off;
                meteorRenderer.receiveShadows = false;

                var meteorFlames = CreateMeteorFlames(meteor.transform, fireball);
                var meteorSmoke = CreateMeteorSmoke(meteor.transform, smoke);
                var explosion = CreateExplosion(root.transform, explosionMaterial);
                var impactSmoke = CreateImpactSmoke(root.transform, smoke);

                var groundCrack = CreateQuad(root.transform, "Meteor - Ground Fire Crack", crack, 0);
                groundCrack.transform.localPosition = Vector3.up * 0.035f;
                groundCrack.transform.localRotation = Quaternion.Euler(-90f, 0f, 0f);
                groundCrack.SetActive(false);

                var groundFlames = CreateGroundFlames(root.transform, flame);
                var shockwave = CreateShockwave(root.transform, shockwaveMaterial);
                shockwave.gameObject.SetActive(false);

                controller.ConfigurePrefab(marker.transform, marker.GetComponent<Renderer>(), meteor.transform,
                    meteorRenderer, meteorFlames, meteorSmoke, explosion, impactSmoke, groundCrack.transform,
                    groundCrack.GetComponent<Renderer>(), groundFlames, shockwave);

                PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
                AssetDatabase.SaveAssets();
                Debug.Log($"Created editable Meteor Strike VFX prefab at {PrefabPath}");
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }

        private static ParticleSystem CreateMeteorFlames(Transform parent, Material material)
        {
            var particles = CreateParticles(parent, "Meteor - Flames", material, 8, 5, 3);
            var main = particles.main;
            main.loop = true;
            main.startLifetime = new ParticleSystem.MinMaxCurve(0.18f, 0.42f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(0.1f, 0.65f);
            main.startSize = new ParticleSystem.MinMaxCurve(0.55f, 1.15f);
            main.startColor = new ParticleSystem.MinMaxGradient(Color.white, new Color(1f, 0.2f, 0.01f));
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.maxParticles = 1200;
            var emission = particles.emission;
            emission.rateOverTime = 280f;
            var shape = particles.shape;
            shape.shapeType = ParticleSystemShapeType.Sphere;
            shape.radius = 0.08f;
            EnableFade(particles, false);
            return particles;
        }

        private static ParticleSystem CreateMeteorSmoke(Transform parent, Material material)
        {
            var particles = CreateParticles(parent, "Meteor - Smoke", material, 8, 5, 2);
            var main = particles.main;
            main.loop = true;
            main.startLifetime = new ParticleSystem.MinMaxCurve(0.65f, 1.25f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(0.15f, 0.55f);
            main.startSize = new ParticleSystem.MinMaxCurve(0.8f, 1.8f);
            main.startColor = new ParticleSystem.MinMaxGradient(
                new Color(0.12f, 0.1f, 0.09f, 0.9f), new Color(0.38f, 0.28f, 0.22f, 1f));
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.maxParticles = 700;
            var emission = particles.emission;
            emission.rateOverTime = 85f;
            var shape = particles.shape;
            shape.shapeType = ParticleSystemShapeType.Sphere;
            shape.radius = 0.12f;
            var noise = particles.noise;
            noise.enabled = true;
            noise.strength = 0.35f;
            noise.frequency = 0.65f;
            noise.scrollSpeed = 0.55f;
            EnableFade(particles, true);
            return particles;
        }

        private static ParticleSystem CreateExplosion(Transform parent, Material material)
        {
            var particles = CreateParticles(parent, "Meteor - Explosion", material, 9, 9, 5);
            particles.transform.localPosition = Vector3.up * 0.5f;
            var main = particles.main;
            main.loop = false;
            main.duration = 0.9f;
            main.startLifetime = 0.9f;
            main.startSpeed = 0f;
            main.startSize = 6.2f;
            main.startRotation = new ParticleSystem.MinMaxCurve(-0.18f, 0.18f);
            main.startColor = Color.white;
            main.maxParticles = 4;
            var emission = particles.emission;
            emission.rateOverTime = 0f;
            emission.SetBursts(new[] { new ParticleSystem.Burst(0f, 1) });
            return particles;
        }

        private static ParticleSystem CreateImpactSmoke(Transform parent, Material material)
        {
            var particles = CreateParticles(parent, "Meteor - Impact Smoke", material, 8, 5, 2);
            particles.transform.localPosition = Vector3.up * 0.25f;
            var main = particles.main;
            main.loop = false;
            main.duration = 0.35f;
            main.startLifetime = new ParticleSystem.MinMaxCurve(1.2f, 2.4f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(1.8f, 5.2f);
            main.startSize = new ParticleSystem.MinMaxCurve(1.4f, 3.6f);
            main.startColor = new ParticleSystem.MinMaxGradient(
                new Color(0.1f, 0.085f, 0.075f, 0.9f), new Color(0.42f, 0.3f, 0.22f, 1f));
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.maxParticles = 300;
            var emission = particles.emission;
            emission.rateOverTime = 0f;
            emission.SetBursts(new[] { new ParticleSystem.Burst(0f, 52) });
            var shape = particles.shape;
            shape.shapeType = ParticleSystemShapeType.Hemisphere;
            shape.radius = 0.42f;
            var noise = particles.noise;
            noise.enabled = true;
            noise.strength = 0.7f;
            noise.frequency = 0.55f;
            EnableFade(particles, true);
            return particles;
        }

        private static ParticleSystem CreateGroundFlames(Transform parent, Material material)
        {
            var particles = CreateParticles(parent, "Meteor - Burning Ground", material, 8, 4, 3);
            particles.transform.localPosition = Vector3.up * 0.045f;
            particles.transform.localRotation = Quaternion.Euler(-90f, 0f, 0f);
            var main = particles.main;
            main.loop = false;
            main.duration = 4f;
            main.startLifetime = new ParticleSystem.MinMaxCurve(0.5f, 1.1f);
            main.startSpeed = 0f;
            main.startSize = new ParticleSystem.MinMaxCurve(1.1f, 2.8f);
            main.startColor = Color.white;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.maxParticles = 1800;
            var emission = particles.emission;
            emission.rateOverTime = 120f;
            emission.SetBursts(new[] { new ParticleSystem.Burst(0f, 55) });
            var shape = particles.shape;
            shape.shapeType = ParticleSystemShapeType.Circle;
            shape.radius = 2.87f;
            shape.radiusThickness = 1f;
            EnableFade(particles, false);
            return particles;
        }

        private static ParticleSystem CreateParticles(Transform parent, string name, Material material,
            int columns, int rows, int sortingOrder)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            var particles = go.AddComponent<ParticleSystem>();
            particles.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var main = particles.main;
            main.duration = 1f;
            main.playOnAwake = false;
            main.scalingMode = ParticleSystemScalingMode.Hierarchy;
            var renderer = particles.GetComponent<ParticleSystemRenderer>();
            renderer.sharedMaterial = material;
            renderer.renderMode = ParticleSystemRenderMode.Billboard;
            renderer.sortingOrder = sortingOrder;
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            var sheet = particles.textureSheetAnimation;
            sheet.enabled = true;
            sheet.mode = ParticleSystemAnimationMode.Grid;
            sheet.animation = ParticleSystemAnimationType.WholeSheet;
            sheet.numTilesX = columns;
            sheet.numTilesY = rows;
            sheet.cycleCount = 1;
            sheet.frameOverTime = new ParticleSystem.MinMaxCurve(1f,
                AnimationCurve.Linear(0f, 0f, 1f, 1f));
            return particles;
        }

        private static void EnableFade(ParticleSystem particles, bool smoke)
        {
            var color = particles.colorOverLifetime;
            color.enabled = true;
            color.color = new ParticleSystem.MinMaxGradient(new Gradient
            {
                colorKeys = smoke
                    ? new[] { new GradientColorKey(new Color(0.35f, 0.25f, 0.2f), 0f), new GradientColorKey(new Color(0.07f, 0.065f, 0.06f), 1f) }
                    : new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(new Color(1f, 0.24f, 0.02f), 1f) },
                alphaKeys = new[]
                {
                    new GradientAlphaKey(0f, 0f), new GradientAlphaKey(1f, 0.08f),
                    new GradientAlphaKey(smoke ? 0.8f : 0.65f, 0.65f), new GradientAlphaKey(0f, 1f)
                }
            });
        }

        private static GameObject CreateQuad(Transform parent, string name, Material material, int sortingOrder)
        {
            var quad = GameObject.CreatePrimitive(PrimitiveType.Quad);
            quad.name = name;
            quad.transform.SetParent(parent, false);
            Object.DestroyImmediate(quad.GetComponent<Collider>());
            var renderer = quad.GetComponent<MeshRenderer>();
            renderer.sharedMaterial = material;
            renderer.sortingOrder = sortingOrder;
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            return quad;
        }

        private static LineRenderer CreateShockwave(Transform parent, Material material)
        {
            var go = new GameObject("Meteor - Shockwave Ring");
            go.transform.SetParent(parent, false);
            var line = go.AddComponent<LineRenderer>();
            line.loop = true;
            line.useWorldSpace = false;
            line.positionCount = 64;
            line.numCornerVertices = 3;
            line.sharedMaterial = material;
            line.shadowCastingMode = ShadowCastingMode.Off;
            line.receiveShadows = false;
            line.sortingOrder = 4;
            return line;
        }

        private static Material GetOrCreateGlowMaterial(string path, Color glow)
        {
            var existing = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (existing != null) return existing;
            var shader = Shader.Find("Universal Render Pipeline/Particles/Unlit") ?? Shader.Find("Sprites/Default");
            var material = new Material(shader);
            if (material.HasProperty("_BaseColor")) material.SetColor("_BaseColor", glow);
            if (material.HasProperty("_Color")) material.SetColor("_Color", glow);
            if (material.HasProperty("_EmissionColor"))
            {
                material.EnableKeyword("_EMISSION");
                material.SetColor("_EmissionColor", glow);
            }
            AssetDatabase.CreateAsset(material, path);
            return material;
        }

        private static void EnsureFolder(string parent, string name)
        {
            var path = $"{parent}/{name}";
            if (!AssetDatabase.IsValidFolder(path)) AssetDatabase.CreateFolder(parent, name);
        }
    }
}
#endif
