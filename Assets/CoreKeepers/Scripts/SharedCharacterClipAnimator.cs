using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Animations;
using UnityEngine.Playables;

namespace CoreKeepers
{
    [Serializable]
    public struct NamedCharacterClip
    {
        public string name;
        public AnimationClip clip;
    }

    /// <summary>
    /// Plays the shared Generic-rig animation library without an Animator Controller.
    /// One-shot clip time is derived from the replicated gameplay action progress, so
    /// animation remains deterministic without synchronizing Animator parameters.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class SharedCharacterClipAnimator : MonoBehaviour
    {
        private const float HeroRunAnimationSpeedScale = 0.375f;

        private readonly struct ClipRequest
        {
            public readonly string Name;
            public readonly bool Synchronized;
            public readonly float NormalizedTime;
            public readonly float Speed;

            public ClipRequest(string name, bool synchronized, float normalizedTime = 0f, float speed = 1f)
            {
                Name = name;
                Synchronized = synchronized;
                NormalizedTime = normalizedTime;
                Speed = speed;
            }
        }

        [SerializeField] private Animator targetAnimator;
        [SerializeField] private NamedCharacterClip[] clips = Array.Empty<NamedCharacterClip>();
        [SerializeField, Min(0f)] private float blendDuration = 0.08f;

        private readonly Dictionary<string, AnimationClip> clipLookup = new(StringComparer.Ordinal);
        private NetworkWarrior hero;
        private EnemyBrain enemy;
        private EnemyProceduralAnimator enemyPreset;
        private PlayableGraph graph;
        private AnimationMixerPlayable mixer;
        private AnimationClipPlayable[] playables;
        private int activeSlot = -1;
        private int fadingFromSlot = -1;
        private string activeClipName;
        private float activeLocalTime;
        private float blendProgress = 1f;
        private Transform[] poseTransforms = Array.Empty<Transform>();
        private Vector3[] targetRestPositions = Array.Empty<Vector3>();
        private Quaternion[] targetRestRotations = Array.Empty<Quaternion>();
        private Vector3[] targetRestScales = Array.Empty<Vector3>();
        private Vector3[] positionOffsets = Array.Empty<Vector3>();
        private Quaternion[] rotationOffsets = Array.Empty<Quaternion>();
        private Vector3[] scaleRatios = Array.Empty<Vector3>();
        private bool restPoseCalibrated;

        public Animator TargetAnimator => targetAnimator;

        private void Awake()
        {
            hero = GetComponent<NetworkWarrior>();
            enemy = GetComponent<EnemyBrain>();
            enemyPreset = GetComponent<EnemyProceduralAnimator>();
            RebuildLookup();
            CacheTargetRestPose();
        }

        private void OnEnable()
        {
            EnsureGraph();
        }

        private void OnDisable()
        {
            DestroyGraph();
        }

        private void Start()
        {
            CalibrateRestPoseOffsets();
        }

        private void LateUpdate()
        {
            EnsureGraph();
            if (!graph.IsValid())
                return;

            var request = hero != null ? GetHeroRequest() : enemy != null ? GetEnemyRequest() : default;
            if (string.IsNullOrEmpty(request.Name))
            {
                graph.Evaluate(0f);
                return;
            }

            ApplyRequest(request);
            UpdateBlend();
            graph.Evaluate(0f);
            ApplyRestPoseOffsets();
        }

        public void Configure(Animator animator, NamedCharacterClip[] animationClips)
        {
            targetAnimator = animator;
            clips = animationClips ?? Array.Empty<NamedCharacterClip>();
            RebuildLookup();
        }

        private ClipRequest GetHeroRequest()
        {
            if (hero.IsDowned)
                return Loop("DeadLoop", 0.85f);

            var progress = hero.ActionProgress;
            switch (hero.CurrentAction)
            {
                case WarriorAction.Attack:
                    return hero.PlayerClass == CorePlayerClass.Mage || hero.PlayerClass == CorePlayerClass.Healer
                        ? Sync("CastProjectileRHand", progress)
                        : Sync("AttackRHand", progress);
                case WarriorAction.Build: return Sync("Build", progress);
                case WarriorAction.Mine: return Sync("Mine", progress);
                case WarriorAction.Deposit: return Sync("Deposit", progress);
                case WarriorAction.Revive: return SyncLoop("ReviveLoop", progress, 6f);
                case WarriorAction.Whirlwind: return SyncLoop("TwirilLoop", progress, 8f);
                case WarriorAction.ShieldBash: return Sync("ShieldSmash", progress);
                case WarriorAction.BattleCharge: return SyncLoop("RunLoop", progress, 2f);
                case WarriorAction.Earthshatter: return Sync("Smash", progress);
                case WarriorAction.CastProjectile: return Sync("CastProjectileRHand", progress);
                case WarriorAction.CastSpellUp: return Sync("CastSpellUp", progress);
                case WarriorAction.CastSpellAround: return Sync("CastSpellAround", progress);
            }

            if (hero.NormalizedSpeed > 0.08f)
                return Loop("RunLoop", Mathf.Lerp(0.7f, 1.35f, hero.NormalizedSpeed) * HeroRunAnimationSpeedScale);
            return Loop("Idle", 1f);
        }

        private ClipRequest GetEnemyRequest()
        {
            var state = enemy.CurrentAnimation;
            var progress = enemy.AnimationProgress;
            if (state == EnemyAnimationState.Idle && enemy.NormalizedSpeed > 0.05f)
                state = EnemyAnimationState.Walk;

            switch (state)
            {
                case EnemyAnimationState.Idle: return Loop("Idle", 1f);
                case EnemyAnimationState.Walk: return GetEnemyLocomotionRequest();
                case EnemyAnimationState.Attack_LHand: return Sync("AttackLHand", progress);
                case EnemyAnimationState.Attack_RHand: return Sync("AttackRHand", progress);
                case EnemyAnimationState.Smash: return Sync("Smash", progress);
                case EnemyAnimationState.ThrowRock: return Sync("ThrowRock", progress);
                case EnemyAnimationState.BowShot: return Sync("AttackRHand", progress);
                case EnemyAnimationState.CastProjectile_LHand: return Sync("CastProjectileLHand", progress);
                case EnemyAnimationState.CastProjectile_RHand: return Sync("CastProjectileRHand", progress);
                case EnemyAnimationState.CastBuff: return Sync("CastSpellUp", progress);
                case EnemyAnimationState.HeadAttack: return Sync("HeadAttack", progress);
                case EnemyAnimationState.TakeHit: return Sync("Hit", progress);
                case EnemyAnimationState.Burn: return Sync("Hit", progress);
                case EnemyAnimationState.Die:
                    return progress < 0.25f
                        ? Sync("DeadStart", progress / 0.25f)
                        : SyncLoop("DeadLoop", (progress - 0.25f) / 0.75f, 2f);
                case EnemyAnimationState.Freeze:
                    return new ClipRequest(activeClipName, true,
                        GetActiveNormalizedTime(), 0f);
                default: return Loop("Idle", 1f);
            }
        }

        private ClipRequest GetEnemyLocomotionRequest()
        {
            var speed = Mathf.Lerp(0.65f, 1.25f, enemy.NormalizedSpeed);
            if (enemyPreset == null)
                return Loop("WalkLoop", speed);

            return enemyPreset.MovementPreset switch
            {
                EnemyMovementAnimationPreset.Run => Loop("RunLoop", speed),
                EnemyMovementAnimationPreset.Floating => Loop("Float", speed * 0.75f),
                _ => Loop("WalkLoop", speed)
            };
        }

        private void ApplyRequest(ClipRequest request)
        {
            if (!clipLookup.TryGetValue(request.Name, out var clip) || clip == null)
                return;

            if (!string.Equals(activeClipName, request.Name, StringComparison.Ordinal))
                SwitchClip(request.Name, clip);

            if (activeSlot < 0 || !playables[activeSlot].IsValid())
                return;

            if (request.Synchronized)
            {
                var normalized = Mathf.Max(0f, request.NormalizedTime);
                playables[activeSlot].SetTime(normalized * clip.length);
                activeLocalTime = normalized * clip.length;
            }
            else
            {
                activeLocalTime += Time.deltaTime * Mathf.Max(0f, request.Speed);
                if (clip.isLooping && clip.length > 0f)
                    activeLocalTime %= clip.length;
                playables[activeSlot].SetTime(activeLocalTime);
            }

            playables[activeSlot].SetSpeed(0d);
        }

        private void SwitchClip(string clipName, AnimationClip clip)
        {
            var nextSlot = activeSlot < 0 ? 0 : 1 - activeSlot;
            if (playables[nextSlot].IsValid())
            {
                mixer.DisconnectInput(nextSlot);
                playables[nextSlot].Destroy();
            }

            var playable = AnimationClipPlayable.Create(graph, clip);
            playable.SetApplyFootIK(false);
            playable.SetApplyPlayableIK(false);
            playable.SetSpeed(0d);
            graph.Connect(playable, 0, mixer, nextSlot);
            playables[nextSlot] = playable;

            fadingFromSlot = activeSlot;
            activeSlot = nextSlot;
            activeClipName = clipName;
            activeLocalTime = 0f;
            blendProgress = fadingFromSlot < 0 || blendDuration <= 0f ? 1f : 0f;
            mixer.SetInputWeight(activeSlot, blendProgress);
            if (fadingFromSlot >= 0)
                mixer.SetInputWeight(fadingFromSlot, 1f - blendProgress);
        }

        private void UpdateBlend()
        {
            if (activeSlot < 0 || blendProgress >= 1f)
                return;

            blendProgress = Mathf.MoveTowards(blendProgress, 1f, Time.deltaTime / blendDuration);
            mixer.SetInputWeight(activeSlot, blendProgress);
            if (fadingFromSlot < 0)
                return;

            mixer.SetInputWeight(fadingFromSlot, 1f - blendProgress);
            if (blendProgress < 1f)
                return;

            mixer.DisconnectInput(fadingFromSlot);
            if (playables[fadingFromSlot].IsValid())
                playables[fadingFromSlot].Destroy();
            fadingFromSlot = -1;
        }

        private float GetActiveNormalizedTime()
        {
            if (activeSlot < 0 || !playables[activeSlot].IsValid())
                return 0f;
            var clip = playables[activeSlot].GetAnimationClip();
            return clip == null || clip.length <= 0f ? 0f : activeLocalTime / clip.length;
        }

        private void EnsureGraph()
        {
            if (graph.IsValid() || targetAnimator == null)
                return;

            graph = PlayableGraph.Create($"Shared Character Animation - {name}");
            graph.SetTimeUpdateMode(DirectorUpdateMode.Manual);
            mixer = AnimationMixerPlayable.Create(graph, 2);
            playables = new AnimationClipPlayable[2];
            var output = AnimationPlayableOutput.Create(graph, "Character Pose", targetAnimator);
            output.SetSourcePlayable(mixer);
            graph.Play();
        }

        private void DestroyGraph()
        {
            if (graph.IsValid())
                graph.Destroy();
            activeSlot = -1;
            fadingFromSlot = -1;
            activeClipName = null;
            activeLocalTime = 0f;
        }

        private void RebuildLookup()
        {
            clipLookup.Clear();
            if (clips == null)
                return;
            foreach (var entry in clips)
                if (!string.IsNullOrWhiteSpace(entry.name) && entry.clip != null)
                    clipLookup[entry.name] = entry.clip;
        }

        private void CacheTargetRestPose()
        {
            if (targetAnimator == null)
                return;

            var transforms = new List<Transform>(3);
            AddRigPartIfPresent(transforms, "Head");
            AddRigPartIfPresent(transforms, "LHand");
            AddRigPartIfPresent(transforms, "RHand");
            poseTransforms = transforms.ToArray();
            targetRestPositions = new Vector3[poseTransforms.Length];
            targetRestRotations = new Quaternion[poseTransforms.Length];
            targetRestScales = new Vector3[poseTransforms.Length];
            positionOffsets = new Vector3[poseTransforms.Length];
            rotationOffsets = new Quaternion[poseTransforms.Length];
            scaleRatios = new Vector3[poseTransforms.Length];
            for (var index = 0; index < poseTransforms.Length; index++)
            {
                var item = poseTransforms[index];
                targetRestPositions[index] = item.localPosition;
                targetRestRotations[index] = item.localRotation;
                targetRestScales[index] = item.localScale;
                rotationOffsets[index] = Quaternion.identity;
                scaleRatios[index] = Vector3.one;
            }
        }

        private void CalibrateRestPoseOffsets()
        {
            if (restPoseCalibrated || targetAnimator == null || poseTransforms.Length == 0 ||
                !clipLookup.TryGetValue("Idle", out var idleClip) || idleClip == null)
                return;

            EnsureGraph();
            if (!graph.IsValid())
                return;

            var probe = AnimationClipPlayable.Create(graph, idleClip);
            probe.SetSpeed(0d);
            probe.SetTime(0d);
            graph.Connect(probe, 0, mixer, 0);
            mixer.SetInputWeight(0, 1f);
            mixer.SetInputWeight(1, 0f);
            graph.Evaluate(0f);

            for (var index = 0; index < poseTransforms.Length; index++)
            {
                var item = poseTransforms[index];
                var sourcePosition = item.localPosition;
                var sourceRotation = item.localRotation;
                var sourceScale = item.localScale;
                positionOffsets[index] = targetRestPositions[index] - sourcePosition;
                rotationOffsets[index] = targetRestRotations[index] * Quaternion.Inverse(sourceRotation);
                scaleRatios[index] = DivideScale(targetRestScales[index], sourceScale);
            }

            mixer.DisconnectInput(0);
            probe.Destroy();
            mixer.SetInputWeight(0, 0f);
            for (var index = 0; index < poseTransforms.Length; index++)
            {
                var item = poseTransforms[index];
                item.localPosition = targetRestPositions[index];
                item.localRotation = targetRestRotations[index];
                item.localScale = targetRestScales[index];
            }
            restPoseCalibrated = true;
        }

        private void ApplyRestPoseOffsets()
        {
            if (!restPoseCalibrated)
            {
                CalibrateRestPoseOffsets();
                if (!restPoseCalibrated)
                    return;
            }

            for (var index = 0; index < poseTransforms.Length; index++)
            {
                var item = poseTransforms[index];
                if (item == null)
                    continue;
                item.localPosition += positionOffsets[index];
                item.localRotation = rotationOffsets[index] * item.localRotation;
                item.localScale = Vector3.Scale(scaleRatios[index], item.localScale);
            }
        }

        private void AddRigPartIfPresent(List<Transform> destination, string partName)
        {
            foreach (var item in targetAnimator.GetComponentsInChildren<Transform>(true))
            {
                if (!string.Equals(item.name, partName, StringComparison.OrdinalIgnoreCase))
                    continue;
                destination.Add(item);
                return;
            }
        }

        private static Vector3 DivideScale(Vector3 target, Vector3 source)
        {
            return new Vector3(
                Mathf.Abs(source.x) > 0.00001f ? target.x / source.x : 1f,
                Mathf.Abs(source.y) > 0.00001f ? target.y / source.y : 1f,
                Mathf.Abs(source.z) > 0.00001f ? target.z / source.z : 1f);
        }

        private static ClipRequest Sync(string name, float progress) =>
            new(name, true, Mathf.Clamp01(progress));

        private static ClipRequest SyncLoop(string name, float progress, float repetitions) =>
            new(name, true, Mathf.Clamp01(progress) * Mathf.Max(1f, repetitions));

        private static ClipRequest Loop(string name, float speed) => new(name, false, 0f, speed);
    }
}
