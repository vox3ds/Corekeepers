using UnityEngine;

namespace CoreKeepers
{
    public sealed class MeteorStrikeVfx : MonoBehaviour
    {
        [Header("Scene References")]
        [SerializeField] private Transform marker;
        [SerializeField] private Renderer markerRenderer;
        [SerializeField] private Transform meteor;
        [SerializeField] private Renderer meteorRenderer;
        [SerializeField] private ParticleSystem meteorFlames;
        [SerializeField] private ParticleSystem meteorSmoke;
        [SerializeField] private ParticleSystem explosion;
        [SerializeField] private ParticleSystem impactSmoke;
        [SerializeField] private Transform groundCrack;
        [SerializeField] private Renderer groundCrackRenderer;
        [SerializeField] private ParticleSystem groundFlames;
        [SerializeField] private LineRenderer shockwave;

        [Header("Timing")]
        [SerializeField, Min(0.05f)] private float fallDuration = 0.65f;
        [SerializeField, Min(0.05f)] private float shockwaveDuration = 0.7f;
        [SerializeField, Min(0f)] private float cleanupPadding = 1.5f;

        [Header("Meteor Motion")]
        [SerializeField] private Vector3 meteorStartOffset = new(-6f, 11f, -4f);
        [SerializeField] private Vector3 meteorImpactOffset = new(0f, 0.55f, 0f);
        [SerializeField] private Vector3 meteorSpin = new(160f, 210f, 120f);

        [Header("Scale")]
        [SerializeField, Min(0.1f)] private float authoredRadius = 3.5f;
        [SerializeField, Range(0.5f, 1f)] private float groundFlameRadiusFactor = 0.82f;

        private float radius;
        private float groundDuration;
        private float impactDelay;
        private float age;
        private bool initialized;
        private bool impacted;
        private Vector3 markerBaseScale;
        private MaterialPropertyBlock markerProperties;
        private MaterialPropertyBlock crackProperties;

        public void ConfigurePrefab(Transform requestedMarker, Renderer requestedMarkerRenderer,
            Transform requestedMeteor, Renderer requestedMeteorRenderer, ParticleSystem requestedMeteorFlames,
            ParticleSystem requestedMeteorSmoke, ParticleSystem requestedExplosion,
            ParticleSystem requestedImpactSmoke, Transform requestedGroundCrack,
            Renderer requestedGroundCrackRenderer, ParticleSystem requestedGroundFlames,
            LineRenderer requestedShockwave)
        {
            marker = requestedMarker;
            markerRenderer = requestedMarkerRenderer;
            meteor = requestedMeteor;
            meteorRenderer = requestedMeteorRenderer;
            meteorFlames = requestedMeteorFlames;
            meteorSmoke = requestedMeteorSmoke;
            explosion = requestedExplosion;
            impactSmoke = requestedImpactSmoke;
            groundCrack = requestedGroundCrack;
            groundCrackRenderer = requestedGroundCrackRenderer;
            groundFlames = requestedGroundFlames;
            shockwave = requestedShockwave;
        }

        public void Initialize(float requestedRadius, float requestedGroundDuration, float requestedImpactDelay)
        {
            radius = Mathf.Max(0.5f, requestedRadius);
            groundDuration = Mathf.Max(1f, requestedGroundDuration);
            impactDelay = Mathf.Max(fallDuration, requestedImpactDelay);
            age = 0f;
            impacted = false;
            initialized = true;

            var scale = radius / Mathf.Max(0.1f, authoredRadius);
            markerBaseScale = new Vector3(radius * 2f, radius * 2f, 1f);
            marker.localScale = markerBaseScale;
            marker.gameObject.SetActive(true);
            meteor.localScale = Vector3.one * scale;
            meteor.localPosition = meteorStartOffset * scale;
            meteor.gameObject.SetActive(false);
            explosion.transform.localScale = Vector3.one * scale;
            impactSmoke.transform.localScale = Vector3.one * scale;
            groundCrack.localScale = new Vector3(radius * 2f, radius * 2f, 1f);
            groundCrack.localRotation = Quaternion.Euler(-90f, 0f, Random.Range(0f, 360f));
            groundCrack.gameObject.SetActive(false);
            shockwave.gameObject.SetActive(false);

            StopAndClear(meteorFlames);
            StopAndClear(meteorSmoke);
            StopAndClear(explosion);
            StopAndClear(impactSmoke);
            StopAndClear(groundFlames);

            var groundMain = groundFlames.main;
            groundMain.duration = Mathf.Max(0.1f, groundDuration - 1f);
            var groundShape = groundFlames.shape;
            groundShape.radius = radius * groundFlameRadiusFactor;
            markerProperties ??= new MaterialPropertyBlock();
            crackProperties ??= new MaterialPropertyBlock();
            SetRendererAlpha(markerRenderer, markerProperties, 0.62f);
            SetRendererAlpha(groundCrackRenderer, crackProperties, 1f);
        }

        private void Update()
        {
            if (!initialized) return;
            age += Time.deltaTime;
            if (!impacted)
            {
                UpdateWarning();
                if (age >= impactDelay) Impact();
                return;
            }

            UpdateImpact();
            if (age >= impactDelay + groundDuration + cleanupPadding) Destroy(gameObject);
        }

        private void UpdateWarning()
        {
            var pulse = 0.94f + Mathf.Sin(age * 8f) * 0.06f;
            marker.localScale = markerBaseScale * pulse;
            marker.Rotate(0f, 0f, 22f * Time.deltaTime, Space.Self);

            var fallStart = Mathf.Max(0f, impactDelay - fallDuration);
            if (age < fallStart) return;
            if (!meteor.gameObject.activeSelf)
            {
                meteor.gameObject.SetActive(true);
                meteorFlames.Play(true);
                meteorSmoke.Play(true);
            }
            var t = Mathf.Clamp01((age - fallStart) / Mathf.Max(0.05f, fallDuration));
            var eased = t * t;
            var scale = radius / Mathf.Max(0.1f, authoredRadius);
            meteor.localPosition = Vector3.Lerp(meteorStartOffset * scale, meteorImpactOffset, eased);
            meteor.Rotate(meteorSpin * Time.deltaTime, Space.Self);
        }

        private void Impact()
        {
            impacted = true;
            marker.gameObject.SetActive(false);
            if (meteorRenderer != null) meteorRenderer.enabled = false;
            meteorFlames.Stop(true, ParticleSystemStopBehavior.StopEmitting);
            meteorSmoke.Stop(true, ParticleSystemStopBehavior.StopEmitting);
            explosion.Play(true);
            impactSmoke.Play(true);
            groundCrack.gameObject.SetActive(true);
            groundFlames.Play(true);
            shockwave.gameObject.SetActive(true);
            UpdateShockwave(0f);
        }

        private void UpdateImpact()
        {
            var shockwaveAge = age - impactDelay;
            if (shockwaveAge <= shockwaveDuration)
                UpdateShockwave(Mathf.Clamp01(shockwaveAge / Mathf.Max(0.05f, shockwaveDuration)));
            else if (shockwave.gameObject.activeSelf)
                shockwave.gameObject.SetActive(false);

            var groundAge = age - impactDelay;
            var fadeStart = Mathf.Max(0f, groundDuration - 1f);
            var crackAlpha = 1f - Mathf.SmoothStep(0f, 1f,
                Mathf.InverseLerp(fadeStart, groundDuration, groundAge));
            SetRendererAlpha(groundCrackRenderer, crackProperties, crackAlpha);
        }

        private void UpdateShockwave(float t)
        {
            const int pointCount = 64;
            shockwave.positionCount = pointCount;
            var currentRadius = Mathf.Lerp(radius * 0.08f, radius, 1f - (1f - t) * (1f - t));
            for (var index = 0; index < pointCount; index++)
            {
                var angle = index * Mathf.PI * 2f / pointCount;
                shockwave.SetPosition(index,
                    new Vector3(Mathf.Cos(angle) * currentRadius, 0.08f, Mathf.Sin(angle) * currentRadius));
            }
            var alpha = 1f - t;
            shockwave.startColor = new Color(1f, 0.22f, 0.02f, alpha) * 8f;
            shockwave.endColor = new Color(1f, 0.75f, 0.12f, alpha) * 5f;
            shockwave.widthMultiplier = Mathf.Lerp(0.38f, 0.03f, t);
        }

        private static void StopAndClear(ParticleSystem particles)
        {
            if (particles != null)
                particles.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        }

        private static void SetRendererAlpha(Renderer target, MaterialPropertyBlock properties, float alpha)
        {
            if (target == null) return;
            target.GetPropertyBlock(properties);
            var color = Color.white;
            color.a = Mathf.Clamp01(alpha);
            properties.SetColor("_BaseColor", color);
            properties.SetColor("_Color", color);
            target.SetPropertyBlock(properties);
        }
    }
}
