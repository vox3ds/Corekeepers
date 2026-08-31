using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.InputSystem;

namespace CoreKeepers
{
    public enum CombatVfxPreview
    {
        ArcaneBolt,
        Fireball,
        FrostNova,
        ChainLightning,
        Heal,
        BuffAura,
        GroundImpact,
        Vortex,
        Hit
    }

    internal enum CombatVfxAtlas
    {
        Auto,
        Explosion9x9,
        EnergyExplosion8x8,
        EnergyExplosion5x4,
        DarkEnergy8x5,
        Flame8x4
    }

    /// <summary>Lightweight first-pass combat presentation shared by gameplay and DebugScene.</summary>
    public static class HeroCombatVfx
    {
        private static readonly Color Arcane = new(0.3f, 0.45f, 1f, 1f);
        private static readonly Color Fire = new(1f, 0.24f, 0.04f, 1f);
        private static readonly Color Frost = new(0.2f, 0.85f, 1f, 1f);
        private static readonly Color Holy = new(1f, 0.82f, 0.26f, 1f);
        private static readonly Color Nature = new(0.25f, 1f, 0.48f, 1f);
        private static CombatVfxMaterialLibrary materialLibrary;
        private static bool materialLibraryLoaded;

        public static void PlaySkill(HeroSkillDefinition skill, Vector3 origin, Vector3 point, Transform target)
        {
            if (skill == null) return;
            var targetPoint = target != null ? target.position + Vector3.up * 0.75f : point + Vector3.up * 0.15f;
            var color = SkillColor(skill);
            switch (skill.Effect)
            {
                case HeroSkillEffect.SingleProjectile:
                case HeroSkillEffect.ExplodingProjectile:
                    var isArcaneBolt = skill.StableId == 101;
                    var isFireball = skill.StableId == 102;
                    if (isArcaneBolt)
                        SpawnProjectileMuzzleFlash(origin, point - origin, color,
                            CombatVfxAtlas.EnergyExplosion8x8, 1f);
                    else if (isFireball)
                        SpawnProjectileMuzzleFlash(origin, targetPoint - origin, color,
                            CombatVfxAtlas.Flame8x4, 1.65f);
                    var projectileDestination = isFireball && target == null ? point + Vector3.up * 0.75f : targetPoint;
                    HeroProjectileVisual.Spawn(origin, isArcaneBolt ? point : projectileDestination,
                        isArcaneBolt ? null : target, color,
                        skill.Effect == HeroSkillEffect.ExplodingProjectile ? 0.32f : isArcaneBolt ? 0.14f : 0.2f,
                        isFireball ? skill.Radius : 0.8f,
                        !isArcaneBolt, isArcaneBolt, isFireball, skill.Duration);
                    break;
                case HeroSkillEffect.ChainDamage:
                    SpawnLightning(origin, targetPoint, color);
                    SpawnBurst(targetPoint, color, 0.65f, CombatVfxAtlas.EnergyExplosion8x8);
                    break;
                case HeroSkillEffect.Blink:
                    SpawnBurst(origin, color, 0.9f, CombatVfxAtlas.EnergyExplosion5x4);
                    SpawnBurst(point + Vector3.up * 0.5f, color, 1.1f, CombatVfxAtlas.EnergyExplosion5x4);
                    break;
                case HeroSkillEffect.GroundImpact:
                    SpawnRing(point, color, Mathf.Max(1f, skill.Radius), 0.8f);
                    SpawnBurst(point + Vector3.up * 0.2f, color, 1.5f, CombatVfxAtlas.Explosion9x9);
                    break;
                case HeroSkillEffect.Vortex:
                    SpawnRing(point, color, Mathf.Max(1f, skill.Radius), 0.8f);
                    SpawnBurst(point + Vector3.up * 0.2f, color, 1.5f, CombatVfxAtlas.DarkEnergy8x5);
                    break;
                case HeroSkillEffect.RadialDamage:
                case HeroSkillEffect.RadialDebuff:
                    if (skill.StableId == 103)
                    {
                        SpawnFrostNova(point, Mathf.Max(1f, skill.Radius));
                        break;
                    }
                    SpawnRing(point, color, Mathf.Max(1f, skill.Radius), 0.65f);
                    SpawnBurst(origin, color, 1f);
                    break;
                case HeroSkillEffect.HolyPulse:
                case HeroSkillEffect.RepairPulse:
                    SpawnRing(point, color, Mathf.Max(1f, skill.Radius), 0.65f);
                    SpawnBurst(origin, color, 1f);
                    break;
                case HeroSkillEffect.HealingArea:
                case HeroSkillEffect.Sanctuary:
                case HeroSkillEffect.ConstructionAura:
                case HeroSkillEffect.BuildingBuff:
                case HeroSkillEffect.CleanseWard:
                case HeroSkillEffect.SelfBuff:
                case HeroSkillEffect.Taunt:
                    SpawnRing(point, color, Mathf.Max(1.2f, skill.Radius), 0.9f);
                    SpawnBurst(origin, color, 1.1f, CombatVfxAtlas.EnergyExplosion5x4);
                    break;
                default:
                    SpawnBurst(targetPoint, color, 0.75f);
                    break;
            }
        }

        public static void Preview(CombatVfxPreview preview, Transform subject)
        {
            if (subject == null) return;
            var center = subject.position + Vector3.up * 0.75f;
            var forward = subject.forward.sqrMagnitude > 0.01f ? subject.forward : Vector3.forward;
            var destination = center + forward * 4f;
            switch (preview)
            {
                case CombatVfxPreview.ArcaneBolt:
                    SpawnProjectileMuzzleFlash(center, forward, Arcane,
                        CombatVfxAtlas.EnergyExplosion8x8, 1f);
                    HeroProjectileVisual.Spawn(center, destination, null, Arcane, 0.14f, 0.8f,
                        false, true); break;
                case CombatVfxPreview.Fireball:
                    SpawnProjectileMuzzleFlash(center, forward, Fire, CombatVfxAtlas.Flame8x4, 1.65f);
                    HeroProjectileVisual.Spawn(center, destination, null, Fire, 0.34f, 1.75f,
                        true, false, true, 5f); break;
                case CombatVfxPreview.FrostNova:
                    SpawnFrostNova(subject.position, 4.5f); break;
                case CombatVfxPreview.ChainLightning:
                    SpawnLightning(center, destination, Frost);
                    SpawnBurst(destination, Frost, 0.7f, CombatVfxAtlas.EnergyExplosion8x8); break;
                case CombatVfxPreview.Heal:
                    SpawnRing(subject.position, Holy, 3f, 0.9f);
                    SpawnBurst(center, Holy, 1.1f, CombatVfxAtlas.EnergyExplosion5x4); break;
                case CombatVfxPreview.BuffAura:
                    SpawnRing(subject.position, Nature, 2.5f, 1.1f);
                    SpawnBurst(center, Nature, 0.9f, CombatVfxAtlas.EnergyExplosion8x8); break;
                case CombatVfxPreview.GroundImpact:
                    SpawnRing(subject.position, Fire, 4f, 0.7f);
                    SpawnBurst(center, Fire, 1.5f, CombatVfxAtlas.Explosion9x9); break;
                case CombatVfxPreview.Vortex:
                    SpawnRing(subject.position, Arcane, 4.5f, 1.2f);
                    SpawnBurst(center, Arcane, 1.5f, CombatVfxAtlas.DarkEnergy8x5); break;
                default:
                    SpawnBurst(center, Color.white, 0.75f); break;
            }
        }

        public static void PlayProjectileImpact(int stableId, Vector3 position, Vector3 direction)
        {
            if (stableId != 101) return;
            if (direction.sqrMagnitude < 0.001f) direction = Vector3.forward;
            direction.Normalize();
            HeroProjectileVisual.ResolveArcaneImpact(position, direction);
            SpawnBurst(position, Arcane, 0.72f, CombatVfxAtlas.EnergyExplosion8x8);
            SpawnDirectionalSparkles(position, direction, Arcane);
        }

        public static void DismissProjectile(int stableId, Vector3 position, Vector3 direction)
        {
            if (stableId == 101) HeroProjectileVisual.ResolveArcaneImpact(position, direction);
        }

        public static void SetCharacterBurning(Transform subject, bool active)
        {
            if (subject == null) return;
            const string effectName = "Status VFX - Burning";
            var existing = subject.Find(effectName);
            if (!active)
            {
                if (existing != null)
                {
                    var controller = existing.GetComponent<HeroBurnVisualController>();
                    if (controller != null) controller.BeginFadeOut(1f);
                    else UnityEngine.Object.Destroy(existing.gameObject);
                }
                return;
            }
            if (existing != null)
            {
                existing.GetComponent<HeroBurnVisualController>()?.Resume();
                return;
            }

            var root = new GameObject(effectName);
            root.transform.SetParent(subject, false);
            root.transform.localPosition = Vector3.up * 0.75f;
            var particles = root.AddComponent<ParticleSystem>();
            particles.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var main = particles.main;
            main.duration = 1f;
            main.loop = true;
            main.startLifetime = new ParticleSystem.MinMaxCurve(0.45f, 0.9f);
            main.startSpeed = 0f;
            main.startSize = new ParticleSystem.MinMaxCurve(0.55f, 1.25f);
            main.startRotation = new ParticleSystem.MinMaxCurve(-Mathf.PI, Mathf.PI);
            main.startColor = Color.white;
            main.simulationSpace = ParticleSystemSimulationSpace.Local;
            var emission = particles.emission;
            emission.rateOverTime = 12f;
            var shape = particles.shape;
            shape.shapeType = ParticleSystemShapeType.Sphere;
            shape.radius = 0.01f;
            shape.radiusThickness = 1f;
            var colorOverLifetime = particles.colorOverLifetime;
            colorOverLifetime.enabled = true;
            colorOverLifetime.color = new ParticleSystem.MinMaxGradient(new Gradient
            {
                colorKeys = new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Fire, 1f) },
                alphaKeys = new[]
                {
                    new GradientAlphaKey(0f, 0f), new GradientAlphaKey(1f, 0.15f),
                    new GradientAlphaKey(0f, 1f)
                }
            });
            var renderer = particles.GetComponent<ParticleSystemRenderer>();
            if (TryGetAtlas(CombatVfxAtlas.Flame8x4, Fire, out var material, out var columns, out var rows))
            {
                renderer.sharedMaterial = material;
                var textureSheet = particles.textureSheetAnimation;
                textureSheet.enabled = true;
                textureSheet.mode = ParticleSystemAnimationMode.Grid;
                textureSheet.animation = ParticleSystemAnimationType.WholeSheet;
                textureSheet.numTilesX = columns;
                textureSheet.numTilesY = rows;
                textureSheet.cycleCount = 1;
                textureSheet.frameOverTime = new ParticleSystem.MinMaxCurve(1f,
                    AnimationCurve.Linear(0f, 0f, 1f, 1f));
            }
            else
            {
                var runtimeMaterial = CreateMaterial(Fire, true, 5f);
                renderer.material = runtimeMaterial;
                root.AddComponent<CombatVfxMaterialCleanup>().Initialize(runtimeMaterial);
            }
            root.AddComponent<HeroBurnVisualController>().Initialize(subject, particles);
            particles.Play();
        }

        private static void SpawnDirectionalSparkles(Vector3 position, Vector3 direction, Color color)
        {
            var root = new GameObject("Arcane Bolt - Directional Sparkles");
            root.transform.SetPositionAndRotation(position, Quaternion.LookRotation(direction, Vector3.up));
            var particles = root.AddComponent<ParticleSystem>();
            particles.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var main = particles.main;
            main.duration = 0.4f;
            main.loop = false;
            main.startLifetime = new ParticleSystem.MinMaxCurve(0.28f, 0.6f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(0.65f, 1.8f);
            main.startSize = new ParticleSystem.MinMaxCurve(0.24f, 0.6f);
            main.startColor = Color.white;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.stopAction = ParticleSystemStopAction.Destroy;
            var emission = particles.emission;
            emission.rateOverTime = 0f;
            emission.SetBursts(new[] { new ParticleSystem.Burst(0f, (short)7) });
            var shape = particles.shape;
            shape.shapeType = ParticleSystemShapeType.Cone;
            shape.angle = 24f;
            shape.radius = 0.04f;
            var velocity = particles.velocityOverLifetime;
            velocity.enabled = true;
            velocity.space = ParticleSystemSimulationSpace.World;
            velocity.x = direction.x * 1.15f;
            velocity.y = direction.y * 1.15f;
            velocity.z = direction.z * 1.15f;
            var colorOverLifetime = particles.colorOverLifetime;
            colorOverLifetime.enabled = true;
            colorOverLifetime.color = new ParticleSystem.MinMaxGradient(new Gradient
            {
                colorKeys = new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(color, 1f) },
                alphaKeys = new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(0f, 1f) }
            });
            var renderer = particles.GetComponent<ParticleSystemRenderer>();
            if (TryGetAtlas(CombatVfxAtlas.EnergyExplosion8x8, color, out var material, out var columns,
                    out var rows))
            {
                renderer.sharedMaterial = material;
                var textureSheet = particles.textureSheetAnimation;
                textureSheet.enabled = true;
                textureSheet.mode = ParticleSystemAnimationMode.Grid;
                textureSheet.animation = ParticleSystemAnimationType.WholeSheet;
                textureSheet.numTilesX = columns;
                textureSheet.numTilesY = rows;
                textureSheet.cycleCount = 1;
                textureSheet.frameOverTime = new ParticleSystem.MinMaxCurve(1f,
                    AnimationCurve.Linear(0f, 0f, 1f, 1f));
            }
            else
            {
                var runtimeMaterial = CreateMaterial(color, true);
                renderer.material = runtimeMaterial;
                root.AddComponent<CombatVfxMaterialCleanup>().Initialize(runtimeMaterial);
            }
            particles.Play();
        }

        private static void SpawnProjectileMuzzleFlash(Vector3 position, Vector3 direction, Color color,
            CombatVfxAtlas atlas, float sizeMultiplier)
        {
            if (direction.sqrMagnitude < 0.001f) direction = Vector3.forward;
            direction.Normalize();
            var root = new GameObject("Arcane Bolt - Muzzle Flash");
            root.transform.SetPositionAndRotation(position, Quaternion.LookRotation(direction, Vector3.up));
            var particles = root.AddComponent<ParticleSystem>();
            particles.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var main = particles.main;
            main.duration = 0.16f;
            main.loop = false;
            main.startLifetime = new ParticleSystem.MinMaxCurve(0.08f, 0.2f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(1.8f, 4.2f);
            main.startSize = new ParticleSystem.MinMaxCurve(0.12f * sizeMultiplier, 0.32f * sizeMultiplier);
            main.startColor = Color.white;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.stopAction = ParticleSystemStopAction.Destroy;
            var emission = particles.emission;
            emission.rateOverTime = 0f;
            emission.SetBursts(new[] { new ParticleSystem.Burst(0f, (short)9) });
            var shape = particles.shape;
            shape.shapeType = ParticleSystemShapeType.Cone;
            shape.angle = 18f;
            shape.radius = 0.035f * sizeMultiplier;
            var colorOverLifetime = particles.colorOverLifetime;
            colorOverLifetime.enabled = true;
            colorOverLifetime.color = new ParticleSystem.MinMaxGradient(new Gradient
            {
                colorKeys = new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(color, 0.45f) },
                alphaKeys = new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(0f, 1f) }
            });
            var renderer = particles.GetComponent<ParticleSystemRenderer>();
            if (TryGetAtlas(atlas, color, out var material, out var columns,
                    out var rows))
            {
                renderer.sharedMaterial = material;
                var textureSheet = particles.textureSheetAnimation;
                textureSheet.enabled = true;
                textureSheet.mode = ParticleSystemAnimationMode.Grid;
                textureSheet.numTilesX = columns;
                textureSheet.numTilesY = rows;
                textureSheet.animation = ParticleSystemAnimationType.WholeSheet;
                textureSheet.frameOverTime = new ParticleSystem.MinMaxCurve(0f, 1f);
            }
            else
            {
                var runtimeMaterial = CreateMaterial(color, true);
                renderer.material = runtimeMaterial;
                root.AddComponent<CombatVfxMaterialCleanup>().Initialize(runtimeMaterial);
            }
            particles.Play();
        }

        internal static void SpawnFireballImpact(Vector3 groundPosition, float radius, float duration)
        {
            radius = Mathf.Max(0.5f, radius);
            duration = Mathf.Max(1f, duration);
            SpawnBurst(groundPosition + Vector3.up * 0.45f, Fire, 1.65f, CombatVfxAtlas.Explosion9x9);
            SpawnRing(groundPosition, Fire, radius, 0.5f, 10f);
            SpawnBurningGround(groundPosition, radius, duration);
        }

        private static void SpawnFrostNova(Vector3 position, float radius)
        {
            SpawnRing(position, Frost, radius, 0.78f, 10f);
            SpawnFrostWind(position, radius);
            SpawnFrostSpikes(position, radius);
        }

        private static void SpawnFrostWind(Vector3 position, float radius)
        {
            TryGetAtlas(CombatVfxAtlas.EnergyExplosion5x4, Frost, out _, out _, out _);
            var root = new GameObject("Frost Nova - Cold Wind");
            root.transform.position = position + Vector3.up * 0.16f;
            var particles = root.AddComponent<ParticleSystem>();
            particles.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var main = particles.main;
            main.duration = 1.1f;
            main.loop = false;
            main.startLifetime = new ParticleSystem.MinMaxCurve(0.42f, 0.75f);
            main.startSpeed = 0f;
            main.startSize = new ParticleSystem.MinMaxCurve(0.24f, 0.6f);
            main.startColor = new ParticleSystem.MinMaxGradient(Color.white, Frost);
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.stopAction = ParticleSystemStopAction.Destroy;
            var emission = particles.emission;
            emission.enabled = false;
            var colorOverLifetime = particles.colorOverLifetime;
            colorOverLifetime.enabled = true;
            colorOverLifetime.color = new ParticleSystem.MinMaxGradient(new Gradient
            {
                colorKeys = new[]
                {
                    new GradientColorKey(Color.white, 0f),
                    new GradientColorKey(Frost, 0.45f),
                    new GradientColorKey(new Color(0.15f, 0.5f, 1f), 1f)
                },
                alphaKeys = new[]
                {
                    new GradientAlphaKey(0f, 0f), new GradientAlphaKey(0.8f, 0.12f),
                    new GradientAlphaKey(0.55f, 0.62f), new GradientAlphaKey(0f, 1f)
                }
            });
            var sizeOverLifetime = particles.sizeOverLifetime;
            sizeOverLifetime.enabled = true;
            sizeOverLifetime.size = new ParticleSystem.MinMaxCurve(1f,
                AnimationCurve.EaseInOut(0f, 0.3f, 1f, 1.25f));
            var renderer = particles.GetComponent<ParticleSystemRenderer>();
            renderer.renderMode = ParticleSystemRenderMode.Stretch;
            renderer.velocityScale = 0.28f;
            renderer.lengthScale = 2.2f;
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            if (materialLibrary != null && materialLibrary.ColdWind != null)
                renderer.sharedMaterial = materialLibrary.ColdWind;
            else
            {
                var runtimeMaterial = CreateMaterial(Frost, true, 6f);
                renderer.material = runtimeMaterial;
                root.AddComponent<CombatVfxMaterialCleanup>().Initialize(runtimeMaterial);
            }
            root.AddComponent<FrostWindWaveController>().Initialize(particles, radius);
            particles.Play();
        }

        private static void SpawnFrostSpikes(Vector3 position, float radius)
        {
            TryGetAtlas(CombatVfxAtlas.EnergyExplosion5x4, Frost, out _, out _, out _);
            if (materialLibrary == null || materialLibrary.FrostSpikes == null)
            {
                Debug.LogWarning("FrostSpikes prefab is missing from the Combat VFX material library.");
                return;
            }

            const int spikeCount = 12;
            var spikesMaterial = materialLibrary.FrostSpikesMaterial;
            if (spikesMaterial == null)
                spikesMaterial = Resources.FindObjectsOfTypeAll<Material>()
                    .FirstOrDefault(item => item != null && item.name == "FrostSpikes");
            if (spikesMaterial == null)
                Debug.LogError("FrostSpikes.mat could not be resolved by the Combat VFX material library.");
            var group = new GameObject("Frost Nova - Frost Spikes");
            group.transform.position = position;
            UnityEngine.Object.Destroy(group, 2.8f);
            var angleOffset = UnityEngine.Random.Range(0f, 360f);
            for (var index = 0; index < spikeCount; index++)
            {
                var angle = angleOffset + index * (360f / spikeCount) + UnityEngine.Random.Range(-8f, 8f);
                var direction = Quaternion.Euler(0f, angle, 0f) * Vector3.forward;
                var groundPosition = position + direction * (radius * UnityEngine.Random.Range(0.58f, 0.9f));
                var spike = UnityEngine.Object.Instantiate(materialLibrary.FrostSpikes);
                spike.name = $"Frost Spike {index + 1:00}";
                spike.SetActive(true);
                spike.transform.SetParent(group.transform, true);
                var rotation = Quaternion.Euler(-90f, angle, 0f);
                spike.AddComponent<FrostSpikeVisual>().Initialize(groundPosition, rotation,
                    UnityEngine.Random.Range(1.7f, 2.45f), index * 0.018f,
                    spikesMaterial);
            }
        }

        private static void SpawnBurningGround(Vector3 position, float radius, float duration)
        {
            TryGetAtlas(CombatVfxAtlas.Flame8x4, Fire, out var flameMaterial, out var columns, out var rows);

            if (materialLibrary != null && materialLibrary.GroundFireCrack != null)
            {
                var decal = GameObject.CreatePrimitive(PrimitiveType.Quad);
                decal.name = "Fireball - Ground Fire Crack";
                var collider = decal.GetComponent<Collider>();
                if (collider != null) UnityEngine.Object.Destroy(collider);
                decal.transform.SetPositionAndRotation(position + Vector3.up * 0.02f,
                    Quaternion.Euler(90f, 0f, 0f) *
                    Quaternion.AngleAxis(UnityEngine.Random.Range(0f, 360f), Vector3.forward));
                decal.transform.localScale = Vector3.one * (radius * 2f);
                decal.AddComponent<FireballGroundDecal>().Initialize(materialLibrary.GroundFireCrack, duration);
            }

            var root = new GameObject("Fireball - Burning Ground");
            root.transform.SetPositionAndRotation(position + Vector3.up * 0.04f,
                Quaternion.Euler(-90f, 0f, 0f));
            var particles = root.AddComponent<ParticleSystem>();
            particles.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var main = particles.main;
            main.duration = Mathf.Max(0.1f, duration - 1f);
            main.loop = false;
            main.startLifetime = new ParticleSystem.MinMaxCurve(0.45f, 1f);
            main.startSpeed = 0f;
            main.startSize = new ParticleSystem.MinMaxCurve(0.9f, 2.4f);
            main.startColor = Color.white;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.stopAction = ParticleSystemStopAction.Destroy;
            var emission = particles.emission;
            emission.rateOverTime = Mathf.Max(30f, radius * 15f);
            emission.SetBursts(new[] { new ParticleSystem.Burst(0f, (short)Mathf.CeilToInt(radius * 12f)) });
            var shape = particles.shape;
            shape.shapeType = ParticleSystemShapeType.Circle;
            shape.radius = radius * 0.82f;
            shape.radiusThickness = 1f;
            var colorOverLifetime = particles.colorOverLifetime;
            colorOverLifetime.enabled = true;
            colorOverLifetime.color = new ParticleSystem.MinMaxGradient(new Gradient
            {
                colorKeys = new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Fire, 1f) },
                alphaKeys = new[]
                {
                    new GradientAlphaKey(0f, 0f), new GradientAlphaKey(1f, 0.12f),
                    new GradientAlphaKey(0f, 1f)
                }
            });
            var sizeOverLifetime = particles.sizeOverLifetime;
            sizeOverLifetime.enabled = true;
            sizeOverLifetime.size = new ParticleSystem.MinMaxCurve(1f,
                AnimationCurve.EaseInOut(0f, 0.55f, 1f, 1f));
            var renderer = particles.GetComponent<ParticleSystemRenderer>();
            if (flameMaterial != null)
            {
                renderer.sharedMaterial = flameMaterial;
                var textureSheet = particles.textureSheetAnimation;
                textureSheet.enabled = true;
                textureSheet.mode = ParticleSystemAnimationMode.Grid;
                textureSheet.animation = ParticleSystemAnimationType.WholeSheet;
                textureSheet.numTilesX = columns;
                textureSheet.numTilesY = rows;
                textureSheet.cycleCount = 1;
                textureSheet.frameOverTime = new ParticleSystem.MinMaxCurve(1f,
                    AnimationCurve.Linear(0f, 0f, 1f, 1f));
            }
            else
            {
                var runtimeMaterial = CreateMaterial(Fire, true, 5f);
                renderer.material = runtimeMaterial;
                root.AddComponent<CombatVfxMaterialCleanup>().Initialize(runtimeMaterial);
            }
            var groundFade = root.AddComponent<CombatVfxParticleFade>();
            groundFade.Initialize(particles, Mathf.Max(0.1f, duration - 1f), 1f);
            particles.Play();
        }

        internal static void SpawnBurst(Vector3 position, Color color, float size,
            CombatVfxAtlas requestedAtlas = CombatVfxAtlas.Auto)
        {
            var root = new GameObject("Combat VFX - Burst");
            root.transform.position = position;
            var particles = root.AddComponent<ParticleSystem>();
            particles.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var main = particles.main;
            var hasAtlas = TryGetAtlas(requestedAtlas, color, out var atlasMaterial, out var columns, out var rows);
            main.duration = hasAtlas ? 0.7f : 0.35f;
            main.loop = false;
            main.startLifetime = hasAtlas ? 0.68f : new ParticleSystem.MinMaxCurve(0.22f, 0.48f);
            main.startSpeed = hasAtlas ? 0f : new ParticleSystem.MinMaxCurve(1.5f * size, 4f * size);
            main.startSize = hasAtlas ? size * 1.8f : new ParticleSystem.MinMaxCurve(0.05f * size, 0.18f * size);
            main.startRotation = hasAtlas ? new ParticleSystem.MinMaxCurve(-0.15f, 0.15f) : 0f;
            main.startColor = hasAtlas ? Color.white : new ParticleSystem.MinMaxGradient(color * 1.8f, Color.white);
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.stopAction = ParticleSystemStopAction.Destroy;
            var emission = particles.emission;
            emission.rateOverTime = 0f;
            emission.SetBursts(new[] { new ParticleSystem.Burst(0f,
                hasAtlas ? (short)1 : (short)Mathf.RoundToInt(18f * size)) });
            var shape = particles.shape;
            shape.shapeType = ParticleSystemShapeType.Sphere;
            shape.radius = hasAtlas ? 0f : 0.18f * size;
            var colorOverLifetime = particles.colorOverLifetime;
            colorOverLifetime.enabled = true;
            colorOverLifetime.color = new ParticleSystem.MinMaxGradient(new Gradient
            {
                colorKeys = hasAtlas
                    ? new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) }
                    : new[] { new GradientColorKey(color, 0f), new GradientColorKey(color * 0.5f, 1f) },
                alphaKeys = new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(0f, 1f) }
            });
            var renderer = particles.GetComponent<ParticleSystemRenderer>();
            if (hasAtlas)
            {
                renderer.sharedMaterial = atlasMaterial;
                renderer.renderMode = ParticleSystemRenderMode.Billboard;
                var textureSheet = particles.textureSheetAnimation;
                textureSheet.enabled = true;
                textureSheet.mode = ParticleSystemAnimationMode.Grid;
                textureSheet.animation = ParticleSystemAnimationType.WholeSheet;
                textureSheet.numTilesX = columns;
                textureSheet.numTilesY = rows;
                textureSheet.cycleCount = 1;
                textureSheet.frameOverTime = new ParticleSystem.MinMaxCurve(1f,
                    AnimationCurve.Linear(0f, 0f, 1f, 1f));
            }
            else
            {
                var runtimeMaterial = CreateMaterial(color, true);
                renderer.material = runtimeMaterial;
                root.AddComponent<CombatVfxMaterialCleanup>().Initialize(runtimeMaterial);
            }
            particles.Play();
        }

        private static bool TryGetAtlas(CombatVfxAtlas requested, Color color, out Material material,
            out int columns, out int rows)
        {
            if (!materialLibraryLoaded)
            {
                materialLibraryLoaded = true;
                materialLibrary = Resources.Load<CombatVfxMaterialLibrary>("VFX/CombatVfxMaterialLibrary");
                if (materialLibrary == null)
                    Debug.LogWarning("Combat VFX material library is missing. Procedural VFX fallback will be used.");
            }

            if (requested == CombatVfxAtlas.Auto)
            {
                if (ColorDistance(color, Fire) < 0.35f) requested = CombatVfxAtlas.Explosion9x9;
                else if (ColorDistance(color, Arcane) < 0.45f) requested = CombatVfxAtlas.EnergyExplosion8x8;
                else requested = CombatVfxAtlas.EnergyExplosion5x4;
            }

            material = requested switch
            {
                CombatVfxAtlas.Explosion9x9 => materialLibrary != null ? materialLibrary.Explosion9x9 : null,
                CombatVfxAtlas.EnergyExplosion8x8 => materialLibrary != null ? materialLibrary.EnergyExplosion8x8 : null,
                CombatVfxAtlas.EnergyExplosion5x4 => materialLibrary != null ? materialLibrary.EnergyExplosion5x4 : null,
                CombatVfxAtlas.DarkEnergy8x5 => materialLibrary != null ? materialLibrary.DarkEnergy8x5 : null,
                CombatVfxAtlas.Flame8x4 => materialLibrary != null ? materialLibrary.Flame8x4 : null,
                _ => null
            };
            (columns, rows) = requested switch
            {
                CombatVfxAtlas.Explosion9x9 => (9, 9),
                CombatVfxAtlas.EnergyExplosion8x8 => (8, 8),
                CombatVfxAtlas.EnergyExplosion5x4 => (5, 4),
                CombatVfxAtlas.DarkEnergy8x5 => (8, 5),
                CombatVfxAtlas.Flame8x4 => (8, 4),
                _ => (1, 1)
            };
            return material != null;
        }

        private static float ColorDistance(Color left, Color right)
        {
            var delta = new Vector3(left.r - right.r, left.g - right.g, left.b - right.b);
            return delta.magnitude;
        }

        internal static void SpawnRing(Vector3 position, Color color, float radius, float lifetime,
            float glowMultiplier = 2.5f)
        {
            var root = new GameObject("Combat VFX - Ring");
            root.transform.position = position + Vector3.up * 0.06f;
            root.AddComponent<CombatVfxRing>().Initialize(color, radius, lifetime, glowMultiplier);
        }

        private static void SpawnLightning(Vector3 start, Vector3 end, Color color)
        {
            var root = new GameObject("Combat VFX - Lightning");
            root.AddComponent<CombatVfxLightning>().Initialize(start, end, color);
        }

        internal static Material CreateMaterial(Color color, bool particle = false, float glowMultiplier = 2.5f)
        {
            var shader = Shader.Find(particle ? "Universal Render Pipeline/Particles/Unlit" :
                "Universal Render Pipeline/Unlit");
            if (shader == null) shader = Shader.Find("Sprites/Default");
            var material = new Material(shader) { hideFlags = HideFlags.DontSave };
            var glow = color * glowMultiplier;
            glow.a = color.a;
            if (material.HasProperty("_BaseColor")) material.SetColor("_BaseColor", glow);
            if (material.HasProperty("_Color")) material.SetColor("_Color", glow);
            if (material.HasProperty("_EmissionColor"))
            {
                material.EnableKeyword("_EMISSION");
                material.SetColor("_EmissionColor", glow);
            }
            return material;
        }

        private static Color SkillColor(HeroSkillDefinition skill)
        {
            if (skill.HeroClass == CorePlayerClass.Healer) return Holy;
            if (skill.HeroClass == CorePlayerClass.Builder) return Nature;
            if (skill.StableId is 102 or 110 or 113) return Fire;
            if (skill.StableId == 103) return Frost;
            return skill.HeroClass == CorePlayerClass.Mage ? Arcane : new Color(0.55f, 0.9f, 1f, 1f);
        }
    }

    public sealed class FrostWindWaveController : MonoBehaviour
    {
        private ParticleSystem particles;
        private float radius;
        private float age;
        private int emittedWaves;

        public void Initialize(ParticleSystem requestedParticles, float requestedRadius)
        {
            particles = requestedParticles;
            radius = Mathf.Max(1f, requestedRadius);
        }

        private void Update()
        {
            if (particles == null) return;
            while (emittedWaves < 3 && age >= emittedWaves * 0.12f)
            {
                EmitWave(emittedWaves);
                emittedWaves++;
            }
            age += Time.deltaTime;
        }

        private void EmitWave(int wave)
        {
            const int particlesPerWave = 22;
            var speed = radius / Mathf.Lerp(0.72f, 0.58f, wave / 2f);
            var angleOffset = UnityEngine.Random.Range(0f, 360f);
            for (var index = 0; index < particlesPerWave; index++)
            {
                var angle = angleOffset + index * (360f / particlesPerWave) + UnityEngine.Random.Range(-5f, 5f);
                var direction = Quaternion.Euler(0f, angle, 0f) * Vector3.forward;
                var emit = new ParticleSystem.EmitParams
                {
                    position = transform.position + direction * (0.12f + wave * 0.08f),
                    velocity = direction * speed + Vector3.up * UnityEngine.Random.Range(0.03f, 0.22f),
                    startSize = UnityEngine.Random.Range(0.24f, 0.6f),
                    startLifetime = UnityEngine.Random.Range(0.5f, 0.72f),
                    startColor = Color.Lerp(Color.white, new Color(0.2f, 0.85f, 1f),
                        UnityEngine.Random.Range(0.25f, 0.8f))
                };
                particles.Emit(emit, 1);
            }
        }
    }

    public sealed class FrostSpikeVisual : MonoBehaviour
    {
        private Vector3 startPosition;
        private Vector3 finalPosition;
        private Vector3 finalScale;
        private float delay;
        private float height;
        private float age;

        public void Initialize(Vector3 groundPosition, Quaternion rotation, float requestedHeight,
            float requestedDelay, Material requestedMaterial)
        {
            delay = Mathf.Max(0f, requestedDelay);
            height = Mathf.Max(0.25f, requestedHeight);
            transform.SetPositionAndRotation(groundPosition, rotation);

            foreach (var item in GetComponentsInChildren<Collider>(true)) item.enabled = false;
            var renderers = GetComponentsInChildren<Renderer>(true);
            if (renderers.Length == 0)
            {
                Destroy(gameObject);
                return;
            }
            foreach (var item in renderers)
            {
                item.enabled = true;
                item.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                item.receiveShadows = false;
                if (requestedMaterial != null)
                {
                    item.sharedMaterials = Enumerable.Repeat(requestedMaterial,
                        Mathf.Max(1, item.sharedMaterials.Length)).ToArray();
                }
            }

            var bounds = renderers[0].bounds;
            for (var index = 1; index < renderers.Length; index++) bounds.Encapsulate(renderers[index].bounds);
            var scaleMultiplier = height / Mathf.Max(0.001f, bounds.size.y);
            transform.localScale *= scaleMultiplier;
            finalScale = transform.localScale;

            bounds = renderers[0].bounds;
            for (var index = 1; index < renderers.Length; index++) bounds.Encapsulate(renderers[index].bounds);
            transform.position += new Vector3(groundPosition.x - bounds.center.x,
                0f, groundPosition.z - bounds.center.z);
            finalPosition = new Vector3(transform.position.x, 0f, transform.position.z);
            startPosition = finalPosition - Vector3.up * (height * 0.92f);
            transform.position = startPosition;
            transform.localScale = Vector3.Scale(finalScale, new Vector3(0.72f, 0.35f, 0.72f));
        }

        private void Update()
        {
            age += Time.deltaTime;
            if (age < delay) return;
            var activeAge = age - delay;
            const float riseDuration = 0.36f;
            const float holdDuration = 0.52f;
            const float vanishDuration = 0.78f;
            if (activeAge < riseDuration)
            {
                var progress = Mathf.SmoothStep(0f, 1f, activeAge / riseDuration);
                transform.position = Vector3.LerpUnclamped(startPosition, finalPosition, progress);
                transform.localScale = Vector3.LerpUnclamped(
                    Vector3.Scale(finalScale, new Vector3(0.72f, 0.35f, 0.72f)), finalScale, progress);
                return;
            }
            if (activeAge < riseDuration + holdDuration) return;

            var vanish = Mathf.SmoothStep(0f, 1f,
                (activeAge - riseDuration - holdDuration) / vanishDuration);
            transform.position = Vector3.LerpUnclamped(finalPosition,
                finalPosition - Vector3.up * (height * 0.58f), vanish);
            transform.localScale = Vector3.LerpUnclamped(finalScale,
                Vector3.Scale(finalScale, new Vector3(0.08f, 0.02f, 0.08f)), vanish);
            if (vanish >= 1f) Destroy(gameObject);
        }

    }

    public sealed class CombatVfxMaterialCleanup : MonoBehaviour
    {
        private Material runtimeMaterial;

        public void Initialize(Material material) => runtimeMaterial = material;

        private void OnDestroy()
        {
            if (runtimeMaterial != null) Destroy(runtimeMaterial);
        }
    }

    public sealed class HeroBurnVisualController : MonoBehaviour
    {
        private Renderer[] renderers;
        private MaterialPropertyBlock[] originalBlocks;
        private MaterialPropertyBlock[] animatedBlocks;
        private Color[] baseColors;
        private ParticleSystem particles;
        private ParticleSystem.Particle[] particleBuffer = new ParticleSystem.Particle[32];
        private bool fading;
        private float fadeStartedAt;
        private float fadeDuration;

        public void Initialize(Transform subject, ParticleSystem requestedParticles)
        {
            particles = requestedParticles;
            transform.rotation = Quaternion.identity;
            renderers = subject.GetComponentsInChildren<Renderer>(true)
                .Where(item => item != null && !item.transform.IsChildOf(transform)).ToArray();
            originalBlocks = new MaterialPropertyBlock[renderers.Length];
            animatedBlocks = new MaterialPropertyBlock[renderers.Length];
            baseColors = new Color[renderers.Length];
            for (var index = 0; index < renderers.Length; index++)
            {
                originalBlocks[index] = new MaterialPropertyBlock();
                animatedBlocks[index] = new MaterialPropertyBlock();
                renderers[index].GetPropertyBlock(originalBlocks[index]);
                renderers[index].GetPropertyBlock(animatedBlocks[index]);
                var material = renderers[index].sharedMaterial;
                baseColors[index] = material != null && material.HasProperty("_BaseColor")
                    ? material.GetColor("_BaseColor")
                    : material != null && material.HasProperty("_Color") ? material.GetColor("_Color") : Color.white;
            }
        }

        private void LateUpdate()
        {
            transform.rotation = Quaternion.identity;
            var fade = fading ? 1f - Mathf.Clamp01((Time.time - fadeStartedAt) / fadeDuration) : 1f;
            var pulse = Mathf.SmoothStep(0f, 1f, Mathf.PingPong(Time.time * 4.5f, 1f)) * fade;
            for (var index = 0; index < renderers.Length; index++)
            {
                if (renderers[index] == null) continue;
                var tint = Color.Lerp(baseColors[index], new Color(1.8f, 0.03f, 0.015f, baseColors[index].a), pulse);
                animatedBlocks[index].SetColor("_BaseColor", tint);
                animatedBlocks[index].SetColor("_Color", tint);
                renderers[index].SetPropertyBlock(animatedBlocks[index]);
            }
            SetParticleAlpha(fade);
            if (fading && fade <= 0f) Destroy(gameObject);
        }

        public void BeginFadeOut(float duration)
        {
            if (fading) return;
            fading = true;
            fadeStartedAt = Time.time;
            fadeDuration = Mathf.Max(0.05f, duration);
            if (particles != null) particles.Stop(true, ParticleSystemStopBehavior.StopEmitting);
        }

        public void Resume()
        {
            fading = false;
            SetParticleAlpha(1f);
            if (particles != null && !particles.isPlaying) particles.Play();
        }

        private void SetParticleAlpha(float alpha)
        {
            if (particles == null) return;
            var count = particles.particleCount;
            if (particleBuffer.Length < count) particleBuffer = new ParticleSystem.Particle[count];
            count = particles.GetParticles(particleBuffer);
            var byteAlpha = (byte)Mathf.RoundToInt(Mathf.Clamp01(alpha) * 255f);
            for (var index = 0; index < count; index++)
            {
                var color = particleBuffer[index].startColor;
                color.a = byteAlpha;
                particleBuffer[index].startColor = color;
            }
            particles.SetParticles(particleBuffer, count);
        }

        private void OnDestroy()
        {
            if (renderers == null) return;
            for (var index = 0; index < renderers.Length; index++)
                if (renderers[index] != null) renderers[index].SetPropertyBlock(originalBlocks[index]);
        }
    }

    public sealed class CombatVfxParticleFade : MonoBehaviour
    {
        private ParticleSystem particles;
        private ParticleSystem.Particle[] particleBuffer = new ParticleSystem.Particle[64];
        private float delay;
        private float duration;
        private float age;
        private bool stopping;

        public void Initialize(ParticleSystem requestedParticles, float requestedDelay, float requestedDuration)
        {
            particles = requestedParticles;
            delay = Mathf.Max(0f, requestedDelay);
            duration = Mathf.Max(0.05f, requestedDuration);
            var main = particles.main;
            main.stopAction = ParticleSystemStopAction.None;
        }

        private void Update()
        {
            age += Time.deltaTime;
            if (age < delay) return;
            if (!stopping)
            {
                stopping = true;
                particles.Stop(true, ParticleSystemStopBehavior.StopEmitting);
            }
            var fade = 1f - Mathf.Clamp01((age - delay) / duration);
            var count = particles.particleCount;
            if (particleBuffer.Length < count) particleBuffer = new ParticleSystem.Particle[count];
            count = particles.GetParticles(particleBuffer);
            var byteAlpha = (byte)Mathf.RoundToInt(fade * 255f);
            for (var index = 0; index < count; index++)
            {
                var color = particleBuffer[index].startColor;
                color.a = byteAlpha;
                particleBuffer[index].startColor = color;
            }
            particles.SetParticles(particleBuffer, count);
            if (fade <= 0f) Destroy(gameObject);
        }
    }

    public sealed class FireballGroundDecal : MonoBehaviour
    {
        private Material runtimeMaterial;
        private Color baseColor = Color.white;
        private Color emissionColor = Color.black;
        private float lifetime;
        private float age;

        public void Initialize(Material source, float requestedLifetime)
        {
            lifetime = Mathf.Max(1f, requestedLifetime);
            runtimeMaterial = new Material(source) { hideFlags = HideFlags.DontSave };
            var renderer = GetComponent<Renderer>();
            renderer.sharedMaterial = runtimeMaterial;
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            if (runtimeMaterial.HasProperty("_BaseColor")) baseColor = runtimeMaterial.GetColor("_BaseColor");
            else if (runtimeMaterial.HasProperty("_Color")) baseColor = runtimeMaterial.GetColor("_Color");
            if (runtimeMaterial.HasProperty("_EmissionColor"))
                emissionColor = runtimeMaterial.GetColor("_EmissionColor");
        }

        private void Update()
        {
            age += Time.deltaTime;
            var normalized = Mathf.Clamp01(age / lifetime);
            var alpha = 1f - Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.58f, 1f, normalized));
            var fadedBase = baseColor;
            fadedBase.a *= alpha;
            if (runtimeMaterial.HasProperty("_BaseColor")) runtimeMaterial.SetColor("_BaseColor", fadedBase);
            if (runtimeMaterial.HasProperty("_Color")) runtimeMaterial.SetColor("_Color", fadedBase);
            if (runtimeMaterial.HasProperty("_EmissionColor"))
                runtimeMaterial.SetColor("_EmissionColor", emissionColor * alpha);
            if (age >= lifetime) Destroy(gameObject);
        }

        private void OnDestroy()
        {
            if (runtimeMaterial != null) Destroy(runtimeMaterial);
        }
    }

    public sealed class HeroProjectileVisual : MonoBehaviour
    {
        private const float Speed = 13f;
        private static readonly List<HeroProjectileVisual> Active = new();
        private Transform target;
        private Vector3 destination;
        private Color color;
        private float impactSize;
        private float age;
        private Material material;
        private Material trailMaterial;
        private bool impactOnArrival;
        private bool arcaneBolt;
        private bool fireball;
        private float lingerDuration;
        private Vector3 travelDirection;

        public static void Spawn(Vector3 origin, Vector3 destination, Transform target, Color color,
            float scale, float impactSize, bool impactOnArrival = true, bool arcaneBolt = false,
            bool fireball = false, float lingerDuration = 0f)
        {
            var root = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            root.name = "Hero Projectile VFX";
            var collider = root.GetComponent<Collider>();
            if (collider != null) Destroy(collider);
            root.transform.position = origin;
            root.transform.localScale = Vector3.one * scale;
            var visual = root.AddComponent<HeroProjectileVisual>();
            visual.target = target;
            visual.destination = destination;
            visual.color = color;
            visual.impactSize = impactSize;
            visual.impactOnArrival = impactOnArrival;
            visual.arcaneBolt = arcaneBolt;
            visual.fireball = fireball;
            visual.lingerDuration = lingerDuration;
            visual.travelDirection = (destination - origin).sqrMagnitude > 0.001f
                ? (destination - origin).normalized : Vector3.forward;
            visual.material = HeroCombatVfx.CreateMaterial(color, false, fireball ? 9f : 2.5f);
            root.GetComponent<Renderer>().material = visual.material;
            var trail = root.AddComponent<TrailRenderer>();
            trail.time = 0.22f;
            trail.minVertexDistance = 0.03f;
            trail.widthCurve = new AnimationCurve(new Keyframe(0f, scale * 0.9f), new Keyframe(1f, 0f));
            trail.startColor = color * 1.7f;
            trail.endColor = new Color(color.r, color.g, color.b, 0f);
            visual.trailMaterial = HeroCombatVfx.CreateMaterial(color, true, fireball ? 6f : 2.5f);
            trail.material = visual.trailMaterial;
            trail.numCornerVertices = 3;
            if (arcaneBolt)
            {
                trail.time = 0.3f;
                trail.widthMultiplier = 1.25f;
                var glow = root.AddComponent<Light>();
                glow.type = LightType.Point;
                glow.color = color;
                glow.intensity = 5f;
                glow.range = 3.2f;
                glow.shadows = LightShadows.None;
            }
            else if (fireball)
            {
                trail.time = 0.42f;
                trail.widthMultiplier = 1.45f;
                var glow = root.AddComponent<Light>();
                glow.type = LightType.Point;
                glow.color = color;
                glow.intensity = 12f;
                glow.range = 6f;
                glow.shadows = LightShadows.None;
            }
        }

        private void OnEnable() => Active.Add(this);

        private void OnDisable() => Active.Remove(this);

        public static void ResolveArcaneImpact(Vector3 position, Vector3 direction)
        {
            HeroProjectileVisual closest = null;
            var closestDistance = float.MaxValue;
            foreach (var candidate in Active)
            {
                if (candidate == null || !candidate.arcaneBolt) continue;
                var alignment = Vector3.Dot(candidate.travelDirection, direction);
                if (alignment < 0.5f) continue;
                var distance = (candidate.transform.position - position).sqrMagnitude + (1f - alignment) * 10f;
                if (distance >= closestDistance) continue;
                closestDistance = distance;
                closest = candidate;
            }
            if (closest == null) return;
            closest.transform.position = position;
            Destroy(closest.gameObject);
        }

        private void Update()
        {
            age += Time.deltaTime;
            if (target != null) destination = target.position + Vector3.up * 0.75f;
            var offset = destination - transform.position;
            var step = Speed * (arcaneBolt ? 2f : 1f) * Time.deltaTime;
            if (offset.magnitude <= step || age >= 4f)
            {
                if (impactOnArrival)
                {
                    if (fireball)
                        HeroCombatVfx.SpawnFireballImpact(destination - Vector3.up * 0.75f,
                            impactSize, lingerDuration);
                    else
                    {
                        HeroCombatVfx.SpawnBurst(destination, color, impactSize);
                        HeroCombatVfx.SpawnRing(destination - Vector3.up * 0.7f, color, impactSize, 0.4f);
                    }
                }
                Destroy(gameObject);
                return;
            }
            transform.position += offset.normalized * step;
            transform.localScale *= 1f + Mathf.Sin(age * 22f) * 0.003f;
        }

        private void OnDestroy()
        {
            if (material != null) Destroy(material);
            if (trailMaterial != null) Destroy(trailMaterial);
        }
    }

    public sealed class CombatVfxRing : MonoBehaviour
    {
        private LineRenderer line;
        private Material material;
        private Color color;
        private float radius;
        private float lifetime;
        private float age;

        public void Initialize(Color requestedColor, float requestedRadius, float requestedLifetime,
            float glowMultiplier)
        {
            color = requestedColor;
            radius = requestedRadius;
            lifetime = requestedLifetime;
            line = gameObject.AddComponent<LineRenderer>();
            line.loop = true;
            line.useWorldSpace = false;
            line.positionCount = 48;
            line.numCornerVertices = 2;
            material = HeroCombatVfx.CreateMaterial(color, true, glowMultiplier);
            line.material = material;
        }

        private void Update()
        {
            age += Time.deltaTime;
            var t = Mathf.Clamp01(age / Mathf.Max(0.05f, lifetime));
            var currentRadius = Mathf.Lerp(0.15f, radius, 1f - (1f - t) * (1f - t));
            for (var i = 0; i < line.positionCount; i++)
            {
                var angle = i * Mathf.PI * 2f / line.positionCount;
                line.SetPosition(i, new Vector3(Mathf.Cos(angle) * currentRadius, 0f, Mathf.Sin(angle) * currentRadius));
            }
            var faded = color;
            faded.a = 1f - t;
            line.startColor = faded * 1.8f;
            line.endColor = faded;
            line.widthMultiplier = Mathf.Lerp(0.2f, 0.02f, t);
            if (t >= 1f) Destroy(gameObject);
        }

        private void OnDestroy() { if (material != null) Destroy(material); }
    }

    public sealed class CombatVfxLightning : MonoBehaviour
    {
        private LineRenderer line;
        private Material material;
        private Vector3 start;
        private Vector3 end;
        private float age;

        public void Initialize(Vector3 requestedStart, Vector3 requestedEnd, Color color)
        {
            start = requestedStart;
            end = requestedEnd;
            line = gameObject.AddComponent<LineRenderer>();
            line.positionCount = 12;
            line.widthMultiplier = 0.09f;
            line.startColor = color * 2f;
            line.endColor = Color.white;
            material = HeroCombatVfx.CreateMaterial(color, true);
            line.material = material;
        }

        private void Update()
        {
            age += Time.deltaTime;
            for (var i = 0; i < line.positionCount; i++)
            {
                var t = i / (float)(line.positionCount - 1);
                var jitter = i == 0 || i == line.positionCount - 1 ? Vector3.zero :
                    new Vector3(Mathf.Sin(i * 9.1f + age * 50f), Mathf.Cos(i * 5.3f + age * 45f),
                        Mathf.Sin(i * 7.7f + age * 55f)) * 0.12f;
                line.SetPosition(i, Vector3.Lerp(start, end, t) + jitter);
            }
            line.widthMultiplier = Mathf.Lerp(0.09f, 0f, age / 0.24f);
            if (age >= 0.24f) Destroy(gameObject);
        }

        private void OnDestroy() { if (material != null) Destroy(material); }
    }

    /// <summary>Runtime effect browser for every spawned hero and enemy in DebugScene.</summary>
    public sealed class CoreVfxDebugLab : MonoBehaviour
    {
        private readonly List<Transform> subjects = new();
        private int subjectIndex;
        private int previewIndex;
        private bool visible;
        private float nextRefresh;

        public static void EnsureExists()
        {
            if (FindFirstObjectByType<CoreVfxDebugLab>() != null) return;
            new GameObject("VFX Debug Lab").AddComponent<CoreVfxDebugLab>();
        }

        private void Update()
        {
            var keyboard = Keyboard.current;
            if (keyboard == null) return;
            if (keyboard.f6Key.wasPressedThisFrame) visible = !visible;
            if (!visible) return;
            if (keyboard.f7Key.wasPressedThisFrame) ChangeSubject(-1);
            if (keyboard.f8Key.wasPressedThisFrame) ChangeSubject(1);
            if (keyboard.f9Key.wasPressedThisFrame) ChangePreview(-1);
            if (keyboard.f10Key.wasPressedThisFrame) ChangePreview(1);
            if (keyboard.f11Key.wasPressedThisFrame) PlaySelected();
        }

        private void OnGUI()
        {
            if (!visible) return;
            RefreshSubjects();
            GUILayout.BeginArea(new Rect(Screen.width - 370f, 16f, 350f, 270f), "VFX DEBUG LAB", GUI.skin.window);
            GUILayout.Label("Testuje lokalnie prezentację — bez obrażeń i cooldownów.");
            GUILayout.Space(5f);
            GUILayout.Label($"Obiekt: {CurrentSubjectName}");
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("F7  Poprzedni")) ChangeSubject(-1);
            if (GUILayout.Button("F8  Następny")) ChangeSubject(1);
            GUILayout.EndHorizontal();
            GUILayout.Label($"Efekt: {(CombatVfxPreview)previewIndex}");
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("F9  Poprzedni")) ChangePreview(-1);
            if (GUILayout.Button("F10  Następny")) ChangePreview(1);
            GUILayout.EndHorizontal();
            if (GUILayout.Button("F11  ODTWÓRZ EFEKT", GUILayout.Height(34f))) PlaySelected();
            GUILayout.Space(4f);
            GUILayout.Label($"Aktywne postacie i enemies: {subjects.Count}");
            GUILayout.Label("F6 zamyka laboratorium");
            GUILayout.EndArea();
        }

        private string CurrentSubjectName => subjects.Count == 0 ? "brak aktywnych obiektów" :
            $"{subjects[subjectIndex].name} ({SubjectKind(subjects[subjectIndex])})";

        private void RefreshSubjects()
        {
            if (Time.unscaledTime < nextRefresh) return;
            nextRefresh = Time.unscaledTime + 0.5f;
            var selected = subjects.Count > 0 && subjectIndex < subjects.Count ? subjects[subjectIndex] : null;
            subjects.Clear();
            subjects.AddRange(FindObjectsByType<NetworkWarrior>(FindObjectsSortMode.None).Select(hero => hero.transform));
            subjects.AddRange(FindObjectsByType<EnemyBrain>(FindObjectsSortMode.None).Select(enemy => enemy.transform));
            subjects.RemoveAll(subject => subject == null);
            subjects.Sort((left, right) => string.Compare(left.name, right.name, StringComparison.Ordinal));
            subjectIndex = selected != null ? Mathf.Max(0, subjects.IndexOf(selected)) :
                Mathf.Clamp(subjectIndex, 0, Mathf.Max(0, subjects.Count - 1));
        }

        private void ChangeSubject(int direction)
        {
            RefreshSubjects();
            if (subjects.Count > 0) subjectIndex = (subjectIndex + direction + subjects.Count) % subjects.Count;
        }

        private void ChangePreview(int direction)
        {
            var count = Enum.GetValues(typeof(CombatVfxPreview)).Length;
            previewIndex = (previewIndex + direction + count) % count;
        }

        private void PlaySelected()
        {
            RefreshSubjects();
            if (subjects.Count > 0) HeroCombatVfx.Preview((CombatVfxPreview)previewIndex, subjects[subjectIndex]);
        }

        private static string SubjectKind(Transform subject) =>
            subject.GetComponent<NetworkWarrior>() != null ? "Hero" : "Enemy";
    }
}
