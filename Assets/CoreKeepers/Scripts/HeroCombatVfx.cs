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
        Electric3x4,
        DarkEnergy8x5,
        Flame8x4,
        Fireball8x5,
        Smoke8x5
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
                            CombatVfxAtlas.Fireball8x5, 1.65f);
                    var projectileDestination = isFireball && target == null ? point + Vector3.up * 0.75f : targetPoint;
                    HeroProjectileVisual.Spawn(origin, isArcaneBolt ? point : projectileDestination,
                        isArcaneBolt ? null : target, color,
                        skill.Effect == HeroSkillEffect.ExplodingProjectile ? 0.32f : isArcaneBolt ? 0.14f : 0.2f,
                        isFireball ? skill.Radius : 0.8f,
                        !isArcaneBolt, isArcaneBolt, isFireball, skill.Duration);
                    break;
                case HeroSkillEffect.ChainDamage:
                    SpawnLightning(origin, targetPoint, color, 3f);
                    SpawnBurst(targetPoint, color, 0.65f, CombatVfxAtlas.Electric3x4);
                    break;
                case HeroSkillEffect.Blink:
                    break;
                case HeroSkillEffect.GroundImpact:
                    if (skill.StableId == 110)
                        SpawnMeteorStrike(point, skill.Radius, skill.Duration, skill.SecondaryValue);
                    else
                    {
                        SpawnRing(point, color, Mathf.Max(1f, skill.Radius), 0.8f);
                        SpawnBurst(point + Vector3.up * 0.2f, color, 1.5f, CombatVfxAtlas.Explosion9x9);
                    }
                    break;
                case HeroSkillEffect.Vortex:
                    if (skill.StableId == 111)
                        SpawnGravityVortex(point, Mathf.Max(1f, skill.Radius), Mathf.Max(0.5f, skill.Duration));
                    else
                    {
                        SpawnRing(point, color, Mathf.Max(1f, skill.Radius), 0.8f);
                        SpawnBurst(point + Vector3.up * 0.2f, color, 1.5f, CombatVfxAtlas.DarkEnergy8x5);
                    }
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
                    SpawnProjectileMuzzleFlash(center, forward, Fire, CombatVfxAtlas.Fireball8x5, 1.65f);
                    HeroProjectileVisual.Spawn(center, destination, null, Fire, 0.34f, 1.75f,
                        true, false, true, 5f); break;
                case CombatVfxPreview.FrostNova:
                    SpawnFrostNova(subject.position, 4.5f); break;
                case CombatVfxPreview.ChainLightning:
                    SpawnLightning(center, destination, Frost, 3f);
                    SpawnBurst(destination, Frost, 0.7f, CombatVfxAtlas.Electric3x4); break;
                case CombatVfxPreview.Heal:
                    SpawnRing(subject.position, Holy, 3f, 0.9f);
                    SpawnBurst(center, Holy, 1.1f, CombatVfxAtlas.EnergyExplosion5x4); break;
                case CombatVfxPreview.BuffAura:
                    SpawnRing(subject.position, Nature, 2.5f, 1.1f);
                    SpawnBurst(center, Nature, 0.9f, CombatVfxAtlas.EnergyExplosion8x8); break;
                case CombatVfxPreview.GroundImpact:
                    SpawnMeteorStrike(subject.position, 3.5f, 5f, 2f); break;
                case CombatVfxPreview.Vortex:
                    SpawnGravityVortex(subject.position, 4.5f, 6f); break;
                default:
                    SpawnBurst(center, Color.white, 0.75f); break;
            }
        }

        public static void PlayProjectileImpact(int stableId, Vector3 position, Vector3 direction)
        {
            if (direction.sqrMagnitude < 0.001f) direction = Vector3.forward;
            direction.Normalize();
            if (stableId == 102)
            {
                position.y = 0f;
                HeroProjectileVisual.ResolveFireballImpact(position, direction);
                return;
            }
            if (stableId != 101) return;
            HeroProjectileVisual.ResolveArcaneImpact(position, direction);
            SpawnBurst(position, Arcane, 0.72f, CombatVfxAtlas.EnergyExplosion8x8);
            SpawnDirectionalSparkles(position, direction, Arcane);
        }

        public static void DismissProjectile(int stableId, Vector3 position, Vector3 direction)
        {
            if (stableId == 101)
                HeroProjectileVisual.ResolveArcaneImpact(position, direction);
            else
                HeroProjectileVisual.ResolveProjectileDismiss(position, direction);
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
            groundPosition.y = 0f;
            radius = Mathf.Max(0.5f, radius);
            duration = Mathf.Max(1f, duration);
            SpawnBurst(groundPosition + Vector3.up * 0.45f, Fire, 1.65f, CombatVfxAtlas.Explosion9x9);
            SpawnRing(groundPosition, Fire, radius, 0.5f, 10f);
            SpawnBurningGround(groundPosition, radius, duration);
        }

        private static void SpawnMeteorStrike(Vector3 groundPosition, float radius, float groundDuration,
            float impactDelay)
        {
            groundPosition.y = 0f;
            var prefab = Resources.Load<GameObject>("VFX/MeteorStrikeVfx");
            if (prefab == null)
            {
                Debug.LogError("MeteorStrikeVfx prefab was not found in Resources/VFX.");
                return;
            }
            var instance = UnityEngine.Object.Instantiate(prefab, groundPosition, Quaternion.identity);
            instance.name = prefab.name;
            var controller = instance.GetComponent<MeteorStrikeVfx>();
            if (controller != null)
                controller.Initialize(Mathf.Max(0.5f, radius), Mathf.Max(1f, groundDuration),
                    Mathf.Max(0.1f, impactDelay));
        }

        private static void SpawnFrostNova(Vector3 position, float radius)
        {
            // Layer the nova like a sheet of ice breaking from a white-hot centre:
            // a fast outer shockwave, a tighter glassy ring and visible ground fractures.
            SpawnRing(position, new Color(0.34f, 0.86f, 1f), radius, 0.82f, 11f);
            SpawnRing(position + Vector3.up * 0.025f, Color.white, radius * 0.72f, 0.64f, 14f);
            SpawnBurst(position + Vector3.up * 0.28f, Color.white,
                Mathf.Clamp(radius * 0.36f, 1.2f, 2f), CombatVfxAtlas.EnergyExplosion5x4);
            SpawnFrostGround(position, radius);
            SpawnFrostFractures(position, radius);
            SpawnFrostShards(position, radius);
            SpawnFrostWind(position, radius);
            SpawnFrostSpikes(position, radius);
        }

        private static void SpawnGravityVortex(Vector3 position, float radius, float duration)
        {
            position.y = 0f;
            var vortexColor = new Color(0.78f, 0.04f, 1f, 1f);
            SpawnBurst(position + Vector3.up * 0.16f, vortexColor,
                Mathf.Clamp(radius * 0.5f, 1.5f, 2.5f), CombatVfxAtlas.DarkEnergy8x5);
            var root = new GameObject("Gravity Vortex - Spiral Field");
            root.transform.position = position + Vector3.up * 0.055f;
            root.AddComponent<GravityVortexVisual>().Initialize(radius, duration);
        }

        private static void SpawnFrostGround(Vector3 position, float radius)
        {
            EnsureMaterialLibrary();
            if (materialLibrary == null || materialLibrary.IceGround == null)
            {
                Debug.LogWarning("IceGround material is missing from the Combat VFX material library.");
                return;
            }

            SpawnGroundQuad("Frost Nova - Ice Ground", position, radius, materialLibrary.IceGround, 15f, 3f);
        }

        internal static void SpawnMeteorGround(Vector3 position, float radius)
        {
            EnsureMaterialLibrary();
            if (materialLibrary == null || materialLibrary.MeteorGround == null)
            {
                Debug.LogWarning("MeteorGround material is missing from the Combat VFX material library.");
                return;
            }

            SpawnGroundQuad("Meteor Strike - Meteor Ground", position, radius, materialLibrary.MeteorGround,
                15f, 3f);
        }

        private static void SpawnGroundQuad(string objectName, Vector3 position, float radius, Material material,
            float fadeDelay, float fadeDuration)
        {
            var ground = GameObject.CreatePrimitive(PrimitiveType.Quad);
            ground.name = objectName;
            var collider = ground.GetComponent<Collider>();
            if (collider != null) UnityEngine.Object.Destroy(collider);
            ground.transform.SetPositionAndRotation(new Vector3(position.x, 0.03f, position.z),
                Quaternion.Euler(90f, 0f, 0f) *
                Quaternion.AngleAxis(UnityEngine.Random.Range(0f, 360f), Vector3.forward));
            ground.transform.localScale = Vector3.one * (Mathf.Max(1f, radius) * 2f);
            var renderer = ground.GetComponent<Renderer>();
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            ground.AddComponent<CombatGroundFade>().Initialize(material, fadeDelay, fadeDuration);
        }

        private static void SpawnFrostFractures(Vector3 position, float radius)
        {
            var root = new GameObject("Frost Nova - Cracked Ice");
            root.transform.position = position + Vector3.up * 0.085f;
            root.AddComponent<FrostNovaFractureVisual>().Initialize(radius);
        }

        private static void SpawnFrostShards(Vector3 position, float radius)
        {
            var root = new GameObject("Frost Nova - Ice Shards");
            root.transform.position = position + Vector3.up * 0.2f;
            var particles = root.AddComponent<ParticleSystem>();
            particles.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var main = particles.main;
            main.duration = 0.35f;
            main.loop = false;
            main.startLifetime = new ParticleSystem.MinMaxCurve(0.42f, 0.78f);
            main.startSpeed = 0f;
            main.startSize = new ParticleSystem.MinMaxCurve(0.055f, 0.18f);
            main.startRotation = new ParticleSystem.MinMaxCurve(-Mathf.PI, Mathf.PI);
            main.startColor = new ParticleSystem.MinMaxGradient(Color.white,
                new Color(0.22f, 0.78f, 1f, 1f));
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.gravityModifier = 0.24f;
            main.stopAction = ParticleSystemStopAction.Destroy;
            main.maxParticles = 96;

            var emission = particles.emission;
            emission.enabled = false;

            var colorOverLifetime = particles.colorOverLifetime;
            colorOverLifetime.enabled = true;
            colorOverLifetime.color = new ParticleSystem.MinMaxGradient(new Gradient
            {
                colorKeys = new[]
                {
                    new GradientColorKey(Color.white, 0f),
                    new GradientColorKey(new Color(0.25f, 0.72f, 1f), 1f)
                },
                alphaKeys = new[]
                {
                    new GradientAlphaKey(0f, 0f), new GradientAlphaKey(1f, 0.06f),
                    new GradientAlphaKey(0.9f, 0.58f), new GradientAlphaKey(0f, 1f)
                }
            });
            var sizeOverLifetime = particles.sizeOverLifetime;
            sizeOverLifetime.enabled = true;
            sizeOverLifetime.size = new ParticleSystem.MinMaxCurve(1f,
                AnimationCurve.EaseInOut(0f, 0.35f, 1f, 1f));

            var renderer = particles.GetComponent<ParticleSystemRenderer>();
            renderer.renderMode = ParticleSystemRenderMode.Stretch;
            renderer.velocityScale = 0.035f;
            renderer.lengthScale = 3.4f;
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            var material = CreateMaterial(new Color(0.35f, 0.86f, 1f), true, 10f);
            renderer.material = material;
            root.AddComponent<CombatVfxMaterialCleanup>().Initialize(material);
            root.AddComponent<FrostShardBurstController>().Initialize(particles, radius);
            particles.Play();
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
                    UnityEngine.Random.Range(1.7f, 2.45f) / 3f, index * 0.018f,
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

        internal static bool TryGetAtlas(CombatVfxAtlas requested, Color color, out Material material,
            out int columns, out int rows)
        {
            EnsureMaterialLibrary();

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
                CombatVfxAtlas.Electric3x4 => materialLibrary != null ? materialLibrary.Electric3x4 : null,
                CombatVfxAtlas.DarkEnergy8x5 => materialLibrary != null ? materialLibrary.DarkEnergy8x5 : null,
                CombatVfxAtlas.Flame8x4 => materialLibrary != null ? materialLibrary.Flame8x4 : null,
                CombatVfxAtlas.Fireball8x5 => materialLibrary != null ? materialLibrary.Fireball8x5 : null,
                CombatVfxAtlas.Smoke8x5 => materialLibrary != null ? materialLibrary.Smoke8x5 : null,
                _ => null
            };
            (columns, rows) = requested switch
            {
                CombatVfxAtlas.Explosion9x9 => (9, 9),
                CombatVfxAtlas.EnergyExplosion8x8 => (8, 8),
                CombatVfxAtlas.EnergyExplosion5x4 => (5, 4),
                CombatVfxAtlas.Electric3x4 => (3, 4),
                CombatVfxAtlas.DarkEnergy8x5 => (8, 5),
                CombatVfxAtlas.Flame8x4 => (8, 4),
                CombatVfxAtlas.Fireball8x5 => (8, 5),
                CombatVfxAtlas.Smoke8x5 => (8, 5),
                _ => (1, 1)
            };
            return material != null;
        }

        private static void EnsureMaterialLibrary()
        {
            if (materialLibraryLoaded) return;
            materialLibraryLoaded = true;
            materialLibrary = Resources.Load<CombatVfxMaterialLibrary>("VFX/CombatVfxMaterialLibrary");
            if (materialLibrary == null)
                Debug.LogWarning("Combat VFX material library is missing. Procedural VFX fallback will be used.");
        }

        internal static FireballParticleSettings GetFireballParticleSettings(bool smoke)
        {
            return smoke
                ? new FireballParticleSettings
                {
                    lifetimeMin = 0.48f, lifetimeMax = 0.9f,
                    speedMin = 0.08f, speedMax = 0.32f,
                    sizeMin = 0.6f, sizeMax = 1.36f,
                    emissionRate = 117f, burstCount = 12,
                    shapeRadius = 0.01f, maxParticles = 1200,
                    startAlphaMin = 0.92f, startAlphaMax = 1f,
                    noiseStrength = 0.3f, noiseFrequency = 0.65f, noiseScrollSpeed = 0.5f,
                    verticalVelocityMin = 0.12f, verticalVelocityMax = 0.38f,
                    sortingOrder = -1
                }
                : new FireballParticleSettings
                {
                    lifetimeMin = 0.112f, lifetimeMax = 0.294f,
                    speedMin = 0.03f, speedMax = 0.24f,
                    sizeMin = 0.32f, sizeMax = 0.8f,
                    emissionRate = 378f, burstCount = 32,
                    shapeRadius = 0.01f, maxParticles = 2000,
                    startAlphaMin = 1f, startAlphaMax = 1f,
                    noiseStrength = 0.2f, noiseFrequency = 1.4f, noiseScrollSpeed = 1.5f,
                    sortingOrder = 1
                };
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

        public static void PlayChainLightningSegment(Vector3 start, Vector3 end)
        {
            SpawnLightning(start, end, Arcane, 3f);
            SpawnBurst(end, Arcane, 0.5f, CombatVfxAtlas.Electric3x4);
        }

        private static void SpawnLightning(Vector3 start, Vector3 end, Color color, float glowScale = 1f)
        {
            var root = new GameObject("Combat VFX - Lightning");
            root.AddComponent<CombatVfxLightning>().Initialize(start, end, color, glowScale);
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

    public sealed class GravityVortexVisual : MonoBehaviour
    {
        private readonly List<LineRenderer> arms = new();
        private readonly List<float> armWidths = new();
        private Material material;
        private Light centerLight;
        private float duration;
        private float age;

        public void Initialize(float requestedRadius, float requestedDuration)
        {
            var radius = Mathf.Max(1f, requestedRadius);
            duration = Mathf.Max(0.5f, requestedDuration);
            var violet = new Color(0.82f, 0.035f, 1f, 1f);
            material = HeroCombatVfx.CreateMaterial(violet, true, 14f);

            const int armCount = 9;
            const int pointsPerArm = 42;
            for (var armIndex = 0; armIndex < armCount; armIndex++)
            {
                var child = new GameObject($"Vortex Arm {armIndex + 1:00}");
                child.transform.SetParent(transform, false);
                var line = child.AddComponent<LineRenderer>();
                line.useWorldSpace = false;
                line.positionCount = pointsPerArm;
                line.numCornerVertices = 4;
                line.numCapVertices = 3;
                line.sharedMaterial = material;
                line.textureMode = LineTextureMode.Stretch;
                line.startColor = Color.white * 2.4f;
                line.endColor = violet * 1.8f;
                var width = radius * UnityEngine.Random.Range(0.035f, 0.062f);
                line.widthMultiplier = width;
                line.widthCurve = new AnimationCurve(
                    new Keyframe(0f, 0.05f), new Keyframe(0.18f, 1f),
                    new Keyframe(0.72f, 0.5f), new Keyframe(1f, 0.02f));
                line.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                line.receiveShadows = false;

                var armOffset = armIndex * 360f / armCount + UnityEngine.Random.Range(-8f, 8f);
                var turns = UnityEngine.Random.Range(1.65f, 2.2f);
                for (var pointIndex = 0; pointIndex < pointsPerArm; pointIndex++)
                {
                    var progress = pointIndex / (float)(pointsPerArm - 1);
                    var curvedProgress = Mathf.Pow(progress, 0.72f);
                    var distance = Mathf.Lerp(radius * 0.035f, radius, curvedProgress);
                    var angle = armOffset + progress * turns * 360f;
                    var wobble = Mathf.Sin(progress * Mathf.PI * 5f + armIndex) * radius * 0.018f;
                    var direction = Quaternion.Euler(0f, angle, 0f) * Vector3.forward;
                    var tangent = Vector3.Cross(Vector3.up, direction);
                    line.SetPosition(pointIndex, direction * distance + tangent * wobble);
                }
                arms.Add(line);
                armWidths.Add(width);
            }

            centerLight = gameObject.AddComponent<Light>();
            centerLight.type = LightType.Point;
            centerLight.color = new Color(0.82f, 0.05f, 1f);
            centerLight.intensity = 7f;
            centerLight.range = radius * 1.35f;
            centerLight.shadows = LightShadows.None;
            transform.localScale = Vector3.one * 0.12f;
        }

        private void Update()
        {
            age += Time.deltaTime;
            var reveal = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(age / 0.32f));
            var fade = Mathf.SmoothStep(0f, 1f,
                Mathf.InverseLerp(Mathf.Max(0f, duration - 1f), duration, age));
            var scale = Mathf.Lerp(0.12f, 1f, reveal);
            transform.localScale = Vector3.one * scale;
            transform.Rotate(0f, (105f + Mathf.Sin(age * 2f) * 18f) * Time.deltaTime, 0f, Space.Self);

            for (var index = 0; index < arms.Count; index++)
            {
                if (arms[index] == null) continue;
                arms[index].widthMultiplier = armWidths[index] * (1f - fade);
                var inner = Color.white * 2.4f;
                inner.a = 1f - fade;
                var outer = new Color(0.82f, 0.035f, 1f, 1f) * 1.8f;
                outer.a = 1f - fade;
                arms[index].startColor = inner;
                arms[index].endColor = outer;
            }
            if (centerLight != null)
                centerLight.intensity = (7f + Mathf.Sin(age * 7f) * 1.2f) * reveal * (1f - fade);
            if (age >= duration) Destroy(gameObject);
        }

        private void OnDestroy()
        {
            if (material != null) Destroy(material);
        }
    }

    public sealed class FrostNovaFractureVisual : MonoBehaviour
    {
        private sealed class CrackLine
        {
            public LineRenderer Renderer;
            public Vector3[] Points;
        }

        private readonly List<CrackLine> cracks = new();
        private Material material;
        private Color crackColor;
        private float age;

        public void Initialize(float requestedRadius)
        {
            var radius = Mathf.Max(1f, requestedRadius);
            crackColor = new Color(0.08f, 0.48f, 1f, 0.96f);
            material = HeroCombatVfx.CreateMaterial(crackColor, true, 12f);
            var angleOffset = UnityEngine.Random.Range(0f, 360f);

            const int crackCount = 14;
            for (var index = 0; index < crackCount; index++)
            {
                var angle = angleOffset + index * (360f / crackCount) + UnityEngine.Random.Range(-7f, 7f);
                var direction = Quaternion.Euler(0f, angle, 0f) * Vector3.forward;
                var tangent = Vector3.Cross(Vector3.up, direction);
                var pointCount = UnityEngine.Random.Range(4, 7);
                var points = new Vector3[pointCount];
                var endDistance = radius * UnityEngine.Random.Range(0.58f, 0.82f);
                for (var pointIndex = 0; pointIndex < pointCount; pointIndex++)
                {
                    var progress = pointIndex / (float)(pointCount - 1);
                    var distance = Mathf.Lerp(radius * UnityEngine.Random.Range(0.06f, 0.13f),
                        endDistance, progress);
                    var sideJitter = pointIndex == 0 ? 0f :
                        UnityEngine.Random.Range(-0.09f, 0.09f) * radius * progress;
                    points[pointIndex] = direction * distance + tangent * sideJitter;
                }
                CreateCrack($"Main Crack {index + 1:00}", points, radius * 0.007f);

                // Short forks make the silhouette read as fractured ice instead of a sunburst.
                if (index % 2 != 0) continue;
                var forkOrigin = points[UnityEngine.Random.Range(1, pointCount - 1)];
                var forkDirection = Quaternion.Euler(0f, UnityEngine.Random.Range(-48f, 48f), 0f) * direction;
                CreateCrack($"Crack Fork {index + 1:00}", new[]
                {
                    forkOrigin,
                    forkOrigin + forkDirection * radius * UnityEngine.Random.Range(0.1f, 0.18f) +
                    tangent * UnityEngine.Random.Range(-0.04f, 0.04f) * radius,
                    forkOrigin + forkDirection * radius * UnityEngine.Random.Range(0.2f, 0.3f)
                }, radius * 0.0046667f);
            }

            transform.localScale = new Vector3(0.58f, 1f, 0.58f);
        }

        private void CreateCrack(string crackName, Vector3[] points, float width)
        {
            var child = new GameObject(crackName);
            child.transform.SetParent(transform, false);
            var line = child.AddComponent<LineRenderer>();
            line.useWorldSpace = false;
            line.positionCount = points.Length;
            line.SetPositions(points);
            line.widthMultiplier = width;
            line.numCornerVertices = 1;
            line.numCapVertices = 1;
            line.sharedMaterial = material;
            line.startColor = crackColor;
            line.endColor = new Color(crackColor.r, crackColor.g, crackColor.b, crackColor.a * 0.42f);
            line.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            line.receiveShadows = false;
            cracks.Add(new CrackLine { Renderer = line, Points = points });
        }

        private void Update()
        {
            age += Time.deltaTime;
            var reveal = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(age / 0.18f));
            var erase = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.42f, 1.8f, age));
            var scale = Mathf.Lerp(0.58f, 1f, reveal);
            transform.localScale = new Vector3(scale, 1f, scale);
            for (var index = 0; index < cracks.Count; index++)
            {
                var crack = cracks[index];
                if (crack.Renderer == null) continue;
                EraseFromStart(crack, erase);
                var start = crackColor;
                start.a *= reveal * Mathf.Lerp(1f, 0.18f, erase);
                var end = start;
                end.a *= 0.42f;
                crack.Renderer.startColor = start;
                crack.Renderer.endColor = end;
            }
            if (age >= 1.8f) Destroy(gameObject);
        }

        private static void EraseFromStart(CrackLine crack, float progress)
        {
            var points = crack.Points;
            if (points == null || points.Length < 2) return;
            var scaledProgress = Mathf.Clamp01(progress) * (points.Length - 1);
            var segment = Mathf.Min(Mathf.FloorToInt(scaledProgress), points.Length - 2);
            var segmentProgress = scaledProgress - segment;
            var remainingPointCount = points.Length - segment;
            crack.Renderer.positionCount = remainingPointCount;
            crack.Renderer.SetPosition(0, Vector3.Lerp(points[segment], points[segment + 1], segmentProgress));
            for (var index = 1; index < remainingPointCount; index++)
                crack.Renderer.SetPosition(index, points[segment + index]);
        }

        private void OnDestroy()
        {
            if (material != null) Destroy(material);
        }
    }

    public sealed class FrostShardBurstController : MonoBehaviour
    {
        public void Initialize(ParticleSystem particles, float requestedRadius)
        {
            var radius = Mathf.Max(1f, requestedRadius);
            var count = Mathf.Clamp(Mathf.RoundToInt(radius * 13f), 42, 72);
            var angleOffset = UnityEngine.Random.Range(0f, 360f);
            for (var index = 0; index < count; index++)
            {
                var angle = angleOffset + index * (360f / count) + UnityEngine.Random.Range(-4f, 4f);
                var direction = Quaternion.Euler(0f, angle, 0f) * Vector3.forward;
                var emit = new ParticleSystem.EmitParams
                {
                    position = transform.position + direction * UnityEngine.Random.Range(0.05f, radius * 0.2f),
                    velocity = direction * UnityEngine.Random.Range(radius * 1.6f, radius * 2.75f) +
                               Vector3.up * UnityEngine.Random.Range(0.3f, 1.25f),
                    startSize = UnityEngine.Random.Range(0.055f, 0.18f),
                    startLifetime = UnityEngine.Random.Range(0.42f, 0.78f),
                    startColor = Color.Lerp(Color.white, new Color(0.22f, 0.78f, 1f),
                        UnityEngine.Random.Range(0.15f, 0.75f))
                };
                particles.Emit(emit, 1);
            }
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

    public sealed class CombatGroundFade : MonoBehaviour
    {
        private Material runtimeMaterial;
        private Color baseColor = Color.white;
        private float fadeDelay;
        private float fadeDuration;
        private float age;

        public void Initialize(Material source, float requestedFadeDelay, float requestedFadeDuration)
        {
            fadeDelay = Mathf.Max(0f, requestedFadeDelay);
            fadeDuration = Mathf.Max(0.05f, requestedFadeDuration);
            runtimeMaterial = new Material(source) { hideFlags = HideFlags.DontSave };
            ConfigureTransparentSurface(runtimeMaterial);

            var renderer = GetComponent<Renderer>();
            renderer.sharedMaterial = runtimeMaterial;
            if (runtimeMaterial.HasProperty("_BaseColor"))
                baseColor = runtimeMaterial.GetColor("_BaseColor");
            else if (runtimeMaterial.HasProperty("_Color"))
                baseColor = runtimeMaterial.GetColor("_Color");
        }

        private static void ConfigureTransparentSurface(Material target)
        {
            if (target.HasProperty("_Surface")) target.SetFloat("_Surface", 1f);
            if (target.HasProperty("_Blend")) target.SetFloat("_Blend", 0f);
            if (target.HasProperty("_SrcBlend"))
                target.SetFloat("_SrcBlend", (float)UnityEngine.Rendering.BlendMode.SrcAlpha);
            if (target.HasProperty("_DstBlend"))
                target.SetFloat("_DstBlend", (float)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
            if (target.HasProperty("_SrcBlendAlpha"))
                target.SetFloat("_SrcBlendAlpha", (float)UnityEngine.Rendering.BlendMode.One);
            if (target.HasProperty("_DstBlendAlpha"))
                target.SetFloat("_DstBlendAlpha", (float)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
            if (target.HasProperty("_ZWrite")) target.SetFloat("_ZWrite", 0f);
            target.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            target.DisableKeyword("_ALPHAPREMULTIPLY_ON");
            target.SetOverrideTag("RenderType", "Transparent");
            target.SetShaderPassEnabled("ShadowCaster", false);
            target.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;
        }

        private void Update()
        {
            age += Time.deltaTime;
            if (age < fadeDelay) return;

            var progress = Mathf.Clamp01((age - fadeDelay) / fadeDuration);
            var faded = baseColor;
            faded.a *= 1f - Mathf.SmoothStep(0f, 1f, progress);
            if (runtimeMaterial.HasProperty("_BaseColor")) runtimeMaterial.SetColor("_BaseColor", faded);
            if (runtimeMaterial.HasProperty("_Color")) runtimeMaterial.SetColor("_Color", faded);
            if (progress >= 1f) Destroy(gameObject);
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
        private bool resolved;

        public static void Spawn(Vector3 origin, Vector3 destination, Transform target, Color color,
            float scale, float impactSize, bool impactOnArrival = true, bool arcaneBolt = false,
            bool fireball = false, float lingerDuration = 0f)
        {
            if (arcaneBolt) scale *= 0.6f;
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
            root.transform.rotation = Quaternion.LookRotation(visual.travelDirection, Vector3.up);
            var sphereRenderer = root.GetComponent<Renderer>();
            if (fireball)
            {
                sphereRenderer.enabled = false;
                CreateFireballFlightParticles(root.transform, scale, color);
            }
            else
            {
                var glowMultiplier = arcaneBolt ? 6f : 2.5f;
                visual.material = HeroCombatVfx.CreateMaterial(color, false, glowMultiplier);
                sphereRenderer.material = visual.material;
                var trail = root.AddComponent<TrailRenderer>();
                trail.time = arcaneBolt ? 0.3f : 0.22f;
                trail.minVertexDistance = 0.03f;
                trail.widthCurve = new AnimationCurve(new Keyframe(0f, scale * 0.9f), new Keyframe(1f, 0f));
                trail.startColor = color * (arcaneBolt ? 2.5f : 1.7f);
                trail.endColor = new Color(color.r, color.g, color.b, 0f);
                visual.trailMaterial = HeroCombatVfx.CreateMaterial(color, true, glowMultiplier);
                trail.material = visual.trailMaterial;
                trail.numCornerVertices = 3;
                if (arcaneBolt) trail.widthMultiplier = 1.25f;
            }
            if (fireball)
            {
                var glow = root.AddComponent<Light>();
                glow.type = LightType.Point;
                glow.color = color;
                glow.intensity = 9f;
                glow.range = 4.8f;
                glow.shadows = LightShadows.None;
            }
        }

        private static void CreateFireballFlightParticles(Transform projectile, float projectileScale, Color fireColor)
        {
            var prefab = Resources.Load<GameObject>("VFX/FireballProjectileVfx");
            if (prefab != null)
            {
                var instance = Instantiate(prefab, projectile, false);
                instance.name = prefab.name;
                instance.transform.localPosition = Vector3.zero;
                instance.transform.localRotation = Quaternion.identity;
                instance.transform.localScale = Vector3.one / Mathf.Max(0.01f, projectileScale);
                return;
            }

            Debug.LogWarning("FireballProjectileVfx prefab was not found in Resources/VFX. Using generated fallback particles.");
            HeroCombatVfx.TryGetAtlas(CombatVfxAtlas.Fireball8x5, fireColor,
                out var flameMaterial, out var flameColumns, out var flameRows);
            HeroCombatVfx.TryGetAtlas(CombatVfxAtlas.Smoke8x5, Color.gray,
                out var smokeMaterial, out var smokeColumns, out var smokeRows);
            CreateFireballEmitter(projectile, "Fireball - Flames", projectileScale, flameMaterial,
                flameColumns, flameRows, false, HeroCombatVfx.GetFireballParticleSettings(false));
            CreateFireballEmitter(projectile, "Fireball - Smoke", projectileScale, smokeMaterial,
                smokeColumns, smokeRows, true, HeroCombatVfx.GetFireballParticleSettings(true));
        }

        private static void CreateFireballEmitter(Transform projectile, string name, float projectileScale,
            Material material, int columns, int rows, bool smoke, FireballParticleSettings settings)
        {
            settings ??= new FireballParticleSettings();
            var root = new GameObject(name);
            root.transform.SetParent(projectile, false);
            root.transform.localRotation = Quaternion.Euler(0f, 180f, 0f);
            var inverseScale = 1f / Mathf.Max(0.01f, projectileScale);
            root.transform.localScale = Vector3.one * inverseScale;
            var particles = root.AddComponent<ParticleSystem>();
            particles.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var main = particles.main;
            main.duration = 1f;
            main.loop = true;
            main.startLifetime = new ParticleSystem.MinMaxCurve(
                Mathf.Min(settings.lifetimeMin, settings.lifetimeMax),
                Mathf.Max(settings.lifetimeMin, settings.lifetimeMax));
            main.startSpeed = new ParticleSystem.MinMaxCurve(
                Mathf.Min(settings.speedMin, settings.speedMax),
                Mathf.Max(settings.speedMin, settings.speedMax));
            main.startSize = new ParticleSystem.MinMaxCurve(
                Mathf.Min(settings.sizeMin, settings.sizeMax),
                Mathf.Max(settings.sizeMin, settings.sizeMax));
            main.startRotation = new ParticleSystem.MinMaxCurve(-Mathf.PI, Mathf.PI);
            main.startColor = smoke
                ? new ParticleSystem.MinMaxGradient(
                    new Color(0.12f, 0.1f, 0.09f, settings.startAlphaMin),
                    new Color(0.34f, 0.25f, 0.2f, settings.startAlphaMax))
                : new ParticleSystem.MinMaxGradient(
                    new Color(1f, 1f, 1f, settings.startAlphaMin),
                    new Color(1f, 0.22f, 0.015f, settings.startAlphaMax));
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.scalingMode = ParticleSystemScalingMode.Hierarchy;
            main.maxParticles = Mathf.Max(1, settings.maxParticles);
            var emission = particles.emission;
            emission.rateOverTime = Mathf.Max(0f, settings.emissionRate);
            emission.SetBursts(new[]
            {
                new ParticleSystem.Burst(0f, (short)Mathf.Clamp(settings.burstCount, 0, short.MaxValue))
            });
            var shape = particles.shape;
            shape.shapeType = ParticleSystemShapeType.Sphere;
            shape.radius = Mathf.Max(0.001f, settings.shapeRadius);
            shape.radiusThickness = 1f;
            var colorOverLifetime = particles.colorOverLifetime;
            colorOverLifetime.enabled = true;
            colorOverLifetime.color = new ParticleSystem.MinMaxGradient(new Gradient
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
                        new GradientAlphaKey(0.15f, 0f), new GradientAlphaKey(1f, 0.08f),
                        new GradientAlphaKey(0.85f, 0.62f), new GradientAlphaKey(0f, 1f)
                    }
                    : new[]
                    {
                        new GradientAlphaKey(0f, 0f), new GradientAlphaKey(1f, 0.08f),
                        new GradientAlphaKey(0f, 1f)
                    }
            });
            if (smoke)
            {
                var velocity = particles.velocityOverLifetime;
                velocity.enabled = true;
                velocity.space = ParticleSystemSimulationSpace.World;
                velocity.y = (settings.verticalVelocityMin + settings.verticalVelocityMax) * 0.5f;
                var noise = particles.noise;
                noise.enabled = true;
                noise.strength = Mathf.Max(0f, settings.noiseStrength);
                noise.frequency = Mathf.Max(0f, settings.noiseFrequency);
                noise.scrollSpeed = settings.noiseScrollSpeed;
            }
            else
            {
                var noise = particles.noise;
                noise.enabled = true;
                noise.strength = Mathf.Max(0f, settings.noiseStrength);
                noise.frequency = Mathf.Max(0f, settings.noiseFrequency);
                noise.scrollSpeed = settings.noiseScrollSpeed;
            }
            var renderer = particles.GetComponent<ParticleSystemRenderer>();
            renderer.renderMode = ParticleSystemRenderMode.Billboard;
            renderer.sortingOrder = settings.sortingOrder;
            renderer.sharedMaterial = material;
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            if (material != null && columns > 1 && rows > 1)
            {
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
            particles.Play();
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

        public static void ResolveFireballImpact(Vector3 groundPosition, Vector3 direction)
        {
            HeroProjectileVisual closest = null;
            var closestDistance = float.MaxValue;
            foreach (var candidate in Active)
            {
                if (candidate == null || !candidate.fireball || candidate.resolved) continue;
                var alignment = Vector3.Dot(candidate.travelDirection, direction);
                if (alignment < 0.35f) continue;
                var expectedPosition = groundPosition + Vector3.up * 0.75f;
                var distance = (candidate.transform.position - expectedPosition).sqrMagnitude +
                               (1f - alignment) * 4f;
                if (distance >= closestDistance) continue;
                closestDistance = distance;
                closest = candidate;
            }
            if (closest != null) closest.CompleteFireballImpact(groundPosition);
        }

        public static void ResolveProjectileDismiss(Vector3 position, Vector3 direction)
        {
            HeroProjectileVisual closest = null;
            var closestDistance = float.MaxValue;
            foreach (var candidate in Active)
            {
                if (candidate == null || candidate.arcaneBolt || candidate.resolved) continue;
                var alignment = Vector3.Dot(candidate.travelDirection, direction);
                if (alignment < 0.35f) continue;
                var distance = (candidate.transform.position - position).sqrMagnitude + (1f - alignment) * 4f;
                if (distance >= closestDistance) continue;
                closestDistance = distance;
                closest = candidate;
            }
            if (closest == null) return;
            closest.resolved = true;
            if (closest.fireball) closest.ReleaseFireballParticles();
            Destroy(closest.gameObject);
        }

        private void Update()
        {
            if (resolved) return;
            age += Time.deltaTime;
            if (target != null) destination = target.position + Vector3.up * 0.75f;
            var offset = destination - transform.position;
            var step = Speed * (arcaneBolt ? 2f : 1f) * Time.deltaTime;
            if (offset.magnitude <= step || age >= 4f)
            {
                if (fireball)
                {
                    CompleteFireballImpact(destination - Vector3.up * 0.75f);
                    return;
                }
                if (impactOnArrival)
                {
                    HeroCombatVfx.SpawnBurst(destination, color, impactSize);
                    HeroCombatVfx.SpawnRing(destination - Vector3.up * 0.7f, color, impactSize, 0.4f);
                }
                Destroy(gameObject);
                return;
            }
            travelDirection = offset.normalized;
            transform.rotation = Quaternion.LookRotation(travelDirection, Vector3.up);
            transform.position += travelDirection * step;
            transform.localScale *= 1f + Mathf.Sin(age * 22f) * 0.003f;
        }

        private void CompleteFireballImpact(Vector3 groundPosition)
        {
            if (resolved) return;
            resolved = true;
            groundPosition.y = 0f;
            transform.position = groundPosition + Vector3.up * 0.75f;
            if (impactOnArrival)
                HeroCombatVfx.SpawnFireballImpact(groundPosition, impactSize, lingerDuration);
            ReleaseFireballParticles();
            Destroy(gameObject);
        }

        private void ReleaseFireballParticles()
        {
            var systems = GetComponentsInChildren<ParticleSystem>(true);
            if (systems.Length == 0) return;

            var prefabRoot = transform.Find("FireballProjectileVfx");
            GameObject releasedRoot;
            if (prefabRoot != null)
            {
                prefabRoot.SetParent(null, true);
                releasedRoot = prefabRoot.gameObject;
            }
            else
            {
                releasedRoot = new GameObject("FireballProjectileVfx - Fading");
                releasedRoot.transform.SetPositionAndRotation(transform.position, transform.rotation);
                foreach (var system in systems)
                    system.transform.SetParent(releasedRoot.transform, true);
            }

            var maximumRemainingLifetime = 0f;
            foreach (var system in systems)
            {
                maximumRemainingLifetime = Mathf.Max(maximumRemainingLifetime,
                    system.main.startLifetime.constantMax);
                system.Stop(false, ParticleSystemStopBehavior.StopEmitting);
            }

            Destroy(releasedRoot, maximumRemainingLifetime + 0.25f);
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

        public void Initialize(Vector3 requestedStart, Vector3 requestedEnd, Color color, float glowScale)
        {
            start = requestedStart;
            end = requestedEnd;
            line = gameObject.AddComponent<LineRenderer>();
            line.positionCount = 12;
            line.widthMultiplier = 0.09f;
            line.startColor = color * 2f;
            line.endColor = Color.white;
            material = HeroCombatVfx.CreateMaterial(color, true, 2.5f * Mathf.Max(1f, glowScale));
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
