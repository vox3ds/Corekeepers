using UnityEngine;

namespace CoreKeepers
{
    [RequireComponent(typeof(NetworkWarrior))]
    public sealed class WarriorProceduralAnimator : MonoBehaviour
    {
        [Header("Idle")]
        [SerializeField] private float idleBobHeight = 0.08f;
        [SerializeField] private float idleFrequency = 1.8f;
        [Header("Run")]
        [SerializeField] private float runBobHeight = 0.18f;
        [SerializeField] private float runFrequency = 8f;
        [SerializeField] private float runHandTravel = 0.22f;
        [SerializeField] private float runTilt = 7f;
        [SerializeField, Min(0.01f)] private float runBlendTime = 0.14f;
        [Header("Actions")]
        [SerializeField] private float attackSwingAngle = 155f;
        [SerializeField] private float toolSwingAngle = 125f;
        [Header("Sword Trail")]
        [SerializeField, Min(0.02f)] private float swordTrailTime = 0.16f;
        [SerializeField, Min(0.01f)] private float swordTrailWidth = 0.2f;
        [SerializeField] private Color swordTrailColor = new(0.55f, 0.9f, 1f, 0.9f);

        private NetworkWarrior warrior;
        private Transform body;
        private Transform head;
        private Transform leftHand;
        private Transform rightHand;
        private Transform sword;
        private Transform shield;
        private Transform hammer;
        private Transform pickaxe;
        private TrailRenderer swordTrail;
        private Material swordTrailMaterial;
        private Vector3 headPosition;
        private Vector3 leftPosition;
        private Vector3 rightPosition;
        private Vector3 rightScale;
        private Quaternion headRotation;
        private Quaternion leftRotation;
        private Quaternion rightRotation;
        private Quaternion bodyRotation;
        private Vector3 bodyPosition;
        private float locomotionBlend;
        private float runPhase;
        private float idlePhase;
        private float downedBlend;
        private bool ready;
        private WarriorAction previousAction;
        private bool impactVisualTriggered;

        public string DisplayState => warrior == null ? "Unknown" : warrior.IsDowned ? "Downed" : warrior.CurrentAction != WarriorAction.None
            ? warrior.CurrentAction.ToString()
            : warrior.NormalizedSpeed > 0.08f ? "Run" : "Idle";

        private void Awake()
        {
            warrior = GetComponent<NetworkWarrior>();
        }

        private void Start()
        {
            body = FindDeepChild(transform, "Root");
            head = FindDeepChild(transform, "Head");
            leftHand = FindDeepChild(transform, "LHand");
            rightHand = FindDeepChild(transform, "RHand");
            sword = FindDeepChild(transform, "SwordPlaceholder");
            shield = FindDeepChild(transform, "ShieldPlaceholder");
            hammer = FindDeepChild(transform, "HammerPlaceholder");
            pickaxe = FindDeepChild(transform, "PickaxePlaceholder");
            CreateSwordTrail();
            if (head == null || leftHand == null || rightHand == null)
            {
                Debug.LogWarning("Warrior procedural animation requires Head, LHand and RHand transforms.", this);
                return;
            }
            headPosition = head.localPosition;
            leftPosition = leftHand.localPosition;
            rightPosition = rightHand.localPosition;
            rightScale = rightHand.localScale;
            headRotation = head.localRotation;
            leftRotation = leftHand.localRotation;
            rightRotation = rightHand.localRotation;
            if (body != null)
            {
                bodyRotation = body.localRotation;
                bodyPosition = body.localPosition;
            }
            ready = true;
        }

        private void LateUpdate()
        {
            if (!ready)
                return;

            ResetPose();
            SetToolVisibility();
            if (swordTrail != null)
                swordTrail.emitting = false;
            UpdateLocomotionBlend();
            if (warrior.CurrentAction != previousAction)
            {
                previousAction = warrior.CurrentAction;
                impactVisualTriggered = false;
            }
            downedBlend = Mathf.MoveTowards(downedBlend, warrior.IsDowned ? 1f : 0f, Time.deltaTime * 3.5f);
            if (downedBlend > 0f)
            {
                AnimateDowned(downedBlend);
                return;
            }
            if (warrior.CurrentAction != WarriorAction.None)
                AnimateAction(warrior.CurrentAction, warrior.ActionProgress);
            else
                AnimateLocomotion(warrior.NormalizedSpeed);
        }

        private void ResetPose()
        {
            head.localPosition = headPosition;
            leftHand.localPosition = leftPosition;
            rightHand.localPosition = rightPosition;
            rightHand.localScale = rightScale;
            head.localRotation = headRotation;
            leftHand.localRotation = leftRotation;
            rightHand.localRotation = rightRotation;
            if (body != null)
            {
                body.localRotation = bodyRotation;
                body.localPosition = bodyPosition;
            }
        }

        private void UpdateLocomotionBlend()
        {
            var target = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.03f, 0.22f, warrior.NormalizedSpeed));
            locomotionBlend = Mathf.MoveTowards(locomotionBlend, target, Time.deltaTime / runBlendTime);
            idlePhase += Time.deltaTime * idleFrequency;
            runPhase += Time.deltaTime * runFrequency * Mathf.Lerp(0.55f, 1f, warrior.NormalizedSpeed);
        }

        private void AnimateLocomotion(float speed)
        {
            var idleWeight = 1f - locomotionBlend;
            var idleWave = Mathf.Sin(idlePhase);
            var runWave = Mathf.Sin(runPhase);
            head.localPosition += Vector3.up * (idleWave * idleBobHeight * idleWeight +
                Mathf.Abs(runWave) * runBobHeight * locomotionBlend);
            leftHand.localPosition += Vector3.up * (Mathf.Sin(idlePhase + 0.7f) * idleBobHeight * 0.7f * idleWeight);
            rightHand.localPosition += Vector3.up * (Mathf.Sin(idlePhase + 2.1f) * idleBobHeight * 0.7f * idleWeight);
            head.localRotation = headRotation * Quaternion.Euler(runTilt * speed * locomotionBlend, 0f,
                -runWave * 2f * locomotionBlend);
            leftHand.localPosition += new Vector3(0f, runWave * runHandTravel * 0.45f,
                runWave * runHandTravel) * locomotionBlend;
            rightHand.localPosition += new Vector3(0f, -runWave * runHandTravel * 0.45f,
                -runWave * runHandTravel) * locomotionBlend;
        }

        private void AnimateAction(WarriorAction state, float t)
        {
            switch (state)
            {
                case WarriorAction.Attack:
                    AnimateSwordSwing(t, warrior.ActionVariant);
                    break;
                case WarriorAction.Build:
                    AnimateToolSwing(t, 1f);
                    break;
                case WarriorAction.Mine:
                    AnimateToolSwing(t, 1.15f);
                    head.localPosition += new Vector3(0f, -0.13f, 0.09f) *
                        Mathf.Sin(Mathf.Clamp01(t) * Mathf.PI);
                    break;
                case WarriorAction.Deposit:
                    var reach = Mathf.Sin(Mathf.Clamp01(t) * Mathf.PI);
                    leftHand.localPosition += new Vector3(0.2f, 0.05f, 0.4f) * reach;
                    rightHand.localPosition += new Vector3(-0.2f, 0.05f, 0.4f) * reach;
                    head.localPosition += Vector3.down * reach * 0.05f;
                    break;
                case WarriorAction.Revive:
                    var channelPulse = 0.75f + Mathf.Sin(t * Mathf.PI * 20f) * 0.12f;
                    leftHand.localPosition += new Vector3(0.2f, 0.1f, 0.42f) * channelPulse;
                    rightHand.localPosition += new Vector3(-0.2f, 0.1f, 0.42f) * channelPulse;
                    leftHand.localRotation = leftRotation * Quaternion.Euler(-55f, 0f, -18f);
                    rightHand.localRotation = rightRotation * Quaternion.Euler(-55f, 0f, 18f);
                    head.localPosition += Vector3.down * 0.05f;
                    break;
                case WarriorAction.Whirlwind:
                    AnimateWhirlwind(t);
                    break;
                case WarriorAction.ShieldBash:
                    AnimateShieldBash(t);
                    break;
                case WarriorAction.BattleCharge:
                    AnimateBattleCharge(t);
                    break;
                case WarriorAction.Earthshatter:
                    AnimateEarthshatter(t);
                    break;
            }
        }

        private void AnimateWhirlwind(float t)
        {
            rightHand.localPosition = new Vector3(0.1f, 0.33f, 0.5f);
            rightHand.localRotation = Quaternion.Euler(180f, 180f, -90f);
            rightHand.localScale = Vector3.one;
            leftHand.localPosition += new Vector3(0.04f, 0.12f, 0.28f);
            leftHand.localRotation = leftRotation * Quaternion.Euler(-22f, 48f, -34f);
            head.localPosition += Vector3.up * (0.04f + Mathf.Sin(t * Mathf.PI * 20f) * 0.025f);
            if (swordTrail != null) swordTrail.emitting = t > 0.015f && t < 0.99f;
        }

        private void AnimateShieldBash(float t)
        {
            var windup = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(t / 0.28f));
            var bash = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01((t - 0.28f) / 0.24f));
            var recover = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01((t - 0.68f) / 0.32f));
            var guardedPosition = new Vector3(0.28f, 0.2f, 0.28f);
            var bashPosition = new Vector3(0.05f, 0.12f, 0.72f);
            var offset = Vector3.Lerp(Vector3.zero, guardedPosition, windup);
            offset = Vector3.Lerp(offset, bashPosition, bash);
            leftHand.localPosition += Vector3.Lerp(offset, Vector3.zero, recover);
            var guardedRotation = Quaternion.Euler(-72f, 18f, -38f);
            var bashRotation = Quaternion.Euler(-92f, -4f, -8f);
            var shieldPose = Quaternion.Slerp(Quaternion.identity, guardedRotation, windup);
            shieldPose = Quaternion.Slerp(shieldPose, bashRotation, bash);
            shieldPose = Quaternion.Slerp(shieldPose, Quaternion.identity, recover);
            leftHand.localRotation = leftRotation * shieldPose;
            rightHand.localPosition += new Vector3(-0.12f, 0.03f, -0.18f) * windup;
            rightHand.localRotation = rightRotation * Quaternion.Euler(-25f * windup, 22f * windup, 30f * windup);
            if (body != null)
            {
                body.localRotation = bodyRotation * Quaternion.Euler(-10f * bash, 0f, -7f * bash);
                body.localPosition = bodyPosition + Vector3.forward * (0.2f * bash);
            }
        }

        private void AnimateBattleCharge(float t)
        {
            var thrust = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(t / 0.32f));
            var recover = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01((t - 0.82f) / 0.18f));
            rightHand.localPosition += Vector3.Lerp(new Vector3(-0.08f, 0.25f, -0.2f),
                new Vector3(0f, 0.05f, 0.68f), thrust) * (1f - recover);
            rightHand.localRotation = rightRotation * Quaternion.Euler(
                Vector3.Lerp(new Vector3(-80f, -35f, -25f), new Vector3(-8f, 5f, 88f), thrust));
            leftHand.localPosition += new Vector3(0.08f, 0.18f, 0.34f) * (1f - recover);
            head.localRotation = headRotation * Quaternion.Euler(14f, 0f, 0f);
            if (body != null) body.localRotation = bodyRotation * Quaternion.Euler(18f, 0f, 0f);
            if (swordTrail != null) swordTrail.emitting = t > 0.15f && t < 0.9f;
        }

        private void AnimateEarthshatter(float t)
        {
            var jumpT = Mathf.Clamp01(t / 0.68f);
            var jumpHeight = Mathf.Sin(jumpT * Mathf.PI) * 1.85f;
            var windup = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(t / 0.42f));
            var slam = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01((t - 0.42f) / 0.26f));
            var recover = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01((t - 0.76f) / 0.24f));
            if (body != null)
            {
                body.localPosition = bodyPosition + Vector3.up * jumpHeight + Vector3.down * (0.22f * slam * (1f - recover));
                body.localRotation = bodyRotation * Quaternion.Euler(Mathf.Lerp(-12f, 24f, slam) * (1f - recover), 0f, 0f);
            }
            var overhead = new Vector3(-145f, -12f, -18f);
            var impact = new Vector3(72f, 8f, 18f);
            var pose = Vector3.Lerp(Vector3.zero, overhead, windup);
            pose = Vector3.Lerp(pose, impact, slam);
            pose = Vector3.Lerp(pose, Vector3.zero, recover);
            rightHand.localRotation = rightRotation * Quaternion.Euler(pose);
            rightHand.localPosition += Vector3.Lerp(new Vector3(0f, 0.5f, -0.2f),
                new Vector3(0f, -0.22f, 0.52f), slam) * (1f - recover);
            if (swordTrail != null) swordTrail.emitting = t > 0.5f && t < 0.74f;
            if (!impactVisualTriggered && t >= 0.68f)
            {
                impactVisualTriggered = true;
                EarthshatterShockwave.Spawn(transform.position, swordTrailColor, 5f);
            }
        }

        private void AnimateDowned(float blend)
        {
            if (body != null)
            {
                body.localRotation = bodyRotation * Quaternion.Euler(-90f * blend, 0f, 0f);
                body.localPosition = bodyPosition + new Vector3(0f, 0.32f, 0.08f) * blend;
            }
            head.localRotation = headRotation * Quaternion.Euler(-12f * blend, 0f, 0f);
            leftHand.localRotation = leftRotation * Quaternion.Euler(-150f * blend, 0f, -22f * blend);
            rightHand.localRotation = rightRotation * Quaternion.Euler(-150f * blend, 0f, 22f * blend);
            leftHand.localPosition += new Vector3(0.18f, 0.48f, 0.18f) * blend;
            rightHand.localPosition += new Vector3(-0.18f, 0.48f, 0.18f) * blend;
        }

        private void AnimateToolSwing(float t, float strength)
        {
            var windupT = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(t / 0.34f));
            var impactT = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01((t - 0.34f) / 0.3f));
            var recoverT = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01((t - 0.7f) / 0.3f));
            var windup = new Vector3(-toolSwingAngle * 0.88f * strength, -18f, -28f);
            var impact = new Vector3(toolSwingAngle * 0.72f * strength, 12f, 20f);
            var pose = Vector3.Lerp(Vector3.zero, windup, windupT);
            pose = Vector3.Lerp(pose, impact, impactT);
            pose = Vector3.Lerp(pose, Vector3.zero, recoverT);
            rightHand.localRotation = rightRotation * Quaternion.Euler(pose);

            var raised = new Vector3(0f, 0.32f, -0.2f) * strength;
            var driven = new Vector3(0f, -0.18f, 0.4f) * strength;
            var rightOffset = Vector3.Lerp(Vector3.zero, raised, windupT);
            rightOffset = Vector3.Lerp(rightOffset, driven, impactT);
            rightHand.localPosition += Vector3.Lerp(rightOffset, Vector3.zero, recoverT);
            leftHand.localPosition += new Vector3(0f, 0.12f, 0.16f) *
                Mathf.Sin(Mathf.Clamp01(t) * Mathf.PI);
        }

        private void AnimateSwordSwing(float t, int variant)
        {
            var windupT = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(t / 0.3f));
            var strikeT = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01((t - 0.3f) / 0.32f));
            var recoverT = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01((t - 0.68f) / 0.32f));
            var angleScale = attackSwingAngle / 155f;

            Vector3 windup;
            Vector3 strike;
            Vector3 windupPosition;
            Vector3 strikePosition;
            Vector3 bodyWindup;
            Vector3 bodyStrike;
            switch (variant % 3)
            {
                case 1:
                    windup = new Vector3(-8f, -118f, -8f);
                    strike = new Vector3(12f, 112f, 10f);
                    windupPosition = new Vector3(-0.2f, 0.08f, -0.24f);
                    strikePosition = new Vector3(0.22f, 0.02f, 0.5f);
                    bodyWindup = new Vector3(0f, -30f, -7f);
                    bodyStrike = new Vector3(7f, 32f, 7f);
                    break;
                case 2:
                    windup = new Vector3(82f, -92f, -6f);
                    strike = new Vector3(-92f, 78f, 12f);
                    windupPosition = new Vector3(-0.2f, -0.18f, -0.18f);
                    strikePosition = new Vector3(0.18f, 0.2f, 0.48f);
                    bodyWindup = new Vector3(8f, -25f, 9f);
                    bodyStrike = new Vector3(-5f, 28f, -7f);
                    break;
                default:
                    windup = new Vector3(-122f, -42f, -5f);
                    strike = new Vector3(98f, 38f, 10f);
                    windupPosition = new Vector3(-0.08f, 0.34f, -0.2f);
                    strikePosition = new Vector3(0.12f, -0.12f, 0.54f);
                    bodyWindup = new Vector3(-4f, -24f, -8f);
                    bodyStrike = new Vector3(10f, 27f, 6f);
                    break;
            }

            windup *= angleScale;
            strike *= angleScale;
            var pose = Vector3.Lerp(Vector3.zero, windup, windupT);
            pose = Vector3.Lerp(pose, strike, strikeT);
            pose = Vector3.Lerp(pose, Vector3.zero, recoverT);
            rightHand.localRotation = rightRotation * Quaternion.Euler(pose);
            if (swordTrail != null)
                swordTrail.emitting = t >= 0.3f && t <= 0.76f;

            if (body != null)
            {
                var bodyPose = Vector3.Lerp(Vector3.zero, bodyWindup, windupT);
                bodyPose = Vector3.Lerp(bodyPose, bodyStrike, strikeT);
                bodyPose = Vector3.Lerp(bodyPose, Vector3.zero, recoverT);
                body.localRotation = bodyRotation * Quaternion.Euler(bodyPose);
                body.localPosition = bodyPosition + Vector3.forward *
                    (Mathf.Sin(Mathf.Clamp01(t) * Mathf.PI) * 0.1f);
            }

            var position = Vector3.Lerp(Vector3.zero, windupPosition, windupT);
            position = Vector3.Lerp(position, strikePosition, strikeT);
            rightHand.localPosition += Vector3.Lerp(position, Vector3.zero, recoverT);
            var commitment = Mathf.Sin(Mathf.Clamp01(t) * Mathf.PI);
            head.localRotation = headRotation * Quaternion.Euler(6f * commitment, 0f, -5f * commitment);
            head.localPosition += new Vector3(0f, -0.04f, 0.08f) * commitment;
        }

        private void SetToolVisibility()
        {
            if (sword != null) sword.gameObject.SetActive(!warrior.IsDowned && (warrior.CurrentAction == WarriorAction.None ||
                warrior.CurrentAction == WarriorAction.Attack || warrior.CurrentAction == WarriorAction.Whirlwind ||
                warrior.CurrentAction == WarriorAction.BattleCharge || warrior.CurrentAction == WarriorAction.Earthshatter ||
                warrior.CurrentAction == WarriorAction.ShieldBash));
            if (shield != null) shield.gameObject.SetActive(!warrior.IsDowned && warrior.CurrentAction != WarriorAction.Build &&
                warrior.CurrentAction != WarriorAction.Mine);
            if (hammer != null) hammer.gameObject.SetActive(!warrior.IsDowned && warrior.CurrentAction == WarriorAction.Build);
            if (pickaxe != null) pickaxe.gameObject.SetActive(!warrior.IsDowned && warrior.CurrentAction == WarriorAction.Mine);
        }

        private void CreateSwordTrail()
        {
            if (sword == null || swordTrail != null)
                return;
            var tip = FindDeepChild(sword, "Tip");
            if (tip == null)
            {
                Debug.LogWarning("SwordPlaceholder requires a Tip transform for the sword trail.", this);
                return;
            }
            swordTrail = tip.GetComponent<TrailRenderer>();
            if (swordTrail == null)
                swordTrail = tip.gameObject.AddComponent<TrailRenderer>();
            swordTrail.time = swordTrailTime;
            swordTrail.minVertexDistance = 0.025f;
            swordTrail.widthCurve = new AnimationCurve(new Keyframe(0f, swordTrailWidth), new Keyframe(1f, 0f));
            swordTrail.startColor = swordTrailColor * 1.8f;
            swordTrail.endColor = new Color(swordTrailColor.r, swordTrailColor.g, swordTrailColor.b, 0f);
            swordTrail.textureMode = LineTextureMode.Stretch;
            swordTrail.alignment = LineAlignment.View;
            swordTrail.numCornerVertices = 3;
            swordTrail.numCapVertices = 2;
            swordTrail.emitting = false;

            var shader = Shader.Find("Universal Render Pipeline/Particles/Unlit");
            if (shader == null) shader = Shader.Find("Sprites/Default");
            if (shader != null)
            {
                swordTrailMaterial = new Material(shader) { hideFlags = HideFlags.DontSave };
                var glow = swordTrailColor * 3f;
                glow.a = swordTrailColor.a;
                if (swordTrailMaterial.HasProperty("_BaseColor")) swordTrailMaterial.SetColor("_BaseColor", glow);
                if (swordTrailMaterial.HasProperty("_Color")) swordTrailMaterial.SetColor("_Color", glow);
                if (swordTrailMaterial.HasProperty("_EmissionColor"))
                {
                    swordTrailMaterial.EnableKeyword("_EMISSION");
                    swordTrailMaterial.SetColor("_EmissionColor", glow);
                }
                swordTrail.material = swordTrailMaterial;
            }
        }

        private void OnDestroy()
        {
            if (swordTrailMaterial != null)
                Destroy(swordTrailMaterial);
        }

        private static Transform FindDeepChild(Transform root, string childName)
        {
            foreach (var child in root.GetComponentsInChildren<Transform>(true))
                if (child.name == childName)
                    return child;
            return null;
        }
    }

    public sealed class EarthshatterShockwave : MonoBehaviour
    {
        private const float Lifetime = 0.55f;
        private LineRenderer line;
        private Material material;
        private Color color;
        private float maximumRadius;
        private float age;

        public static void Spawn(Vector3 position, Color requestedColor, float radius)
        {
            var effectObject = new GameObject("Earthshatter Shockwave");
            effectObject.transform.position = position + Vector3.up * 0.08f;
            var effect = effectObject.AddComponent<EarthshatterShockwave>();
            effect.Initialize(requestedColor, radius);
        }

        private void Initialize(Color requestedColor, float radius)
        {
            color = requestedColor;
            maximumRadius = radius;
            line = gameObject.AddComponent<LineRenderer>();
            line.loop = true;
            line.useWorldSpace = false;
            line.positionCount = 64;
            line.widthMultiplier = 0.18f;
            line.numCornerVertices = 2;
            var shader = Shader.Find("Sprites/Default");
            if (shader != null)
            {
                material = new Material(shader) { hideFlags = HideFlags.DontSave };
                line.material = material;
            }
        }

        private void Update()
        {
            age += Time.deltaTime;
            var t = Mathf.Clamp01(age / Lifetime);
            var radius = Mathf.Lerp(0.35f, maximumRadius, 1f - (1f - t) * (1f - t));
            for (var index = 0; index < line.positionCount; index++)
            {
                var angle = index * Mathf.PI * 2f / line.positionCount;
                line.SetPosition(index, new Vector3(Mathf.Cos(angle) * radius, 0f, Mathf.Sin(angle) * radius));
            }
            var faded = color;
            faded.a *= 1f - t;
            line.startColor = faded * 1.6f;
            line.endColor = faded;
            line.widthMultiplier = Mathf.Lerp(0.24f, 0.03f, t);
            if (age >= Lifetime) Destroy(gameObject);
        }

        private void OnDestroy()
        {
            if (material != null) Destroy(material);
        }
    }
}
