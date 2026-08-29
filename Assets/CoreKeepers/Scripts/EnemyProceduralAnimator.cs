using UnityEngine;
using UnityEngine.Serialization;

namespace CoreKeepers
{
    public enum EnemyAttackAnimationPreset
    {
        AlternatingMelee,
        TwoHandedSmash,
        ThrowRock,
        BowShot,
        AlternatingMagicProjectile,
        RaisedHandInstantSpell,
        None,
        HeadAttack
    }

    public enum EnemyMovementAnimationPreset
    {
        Walk,
        Run,
        Floating,
        PhysicsRoll
    }

    [RequireComponent(typeof(EnemyBrain))]
    public sealed class EnemyProceduralAnimator : MonoBehaviour
    {
        [Header("Animation Presets")]
        [SerializeField] private EnemyAttackAnimationPreset attackPreset = EnemyAttackAnimationPreset.AlternatingMelee;
        [SerializeField] private EnemyMovementAnimationPreset movementPreset = EnemyMovementAnimationPreset.Walk;
        [SerializeField, HideInInspector] private bool useImportedAnimation;

        [Header("Magic Projectile")]
        [SerializeField] private GameObject projectilePrefab;
        [SerializeField, Range(0.35f, 0.75f)] private float projectileReleaseTime = 0.55f;

        [Header("Movement")]
        [SerializeField] private float walkFrequency = 7f;
        [SerializeField] private float walkBobHeight = 0.12f;
        [SerializeField] private float handTravel = 0.2f;
        [Header("Alternating Melee")]
        [SerializeField] private float attackAngle = 165f;
        [SerializeField] private float windupAngle = 105f;
        [SerializeField] private float strikeReach = 0.52f;
        [SerializeField, Range(180f, 300f)] private float swooshArc = 220f;
        [SerializeField, Min(0.1f)] private float swooshRadius = 0.7f;
        [SerializeField, Min(0f)] private float swooshLift = 0.16f;

        [Header("Two Handed Smash")]
        [SerializeField] private float smashWindupAngle = 105f;
        [SerializeField] private float smashStrikeAngle = 165f;
        [FormerlySerializedAs("windupDistance")]
        [SerializeField, Min(0f)] private float smashWindupDistance = 0.28f;
        [SerializeField, Min(0f)] private float smashStrikeReach = 0.42f;
        [SerializeField, Min(0f)] private float smashStrikeDrop = 0.24f;
        [SerializeField] private float smashHandTilt = 18f;
        [SerializeField, Min(0f)] private float smashHeadDrop = 0.16f;
        [SerializeField, Min(0f)] private float smashHeadReach = 0.14f;

        [Header("Head Attack")]
        [SerializeField, Min(0f)] private float headWindupDistance = 0.3f;
        [SerializeField] private float headWindupLift = 0.12f;
        [SerializeField] private float headWindupAngle = -24f;
        [SerializeField, Min(0f)] private float headStrikeReach = 0.95f;
        [SerializeField] private float headStrikeDrop = 0.08f;
        [SerializeField] private float headStrikeAngle = 34f;

        [Header("Attack Trails")]
        [SerializeField, ColorUsage(true, true)] private Color trailColor = new(0.25f, 0.8f, 1f, 1f);
        [SerializeField, Min(0.1f)] private float trailEmissionIntensity = 4f;
        [SerializeField, Min(0.01f)] private float trailWidth = 0.22f;
        [SerializeField, Min(0.02f)] private float trailLifetime = 0.22f;
        [SerializeField] private Vector3 trailHandOffset = new(0f, 0f, 0.38f);
        [SerializeField] private Vector3 trailHeadOffset = new(0f, 0f, 0.45f);

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
        private TrailRenderer headTrail;
        private Material trailMaterial;
        private GameObject heldRock;
        private float phase;
        private bool ready;

        public EnemyAttackAnimationPreset AttackPreset => attackPreset;
        public EnemyMovementAnimationPreset MovementPreset => movementPreset;
        public GameObject ProjectilePrefab => projectilePrefab;
        public float ProjectileReleaseTime => projectileReleaseTime;
        public bool UsesPhysicsRolling => movementPreset == EnemyMovementAnimationPreset.PhysicsRoll;

        public void ConfigureImportedAnimation(bool enabled)
        {
            useImportedAnimation = enabled;
        }

        public EnemyAnimationState GetNextAttackState(ref bool useRightSide)
        {
            switch (attackPreset)
            {
                case EnemyAttackAnimationPreset.AlternatingMelee:
                    var melee = useRightSide ? EnemyAnimationState.Attack_RHand : EnemyAnimationState.Attack_LHand;
                    useRightSide = !useRightSide;
                    return melee;
                case EnemyAttackAnimationPreset.TwoHandedSmash: return EnemyAnimationState.Smash;
                case EnemyAttackAnimationPreset.ThrowRock: return EnemyAnimationState.ThrowRock;
                case EnemyAttackAnimationPreset.BowShot: return EnemyAnimationState.BowShot;
                case EnemyAttackAnimationPreset.AlternatingMagicProjectile:
                    var magic = useRightSide
                        ? EnemyAnimationState.CastProjectile_RHand
                        : EnemyAnimationState.CastProjectile_LHand;
                    useRightSide = !useRightSide;
                    return magic;
                case EnemyAttackAnimationPreset.RaisedHandInstantSpell: return EnemyAnimationState.CastBuff;
                case EnemyAttackAnimationPreset.HeadAttack: return EnemyAnimationState.HeadAttack;
                default: return EnemyAnimationState.Idle;
            }
        }

        public Vector3 GetProjectileOrigin(bool rightSide)
        {
            var hand = rightSide ? rightHand : leftHand;
            return hand != null
                ? hand.position
                : transform.position + Vector3.up + transform.forward * 0.65f;
        }

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
            CreateHeldRock();
            CreateStatusVisuals();
            ready = true;
        }

        private void LateUpdate()
        {
            if (!ready) return;
            SetTrailEmission(false, false, false);
            if (heldRock != null) heldRock.SetActive(false);
            if (useImportedAnimation)
            {
                UpdateImportedAnimationVisuals();
                UpdateStatusVisuals();
                return;
            }

            ResetPose();
            phase += Time.deltaTime * walkFrequency;
            AnimateState();
            UpdateStatusVisuals();
        }

        private void UpdateImportedAnimationVisuals()
        {
            var state = enemy.CurrentAnimation;
            var t = enemy.AnimationProgress;
            switch (state)
            {
                case EnemyAnimationState.Attack_LHand:
                    SetTrailEmission(IsTrailPhase(t), false);
                    break;
                case EnemyAnimationState.Attack_RHand:
                    SetTrailEmission(false, IsTrailPhase(t));
                    break;
                case EnemyAnimationState.Smash:
                    SetTrailEmission(IsTrailPhase(t), IsTrailPhase(t));
                    break;
                case EnemyAnimationState.ThrowRock:
                    SetTrailEmission(IsTrailPhase(t), IsTrailPhase(t));
                    if (heldRock != null && t >= 0.16f && t < 0.61f)
                    {
                        heldRock.SetActive(true);
                        var hand = rightHand != null ? rightHand : leftHand;
                        if (hand != null)
                        {
                            heldRock.transform.position = hand.position + hand.forward * 0.22f;
                            heldRock.transform.rotation = hand.rotation;
                        }
                    }
                    break;
                case EnemyAnimationState.HeadAttack:
                    SetTrailEmission(false, false, IsTrailPhase(t));
                    break;
            }
        }

        private void AnimateState()
        {
            var state = enemy.CurrentAnimation;
            var t = enemy.AnimationProgress;
            if (state == EnemyAnimationState.Idle && enemy.NormalizedSpeed > 0.05f)
                state = EnemyAnimationState.Walk;
            switch (state)
            {
                case EnemyAnimationState.Walk: AnimateMovement(); break;
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
                    break;
                case EnemyAnimationState.BowShot: AnimateBowShot(t); break;
                case EnemyAnimationState.CastProjectile_LHand: AnimateProjectileCast(t, false); break;
                case EnemyAnimationState.CastProjectile_RHand: AnimateProjectileCast(t, true); break;
                case EnemyAnimationState.CastBuff: AnimateInstantSpell(t); break;
                case EnemyAnimationState.HeadAttack: AnimateHeadAttack(t); break;
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

        private void AnimateMovement()
        {
            var wave = Mathf.Sin(phase);
            var speed = enemy.NormalizedSpeed;
            switch (movementPreset)
            {
                case EnemyMovementAnimationPreset.Run:
                    head.localPosition += Vector3.up * Mathf.Abs(wave) * walkBobHeight * 1.65f * speed;
                    head.localRotation = headRotation * Quaternion.Euler(12f * speed, 0f, 0f);
                    leftHand.localPosition += new Vector3(0f, wave * handTravel * 0.65f, wave * handTravel * 1.7f);
                    rightHand.localPosition += new Vector3(0f, -wave * handTravel * 0.65f, -wave * handTravel * 1.7f);
                    break;
                case EnemyMovementAnimationPreset.Floating:
                    head.localPosition += Vector3.up * (Mathf.Sin(phase * 0.45f) * walkBobHeight + walkBobHeight);
                    break;
                case EnemyMovementAnimationPreset.PhysicsRoll:
                    break;
                default:
                    head.localPosition += Vector3.up * Mathf.Abs(wave) * walkBobHeight * speed;
                    leftHand.localPosition += new Vector3(0f, wave * handTravel * 0.4f, wave * handTravel);
                    rightHand.localPosition += new Vector3(0f, -wave * handTravel * 0.4f, -wave * handTravel);
                    break;
            }
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
            var rotation = Mathf.Lerp(smashWindupAngle, -Mathf.Abs(smashStrikeAngle), strike);
            rotation = Mathf.Lerp(rotation, 0f, recover);
            var poseWeight = Mathf.Max(windup, strike) * (1f - recover);
            leftHand.localRotation = leftRotation * Quaternion.Euler(rotation * poseWeight, 0f,
                -smashHandTilt * poseWeight);
            rightHand.localRotation = rightRotation * Quaternion.Euler(rotation * poseWeight, 0f,
                smashHandTilt * poseWeight);
            var lift = Vector3.up * smashWindupDistance * windup;
            var drive = new Vector3(0f, -smashStrikeDrop, smashStrikeReach) * strike;
            leftHand.localPosition += Vector3.Lerp(lift + drive, Vector3.zero, recover);
            rightHand.localPosition += Vector3.Lerp(lift + drive, Vector3.zero, recover);
            head.localPosition += new Vector3(0f, -smashHeadDrop, smashHeadReach) * strike;
            SetTrailEmission(IsTrailPhase(t), IsTrailPhase(t));
        }

        private void AnimateThrow(float t)
        {
            GetAttackPhases(t, out var windup, out var strike, out var recover);
            var dig = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(t / 0.22f));
            var lift = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01((t - 0.18f) / 0.25f));
            var throwWeight = Mathf.Max(windup, strike) * (1f - recover);
            var handOffset = new Vector3(0f, Mathf.Lerp(-0.34f, 0.34f, lift), Mathf.Lerp(0.28f, -0.18f, windup));
            leftHand.localPosition += handOffset + Vector3.right * 0.1f;
            rightHand.localPosition += handOffset + Vector3.left * 0.1f;
            leftHand.localRotation = leftRotation * Quaternion.Euler(Mathf.Lerp(75f, -150f, strike), 0f, -18f);
            rightHand.localRotation = rightRotation * Quaternion.Euler(Mathf.Lerp(75f, -150f, strike), 0f, 18f);
            head.localPosition += Vector3.down * 0.14f * dig;
            if (heldRock != null)
            {
                heldRock.SetActive(t >= 0.16f && t < 0.61f);
                heldRock.transform.localPosition = Vector3.Lerp(new Vector3(0f, 0.25f, 0.35f),
                    new Vector3(0f, 1.25f, 0.05f), lift) + Vector3.forward * 0.12f * throwWeight;
            }
            SetTrailEmission(IsTrailPhase(t), IsTrailPhase(t));
        }

        private void AnimateBowShot(float t)
        {
            GetAttackPhases(t, out var draw, out var release, out var recover);
            var pose = Mathf.Max(draw, release) * (1f - recover);
            leftHand.localPosition += new Vector3(-0.22f, 0.15f, 0.46f) * pose;
            rightHand.localPosition += new Vector3(0.18f, 0.17f, Mathf.Lerp(0.34f, -0.22f, draw)) * pose;
            leftHand.localRotation = leftRotation * Quaternion.Euler(-82f * pose, -18f * pose, -22f * pose);
            rightHand.localRotation = rightRotation * Quaternion.Euler(-105f * pose, 32f * pose, 34f * pose);
            head.localRotation = headRotation * Quaternion.Euler(0f, -8f * pose, 0f);
        }

        private void AnimateProjectileCast(float t, bool right)
        {
            var hand = right ? rightHand : leftHand;
            var rest = right ? rightRotation : leftRotation;
            var side = right ? 1f : -1f;
            GetAttackPhases(t, out var windup, out var strike, out var recover);
            var windupPosition = new Vector3(0.16f * side, 0.12f, -0.22f);
            var releasePosition = new Vector3(0.08f * side, 0.08f, 1f);
            var position = Vector3.Lerp(Vector3.zero, windupPosition, windup);
            position = Vector3.Lerp(position, releasePosition, strike);
            position = Vector3.Lerp(position, Vector3.zero, recover);
            hand.localPosition += position;

            var windupRotation = new Vector3(38f, -22f * side, -18f * side);
            var releaseRotation = new Vector3(-92f, 12f * side, 18f * side);
            var rotation = Vector3.Lerp(Vector3.zero, windupRotation, windup);
            rotation = Vector3.Lerp(rotation, releaseRotation, strike);
            rotation = Vector3.Lerp(rotation, Vector3.zero, recover);
            hand.localRotation = rest * Quaternion.Euler(rotation);
            head.localRotation = headRotation * Quaternion.Euler(0f, 7f * side * strike, 0f);
        }

        private void AnimateInstantSpell(float t)
        {
            var wave = Mathf.Sin(Mathf.Clamp01(t) * Mathf.PI);
            rightHand.localPosition += new Vector3(0.08f, 0.62f, 0.08f) * wave;
            rightHand.localRotation = rightRotation * Quaternion.Euler(-145f * wave, 0f, 24f * wave);
            head.localRotation = headRotation * Quaternion.Euler(-12f * wave, 0f, 0f);
        }

        private void AnimateHeadAttack(float t)
        {
            GetAttackPhases(t, out var windup, out var strike, out var recover);
            var position = Vector3.Lerp(Vector3.zero,
                new Vector3(0f, headWindupLift, -headWindupDistance), windup);
            position = Vector3.Lerp(position, new Vector3(0f, -headStrikeDrop, headStrikeReach), strike);
            position = Vector3.Lerp(position, Vector3.zero, recover);
            head.localPosition += position;

            var rotation = Vector3.Lerp(Vector3.zero, new Vector3(headWindupAngle, 0f, 0f), windup);
            rotation = Vector3.Lerp(rotation, new Vector3(headStrikeAngle, 0f, 0f), strike);
            rotation = Vector3.Lerp(rotation, Vector3.zero, recover);
            head.localRotation = headRotation * Quaternion.Euler(rotation);
            SetTrailEmission(false, false, IsTrailPhase(t));
        }

        private void CreateHeldRock()
        {
            heldRock = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            heldRock.name = "Held Rock (Runtime)";
            heldRock.transform.SetParent(transform, false);
            heldRock.transform.localScale = Vector3.one * 0.38f;
            Destroy(heldRock.GetComponent<Collider>());
            var renderer = heldRock.GetComponent<Renderer>();
            if (renderer != null) renderer.material.color = new Color(0.28f, 0.24f, 0.2f);
            heldRock.SetActive(false);
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
            return new Vector3(-Mathf.Sin(radians) * swooshRadius * side, lift,
                Mathf.Cos(radians) * Mathf.Max(swooshRadius, strikeReach)) * weight;
        }

        private void SetTrailEmission(bool left, bool right, bool headAttack = false)
        {
            if (leftHandTrail != null) leftHandTrail.emitting = left;
            if (rightHandTrail != null) rightHandTrail.emitting = right;
            if (headTrail != null) headTrail.emitting = headAttack;
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

            leftHandTrail = CreateAttackTrail(leftHand, "LHand Attack Trail", trailHandOffset);
            rightHandTrail = CreateAttackTrail(rightHand, "RHand Attack Trail", trailHandOffset);
            headTrail = CreateAttackTrail(head, "Head Attack Trail", trailHeadOffset);
        }

        private TrailRenderer CreateAttackTrail(Transform anchor, string trailName, Vector3 localOffset)
        {
            var trailObject = new GameObject(trailName);
            trailObject.transform.SetParent(anchor, false);
            trailObject.transform.localPosition = localOffset;
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
