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
        private Transform hammer;
        private Transform pickaxe;
        private TrailRenderer swordTrail;
        private Material swordTrailMaterial;
        private Vector3 headPosition;
        private Vector3 leftPosition;
        private Vector3 rightPosition;
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
                warrior.CurrentAction == WarriorAction.Attack));
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
            swordTrail.startColor = swordTrailColor;
            swordTrail.endColor = new Color(swordTrailColor.r, swordTrailColor.g, swordTrailColor.b, 0f);
            swordTrail.textureMode = LineTextureMode.Stretch;
            swordTrail.alignment = LineAlignment.View;
            swordTrail.numCornerVertices = 3;
            swordTrail.numCapVertices = 2;
            swordTrail.emitting = false;

            var shader = Shader.Find("Sprites/Default");
            if (shader == null)
                shader = Shader.Find("Universal Render Pipeline/Unlit");
            if (shader != null)
            {
                swordTrailMaterial = new Material(shader) { hideFlags = HideFlags.DontSave };
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
}
