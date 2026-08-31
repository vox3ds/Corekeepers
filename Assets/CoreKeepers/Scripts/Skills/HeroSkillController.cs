using System;
using System.Collections.Generic;
using System.Linq;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.InputSystem;

namespace CoreKeepers
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(NetworkWarrior))]
    public sealed class HeroSkillController : MonoBehaviour
    {
        private sealed class ActiveZone
        {
            public HeroSkillDefinition Skill;
            public Vector3 Position;
            public double EndsAt;
            public double NextTick;
        }

        private sealed class PendingProjectile
        {
            public HeroSkillDefinition Skill;
            public EnemyBrain Target;
            public Vector3 Position;
            public Vector3 TargetPoint;
            public double ExpiresAt;
        }

        private sealed class PendingArcaneBolt
        {
            public HeroSkillDefinition Skill;
            public Vector3 Position;
            public Vector3 Direction;
            public float DistanceTraveled;
            public double ExpiresAt;
        }

        private sealed class PendingFirePatch
        {
            public Vector3 Position;
            public float Radius;
            public double EndsAt;
            public double NextTick;
        }

        private const float HeroProjectileSpeed = 13f;
        private const float ArcaneBoltSpeed = HeroProjectileSpeed * 2f;

        private readonly HeroSkillDefinition[] slots = new HeroSkillDefinition[4];
        private readonly float[] localReadyAt = new float[4];
        private readonly Dictionary<int, double> serverReadyAt = new();
        private readonly HashSet<int> acquired = new();
        private readonly HashSet<int> chosenWaves = new();
        private readonly HashSet<int> localChosenWaves = new();
        private readonly List<ActiveZone> activeZones = new();
        private readonly List<PendingProjectile> pendingProjectiles = new();
        private readonly List<PendingArcaneBolt> pendingArcaneBolts = new();
        private readonly List<PendingFirePatch> pendingFirePatches = new();
        private readonly Dictionary<ulong, double> guardianAngelReadyAt = new();
        private NetworkWarrior hero;
        private int selectedSlot;
        private int offeredWave;
        private int localLastChosenWave;
        private int serverDebugUnlockedWave;
        private int observedMissionRevision = -1;
        private float nextHudRefresh;
        private double nextPassiveTick;
        private double selfBuffEndsAt;
        private float selfDamageMultiplier = 1f;
        private float selfResistance;
        private bool selfCcImmune;
        private int arcaneMasteryStacks;
        private HeroSkillDefinition pendingLeapSkill;
        private EnemyBrain pendingLeapTarget;
        private bool leapDamageApplied;
        private HeroSkillDefinition pendingWhirlwindSkill;
        private readonly HashSet<EnemyBrain> whirlwindHits = new();
        private HeroSkillDefinition pendingEarthshatterSkill;
        private bool earthshatterImpactApplied;

        public int SelectedSlot => selectedSlot;
        public bool BlocksLocalGameplay => SkillUpgradePopupUI.Instance != null && SkillUpgradePopupUI.Instance.IsOpen;

        private void Awake() => hero = GetComponent<NetworkWarrior>();

        public void InitializeForMission()
        {
            observedMissionRevision = CoreMissionWaveController.Instance != null
                ? CoreMissionWaveController.Instance.MissionRevision : 0;
            ResetProgression();
            if (hero.IsOwner)
            {
                var ui = HeroSkillsUI.Instance ?? FindFirstObjectByType<HeroSkillsUI>(FindObjectsInactive.Include);
                ui?.Present(this);
            }
        }

        private void Update()
        {
            if (hero == null || !hero.IsSpawned) return;
            CheckMissionReset();
            if (hero.IsServer) UpdateServerState();
            if (!hero.IsOwner) return;
            HandleSelectionInput();
            OfferCompletedWaveUpgrade();
            if (Time.unscaledTime >= nextHudRefresh)
            {
                nextHudRefresh = Time.unscaledTime + 0.05f;
                HeroSkillsUI.Instance?.Refresh();
            }
        }

        private void CheckMissionReset()
        {
            var waves = CoreMissionWaveController.Instance;
            if (waves == null || observedMissionRevision == waves.MissionRevision) return;
            observedMissionRevision = waves.MissionRevision;
            ResetProgression();
            if (hero.IsOwner) HeroSkillsUI.Instance?.Present(this);
        }

        public HeroSkillDefinition GetSlot(int index) => index >= 0 && index < slots.Length ? slots[index] : null;
        public bool IsSlotUnlocked(int index) => index == 0 || (index > 0 && index < slots.Length && slots[index] != null);
        public float SelectedUseRange => slots[selectedSlot] == null ? 2f : slots[selectedSlot].Effect ==
            HeroSkillEffect.ExplodingProjectile && slots[selectedSlot].SecondaryValue > 0f
                ? slots[selectedSlot].SecondaryValue : slots[selectedSlot].Radius;
        public float SelectedApproachRange => slots[selectedSlot] == null ? SelectedUseRange :
            slots[selectedSlot].StableId == 103 ? 2f :
            slots[selectedSlot].Effect is HeroSkillEffect.MeleeArc or HeroSkillEffect.ShieldBash
                ? Mathf.Min(SelectedUseRange, 1.35f) : SelectedUseRange;
        public float GetRemainingCooldown(int index) => index < 0 || index >= slots.Length
            ? 0f : Mathf.Max(0f, localReadyAt[index] - Time.time);

        public float GetEffectiveCooldown(HeroSkillDefinition definition)
        {
            if (definition == null) return 0f;
            var multiplier = definition.SkillType == HeroSkillType.Active && acquired.Contains(105) ? 0.8f : 1f;
            if (definition.SkillType == HeroSkillType.Basic && acquired.Contains(8) && hero.HealthRatio < 0.3f)
                multiplier *= 0.75f;
            return Mathf.Max(0.05f, definition.Cooldown * multiplier);
        }

        public void SelectSlot(int index)
        {
            if (!hero.IsOwner || index < 0 || index >= slots.Length || !IsSlotUnlocked(index) || slots[index] == null)
                return;
            selectedSlot = index;
            HeroSkillsUI.Instance?.Refresh();
        }

        public void ChooseUpgrade(HeroSkillDefinition definition)
        {
            if (!hero.IsOwner || definition == null || offeredWave <= 0 || definition.UnlockWave != offeredWave ||
                localChosenWaves.Contains(offeredWave)) return;
            localChosenWaves.Add(offeredWave);
            acquired.Add(definition.StableId);
            localLastChosenWave = offeredWave;
            if (definition.SkillType == HeroSkillType.Active)
            {
                var slot = (offeredWave + 1) / 2;
                slots[slot] = definition;
                localReadyAt[slot] = 0f;
            }
            hero.RequestSkillChoice(definition.StableId, offeredWave);
            offeredWave = 0;
            HeroSkillsUI.Instance?.Refresh();
        }

        public bool TryGrantNextDebugLevel()
        {
            if (!hero.IsOwner || BlocksLocalGameplay || offeredWave > 0) return false;
            var next = localLastChosenWave + 1;
            if (next > 6) return false;
            var choices = HeroSkillCatalog.Choices(hero.PlayerClass, next);
            if (choices.Length != 2)
            {
                Debug.LogError($"{hero.PlayerClass} Level {next} requires exactly two skill definitions.", this);
                return false;
            }
            var popup = SkillUpgradePopupUI.Instance ??
                FindFirstObjectByType<SkillUpgradePopupUI>(FindObjectsInactive.Include);
            if (popup == null)
            {
                Debug.LogError("Skill Upgrade Popup was not found in DebugScene.", this);
                return false;
            }
            offeredWave = next;
            hero.RequestDebugSkillLevel(next);
            popup.Show(this, next, choices);
            return true;
        }

        public bool TryUseSelectedOnTarget(NetworkObject target)
        {
            if (!hero.IsOwner || target == null || selectedSlot < 0 || selectedSlot >= slots.Length)
                return false;
            var definition = slots[selectedSlot];
            if (definition == null ||
                (definition.Targeting != HeroSkillTargeting.Enemy && definition.StableId != 103) ||
                GetRemainingCooldown(selectedSlot) > 0f)
                return false;
            return RequestUse(definition, target.transform.position, target);
        }

        public bool TryUseSelectedInPlace(Vector3 point)
        {
            if (!hero.IsOwner || selectedSlot < 0 || selectedSlot >= slots.Length) return false;
            var definition = slots[selectedSlot];
            if (definition == null || GetRemainingCooldown(selectedSlot) > 0f ||
                definition.Effect != HeroSkillEffect.MeleeArc && definition.StableId is not (101 or 102 or 103)) return false;
            return RequestUse(definition, point, null);
        }

        public void ResolveUseResult(int stableId, bool succeeded, float authoritativeCooldown)
        {
            var slot = Array.FindIndex(slots, skill => skill != null && skill.StableId == stableId);
            if (slot < 0) return;
            localReadyAt[slot] = succeeded ? Time.time + authoritativeCooldown : 0f;
            HeroSkillsUI.Instance?.Refresh();
        }

        public void ServerChoose(HeroSkillDefinition definition, int wave)
        {
            if (!hero.IsServer || definition == null || definition.HeroClass != hero.PlayerClass ||
                definition.UnlockWave != wave || wave < 1 || wave > 6 || chosenWaves.Contains(wave)) return;
            var waveController = CoreMissionWaveController.Instance;
            if ((waveController == null || waveController.CompletedWaves < wave) && serverDebugUnlockedWave < wave)
                return;
            chosenWaves.Add(wave);
            acquired.Add(definition.StableId);
            ApplyImmediatePassive(definition);
        }

        public bool ServerGrantDebugLevel(int wave)
        {
            var highestChosenWave = chosenWaves.Count > 0 ? chosenWaves.Max() : 0;
            var currentWave = Mathf.Max(serverDebugUnlockedWave, highestChosenWave);
            if (!hero.IsServer || wave < 1 || wave > 6 || wave != currentWave + 1)
                return false;
            serverDebugUnlockedWave = wave;
            return true;
        }

        public bool ServerTryExecute(HeroSkillDefinition definition, Vector3 requestedPosition,
            NetworkObject requestedTarget, out float effectiveCooldown)
        {
            effectiveCooldown = 0f;
            if (!hero.IsServer || hero.IsDowned || definition == null || definition.HeroClass != hero.PlayerClass ||
                hero.CurrentAction == WarriorAction.Whirlwind ||
                definition.SkillType == HeroSkillType.Passive ||
                (definition.SkillType != HeroSkillType.Basic && !acquired.Contains(definition.StableId))) return false;
            var now = hero.NetworkManager.ServerTime.Time;
            if (serverReadyAt.TryGetValue(definition.StableId, out var readyAt) && now < readyAt) return false;
            if (!Execute(definition, requestedPosition, requestedTarget, now)) return false;
            hero.ServerPresentSkill(definition, requestedPosition, requestedTarget);
            effectiveCooldown = GetEffectiveServerCooldown(definition);
            serverReadyAt[definition.StableId] = now + effectiveCooldown;
            if (definition.SkillType == HeroSkillType.Active && acquired.Contains(112))
                arcaneMasteryStacks = Mathf.Min(3, arcaneMasteryStacks + 1);
            return true;
        }

        public float ModifyIncomingDamage(float amount)
        {
            var multiplier = Mathf.Clamp01(1f - selfResistance) *
                Mathf.Clamp01(1f - hero.ExternalDamageResistance);
            if (hero.PlayerClass == CorePlayerClass.Warrior && hero.HealthRatio < 0.3f && acquired.Contains(9))
                multiplier *= 0.6f;
            return amount * multiplier;
        }

        public bool TryPreventLethalDamage()
        {
            if (!hero.IsServer || !acquired.Contains(312)) return false;
            var now = hero.NetworkManager.ServerTime.Time;
            if (serverReadyAt.TryGetValue(312, out var readyAt) && now < readyAt) return false;
            serverReadyAt[312] = now + 90d;
            hero.ServerSetHealth(Mathf.Max(1f, hero.MaximumHealth * 0.5f));
            return true;
        }

        public float ModifyHealingFor(NetworkWarrior target, float amount)
        {
            if (!acquired.Contains(313) || target == null) return amount;
            if (target.HealthRatio < 0.25f) return amount * 1.5f;
            if (target.HealthRatio < 0.5f) return amount * 1.2f;
            return amount;
        }

        public float MiningSpeedMultiplier => acquired.Contains(204) ? 1.3f : 1f;
        public float BuildSpeedMultiplier => acquired.Contains(204) ? 1.3f : 1f;
        public float RepairSpeedMultiplier => acquired.Contains(204) ? 1.3f : 1f;
        public float CarryingCapacityMultiplier => acquired.Contains(205) ? 1.5f : 1f;
        public bool HasProspector => acquired.Contains(209);
        public bool IsCrowdControlImmune => selfCcImmune;
        public bool HasPassive(int stableId) => acquired.Contains(stableId);

        public void GetActivePassiveEffects(List<HeroSkillDefinition> results)
        {
            if (results == null) return;
            results.Clear();
            foreach (var stableId in acquired.OrderBy(id => id))
            {
                var definition = HeroSkillCatalog.Find(stableId);
                if (definition == null || definition.SkillType != HeroSkillType.Passive) continue;
                if (stableId is 8 or 9 && hero.HealthRatio >= 0.3f) continue;
                if (stableId == 12 && EnemiesInRadius(hero.transform.position, 7f).Count < 10) continue;
                results.Add(definition);
            }
        }

        public void ServerElementalDetonation(Vector3 position)
        {
            if (!hero.IsServer || !acquired.Contains(113)) return;
            foreach (var enemy in EnemiesInRadius(position, 2.5f))
                enemy.TakeDamage(18f, hero, true, true);
        }

        private void ResetProgression()
        {
            Array.Clear(slots, 0, slots.Length);
            Array.Clear(localReadyAt, 0, localReadyAt.Length);
            serverReadyAt.Clear();
            acquired.Clear();
            chosenWaves.Clear();
            localChosenWaves.Clear();
            activeZones.Clear();
            pendingProjectiles.Clear();
            pendingArcaneBolts.Clear();
            pendingFirePatches.Clear();
            guardianAngelReadyAt.Clear();
            selectedSlot = 0;
            offeredWave = 0;
            localLastChosenWave = 0;
            serverDebugUnlockedWave = 0;
            selfBuffEndsAt = 0d;
            selfDamageMultiplier = 1f;
            selfResistance = 0f;
            selfCcImmune = false;
            arcaneMasteryStacks = 0;
            pendingLeapSkill = null;
            pendingLeapTarget = null;
            leapDamageApplied = false;
            pendingWhirlwindSkill = null;
            whirlwindHits.Clear();
            pendingEarthshatterSkill = null;
            earthshatterImpactApplied = false;
            slots[0] = HeroSkillCatalog.Basic(hero.PlayerClass);
            if (slots[0] == null)
                Debug.LogError($"No Basic skill definition exists for {hero.PlayerClass}.", this);
            if (hero.IsOwner) SkillUpgradePopupUI.Instance?.Close();
        }

        private void HandleSelectionInput()
        {
            if (BlocksLocalGameplay) return;
            var keyboard = Keyboard.current;
            if (keyboard != null)
            {
                if (keyboard.digit1Key.wasPressedThisFrame || keyboard.numpad1Key.wasPressedThisFrame) SelectSlot(0);
                else if (keyboard.digit2Key.wasPressedThisFrame || keyboard.numpad2Key.wasPressedThisFrame) SelectSlot(1);
                else if (keyboard.digit3Key.wasPressedThisFrame || keyboard.numpad3Key.wasPressedThisFrame) SelectSlot(2);
                else if (keyboard.digit4Key.wasPressedThisFrame || keyboard.numpad4Key.wasPressedThisFrame) SelectSlot(3);
            }
            var mouse = Mouse.current;
            if (mouse == null) return;
            var scroll = mouse.scroll.ReadValue().y;
            if (Mathf.Abs(scroll) > 0.01f) CycleSelection(scroll > 0f ? -1 : 1);
            if (!mouse.leftButton.wasPressedThisFrame || GameplayInputGate.IsPointerBlocked || hero.IsDowned) return;
            var definition = slots[selectedSlot];
            if (definition == null || definition.StableId == 103 ||
                definition.Targeting == HeroSkillTargeting.Enemy || GetRemainingCooldown(selectedSlot) > 0f)
                return;
            var point = hero.transform.position;
            if (definition.Targeting == HeroSkillTargeting.Ground)
            {
                var camera = Camera.main;
                if (camera == null || !Physics.Raycast(camera.ScreenPointToRay(mouse.position.ReadValue()), out var hit, 500f)) return;
                point = hit.point;
            }
            RequestUse(definition, point, null);
        }

        private void CycleSelection(int direction)
        {
            for (var offset = 1; offset <= slots.Length; offset++)
            {
                var candidate = (selectedSlot + direction * offset + slots.Length * 2) % slots.Length;
                if (!IsSlotUnlocked(candidate) || slots[candidate] == null) continue;
                SelectSlot(candidate);
                return;
            }
        }

        private bool RequestUse(HeroSkillDefinition definition, Vector3 point, NetworkObject target)
        {
            if (definition == null || hero.IsDowned || BlocksLocalGameplay) return false;
            GameplayInputGate.ConsumeGameplayPointer();
            localReadyAt[selectedSlot] = Time.time + GetEffectiveCooldown(definition);
            hero.RequestSkillUse(definition.StableId, point, target);
            HeroSkillsUI.Instance?.Refresh();
            return true;
        }

        private void OfferCompletedWaveUpgrade()
        {
            if (offeredWave > 0 || BlocksLocalGameplay) return;
            var waves = CoreMissionWaveController.Instance;
            if (waves == null) return;
            var next = localLastChosenWave + 1;
            if (next > 6 || waves.CompletedWaves < next) return;
            var choices = HeroSkillCatalog.Choices(hero.PlayerClass, next);
            if (choices.Length != 2)
            {
                Debug.LogError($"{hero.PlayerClass} Wave {next} requires exactly two skill definitions.", this);
                localLastChosenWave = next;
                return;
            }
            offeredWave = next;
            var popup = SkillUpgradePopupUI.Instance ?? FindFirstObjectByType<SkillUpgradePopupUI>(FindObjectsInactive.Include);
            popup?.Show(this, next, choices);
        }

        private float GetEffectiveServerCooldown(HeroSkillDefinition definition)
        {
            var multiplier = definition.SkillType == HeroSkillType.Active && acquired.Contains(105) ? 0.8f : 1f;
            if (definition.SkillType == HeroSkillType.Basic && acquired.Contains(8) && hero.HealthRatio < 0.3f)
                multiplier *= 0.75f;
            return Mathf.Max(0.05f, definition.Cooldown * multiplier);
        }

        private bool Execute(HeroSkillDefinition skill, Vector3 point, NetworkObject target, double now)
        {
            switch (skill.Effect)
            {
                case HeroSkillEffect.MeleeArc:
                    hero.ServerFace(point);
                    var swung = DamageArc(skill);
                    if (swung && skill.StableId == 1) hero.ServerPlaySkillAction(WarriorAction.Attack, 0.62f);
                    return swung;
                case HeroSkillEffect.RadialDamage:
                    if (skill.StableId == 2)
                    {
                        hero.ServerPlaySkillAction(WarriorAction.Whirlwind, Mathf.Max(5f, skill.Duration));
                        pendingWhirlwindSkill = skill;
                        whirlwindHits.Clear();
                        return true;
                    }
                    return DamageRadius(skill, hero.transform.position);
                case HeroSkillEffect.ShieldBash: return Bash(skill, target);
                case HeroSkillEffect.Charge: return Charge(skill, point, target);
                case HeroSkillEffect.Taunt: return Taunt(skill, now);
                case HeroSkillEffect.SelfBuff: return ApplySelfBuff(skill, now);
                case HeroSkillEffect.SingleProjectile:
                    return skill.StableId == 101 ? QueueArcaneBolt(skill, point, target) : QueueProjectile(skill, point, target);
                case HeroSkillEffect.ExplodingProjectile: return QueueProjectile(skill, point, target);
                case HeroSkillEffect.RadialDebuff:
                    if (skill.StableId == 10) return BeginEarthshatter(skill);
                    return DamageAndDebuffRadius(skill, hero.transform.position);
                case HeroSkillEffect.ChainDamage: return Chain(skill, target);
                case HeroSkillEffect.Blink: return Blink(skill, point);
                case HeroSkillEffect.GroundImpact: return DamageAndDebuffRadius(skill, point);
                case HeroSkillEffect.Vortex: return StartZone(skill, point, now);
                case HeroSkillEffect.RepairPulse:
                    var repaired = Repair(skill);
                    if (repaired && skill.Duration > 0f && skill.SecondaryValue > 0f)
                        StartZone(skill, hero.transform.position, now);
                    return repaired;
                case HeroSkillEffect.ConstructionAura:
                case HeroSkillEffect.BuildingBuff:
                case HeroSkillEffect.HealingArea:
                case HeroSkillEffect.Sanctuary: return StartZone(skill, point, now);
                case HeroSkillEffect.HolyPulse: return HolyPulse(skill);
                case HeroSkillEffect.CleanseWard: return CleanseWard(skill, now);
                case HeroSkillEffect.CoreMend: return CoreMend(skill, now);
                case HeroSkillEffect.DivineIntervention: return DivineIntervention(skill);
                default: return false;
            }
        }

        private bool DamageArc(HeroSkillDefinition skill)
        {
            foreach (var enemy in MeleeTargetsInFront(skill.Radius)) DealDamage(skill, enemy);
            return true;
        }

        private bool DamageRadius(HeroSkillDefinition skill, Vector3 center)
        {
            var hit = false;
            foreach (var enemy in EnemiesInRadius(center, skill.Radius)) { DealDamage(skill, enemy); hit = true; }
            hit |= DamageFriendlyTargets(skill, center);
            return hit || skill.Targeting != HeroSkillTargeting.Enemy;
        }

        private bool DamageAndDebuffRadius(HeroSkillDefinition skill, Vector3 center)
        {
            var hit = false;
            foreach (var enemy in EnemiesInRadius(center, skill.Radius))
            {
                DealDamage(skill, enemy);
                if (skill.Debuff != EnemyDebuff.None) enemy.ApplyDebuff(skill.Debuff, skill.Duration, hero);
                hit = true;
            }
            hit |= DamageFriendlyTargets(skill, center);
            return hit || skill.Targeting != HeroSkillTargeting.Enemy;
        }

        private bool DamageFriendlyTargets(HeroSkillDefinition skill, Vector3 center)
        {
            var hit = false;
            var amount = FriendlyFireDamage(skill);
            foreach (var friendlyHero in HeroesInRadius(center, skill.Radius))
            {
                if (skill.StableId == 103 && friendlyHero == hero) continue;
                friendlyHero.TakeDamage(amount);
                if (skill.Debuff != EnemyDebuff.None)
                    friendlyHero.ServerApplyDebuff(skill.Debuff, Mathf.Max(1f, skill.Duration));
                hit = true;
            }
            foreach (var building in BuildingsInRadius(center, skill.Radius))
            {
                building.Damage(amount);
                hit = true;
            }
            return hit;
        }

        private bool Bash(HeroSkillDefinition skill, NetworkObject target)
        {
            var enemy = target != null ? target.GetComponent<EnemyBrain>() : null;
            if (enemy == null || !enemy.IsAlive || Vector3.Distance(hero.transform.position, enemy.transform.position) > skill.Radius) return false;
            hero.ServerFace(enemy.transform.position);
            var targets = MeleeTargetsInFront(skill.Radius);
            if (targets.Count == 0) return false;
            foreach (var hitEnemy in targets)
            {
                DealDamage(skill, hitEnemy);
                hitEnemy.ApplyDebuff(EnemyDebuff.Stun, skill.Duration);
            }
            hero.ServerPlaySkillAction(WarriorAction.ShieldBash, 0.85f);
            return true;
        }

        private bool Charge(HeroSkillDefinition skill, Vector3 point, NetworkObject target)
        {
            var enemy = target != null ? target.GetComponent<EnemyBrain>() : null;
            if (enemy == null || !enemy.IsAlive) return false;
            var offset = enemy.transform.position - hero.transform.position;
            offset.y = 0f;
            if (offset.magnitude > skill.Radius + 0.35f || offset.sqrMagnitude < 0.1f) return false;
            var destination = enemy.transform.position - offset.normalized * 1.1f;
            if (!hero.ServerBeginLeap(destination, 0.9f)) return false;
            pendingLeapSkill = skill;
            pendingLeapTarget = enemy;
            leapDamageApplied = false;
            return true;
        }

        private bool BeginEarthshatter(HeroSkillDefinition skill)
        {
            hero.ServerPlaySkillAction(WarriorAction.Earthshatter, 1.3f);
            pendingEarthshatterSkill = skill;
            earthshatterImpactApplied = false;
            return true;
        }

        private bool Taunt(HeroSkillDefinition skill, double now)
        {
            foreach (var enemy in EnemiesInRadius(hero.transform.position, skill.Radius)) enemy.ApplyTaunt(hero, skill.Duration);
            selfResistance = Mathf.Clamp01(skill.SecondaryValue);
            selfBuffEndsAt = Math.Max(selfBuffEndsAt, now + skill.Duration);
            return true;
        }

        private bool ApplySelfBuff(HeroSkillDefinition skill, double now)
        {
            selfDamageMultiplier = Mathf.Max(1f, skill.Power);
            selfResistance = Mathf.Clamp01(skill.SecondaryValue);
            selfCcImmune = true;
            selfBuffEndsAt = now + skill.Duration;
            return true;
        }

        private bool QueueProjectile(HeroSkillDefinition skill, Vector3 point, NetworkObject target)
        {
            var enemy = target != null ? target.GetComponent<EnemyBrain>() : null;
            var castRange = skill.Effect == HeroSkillEffect.ExplodingProjectile && skill.SecondaryValue > 0f
                ? skill.SecondaryValue : skill.Radius;
            if (target != null && (enemy == null || !enemy.IsAlive))
                return false;
            var targetPoint = enemy != null ? enemy.transform.position : point;
            if (target == null && skill.Effect != HeroSkillEffect.ExplodingProjectile) return false;
            if (Vector3.Distance(hero.transform.position, targetPoint) > castRange) return false;
            pendingProjectiles.Add(new PendingProjectile
            {
                Skill = skill,
                Target = enemy,
                Position = hero.transform.position + Vector3.up * 0.75f,
                TargetPoint = targetPoint,
                ExpiresAt = hero.NetworkManager.ServerTime.Time + 4d
            });
            return true;
        }

        private bool QueueArcaneBolt(HeroSkillDefinition skill, Vector3 point, NetworkObject target)
        {
            var enemy = target != null ? target.GetComponent<EnemyBrain>() : null;
            var dummy = target != null ? target.GetComponent<CoreDebugDummy>() : null;
            if (target != null && (enemy == null || !enemy.IsAlive) && (dummy == null || dummy.Health <= 0f))
                return false;
            if (target != null && Vector3.Distance(hero.transform.position, target.transform.position) > skill.Radius)
                return false;

            var origin = hero.transform.position + Vector3.up * 0.75f;
            var aimPoint = target != null ? target.transform.position + Vector3.up * 0.75f : point + Vector3.up * 0.75f;
            var direction = aimPoint - origin;
            if (direction.sqrMagnitude < 0.001f) direction = hero.transform.forward;
            direction.Normalize();
            hero.ServerFace(origin + direction);
            pendingArcaneBolts.Add(new PendingArcaneBolt
            {
                Skill = skill,
                Position = origin,
                Direction = direction,
                ExpiresAt = hero.NetworkManager.ServerTime.Time + skill.Radius / ArcaneBoltSpeed
            });
            return true;
        }

        private bool Chain(HeroSkillDefinition skill, NetworkObject target)
        {
            var first = target != null ? target.GetComponent<EnemyBrain>() : null;
            if (first == null || !first.IsAlive || Vector3.Distance(hero.transform.position, first.transform.position) > skill.Radius) return false;
            var hit = new HashSet<EnemyBrain>();
            var current = first;
            for (var jump = 0; jump < Mathf.Max(1, skill.Count) && current != null; jump++)
            {
                DealDamage(skill, current, Mathf.Pow(Mathf.Clamp01(1f - skill.SecondaryValue), jump));
                hit.Add(current);
                current = EnemiesInRadius(current.transform.position, skill.Duration)
                    .Where(enemy => !hit.Contains(enemy)).OrderBy(enemy =>
                        (enemy.transform.position - current.transform.position).sqrMagnitude).FirstOrDefault();
            }
            return true;
        }

        private bool Blink(HeroSkillDefinition skill, Vector3 point)
        {
            var offset = point - hero.transform.position; offset.y = 0f;
            if (offset.magnitude > skill.Radius) point = hero.transform.position + offset.normalized * skill.Radius;
            if (!NavMesh.SamplePosition(point, out var sample, 1.5f, NavMesh.AllAreas)) return false;
            var origin = hero.transform.position;
            hero.ServerWarp(sample.position);
            foreach (var enemy in EnemiesInRadius(origin, skill.SecondaryValue))
                enemy.ApplyDebuff(EnemyDebuff.Chill, skill.Duration, hero);
            return true;
        }

        private bool Repair(HeroSkillDefinition skill)
        {
            var repaired = false;
            foreach (var building in BuildingsInRadius(hero.transform.position, skill.Radius))
            { building.BuildOrRepair(Mathf.RoundToInt(skill.Power)); repaired = true; }
            return repaired;
        }

        private bool StartZone(HeroSkillDefinition skill, Vector3 point, double now)
        {
            activeZones.Add(new ActiveZone { Skill = skill, Position = point, EndsAt = now + skill.Duration, NextTick = now });
            return true;
        }

        private bool HolyPulse(HeroSkillDefinition skill)
        {
            foreach (var ally in HeroesInRadius(hero.transform.position, skill.Radius))
                ally.ServerHeal(ModifyHealingFor(ally, skill.Power));
            foreach (var enemy in EnemiesInRadius(hero.transform.position, skill.Radius))
                if (enemy.EnemyType == CoreEnemyType.Undead) DealDamage(skill, enemy);
            return true;
        }

        private bool CleanseWard(HeroSkillDefinition skill, double now)
        {
            foreach (var ally in HeroesInRadius(hero.transform.position, skill.Radius))
                ally.ServerGrantStatusProtection(skill.Duration);
            return true;
        }

        private bool CoreMend(HeroSkillDefinition skill, double now)
        {
            var core = CoreDebugDeposit.Instance;
            if (core == null || Vector3.Distance(hero.transform.position, core.transform.position) > skill.Radius) return false;
            core.Heal(skill.Power);
            core.ApplyDamageResistance(skill.SecondaryValue, skill.Duration);
            return true;
        }

        private bool DivineIntervention(HeroSkillDefinition skill)
        {
            foreach (var ally in HeroesInRadius(hero.transform.position, skill.Radius))
            {
                if (ally.IsDowned) ally.ServerRevive(0.4f);
                else ally.ServerHeal(ModifyHealingFor(ally, skill.Power));
            }
            return true;
        }

        private void DealDamage(HeroSkillDefinition skill, EnemyBrain enemy, float chainMultiplier = 1f)
        {
            var amount = skill.Power * chainMultiplier * selfDamageMultiplier * hero.OutgoingDamageMultiplier;
            if (skill.SkillType == HeroSkillType.Basic && acquired.Contains(5)) amount *= 1.2f;
            if (skill.SkillType == HeroSkillType.Active && acquired.Contains(104)) amount *= 1.2f;
            if (skill.SkillType == HeroSkillType.Active && acquired.Contains(109)) amount *= 1.25f;
            if (hero.PlayerClass == CorePlayerClass.Warrior && hero.HealthRatio < 0.3f && acquired.Contains(8)) amount *= 1.4f;
            if (acquired.Contains(13) && enemy.Health / Mathf.Max(1f, enemy.MaximumHealth) < 0.25f) amount *= 1.5f;
            if (acquired.Contains(12) && EnemiesInRadius(hero.transform.position, 7f).Count >= 10) amount *= 1.35f;
            if (hero.PlayerClass == CorePlayerClass.Healer && enemy.EnemyType == CoreEnemyType.Undead)
                amount *= 1.75f * (acquired.Contains(309) ? 1.35f : 1f);
            if (skill.SkillType == HeroSkillType.Active && acquired.Contains(112) && arcaneMasteryStacks > 0)
            { amount *= 1f + arcaneMasteryStacks * 0.1f; arcaneMasteryStacks = 0; }
            enemy.TakeDamage(amount, hero, skill.SkillType == HeroSkillType.Active);
            if (skill.Debuff != EnemyDebuff.None) enemy.ApplyDebuff(skill.Debuff, skill.Duration, hero);
            if (acquired.Contains(108) && skill.SkillType == HeroSkillType.Active)
                enemy.ApplyArcaneExposure(hero.OwnerClientId, 5f, 0.15f);
        }

        private void ApplyImmediatePassive(HeroSkillDefinition definition)
        {
            if (definition.SkillType != HeroSkillType.Passive) return;
            if (definition.StableId == 4) hero.ServerMultiplyMaximumHealth(1.25f);
            else if (definition.StableId == 109) hero.ServerMultiplyMaximumHealth(0.85f);
        }

        private void UpdateServerState()
        {
            var now = hero.NetworkManager.ServerTime.Time;
            UpdateArcaneBolts(now);
            UpdateProjectiles(now);
            UpdateFirePatches(now);
            UpdatePendingWarriorImpacts();
            if (selfBuffEndsAt > 0d && now >= selfBuffEndsAt)
            {
                selfBuffEndsAt = 0d; selfDamageMultiplier = 1f; selfResistance = 0f; selfCcImmune = false;
            }
            UpdateZones(now);
            if (now < nextPassiveTick) return;
            nextPassiveTick = now + 1d;
            if (acquired.Contains(304))
                foreach (var ally in HeroesInRadius(hero.transform.position, 5f))
                    if (ally != hero && !ally.IsDowned) ally.ServerHeal(2f);
            if (acquired.Contains(305))
                foreach (var ally in HeroesInRadius(hero.transform.position, 6f))
                    ally.ServerGrantDamageBonus(1.1f, 1.2f);
            if (acquired.Contains(308))
            {
                foreach (var ally in HeroesInRadius(hero.transform.position, 6f))
                {
                    if (ally == hero || ally.IsDowned || ally.HealthRatio >= 0.25f) continue;
                    if (guardianAngelReadyAt.TryGetValue(ally.NetworkObjectId, out var ready) && now < ready) continue;
                    guardianAngelReadyAt[ally.NetworkObjectId] = now + 20d;
                    ally.ServerHeal(15f);
                }
            }
            if (acquired.Contains(212))
                foreach (var building in BuildingsInRadius(hero.transform.position, 5f)) building.BuildOrRepair(2);
            if (acquired.Contains(213))
                foreach (var building in BuildingsInRadius(hero.transform.position, 6f))
                    building.ApplyTimedModifiers(hero.NetworkObjectId, 1.1f, 1.1f, 1f, 0.1f, 1.2f);
        }

        private void UpdateArcaneBolts(double now)
        {
            for (var index = pendingArcaneBolts.Count - 1; index >= 0; index--)
            {
                var bolt = pendingArcaneBolts[index];
                if (now >= bolt.ExpiresAt)
                {
                    pendingArcaneBolts.RemoveAt(index);
                    continue;
                }

                var distance = ArcaneBoltSpeed * Time.deltaTime;
                var hits = Physics.SphereCastAll(bolt.Position, 0.22f, bolt.Direction, distance,
                    Physics.AllLayers, QueryTriggerInteraction.Collide);
                Array.Sort(hits, (left, right) => left.distance.CompareTo(right.distance));
                var consumed = false;
                foreach (var hit in hits)
                {
                    var friendlyHero = hit.collider.GetComponentInParent<NetworkWarrior>();
                    if (friendlyHero != null)
                    {
                        if (friendlyHero == hero && bolt.DistanceTraveled < 1.25f) continue;
                        friendlyHero.TakeDamage(ArcaneBoltDamage(bolt.Skill));
                        hero.ServerPresentProjectileImpact(bolt.Skill.StableId,
                            hit.point - bolt.Direction * 0.06f, bolt.Direction);
                        consumed = true;
                        break;
                    }
                    var building = hit.collider.GetComponentInParent<CoreBuilding>();
                    if (building != null)
                    {
                        building.Damage(ArcaneBoltDamage(bolt.Skill));
                        hero.ServerPresentProjectileImpact(bolt.Skill.StableId,
                            hit.point - bolt.Direction * 0.06f, bolt.Direction);
                        consumed = true;
                        break;
                    }
                    var enemy = hit.collider.GetComponentInParent<EnemyBrain>();
                    var dummy = hit.collider.GetComponentInParent<CoreDebugDummy>();
                    if (enemy != null && enemy.IsAlive)
                    {
                        DealDamage(bolt.Skill, enemy);
                        hero.ServerPresentProjectileImpact(bolt.Skill.StableId,
                            hit.point - bolt.Direction * 0.06f, bolt.Direction);
                        consumed = true;
                        break;
                    }
                    if (dummy != null && dummy.Health > 0f)
                    {
                        dummy.TakeDamage(ArcaneBoltDamage(bolt.Skill));
                        hero.ServerPresentProjectileImpact(bolt.Skill.StableId,
                            hit.point - bolt.Direction * 0.06f, bolt.Direction);
                        consumed = true;
                        break;
                    }
                    if (!hit.collider.isTrigger)
                    {
                        hero.ServerDismissProjectile(bolt.Skill.StableId, hit.point, bolt.Direction);
                        consumed = true;
                        break;
                    }
                }

                if (consumed)
                {
                    pendingArcaneBolts.RemoveAt(index);
                    continue;
                }
                bolt.Position += bolt.Direction * distance;
                bolt.DistanceTraveled += distance;
            }
        }

        private float ArcaneBoltDamage(HeroSkillDefinition skill)
        {
            var amount = skill.Power * selfDamageMultiplier * hero.OutgoingDamageMultiplier;
            if (skill.SkillType == HeroSkillType.Active && acquired.Contains(104)) amount *= 1.2f;
            if (skill.SkillType == HeroSkillType.Active && acquired.Contains(109)) amount *= 1.25f;
            return amount;
        }

        private void UpdateProjectiles(double now)
        {
            for (var index = pendingProjectiles.Count - 1; index >= 0; index--)
            {
                var projectile = pendingProjectiles[index];
                if ((projectile.Target != null && !projectile.Target.IsAlive) || now >= projectile.ExpiresAt)
                {
                    pendingProjectiles.RemoveAt(index);
                    continue;
                }
                var impactPoint = projectile.Target != null ? projectile.Target.transform.position : projectile.TargetPoint;
                var targetPoint = impactPoint + Vector3.up * 0.75f;
                var offset = targetPoint - projectile.Position;
                var step = HeroProjectileSpeed * Time.deltaTime;
                if (offset.magnitude > step)
                {
                    projectile.Position += offset.normalized * step;
                    continue;
                }
                pendingProjectiles.RemoveAt(index);
                if (projectile.Skill.Effect == HeroSkillEffect.ExplodingProjectile)
                {
                    DamageRadius(projectile.Skill, impactPoint);
                    if (projectile.Skill.StableId == 102)
                        pendingFirePatches.Add(new PendingFirePatch
                        {
                            Position = impactPoint,
                            Radius = projectile.Skill.Radius,
                            EndsAt = now + projectile.Skill.Duration,
                            NextTick = now
                        });
                }
                else
                    DealDamage(projectile.Skill, projectile.Target);
            }
        }

        private void UpdateFirePatches(double now)
        {
            const double tickInterval = 0.25d;
            const float fireDamagePerSecond = 3f;
            for (var index = pendingFirePatches.Count - 1; index >= 0; index--)
            {
                var patch = pendingFirePatches[index];
                if (now >= patch.EndsAt)
                {
                    pendingFirePatches.RemoveAt(index);
                    continue;
                }
                if (now < patch.NextTick) continue;
                patch.NextTick = now + tickInterval;
                foreach (var friendlyHero in HeroesInRadius(patch.Position, patch.Radius))
                    friendlyHero.ServerIgnite(2f, fireDamagePerSecond);
                foreach (var enemy in EnemiesInRadius(patch.Position, patch.Radius))
                    enemy.ApplyDebuff(EnemyDebuff.OnFire, 2f, hero);
                foreach (var building in BuildingsInRadius(patch.Position, patch.Radius))
                    building.Damage(fireDamagePerSecond * (float)tickInterval);
            }
        }

        private float FriendlyFireDamage(HeroSkillDefinition skill)
        {
            var amount = skill.Power * selfDamageMultiplier * hero.OutgoingDamageMultiplier;
            if (skill.SkillType == HeroSkillType.Basic && acquired.Contains(5)) amount *= 1.2f;
            if (skill.SkillType == HeroSkillType.Active && acquired.Contains(104)) amount *= 1.2f;
            if (skill.SkillType == HeroSkillType.Active && acquired.Contains(109)) amount *= 1.25f;
            return amount;
        }

        private void UpdatePendingWarriorImpacts()
        {
            if (pendingWhirlwindSkill != null && hero.CurrentAction == WarriorAction.Whirlwind)
            {
                foreach (var enemy in MeleeTargetsAround(pendingWhirlwindSkill.Radius))
                    if (whirlwindHits.Add(enemy)) DealDamage(pendingWhirlwindSkill, enemy);
            }
            else if (pendingWhirlwindSkill != null)
            {
                pendingWhirlwindSkill = null;
                whirlwindHits.Clear();
            }

            if (pendingLeapSkill != null && !leapDamageApplied && hero.ActionProgress >= 0.82f)
            {
                leapDamageApplied = true;
                if (pendingLeapTarget != null && pendingLeapTarget.IsAlive)
                {
                    hero.ServerFace(pendingLeapTarget.transform.position);
                    if (MeleeTargetsInFront(2.6f).Contains(pendingLeapTarget))
                    DealDamage(pendingLeapSkill, pendingLeapTarget);
                }
            }
            if (pendingLeapSkill != null && hero.CurrentAction != WarriorAction.BattleCharge)
            {
                pendingLeapSkill = null;
                pendingLeapTarget = null;
            }

            if (pendingEarthshatterSkill != null && !earthshatterImpactApplied && hero.ActionProgress >= 0.68f)
            {
                earthshatterImpactApplied = true;
                DamageAndDebuffRadius(pendingEarthshatterSkill, hero.transform.position);
            }
            if (pendingEarthshatterSkill != null && hero.CurrentAction != WarriorAction.Earthshatter)
                pendingEarthshatterSkill = null;
        }

        private void UpdateZones(double now)
        {
            for (var index = activeZones.Count - 1; index >= 0; index--)
            {
                var zone = activeZones[index];
                if (now >= zone.EndsAt) { activeZones.RemoveAt(index); continue; }
                if (now < zone.NextTick) continue;
                zone.NextTick = now + 1d;
                var skill = zone.Skill;
                if (skill.Effect == HeroSkillEffect.HealingArea || skill.Effect == HeroSkillEffect.Sanctuary)
                {
                    foreach (var ally in HeroesInRadius(zone.Position, skill.Radius))
                    {
                        ally.ServerHeal(ModifyHealingFor(ally, skill.Power));
                        if (skill.Effect == HeroSkillEffect.Sanctuary)
                            ally.ServerGrantDamageResistance(skill.SecondaryValue, 1.2f);
                    }
                    foreach (var enemy in EnemiesInRadius(zone.Position, skill.Radius))
                        if (enemy.EnemyType == CoreEnemyType.Undead) DealDamage(skill, enemy);
                }
                else if (skill.Effect == HeroSkillEffect.Vortex)
                {
                    foreach (var enemy in EnemiesInRadius(zone.Position, skill.Radius))
                    { DealDamage(skill, enemy); enemy.PullToward(zone.Position, skill.SecondaryValue); }
                }
                else if (skill.Effect == HeroSkillEffect.BuildingBuff)
                {
                    foreach (var building in BuildingsInRadius(zone.Position, skill.Radius))
                        building.ApplyTimedModifiers(hero.NetworkObjectId, 1f + skill.Power, 1f + skill.SecondaryValue,
                            1.15f, skill.Power <= 0f && skill.SecondaryValue <= 0f ? 0.55f : 0f, 1.2f);
                }
                else if (skill.Effect == HeroSkillEffect.RepairPulse)
                    foreach (var building in BuildingsInRadius(zone.Position, skill.Radius))
                        building.BuildOrRepair(Mathf.RoundToInt(skill.SecondaryValue));
                else if (skill.Effect == HeroSkillEffect.ConstructionAura)
                {
                    foreach (var ally in HeroesInRadius(zone.Position, skill.Radius)) ally.ServerGrantWorkSpeed(2f, 1.2f);
                }
            }
        }

        private static List<EnemyBrain> EnemiesInRadius(Vector3 center, float radius) =>
            FindObjectsByType<EnemyBrain>(FindObjectsSortMode.None).Where(enemy => enemy.IsAlive &&
                (enemy.transform.position - center).sqrMagnitude <= radius * radius).ToList();

        private List<EnemyBrain> MeleeTargetsInFront(float reach, Vector3? requestedForward = null)
        {
            var forward = requestedForward ?? hero.transform.forward;
            forward.y = 0f;
            if (forward.sqrMagnitude < 0.01f) forward = Vector3.forward;
            forward.Normalize();
            var safeReach = Mathf.Max(0.5f, reach);
            var center = hero.transform.position + Vector3.up * 0.9f + forward * (safeReach * 0.5f);
            var halfExtents = new Vector3(Mathf.Max(0.65f, safeReach * 0.42f), 1f, safeReach * 0.5f);
            var rotation = Quaternion.LookRotation(forward, Vector3.up);
            var hits = Physics.OverlapBox(center, halfExtents, rotation, Physics.AllLayers, QueryTriggerInteraction.Collide);
            var targets = new HashSet<EnemyBrain>();
            foreach (var hit in hits)
            {
                var enemy = hit.GetComponentInParent<EnemyBrain>();
                if (enemy != null && enemy.IsAlive) targets.Add(enemy);
            }
            return targets.ToList();
        }

        private List<EnemyBrain> MeleeTargetsAround(float radius)
        {
            var safeRadius = Mathf.Max(0.5f, radius);
            var bottom = hero.transform.position + Vector3.up * 0.25f;
            var top = hero.transform.position + Vector3.up * 1.65f;
            var hits = Physics.OverlapCapsule(bottom, top, safeRadius, Physics.AllLayers,
                QueryTriggerInteraction.Collide);
            var targets = new HashSet<EnemyBrain>();
            foreach (var hit in hits)
            {
                var enemy = hit.GetComponentInParent<EnemyBrain>();
                if (enemy != null && enemy.IsAlive) targets.Add(enemy);
            }
            return targets.ToList();
        }
        private static IEnumerable<NetworkWarrior> HeroesInRadius(Vector3 center, float radius) =>
            FindObjectsByType<NetworkWarrior>(FindObjectsSortMode.None).Where(ally => ally.IsSpawned &&
                (ally.transform.position - center).sqrMagnitude <= radius * radius);
        private static IEnumerable<CoreBuilding> BuildingsInRadius(Vector3 center, float radius) =>
            FindObjectsByType<CoreBuilding>(FindObjectsSortMode.None).Where(building => building.IsSpawned &&
                (building.transform.position - center).sqrMagnitude <= radius * radius);
    }
}
