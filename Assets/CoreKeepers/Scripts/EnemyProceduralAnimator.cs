using UnityEngine;

namespace CoreKeepers
{
    [RequireComponent(typeof(EnemyBrain))]
    public sealed class EnemyProceduralAnimator : MonoBehaviour
    {
        [SerializeField] private float walkFrequency = 7f;
        [SerializeField] private float walkBobHeight = 0.12f;
        [SerializeField] private float handTravel = 0.2f;
        [Header("Attack Swing")]
        [SerializeField] private float attackAngle = 165f;
        [SerializeField] private float windupAngle = 105f;
        [SerializeField] private float windupDistance = 0.28f;
        [SerializeField] private float strikeReach = 0.52f;
        [SerializeField, Range(180f, 300f)] private float swooshArc = 220f;
        [SerializeField, Min(0.1f)] private float swooshRadius = 0.7f;
        [SerializeField, Min(0f)] private float swooshLift = 0.16f;
        [Header("Attack Hand Trails")]
        [SerializeField, ColorUsage(true, true)] private Color trailColor = new(0.25f, 0.8f, 1f, 1f);
        [SerializeField, Min(0.1f)] private float trailEmissionIntensity = 4f;
        [SerializeField, Min(0.01f)] private float trailWidth = 0.22f;
        [SerializeField, Min(0.02f)] private float trailLifetime = 0.22f;
        [SerializeField] private Vector3 trailHandOffset = new(0f, 0f, 0.38f);

        private EnemyBrain enemy;
        private Transform head;
        private Transform leftHand;
        private Transform rightHand;
        private Vector3 headPosition;
        private Vector3 leftPosition;
        private Vector3 rightPosition;
        private Quaternion headRotation;
        private Quaternion leftRotation;
        private Quaternion rightRotation;
        private Renderer[] renderers;
        private Color[] originalColors;
        private MaterialPropertyBlock colorBlock;
        private GameObject iceBlock;
        private GameObject stunStars;
        private TrailRenderer leftHandTrail;
        private TrailRenderer rightHandTrail;
        private Material trailMaterial;
        private float phase;
        private bool ready;

        private void Awake()
        {
            enemy = GetComponent<EnemyBrain>();
            colorBlock = new MaterialPropertyBlock();
        }

        private void Start()
        {
            head = FindDeepChild(transform, "Head");
            leftHand = FindDeepChild(transform, "LHand");
            rightHand = FindDeepChild(transform, "RHand");
            renderers = GetComponentsInChildren<Renderer>(true);
            originalColors = new Color[renderers.Length];
            for (var index = 0; index < renderers.Length; index++)
            {
                var material = renderers[index].sharedMaterial;
                originalColors[index] = material != null && material.HasProperty("_BaseColor")
                    ? material.GetColor("_BaseColor")
                    : material != null && material.HasProperty("_Color") ? material.color : Color.white;
            }
            if (head == null || leftHand == null || rightHand == null)
            {
                Debug.LogWarning("Enemy animation requires Head, LHand and RHand transforms.", this);
                return;
            }
            headPosition = head.localPosition;
            leftPosition = leftHand.localPosition;
            rightPosition = rightHand.localPosition;
            headRotation = head.localRotation;
            leftRotation = leftHand.localRotation;
            rightRotation = rightHand.localRotation;
            CreateAttackTrails();
            CreateStatusVisuals();
            ready = true;
        }

        private void LateUpdate()
        {
            if (!ready) return;
            ResetPose();
            SetTrailEmission(false, false);
            phase += Time.deltaTime * walkFrequency;
            AnimateState();
            UpdateStatusVisuals();
        }

        private void AnimateState()
        {
            var state = enemy.CurrentAnimation;
            var t = enemy.AnimationProgress;
            if (state == EnemyAnimationState.Idle && enemy.NormalizedSpeed > 0.05f)
                state = EnemyAnimationState.Walk;
            switch (state)
            {
                case EnemyAnimationState.Walk: AnimateWalk(); break;
                case EnemyAnimationState.Attack_LHand:
                    AnimateHand(leftHand, leftRotation, t, -1f);
                    SetTrailEmission(IsTrailPhase(t), false);
                    break;
                case EnemyAnimationState.Attack_RHand:
                    AnimateHand(rightHand, rightRotation, t, 1f);
                    SetTrailEmission(false, IsTrailPhase(t));
                    break;
                case EnemyAnimationState.Smash: AnimateSmash(t); break;
                case EnemyAnimationState.ThrowRock:
                    AnimateThrow(t);
                    SetTrailEmission(false, IsTrailPhase(t));
                    break;
                case EnemyAnimationState.CastProjectile: AnimateCast(t, false); break;
                case EnemyAnimationState.CastBuff: AnimateCast(t, true); break;
                case EnemyAnimationState.TakeHit: head.localRotation = headRotation * Quaternion.Euler(-18f * Mathf.Sin(t * Mathf.PI), 0f, 12f); break;
                case EnemyAnimationState.Freeze: break;
                case EnemyAnimationState.Burn: head.localPosition += Vector3.up * Mathf.Abs(Mathf.Sin(Time.time * 18f)) * 0.08f; break;
                case EnemyAnimationState.Die:
                    head.localPosition += Vector3.down * Mathf.Lerp(0f, 1.25f, Mathf.SmoothStep(0f, 1f, t));
                    leftHand.localRotation = leftRotation * Quaternion.Euler(0f, 0f, -70f * t);
                    rightHand.localRotation = rightRotation * Quaternion.Euler(0f, 0f, 70f * t);
                    break;
            }
        }

        private void AnimateWalk()
        {
            var wave = Mathf.Sin(phase);
            head.localPosition += Vector3.up * Mathf.Abs(wave) * walkBobHeight * enemy.NormalizedSpeed;
            leftHand.localPosition += new Vector3(0f, wave * handTravel * 0.4f, wave * handTravel);
            rightHand.localPosition += new Vector3(0f, -wave * handTravel * 0.4f, -wave * handTravel);
        }

        private void AnimateHand(Transform hand, Quaternion rest, float t, float side)
        {
            GetAttackPhases(t, out var windup, out var strike, out var recover);
            var windupRotation = new Vector3(windupAngle, -side * 38f, -side * 24f);
            var strikeRotation = new Vector3(-Mathf.Max(attackAngle, 165f), side * 30f, side * 14f);
            var rotation = Vector3.Lerp(Vector3.zero, windupRotation, windup);
            rotation = Vector3.Lerp(rotation, strikeRotation, strike);
            rotation = Vector3.Lerp(rotation, Vector3.zero, recover);
            hand.localRotation = rest * Quaternion.Euler(rotation);

            hand.localPosition += EvaluateSwooshPosition(t, side, windup, strike, recover);
            head.localPosition += new Vector3(0f, -0.06f, 0.1f) * strike;
            head.localRotation = headRotation * Quaternion.Euler(8f * strike, side * 7f * strike, 0f);
        }

        private void AnimateSmash(float t)
        {
            GetAttackPhases(t, out var windup, out var strike, out var recover);
            var rotation = Mathf.Lerp(windupAngle, -Mathf.Max(attackAngle, 165f), strike);
            rotation = Mathf.Lerp(rotation, 0f, recover);
            leftHand.localRotation = leftRotation * Quaternion.Euler(rotation * Mathf.Max(windup, strike), 0f, -18f);
            rightHand.localRotation = rightRotation * Quaternion.Euler(rotation * Mathf.Max(windup, strike), 0f, 18f);
            var lift = Vector3.up * windupDistance * windup;
            var drive = new Vector3(0f, -0.24f, strikeReach * 0.8f) * strike;
            leftHand.localPosition += Vector3.Lerp(lift + drive, Vector3.zero, recover);
            rightHand.localPosition += Vector3.Lerp(lift + drive, Vector3.zero, recover);
            head.localPosition += new Vector3(0f, -0.16f, 0.14f) * strike;
            SetTrailEmission(IsTrailPhase(t), IsTrailPhase(t));
        }

        private void AnimateThrow(float t)
        {
            var wave = Mathf.Sin(Mathf.Clamp01(t) * Mathf.PI);
            rightHand.localRotation = rightRotation * Quaternion.Euler(-155f * wave, 20f * wave, 0f);
            rightHand.localPosition += new Vector3(0.12f, 0.22f, 0.2f) * wave;
        }

        private void AnimateCast(float t, bool buff)
        {
            var wave = Mathf.Sin(Mathf.Clamp01(t) * Mathf.PI);
            var lift = buff ? 0.38f : 0.18f;
            leftHand.localPosition += new Vector3(-0.1f, lift, 0.25f) * wave;
            rightHand.localPosition += new Vector3(0.1f, lift, 0.25f) * wave;
            leftHand.localRotation = leftRotation * Quaternion.Euler(-75f * wave, 0f, -30f * wave);
            rightHand.localRotation = rightRotation * Quaternion.Euler(-75f * wave, 0f, 30f * wave);
        }

        private static void GetAttackPhases(float t, out float windup, out float strike, out float recover)
        {
            t = Mathf.Clamp01(t);
            windup = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(t / 0.32f));
            strike = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01((t - 0.32f) / 0.28f));
            recover = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01((t - 0.7f) / 0.3f));
        }

        private static bool IsTrailPhase(float t) => t >= 0.28f && t <= 0.76f;

        private Vector3 EvaluateSwooshPosition(float t, float side, float windup, float strike, float recover)
        {
            var halfArc = swooshArc * 0.5f;
            float angle;
            float weight;
            if (t < 0.32f)
            {
                angle = -halfArc;
                weight = windup;
            }
            else if (t < 0.7f)
            {
                angle = Mathf.Lerp(-halfArc, halfArc, strike);
                weight = 1f;
            }
            else
            {
                angle = halfArc;
                weight = 1f - recover;
            }
            var radians = angle * Mathf.Deg2Rad;
            var lift = Mathf.Sin(Mathf.Clamp01(strike) * Mathf.PI) * swooshLift;
            return new Vector3(Mathf.Sin(radians) * swooshRadius * side, lift,
                Mathf.Cos(radians) * Mathf.Max(swooshRadius, strikeReach)) * weight;
        }

        private void SetTrailEmission(bool left, bool right)
        {
            if (leftHandTrail != null) leftHandTrail.emitting = left;
            if (rightHandTrail != null) rightHandTrail.emitting = right;
        }

        private void CreateAttackTrails()
        {
            var shader = Shader.Find("Universal Render Pipeline/Particles/Unlit") ??
                         Shader.Find("Universal Render Pipeline/Unlit") ?? Shader.Find("Sprites/Default");
            if (shader == null)
            {
                Debug.LogWarning("No compatible shader was found for enemy hand trails.", this);
                return;
            }

            trailMaterial = new Material(shader) { name = $"{name} Hand Trail (Runtime)", hideFlags = HideFlags.DontSave };
            var emissive = trailColor;
            emissive.r *= trailEmissionIntensity;
            emissive.g *= trailEmissionIntensity;
            emissive.b *= trailEmissionIntensity;
            if (trailMaterial.HasProperty("_BaseColor")) trailMaterial.SetColor("_BaseColor", emissive);
            if (trailMaterial.HasProperty("_Color")) trailMaterial.SetColor("_Color", emissive);
            if (trailMaterial.HasProperty("_EmissionColor")) trailMaterial.SetColor("_EmissionColor", emissive);
            trailMaterial.EnableKeyword("_EMISSION");
            trailMaterial.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            if (trailMaterial.HasProperty("_Surface")) trailMaterial.SetFloat("_Surface", 1f);
            if (trailMaterial.HasProperty("_Blend")) trailMaterial.SetFloat("_Blend", 2f);
            if (trailMaterial.HasProperty("_ZWrite")) trailMaterial.SetFloat("_ZWrite", 0f);
            if (trailMaterial.HasProperty("_SrcBlend"))
                trailMaterial.SetFloat("_SrcBlend", (float)UnityEngine.Rendering.BlendMode.SrcAlpha);
            if (trailMaterial.HasProperty("_DstBlend"))
                trailMaterial.SetFloat("_DstBlend", (float)UnityEngine.Rendering.BlendMode.One);
            trailMaterial.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;
            trailMaterial.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;

            leftHandTrail = CreateHandTrail(leftHand, "LHand Attack Trail");
            rightHandTrail = CreateHandTrail(rightHand, "RHand Attack Trail");
        }

        private TrailRenderer CreateHandTrail(Transform hand, string trailName)
        {
            var trailObject = new GameObject(trailName);
            trailObject.transform.SetParent(hand, false);
            trailObject.transform.localPosition = trailHandOffset;
            var trail = trailObject.AddComponent<TrailRenderer>();
            trail.time = trailLifetime;
            trail.minVertexDistance = 0.025f;
            trail.widthCurve = new AnimationCurve(new Keyframe(0f, trailWidth), new Keyframe(1f, 0f));
            trail.startColor = Color.white;
            trail.endColor = new Color(1f, 1f, 1f, 0f);
            trail.material = trailMaterial;
            trail.alignment = LineAlignment.View;
            trail.textureMode = LineTextureMode.Stretch;
            trail.numCornerVertices = 4;
            trail.numCapVertices = 3;
            trail.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            trail.receiveShadows = false;
            trail.emitting = false;
            return trail;
        }

        private void ResetPose()
        {
            head.localPosition = headPosition;
            leftHand.localPosition = leftPosition;
            rightHand.localPosition = rightPosition;
            head.localRotation = headRotation;
            leftHand.localRotation = leftRotation;
            rightHand.localRotation = rightRotation;
        }

        private void UpdateStatusVisuals()
        {
            var statuses = enemy.ActiveDebuffs;
            iceBlock.SetActive((statuses & EnemyDebuff.Freeze) != 0);
            stunStars.SetActive((statuses & EnemyDebuff.Stun) != 0);
            if (stunStars.activeSelf)
                stunStars.transform.Rotate(0f, Time.deltaTime * 160f, 0f, Space.Self);

            var tint = Color.white;
            if ((statuses & EnemyDebuff.Chill) != 0) tint *= new Color(0.55f, 0.75f, 1.15f);
            if ((statuses & EnemyDebuff.Poisoned) != 0) tint *= new Color(0.55f, 1.15f, 0.55f);
            if ((statuses & EnemyDebuff.OnFire) != 0)
                tint *= Color.Lerp(new Color(1.25f, 0.45f, 0.15f), Color.white, Mathf.PingPong(Time.time * 4f, 1f));
            for (var index = 0; index < renderers.Length; index++)
            {
                var item = renderers[index];
                if (item == null || item.transform.IsChildOf(iceBlock.transform) || item.transform.IsChildOf(stunStars.transform)) continue;
                item.GetPropertyBlock(colorBlock);
                var color = originalColors[index] * tint;
                colorBlock.SetColor("_BaseColor", color);
                colorBlock.SetColor("_Color", color);
                item.SetPropertyBlock(colorBlock);
            }
        }

        private void CreateStatusVisuals()
        {
            iceBlock = GameObject.CreatePrimitive(PrimitiveType.Cube);
            iceBlock.name = "Freeze Visual";
            iceBlock.transform.SetParent(transform, false);
            iceBlock.transform.localPosition = Vector3.up;
            iceBlock.transform.localScale = new Vector3(1.4f, 2.2f, 1.1f);
            Destroy(iceBlock.GetComponent<Collider>());
            var iceRenderer = iceBlock.GetComponent<Renderer>();
            iceRenderer.material.color = new Color(0.25f, 0.75f, 1f, 0.45f);
            iceBlock.SetActive(false);

            stunStars = new GameObject("Stun Stars");
            stunStars.transform.SetParent(transform, false);
            stunStars.transform.localPosition = Vector3.up * 2.25f;
            for (var index = 0; index < 5; index++)
            {
                var star = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                star.name = $"Star {index + 1}";
                star.transform.SetParent(stunStars.transform, false);
                var angle = index * Mathf.PI * 2f / 5f;
                star.transform.localPosition = new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle)) * 0.55f;
                star.transform.localScale = Vector3.one * 0.14f;
                Destroy(star.GetComponent<Collider>());
                star.GetComponent<Renderer>().material.color = new Color(1f, 0.82f, 0.12f);
            }
            stunStars.SetActive(false);
        }

        private static Transform FindDeepChild(Transform root, string childName)
        {
            foreach (var child in root.GetComponentsInChildren<Transform>(true))
                if (child.name == childName) return child;
            return null;
        }

        private void OnDestroy()
        {
            if (trailMaterial != null)
                Destroy(trailMaterial);
        }
    }
}
