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

        public Animator TargetAnimator => targetAnimator;

        private void Awake()
        {
            hero = GetComponent<NetworkWarrior>();
            enemy = GetComponent<EnemyBrain>();
            enemyPreset = GetComponent<EnemyProceduralAnimator>();
            RebuildLookup();
        }

        private void OnEnable()
        {
            EnsureGraph();
        }

        private void OnDisable()
        {
            DestroyGraph();
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
                case WarriorAction.Attack: return Sync("AttackRHand", progress);
                case WarriorAction.Build: return Sync("Build", progress);
                case WarriorAction.Mine: return Sync("Mine", progress);
                case WarriorAction.Deposit: return Sync("Deposit", progress);
                case WarriorAction.Revive: return SyncLoop("ReviveLoop", progress, 6f);
                case WarriorAction.Whirlwind: return SyncLoop("TwirilLoop", progress, 8f);
                case WarriorAction.ShieldBash: return Sync("ShieldSmash", progress);
                case WarriorAction.BattleCharge: return SyncLoop("RunLoop", progress, 2f);
                case WarriorAction.Earthshatter: return Sync("Smash", progress);
            }

            if (hero.NormalizedSpeed > 0.08f)
                return Loop("RunLoop", Mathf.Lerp(0.7f, 1.35f, hero.NormalizedSpeed));
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

        private static ClipRequest Sync(string name, float progress) =>
            new(name, true, Mathf.Clamp01(progress));

        private static ClipRequest SyncLoop(string name, float progress, float repetitions) =>
            new(name, true, Mathf.Clamp01(progress) * Mathf.Max(1f, repetitions));

        private static ClipRequest Loop(string name, float speed) => new(name, false, 0f, speed);
    }
}
