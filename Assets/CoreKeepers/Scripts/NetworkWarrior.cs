using System.Collections.Generic;
using Unity.Collections;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

namespace CoreKeepers
{
    public enum WarriorAction : byte
    {
        None,
        Attack,
        Build,
        Mine,
        Deposit,
        Revive,
        Whirlwind,
        ShieldBash,
        BattleCharge,
        Earthshatter,
        CastProjectile,
        CastSpellUp,
        CastSpellAround
    }
    public enum ContextInteraction : byte { None, AttackEnemy, MineResource, BuildOrRepair, RevivePlayer, DepositResources }
    public enum CorePlayerClass : byte { Warrior, Mage, Builder, Healer }

    [RequireComponent(typeof(NavMeshAgent))]
    public sealed class NetworkWarrior : NetworkBehaviour
    {
        private const int DefaultCarryingCapacity = 20;
        private const int BuilderCarryingCapacity = 30;
        private const float MinimumIncomingDamage = 1f;
        private const ulong NoResurrector = ulong.MaxValue;
        private const float WhirlwindTurns = 15f;

        [Header("Point And Click Movement")]
        [SerializeField, Min(0.1f)] private float movementSpeed = 6f;
        [SerializeField, Min(0.1f)] private float acceleration = 28f;
        [SerializeField, Min(1f)] private float rotationSpeed = 720f;
        [SerializeField, Min(0.1f)] private float groundSampleRadius = 2.5f;
        [SerializeField, Min(0.01f)] private float heldCommandInterval = 0.05f;
        [SerializeField, Min(0.01f)] private float groundRetargetDistance = 0.15f;
        [SerializeField] private CorePlayerClass playerClass = CorePlayerClass.Warrior;
        [SerializeField] private Material arcaneMageMaterial;
        [Header("Attack")]
        [SerializeField, Min(0.05f)] private float attackDuration = 0.62f;
        [SerializeField, Min(0f)] private float attackCooldown = 0.72f;
        [SerializeField, Range(5f, 180f)] private float attackArc = 100f;
        [SerializeField, Min(0.1f)] private float attackRange = 2.3f;
        [SerializeField, Min(0f)] private float damage = 25f;
        [Header("Interactions")]
        [SerializeField, Min(0.1f)] private float buildDuration = 0.75f;
        [SerializeField, Min(0.1f)] private float mineDuration = 0.8f;
        [SerializeField, Min(0.1f)] private float mineRange = 1.7f;
        [SerializeField, Min(0.1f)] private float buildRange = 1.5f;
        [SerializeField, Min(0.1f)] private float depositDuration = 0.65f;
        [Header("Resurrection")]
        [SerializeField, Min(0.1f)] private float resurrectionDuration = 10f;
        [SerializeField, Min(0.1f)] private float healerResurrectionDuration = 5f;
        [SerializeField, Range(0.01f, 1f)] private float revivedHealthFraction = 0.3f;
        [SerializeField, Min(0.01f)] private float resurrectionMoveTolerance = 0.15f;
        [SerializeField, Min(0.1f)] private float interactionRange = 2.6f;
        [SerializeField, Min(1)] private int constructionPower = 15;
        [Header("Hero Stats")]
        [SerializeField, Min(1)] private int startingLevel = 1;
        [SerializeField, Min(1f)] private float configuredMaximumHealth = 250f;

        private readonly NetworkVariable<WarriorAction> action = new(WarriorAction.None);
        private readonly NetworkVariable<double> actionStartedAt = new(0d);
        private readonly NetworkVariable<double> actionEndsAt = new(0d);
        private readonly NetworkVariable<byte> actionVariant = new(0);
        private readonly NetworkVariable<CorePlayerClass> syncedPlayerClass = new(CorePlayerClass.Warrior);
        private readonly NetworkVariable<int> carriedOre = new(0);
        private readonly NetworkVariable<int> carriedCoreShards = new(0);
        private readonly NetworkVariable<int> playerLevel = new(1);
        private readonly NetworkVariable<float> currentHealth = new(100f);
        private readonly NetworkVariable<float> maximumHealth = new(100f);
        private readonly NetworkVariable<FixedString64Bytes> nickname = new(new FixedString64Bytes("Player"));
        private readonly NetworkVariable<bool> downed = new(false);
        private readonly NetworkVariable<bool> burning = new(false);
        private readonly NetworkVariable<EnemyDebuff> activeDebuffs = new(EnemyDebuff.None);
        private readonly NetworkVariable<int> reviveProgress = new(0);
        private readonly NetworkVariable<ulong> resurrectorId = new(NoResurrector);
        private readonly NetworkVariable<double> resurrectionStartedAt = new(0d);
        private readonly NetworkVariable<double> resurrectionEndsAt = new(0d);
        private readonly NetworkVariable<Vector3> leapStart = new(Vector3.zero);
        private readonly NetworkVariable<Vector3> leapEnd = new(Vector3.zero);
        private readonly NetworkVariable<double> arcaneSpeedEndsAt = new(0d);
        private readonly NetworkVariable<float> arcaneSpeedMultiplier = new(1f);
        private readonly NetworkVariable<Vector3> gravityVortexCenter = new(Vector3.zero);
        private readonly NetworkVariable<double> gravityVortexEndsAt = new(0d);
        private readonly NetworkVariable<float> gravityVortexStrength = new(0f);
        private NavMeshAgent agent;
        private NetworkObject interactionTarget;
        private ContextInteraction interaction;
        private Vector3 previousPosition;
        private float normalizedSpeed;
        private double nextInteractionAt;
        private float nextHeldCommandAt;
        private Vector3 lastGroundDestination;
        private bool hasGroundDestination;
        private byte nextAttackVariant;
        private bool preserveSpawnTransform;
        private NetworkWarrior resurrectionTarget;
        private Vector3 resurrectionStartPosition;
        private uint resurrectionDamageRevision;
        private uint damageRevision;
        private GameObject playerMarker;
        private HeroSkillController heroSkills;
        private double statusProtectionEndsAt;
        private double workSpeedEndsAt;
        private float workSpeedMultiplier = 1f;
        private double externalDamageBonusEndsAt;
        private float externalDamageMultiplier = 1f;
        private double externalResistanceEndsAt;
        private float externalResistance;
        private double burningEndsAt;
        private double nextBurnDamageAt;
        private float burningDamagePerSecond;
        private readonly double[] debuffEndsAt = new double[6];
        private double nextPoisonDamageAt;
        private bool leapMovementActive;
        private bool whirlwindMovementActive;
        private Quaternion whirlwindStartRotation;
        private readonly Dictionary<Renderer, Material[]> arcaneOriginalMaterials = new();
        private readonly List<TrailRenderer> arcaneTrails = new();
        private Material arcaneTrailMaterial;
        private bool arcaneSpeedVisualActive;
        private bool gravityVortexMovementActive;
        private bool gravityVortexRecoveryActive;
        private float gravityVortexRecoveryStartedAt;
        private Vector3 gravityVortexRecoveryStart;
        private Vector3 gravityVortexRecoveryTarget;
        private Vector3 gravityVortexVelocity;
        private static int lastDebugClassSwitchFrame = -1;

        public static NetworkWarrior Local { get; private set; }
        public WarriorAction CurrentAction => action.Value;
        public int CarriedResources => carriedOre.Value + carriedCoreShards.Value;
        public int CarryingCapacity => Mathf.RoundToInt(GetCarryingCapacity(syncedPlayerClass.Value) *
            (heroSkills != null ? heroSkills.CarryingCapacityMultiplier : 1f));
        public int CarriedOre => carriedOre.Value;
        public int CarriedCoreShards => carriedCoreShards.Value;
        public int PlayerNumber => (int)(OwnerClientId % (ulong)CoreSessionManager.PlayerLimit) + 1;
        public int PlayerLevel => playerLevel.Value;
        public float CurrentHealth => currentHealth.Value;
        public float MaximumHealth => maximumHealth.Value;
        public float HealthRatio => maximumHealth.Value > 0f ? currentHealth.Value / maximumHealth.Value : 0f;
        public string Nickname => nickname.Value.ToString();
        public float NormalizedSpeed => IsGravityVortexed || gravityVortexMovementActive ||
            gravityVortexRecoveryActive ? 0f : normalizedSpeed;
        public int ActionVariant => actionVariant.Value;
        public CorePlayerClass PlayerClass => syncedPlayerClass.Value;
        public float Defense => GetBaseDefense(syncedPlayerClass.Value);
        public float InteractionRange => interactionRange;
        public bool IsDowned => downed.Value;
        public bool IsGravityVortexed => IsSpawned && NetworkManager != null &&
            NetworkManager.ServerTime.Time < gravityVortexEndsAt.Value;
        public bool IsArcaneSpeedActive => IsSpawned && NetworkManager != null &&
            NetworkManager.ServerTime.Time < arcaneSpeedEndsAt.Value;
        public bool IsBurning => burning.Value;
        public EnemyDebuff ActiveDebuffs => activeDebuffs.Value | (burning.Value ? EnemyDebuff.OnFire : EnemyDebuff.None);
        public int ReviveProgress => Mathf.RoundToInt(ResurrectionProgress * 100f);
        public bool IsBeingResurrected => resurrectorId.Value != NoResurrector;
        public float ResurrectionProgress
        {
            get
            {
                if (!IsBeingResurrected || NetworkManager == null) return 0f;
                var duration = resurrectionEndsAt.Value - resurrectionStartedAt.Value;
                return duration <= 0d ? 0f : Mathf.Clamp01((float)((NetworkManager.ServerTime.Time -
                    resurrectionStartedAt.Value) / duration));
            }
        }
        public float ActionProgress
        {
            get
            {
                var duration = actionEndsAt.Value - actionStartedAt.Value;
                return duration <= 0d || NetworkManager == null ? 1f :
                    Mathf.Clamp01((float)((NetworkManager.ServerTime.Time - actionStartedAt.Value) / duration));
            }
        }

        private void Awake()
        {
            agent = GetComponent<NavMeshAgent>();
            heroSkills = GetComponent<HeroSkillController>() ?? gameObject.AddComponent<HeroSkillController>();
            agent.speed = movementSpeed;
            agent.acceleration = acceleration;
            agent.angularSpeed = rotationSpeed;
            agent.stoppingDistance = 0.08f;
            agent.autoBraking = true;
            previousPosition = transform.position;
        }

        public override void OnNetworkSpawn()
        {
            burning.OnValueChanged += OnBurningChanged;
            agent.enabled = IsOwner;
            if (IsServer)
            {
                arcaneSpeedEndsAt.Value = 0d;
                arcaneSpeedMultiplier.Value = 1f;
                burning.Value = false;
                burningEndsAt = 0d;
                nextBurnDamageAt = 0d;
                burningDamagePerSecond = 0f;
                activeDebuffs.Value = EnemyDebuff.None;
                System.Array.Clear(debuffEndsAt, 0, debuffEndsAt.Length);
                syncedPlayerClass.Value = playerClass;
                playerLevel.Value = startingLevel;
                maximumHealth.Value = configuredMaximumHealth;
                currentHealth.Value = maximumHealth.Value;
                if (!preserveSpawnTransform)
                {
                    var slot = (int)(OwnerClientId % CoreSessionManager.PlayerLimit);
                    var offsets = new[] { new Vector3(-1.5f, 0f, -1.5f), new Vector3(1.5f, 0f, -1.5f),
                        new Vector3(-1.5f, 0f, 1.5f), new Vector3(1.5f, 0f, 1.5f) };
                    transform.position = offsets[slot];
                }
            }
            if (IsOwner)
            {
                Local = this;
                SetNicknameRpc(string.IsNullOrWhiteSpace(CoreSettings.Nickname) ? "Player" : CoreSettings.Nickname);
            }
            heroSkills?.InitializeForMission();
            AttachPlayerMarker();
            HeroCombatVfx.SetCharacterBurning(transform, burning.Value);
        }

        public override void OnNetworkDespawn()
        {
            burning.OnValueChanged -= OnBurningChanged;
            HeroCombatVfx.SetCharacterBurning(transform, false);
            SetArcaneSpeedVisual(false);
            if (agent != null) agent.speed = movementSpeed;
            CancelLocalGravityVortex();
            if (IsServer) CancelResurrectionChannel();
            if (Local == this) Local = null;
        }

        private void Update()
        {
            UpdateArcaneSpeed();
            var delta = transform.position - previousPosition;
            delta.y = 0f;
            var measuredSpeed = delta.magnitude / Mathf.Max(Time.deltaTime, 0.0001f);
            if (IsOwner && agent != null && agent.enabled && agent.isOnNavMesh)
                measuredSpeed = agent.velocity.magnitude;
            var targetSpeed = Mathf.Clamp01(measuredSpeed / movementSpeed);
            var smoothingRate = targetSpeed > normalizedSpeed ? 9f : 12f;
            normalizedSpeed = Mathf.MoveTowards(normalizedSpeed, targetSpeed, smoothingRate * Time.deltaTime);
            previousPosition = transform.position;
            if (IsServer)
            {
                UpdateResurrectionChannel();
                UpdateBurning();
                UpdateDebuffs();
            }
            if (IsServer && action.Value != WarriorAction.None && NetworkManager.ServerTime.Time >= actionEndsAt.Value)
            {
                if (action.Value == WarriorAction.BattleCharge)
                    FinishLeapRpc(leapEnd.Value);
                action.Value = WarriorAction.None;
            }
            if (IsOwner && IsGravityVortexed)
            {
                UpdateLocalGravityVortex();
                return;
            }
            if (IsOwner && gravityVortexMovementActive)
                BeginLocalGravityVortexRecovery();
            if (IsOwner && gravityVortexRecoveryActive)
            {
                UpdateLocalGravityVortexRecovery();
                return;
            }
            if (!IsOwner || SceneManager.GetActiveScene().name != CoreSessionManager.DebugSceneName)
                return;
            UpdateSpecialMovement();
            if (Keyboard.current != null && Keyboard.current.cKey.wasPressedThisFrame &&
                lastDebugClassSwitchFrame != Time.frameCount)
            {
                lastDebugClassSwitchFrame = Time.frameCount;
                CycleDebugClassRpc();
                return;
            }
            if (Keyboard.current != null && Keyboard.current.pKey.wasPressedThisFrame)
                heroSkills?.TryGrantNextDebugLevel();
            if (downed.Value)
            {
                if (agent != null && agent.enabled && agent.isOnNavMesh) agent.ResetPath();
                return;
            }
            if (action.Value is WarriorAction.BattleCharge or WarriorAction.Earthshatter)
                return;
            if (Keyboard.current != null && Keyboard.current.kKey.wasPressedThisFrame)
                SetDebugDownedRpc(true);
            if (Keyboard.current != null && Keyboard.current.rKey.wasPressedThisFrame)
                TryStartResurrection(FindNearestDownedHero(interactionRange));
            if (heroSkills == null || !heroSkills.BlocksLocalGameplay)
                HandleLeftPointer();
            UpdateContextInteraction();
        }

        private void HandleLeftPointer()
        {
            var mouse = Mouse.current;
            if (mouse == null || !mouse.leftButton.isPressed ||
                GameplayInputGate.IsPointerBlocked) return;
            if (!mouse.leftButton.wasPressedThisFrame && Time.unscaledTime < nextHeldCommandAt) return;
            nextHeldCommandAt = Time.unscaledTime + heldCommandInterval;
            var camera = Camera.main;
            if (camera == null) return;
            var pointerRay = camera.ScreenPointToRay(mouse.position.ReadValue());
            if (!Physics.Raycast(pointerRay, out var hit, 500f))
            {
                if (IsShiftHeld())
                {
                    var direction = pointerRay.direction;
                    direction.y = 0f;
                    if (direction.sqrMagnitude < 0.01f) direction = transform.forward;
                    var attackPoint = transform.position + direction.normalized *
                        Mathf.Max(attackRange, heroSkills != null ? heroSkills.SelectedUseRange : attackRange);
                    StopForForcedAttack(attackPoint);
                    heroSkills?.TryUseSelectedInPlace(attackPoint);
                }
                return;
            }

            var player = hit.collider.GetComponentInParent<NetworkWarrior>();
            if (player != null && player != this && player.IsDowned) { BeginInteraction(player.NetworkObject, ContextInteraction.RevivePlayer); return; }
            if (player == this) return;
            var enemy = hit.collider.GetComponentInParent<EnemyBrain>();
            var dummy = hit.collider.GetComponentInParent<CoreDebugDummy>();
            if (IsShiftHeld())
            {
                StopForForcedAttack(hit.point);
                if (enemy != null) heroSkills?.TryUseSelectedOnTarget(enemy.NetworkObject);
                else if (dummy != null) heroSkills?.TryUseSelectedOnTarget(dummy.NetworkObject);
                else heroSkills?.TryUseSelectedInPlace(hit.point);
                return;
            }
            if (enemy != null)
            {
                BeginInteraction(enemy.NetworkObject, ContextInteraction.AttackEnemy);
                return;
            }
            if (dummy != null) { BeginInteraction(dummy.NetworkObject, ContextInteraction.AttackEnemy); return; }
            var resource = hit.collider.GetComponentInParent<CoreDebugResourceNode>();
            if (resource != null) { BeginInteraction(resource.NetworkObject, ContextInteraction.MineResource); return; }
            var building = hit.collider.GetComponentInParent<CoreBuilding>();
            if (building != null && (building.State == CoreBuildingState.UnderConstruction || building.State == CoreBuildingState.Damaged))
            { BeginInteraction(building.NetworkObject, ContextInteraction.BuildOrRepair); return; }
            var core = hit.collider.GetComponentInParent<CoreDebugDeposit>();
            if (core != null) { BeginInteraction(core.NetworkObject, ContextInteraction.DepositResources); return; }
            SetGroundDestination(hit.point);
        }

        private void SetGroundDestination(Vector3 requested)
        {
            if (!NavMesh.SamplePosition(requested, out var hit, groundSampleRadius, NavMesh.AllAreas)) return;
            if (interaction == ContextInteraction.None && hasGroundDestination &&
                (lastGroundDestination - hit.position).sqrMagnitude < groundRetargetDistance * groundRetargetDistance) return;
            ClearInteraction();
            EnsureAgentOnNavMesh(hit.position);
            agent.stoppingDistance = 0.08f;
            agent.SetDestination(hit.position);
            lastGroundDestination = hit.position;
            hasGroundDestination = true;
        }

        private void BeginInteraction(NetworkObject target, ContextInteraction requested)
        {
            if (target != null && interactionTarget == target && interaction == requested) return;
            interactionTarget = target;
            interaction = requested;
            nextInteractionAt = 0d;
            hasGroundDestination = false;
            if (target == null) return;
            EnsureAgentOnNavMesh(transform.position);
            agent.stoppingDistance = GetRange(requested) * 0.9f;
            agent.SetDestination(target.transform.position);
        }

        private void UpdateContextInteraction()
        {
            if (interaction == ContextInteraction.None || interactionTarget == null || !interactionTarget.IsSpawned)
            { if (interaction != ContextInteraction.None) ClearInteraction(); return; }
            if (!ShouldContinue()) { ClearInteraction(); return; }
            var targetPosition = interactionTarget.transform.position;
            agent.SetDestination(targetPosition);
            var interactionPoint = GetInteractionPoint(interactionTarget, interaction, transform.position);
            var offset = interactionPoint - transform.position;
            offset.y = 0f;
            if (offset.magnitude > GetRange(interaction)) return;
            agent.ResetPath();
            if (offset.sqrMagnitude > 0.02f)
                transform.rotation = Quaternion.RotateTowards(transform.rotation, Quaternion.LookRotation(offset.normalized), rotationSpeed * Time.deltaTime);
            if (interaction == ContextInteraction.RevivePlayer)
            {
                RequestStartResurrectionRpc(new NetworkObjectReference(interactionTarget));
                ClearInteraction();
                return;
            }
            var now = NetworkManager.ServerTime.Time;
            if (now < nextInteractionAt) return;
            if (interaction == ContextInteraction.AttackEnemy && heroSkills != null)
            {
                nextInteractionAt = now + 0.08d;
                heroSkills.TryUseSelectedOnTarget(interactionTarget);
                return;
            }
            nextInteractionAt = now + GetCooldown(interaction);
            PerformContextInteractionRpc(new NetworkObjectReference(interactionTarget), interaction);
            if (interaction == ContextInteraction.DepositResources) ClearInteraction();
        }

        private bool ShouldContinue() => interaction switch
        {
            ContextInteraction.AttackEnemy => IsLivingEnemy(interactionTarget),
            ContextInteraction.MineResource => CarriedResources < CarryingCapacity &&
                interactionTarget.GetComponent<CoreDebugResourceNode>()?.Resources > 0,
            ContextInteraction.BuildOrRepair => NeedsWork(interactionTarget.GetComponent<CoreBuilding>()),
            ContextInteraction.RevivePlayer => interactionTarget.GetComponent<NetworkWarrior>()?.IsDowned == true,
            ContextInteraction.DepositResources => CarriedResources > 0,
            _ => false
        };

        private static bool NeedsWork(CoreBuilding building) => building != null &&
            (building.State == CoreBuildingState.UnderConstruction || building.Health < building.MaximumHealth);
        private static bool IsLivingEnemy(NetworkObject target)
        {
            var enemy = target != null ? target.GetComponent<EnemyBrain>() : null;
            if (enemy != null) return enemy.IsAlive;
            return target != null && target.GetComponent<CoreDebugDummy>()?.Health > 0f;
        }
        private float GetRange(ContextInteraction type) => type switch
        {
            ContextInteraction.AttackEnemy => heroSkills != null
                ? Mathf.Max(0.5f, heroSkills.SelectedApproachRange)
                : attackRange,
            ContextInteraction.MineResource => mineRange,
            ContextInteraction.BuildOrRepair => buildRange,
            _ => interactionRange
        };

        private static Vector3 GetInteractionPoint(NetworkObject target, ContextInteraction type, Vector3 origin)
        {
            if (target == null || type != ContextInteraction.BuildOrRepair)
                return target != null ? target.transform.position : origin;

            var buildingCollider = target.GetComponent<Collider>();
            return buildingCollider != null ? buildingCollider.ClosestPoint(origin) : target.transform.position;
        }

        private float GetCooldown(ContextInteraction type)
        {
            var passiveMultiplier = heroSkills == null ? 1f : type switch
            {
                ContextInteraction.MineResource => 1f / Mathf.Max(0.01f, heroSkills.MiningSpeedMultiplier),
                ContextInteraction.BuildOrRepair => 1f / Mathf.Max(0.01f, heroSkills.BuildSpeedMultiplier),
                _ => 1f
            };
            if (NetworkManager != null && NetworkManager.ServerTime.Time < workSpeedEndsAt)
                passiveMultiplier /= Mathf.Max(0.01f, workSpeedMultiplier);
            return type switch
            {
                ContextInteraction.AttackEnemy => attackCooldown,
                ContextInteraction.MineResource => mineDuration * passiveMultiplier,
                ContextInteraction.BuildOrRepair => buildDuration * passiveMultiplier,
                _ => depositDuration
            };
        }

        private void ClearInteraction()
        {
            interaction = ContextInteraction.None;
            interactionTarget = null;
            if (agent != null && agent.enabled && agent.isOnNavMesh) { agent.ResetPath(); agent.stoppingDistance = 0.08f; }
        }

        private void EnsureAgentOnNavMesh(Vector3 position)
        {
            if (agent.isOnNavMesh) return;
            if (NavMesh.SamplePosition(position, out var hit, 5f, NavMesh.AllAreas)) agent.Warp(hit.position);
        }

        [Rpc(SendTo.Server)]
        private void SetNicknameRpc(string value)
        {
            var clean = string.IsNullOrWhiteSpace(value) ? "Player" : value.Trim();
            nickname.Value = new FixedString64Bytes(clean.Substring(0, Mathf.Min(24, clean.Length)));
        }

        [Rpc(SendTo.Server)]
        private void PerformContextInteractionRpc(NetworkObjectReference reference, ContextInteraction requested)
        {
            if (downed.Value || IsGravityVortexed || !reference.TryGet(out var target)) return;
            var interactionPoint = GetInteractionPoint(target, requested, transform.position);
            var offset = interactionPoint - transform.position; offset.y = 0f;
            var range = GetRange(requested);
            if (offset.magnitude > range + 0.35f) return;
            switch (requested)
            {
                case ContextInteraction.AttackEnemy:
                    if (Vector3.Angle(transform.forward, offset) > attackArc * 0.5f) return;
                    var enemy = target.GetComponent<EnemyBrain>();
                    var dummy = target.GetComponent<CoreDebugDummy>();
                    if (enemy == null && dummy == null) return;
                    BeginServerAction(WarriorAction.Attack, attackDuration);
                    if (enemy != null) enemy.TakeDamage(damage, this); else dummy.TakeDamage(damage);
                    break;
                case ContextInteraction.MineResource:
                    var node = target.GetComponent<CoreDebugResourceNode>(); if (node == null) return;
                    if (CarriedResources >= CarryingCapacity) return;
                    BeginServerAction(WarriorAction.Mine, mineDuration);
                    var mined = node.TryMine(OwnerClientId, syncedPlayerClass.Value == CorePlayerClass.Builder,
                        new NetworkObjectReference(NetworkObject));
                    if (mined > 0)
                    {
                        if (heroSkills != null && heroSkills.HasProspector && Random.value < 0.25f)
                            mined++;
                        if (node.ResourceKind == MinedResourceKind.Ore)
                            carriedOre.Value += mined;
                        else
                            carriedCoreShards.Value += mined;
                    }
                    break;
                case ContextInteraction.BuildOrRepair:
                    var building = target.GetComponent<CoreBuilding>(); if (building == null) return;
                    BeginServerAction(WarriorAction.Build, buildDuration);
                    var repairMultiplier = heroSkills != null ? heroSkills.RepairSpeedMultiplier : 1f;
                    if (NetworkManager.ServerTime.Time < workSpeedEndsAt) repairMultiplier *= workSpeedMultiplier;
                    building.BuildOrRepair(Mathf.RoundToInt(constructionPower * repairMultiplier));
                    if (heroSkills != null && heroSkills.HasPassive(208)) building.ApplyMaximumHealthBonus(1.25f);
                    break;
                case ContextInteraction.DepositResources:
                    var core = target.GetComponent<CoreDebugDeposit>(); if (core == null || CarriedResources <= 0) return;
                    BeginServerAction(WarriorAction.Deposit, depositDuration);
                    core.Deposit(carriedOre.Value, carriedCoreShards.Value);
                    carriedOre.Value = 0;
                    carriedCoreShards.Value = 0;
                    break;
            }
        }

        private void BeginServerAction(WarriorAction requested, float duration)
        {
            var now = NetworkManager.ServerTime.Time;
            if (requested == WarriorAction.Attack)
            {
                actionVariant.Value = nextAttackVariant;
                nextAttackVariant = (byte)((nextAttackVariant + 1) % 3);
            }
            else
            {
                actionVariant.Value = 0;
            }
            action.Value = requested; actionStartedAt.Value = now; actionEndsAt.Value = now + duration;
        }

        public void ServerPlaySkillAction(WarriorAction requested, float duration)
        {
            if (!IsServer || IsGravityVortexed || duration <= 0f) return;
            BeginServerAction(requested, duration);
        }

        public void ServerApplyGravityVortex(Vector3 center, float strength, float duration)
        {
            if (!IsServer || downed.Value || duration <= 0f) return;
            var now = NetworkManager.ServerTime.Time;
            center.y = transform.position.y;
            gravityVortexCenter.Value = center;
            gravityVortexStrength.Value = Mathf.Max(gravityVortexStrength.Value, strength);
            gravityVortexEndsAt.Value = System.Math.Max(gravityVortexEndsAt.Value,
                now + duration);
            action.Value = WarriorAction.None;
            actionVariant.Value = 0;
            actionStartedAt.Value = now;
            actionEndsAt.Value = now;
            CancelResurrectionChannel();
            ClearInteraction();
        }

        private void UpdateLocalGravityVortex()
        {
            if (!gravityVortexMovementActive)
            {
                gravityVortexMovementActive = true;
                gravityVortexRecoveryActive = false;
                gravityVortexVelocity = Vector3.zero;
                leapMovementActive = false;
                whirlwindMovementActive = false;
                ClearInteraction();
                if (agent != null && agent.enabled)
                {
                    if (agent.isOnNavMesh) agent.ResetPath();
                    agent.enabled = false;
                }
            }

            var offset = gravityVortexCenter.Value - transform.position;
            offset.y = 0f;
            if (offset.sqrMagnitude > 0.01f)
            {
                var acceleration = Mathf.Max(10f, gravityVortexStrength.Value * 12f);
                gravityVortexVelocity += offset.normalized * acceleration * Time.deltaTime;
                gravityVortexVelocity = Vector3.ClampMagnitude(gravityVortexVelocity,
                    Mathf.Max(5f, gravityVortexStrength.Value * 4.5f));
            }
            gravityVortexVelocity *= Mathf.Exp(-2.2f * Time.deltaTime);
            transform.position += gravityVortexVelocity * Time.deltaTime;
        }

        private void BeginLocalGravityVortexRecovery()
        {
            if (!gravityVortexMovementActive) return;
            gravityVortexMovementActive = false;
            gravityVortexVelocity = Vector3.zero;
            gravityVortexRecoveryActive = true;
            gravityVortexRecoveryStartedAt = Time.time;
            gravityVortexRecoveryStart = transform.position;
            gravityVortexRecoveryTarget = transform.position;
            if (NavMesh.SamplePosition(transform.position, out var hit, 2.5f, NavMesh.AllAreas))
                gravityVortexRecoveryTarget = hit.position;
        }

        private void UpdateLocalGravityVortexRecovery()
        {
            const float recoveryDuration = 0.4f;
            var progress = Mathf.Clamp01((Time.time - gravityVortexRecoveryStartedAt) / recoveryDuration);
            transform.position = Vector3.Lerp(gravityVortexRecoveryStart, gravityVortexRecoveryTarget,
                Mathf.SmoothStep(0f, 1f, progress));
            if (progress < 1f) return;

            gravityVortexRecoveryActive = false;
            if (agent == null) return;
            agent.enabled = true;
            if (agent.isOnNavMesh) agent.ResetPath();
        }

        private void CancelLocalGravityVortex()
        {
            gravityVortexMovementActive = false;
            gravityVortexRecoveryActive = false;
            gravityVortexVelocity = Vector3.zero;
        }

        public void ServerPresentSkill(HeroSkillDefinition skill, Vector3 point, NetworkObject target)
        {
            if (!IsServer || skill == null) return;
            var presentationAction = skill.Effect switch
            {
                HeroSkillEffect.SingleProjectile or HeroSkillEffect.ExplodingProjectile or
                    HeroSkillEffect.ChainDamage => WarriorAction.CastProjectile,
                HeroSkillEffect.RadialDamage or HeroSkillEffect.RadialDebuff or HeroSkillEffect.HolyPulse or
                    HeroSkillEffect.RepairPulse or HeroSkillEffect.ConstructionAura => WarriorAction.CastSpellAround,
                HeroSkillEffect.GroundImpact or HeroSkillEffect.Vortex or HeroSkillEffect.HealingArea or
                    HeroSkillEffect.Sanctuary or HeroSkillEffect.BuildingBuff or HeroSkillEffect.CleanseWard or
                    HeroSkillEffect.CoreMend or HeroSkillEffect.DivineIntervention or HeroSkillEffect.SelfBuff or
                    HeroSkillEffect.Taunt or HeroSkillEffect.Blink => WarriorAction.CastSpellUp,
                _ => WarriorAction.None
            };
            if (presentationAction != WarriorAction.None &&
                action.Value is not (WarriorAction.Whirlwind or WarriorAction.ShieldBash or
                    WarriorAction.BattleCharge or WarriorAction.Earthshatter))
                BeginServerAction(presentationAction, presentationAction == WarriorAction.CastProjectile ? 0.72f : 0.9f);

            var origin = GetSkillOrigin(presentationAction == WarriorAction.CastProjectile);
            if (skill.StableId == 101)
            {
                var aimPoint = target != null ? target.transform.position + Vector3.up * 0.75f : point + Vector3.up * 0.75f;
                var direction = aimPoint - origin;
                direction.y = 0f;
                if (direction.sqrMagnitude < 0.001f) direction = transform.forward;
                direction.y = 0f;
                if (direction.sqrMagnitude < 0.001f) direction = Vector3.forward;
                point = origin + direction.normalized * Mathf.Max(1f, skill.Radius);
                target = null;
            }
            else if (skill.StableId == 103)
            {
                point = transform.position;
                target = null;
            }
            PresentSkillRpc(skill.StableId, origin, point, target != null,
                target != null ? new NetworkObjectReference(target) : default);
        }

        private Vector3 GetSkillOrigin(bool fromHand)
        {
            if (fromHand)
            {
                foreach (var child in GetComponentsInChildren<Transform>(true))
                    if (child.name == "RHand")
                        return child.position + transform.forward * 0.2f;
            }
            return transform.position + Vector3.up * 1.05f;
        }

        [Rpc(SendTo.Everyone)]
        private void PresentSkillRpc(int stableId, Vector3 origin, Vector3 point, bool hasTarget,
            NetworkObjectReference targetReference)
        {
            Transform targetTransform = null;
            if (hasTarget && targetReference.TryGet(out var target)) targetTransform = target.transform;
            HeroCombatVfx.PlaySkill(HeroSkillCatalog.Find(stableId), origin, point, targetTransform);
        }

        public void ServerPresentChainLightning(Vector3 start, Vector3 end)
        {
            if (IsServer) PresentChainLightningRpc(start, end);
        }

        [Rpc(SendTo.Everyone)]
        private void PresentChainLightningRpc(Vector3 start, Vector3 end)
        {
            HeroCombatVfx.PlayChainLightningSegment(start, end);
        }

        public void ServerPresentProjectileImpact(int stableId, Vector3 position, Vector3 direction)
        {
            if (IsServer) PresentProjectileImpactRpc(stableId, position, direction);
        }

        [Rpc(SendTo.Everyone)]
        private void PresentProjectileImpactRpc(int stableId, Vector3 position, Vector3 direction)
        {
            HeroCombatVfx.PlayProjectileImpact(stableId, position, direction);
        }

        public void ServerDismissProjectile(int stableId, Vector3 position, Vector3 direction)
        {
            if (IsServer) DismissProjectileRpc(stableId, position, direction);
        }

        [Rpc(SendTo.Everyone)]
        private void DismissProjectileRpc(int stableId, Vector3 position, Vector3 direction)
        {
            HeroCombatVfx.DismissProjectile(stableId, position, direction);
        }

        public bool ServerBeginLeap(Vector3 destination, float duration)
        {
            if (!IsServer || downed.Value || duration <= 0f) return false;
            if (!NavMesh.SamplePosition(destination, out var sample, 2f, NavMesh.AllAreas)) return false;
            leapStart.Value = transform.position;
            leapEnd.Value = sample.position;
            BeginServerAction(WarriorAction.BattleCharge, duration);
            return true;
        }

        private void UpdateSpecialMovement()
        {
            if (action.Value == WarriorAction.Whirlwind)
            {
                if (!whirlwindMovementActive)
                {
                    whirlwindMovementActive = true;
                    whirlwindStartRotation = transform.rotation;
                }
                if (agent != null && agent.enabled) agent.updateRotation = false;
                transform.rotation = Quaternion.AngleAxis(ActionProgress * 360f * WhirlwindTurns, Vector3.up) * whirlwindStartRotation;
                return;
            }

            if (whirlwindMovementActive)
            {
                whirlwindMovementActive = false;
                if (agent != null && agent.enabled) agent.updateRotation = true;
            }

            if (action.Value != WarriorAction.BattleCharge)
            {
                if (leapMovementActive) FinishLocalLeap(leapEnd.Value);
                if (agent != null && agent.enabled)
                {
                    agent.updateRotation = true;
                    if (action.Value == WarriorAction.Earthshatter && agent.isOnNavMesh)
                        agent.ResetPath();
                }
                return;
            }

            if (!leapMovementActive)
            {
                leapMovementActive = true;
                if (agent != null && agent.enabled)
                {
                    if (agent.isOnNavMesh) agent.ResetPath();
                    agent.enabled = false;
                }
            }
            var t = Mathf.Clamp01(ActionProgress);
            var position = Vector3.Lerp(leapStart.Value, leapEnd.Value, t);
            position.y += Mathf.Sin(t * Mathf.PI) * 2.1f;
            transform.position = position;
            var direction = leapEnd.Value - leapStart.Value;
            direction.y = 0f;
            if (direction.sqrMagnitude > 0.01f)
                transform.rotation = Quaternion.LookRotation(direction.normalized);
        }

        [Rpc(SendTo.Owner)]
        private void FinishLeapRpc(Vector3 destination) => FinishLocalLeap(destination);

        private void FinishLocalLeap(Vector3 destination)
        {
            leapMovementActive = false;
            transform.position = destination;
            if (agent == null) return;
            agent.enabled = true;
            EnsureAgentOnNavMesh(destination);
            if (agent.isOnNavMesh)
            {
                agent.Warp(destination);
                agent.ResetPath();
            }
            agent.updateRotation = true;
        }

        private void StopForForcedAttack(Vector3 targetPosition)
        {
            ClearInteraction();
            if (agent != null && agent.enabled && agent.isOnNavMesh) agent.ResetPath();
            var direction = targetPosition - transform.position;
            direction.y = 0f;
            if (direction.sqrMagnitude > 0.01f)
                transform.rotation = Quaternion.LookRotation(direction.normalized);
        }

        private static bool IsShiftHeld()
        {
            var keyboard = Keyboard.current;
            return keyboard != null && (keyboard.leftShiftKey.isPressed || keyboard.rightShiftKey.isPressed);
        }

        public NetworkWarrior FindNearestDownedHero(float range)
        {
            NetworkWarrior closest = null;
            var closestSqr = range * range;
            foreach (var hero in FindObjectsByType<NetworkWarrior>())
            {
                if (hero == this || !hero.IsSpawned || !hero.IsDowned || hero.IsBeingResurrected) continue;
                var sqr = (hero.transform.position - transform.position).sqrMagnitude;
                if (sqr <= closestSqr) { closestSqr = sqr; closest = hero; }
            }
            return closest;
        }

        public void TryStartResurrection(NetworkWarrior target)
        {
            if (!IsOwner || downed.Value || target == null || !target.IsDowned ||
                Vector3.Distance(transform.position, target.transform.position) > interactionRange)
                return;
            RequestStartResurrectionRpc(new NetworkObjectReference(target.NetworkObject));
        }

        [Rpc(SendTo.Server)]
        private void RequestStartResurrectionRpc(NetworkObjectReference reference)
        {
            if (downed.Value || resurrectionTarget != null || !reference.TryGet(out var targetObject)) return;
            var target = targetObject.GetComponent<NetworkWarrior>();
            if (target == null || target == this || !target.downed.Value || target.resurrectorId.Value != NoResurrector ||
                Vector3.Distance(transform.position, target.transform.position) > interactionRange + 0.35f) return;

            var duration = syncedPlayerClass.Value == CorePlayerClass.Healer
                ? healerResurrectionDuration
                : resurrectionDuration;
            var now = NetworkManager.ServerTime.Time;
            resurrectionTarget = target;
            resurrectionStartPosition = transform.position;
            resurrectionDamageRevision = damageRevision;
            target.resurrectorId.Value = NetworkObjectId;
            target.resurrectionStartedAt.Value = now;
            target.resurrectionEndsAt.Value = now + duration;
            target.reviveProgress.Value = 0;
            BeginServerAction(WarriorAction.Revive, duration);
        }

        private void UpdateResurrectionChannel()
        {
            if (resurrectionTarget == null) return;
            var now = NetworkManager.ServerTime.Time;
            var moved = (transform.position - resurrectionStartPosition).sqrMagnitude >
                        resurrectionMoveTolerance * resurrectionMoveTolerance;
            if (downed.Value || moved || damageRevision != resurrectionDamageRevision ||
                !resurrectionTarget.IsSpawned || !resurrectionTarget.downed.Value ||
                Vector3.Distance(transform.position, resurrectionTarget.transform.position) > interactionRange + 0.5f)
            {
                CancelResurrectionChannel();
                return;
            }

            resurrectionTarget.reviveProgress.Value = Mathf.RoundToInt(resurrectionTarget.ResurrectionProgress * 100f);
            if (now < resurrectionTarget.resurrectionEndsAt.Value) return;
            resurrectionTarget.CompleteResurrection(syncedPlayerClass.Value == CorePlayerClass.Healer);
            resurrectionTarget = null;
            if (action.Value == WarriorAction.Revive) action.Value = WarriorAction.None;
        }

        private void CancelResurrectionChannel()
        {
            if (resurrectionTarget != null && resurrectionTarget.resurrectorId.Value == NetworkObjectId)
                resurrectionTarget.ClearResurrectionState();
            resurrectionTarget = null;
            if (action.Value == WarriorAction.Revive) action.Value = WarriorAction.None;
        }

        private void ClearResurrectionState()
        {
            resurrectorId.Value = NoResurrector;
            resurrectionStartedAt.Value = 0d;
            resurrectionEndsAt.Value = 0d;
            reviveProgress.Value = 0;
        }

        private void CompleteResurrection(bool resurrectedByHealer)
        {
            downed.Value = false;
            currentHealth.Value = maximumHealth.Value * (resurrectedByHealer ? 1f : revivedHealthFraction);
            ClearResurrectionState();
        }

        public void TakeDamage(float amount)
        {
            if (!IsServer || downed.Value || amount <= 0f) return;
            amount = heroSkills != null ? heroSkills.ModifyIncomingDamage(amount) : amount;
            if (amount <= 0f) return;
            amount = Mathf.Max(MinimumIncomingDamage, amount - Defense);
            damageRevision++;
            CancelResurrectionChannel();
            currentHealth.Value = Mathf.Max(0f, currentHealth.Value - amount);
            if (currentHealth.Value <= 0f && heroSkills != null && heroSkills.TryPreventLethalDamage())
                return;
            if (currentHealth.Value <= 0f)
                EnterDownedState();
        }

        public void ServerIgnite(float duration, float damagePerSecond)
        {
            if (!IsServer || downed.Value || duration <= 0f || damagePerSecond <= 0f || HasStatusProtection)
                return;
            var now = NetworkManager.ServerTime.Time;
            burningEndsAt = System.Math.Max(burningEndsAt, now + duration);
            burningDamagePerSecond = Mathf.Max(burningDamagePerSecond, damagePerSecond);
            if (!burning.Value)
            {
                burning.Value = true;
                nextBurnDamageAt = now + 1d;
            }
        }

        public void ServerApplyDebuff(EnemyDebuff debuff, float duration)
        {
            if (!IsServer || downed.Value || debuff == EnemyDebuff.None || duration <= 0f ||
                HasStatusProtection || (heroSkills != null && heroSkills.IsCrowdControlImmune)) return;
            if (debuff == EnemyDebuff.OnFire)
            {
                ServerIgnite(duration, 3f);
                return;
            }
            var index = HeroDebuffIndex(debuff);
            if (index < 0) return;
            activeDebuffs.Value |= debuff;
            debuffEndsAt[index] = System.Math.Max(debuffEndsAt[index], NetworkManager.ServerTime.Time + duration);
            if (debuff == EnemyDebuff.Poisoned && nextPoisonDamageAt <= 0d)
                nextPoisonDamageAt = NetworkManager.ServerTime.Time + 1d;
        }

        private void UpdateBurning()
        {
            if (!burning.Value) return;
            var now = NetworkManager.ServerTime.Time;
            if (downed.Value || now >= burningEndsAt)
            {
                burning.Value = false;
                burningDamagePerSecond = 0f;
                return;
            }
            if (now < nextBurnDamageAt) return;
            nextBurnDamageAt = now + 1d;
            TakeDamage(burningDamagePerSecond);
        }

        private void UpdateDebuffs()
        {
            if (activeDebuffs.Value == EnemyDebuff.None) return;
            var now = NetworkManager.ServerTime.Time;
            for (var index = 0; index < debuffEndsAt.Length; index++)
            {
                var flag = (EnemyDebuff)(1 << index);
                if ((activeDebuffs.Value & flag) == 0 || now < debuffEndsAt[index]) continue;
                activeDebuffs.Value &= ~flag;
            }
            if ((activeDebuffs.Value & EnemyDebuff.Poisoned) != 0 && now >= nextPoisonDamageAt)
            {
                nextPoisonDamageAt = now + 1d;
                TakeDamage(1f);
            }
            if ((activeDebuffs.Value & EnemyDebuff.Poisoned) == 0) nextPoisonDamageAt = 0d;
        }

        private static int HeroDebuffIndex(EnemyDebuff debuff)
        {
            for (var index = 0; index < 6; index++)
                if (debuff == (EnemyDebuff)(1 << index)) return index;
            return -1;
        }

        private void OnBurningChanged(bool previous, bool current) =>
            HeroCombatVfx.SetCharacterBurning(transform, current);

        public void ServerHeal(float amount)
        {
            if (!IsServer || downed.Value || amount <= 0f) return;
            currentHealth.Value = Mathf.Min(maximumHealth.Value, currentHealth.Value + amount);
        }

        public void ServerSetHealth(float amount)
        {
            if (!IsServer) return;
            currentHealth.Value = Mathf.Clamp(amount, 0f, maximumHealth.Value);
        }

        public void ServerMultiplyMaximumHealth(float multiplier)
        {
            if (!IsServer || multiplier <= 0f) return;
            var previous = maximumHealth.Value;
            maximumHealth.Value = Mathf.Max(1f, maximumHealth.Value * multiplier);
            currentHealth.Value = Mathf.Clamp(currentHealth.Value * maximumHealth.Value / Mathf.Max(1f, previous),
                1f, maximumHealth.Value);
        }

        public void ServerWarp(Vector3 position)
        {
            if (!IsServer) return;
            transform.position = position;
            if (agent != null && agent.enabled && agent.isOnNavMesh) agent.Warp(position);
        }

        public void ServerFace(Vector3 point)
        {
            if (!IsServer) return;
            var direction = point - transform.position;
            direction.y = 0f;
            if (direction.sqrMagnitude > 0.01f)
                transform.rotation = Quaternion.LookRotation(direction.normalized);
        }

        public void ServerGrantStatusProtection(float duration)
        {
            if (!IsServer) return;
            statusProtectionEndsAt = System.Math.Max(statusProtectionEndsAt, NetworkManager.ServerTime.Time + duration);
        }

        public bool HasStatusProtection => IsServer && NetworkManager != null &&
            NetworkManager.ServerTime.Time < statusProtectionEndsAt;
        public float OutgoingDamageMultiplier => NetworkManager != null &&
            NetworkManager.ServerTime.Time < externalDamageBonusEndsAt ? externalDamageMultiplier : 1f;
        public float ExternalDamageResistance => NetworkManager != null &&
            NetworkManager.ServerTime.Time < externalResistanceEndsAt ? externalResistance : 0f;

        public void ServerGrantWorkSpeed(float multiplier, float duration)
        {
            if (!IsServer) return;
            workSpeedMultiplier = Mathf.Max(workSpeedMultiplier, multiplier);
            workSpeedEndsAt = System.Math.Max(workSpeedEndsAt, NetworkManager.ServerTime.Time + duration);
        }

        public void ServerGrantArcaneSpeed(float multiplier, float duration)
        {
            if (!IsServer || duration <= 0f) return;
            arcaneSpeedMultiplier.Value = Mathf.Max(1f, multiplier);
            arcaneSpeedEndsAt.Value = System.Math.Max(arcaneSpeedEndsAt.Value,
                NetworkManager.ServerTime.Time + duration);
        }

        private void UpdateArcaneSpeed()
        {
            var active = IsArcaneSpeedActive;
            if (IsOwner && agent != null)
                agent.speed = movementSpeed * (active ? Mathf.Max(1f, arcaneSpeedMultiplier.Value) : 1f);
            if (active != arcaneSpeedVisualActive) SetArcaneSpeedVisual(active);
        }

        private void SetArcaneSpeedVisual(bool active)
        {
            arcaneSpeedVisualActive = active;
            if (active)
            {
                EnsureArcaneVisuals();
                if (arcaneMageMaterial != null)
                    foreach (var entry in arcaneOriginalMaterials)
                    {
                        var materials = new Material[entry.Value.Length];
                        System.Array.Fill(materials, arcaneMageMaterial);
                        entry.Key.sharedMaterials = materials;
                    }
            }
            else
            {
                foreach (var entry in arcaneOriginalMaterials)
                    if (entry.Key != null) entry.Key.sharedMaterials = entry.Value;
            }

            foreach (var trail in arcaneTrails)
            {
                if (trail == null) continue;
                trail.emitting = active;
                if (!active) trail.Clear();
            }
        }

        private void EnsureArcaneVisuals()
        {
            if (arcaneOriginalMaterials.Count > 0 || arcaneTrails.Count > 0) return;
            arcaneTrailMaterial = HeroCombatVfx.CreateMaterial(new Color(0.62f, 0.12f, 1f, 0.9f), true, 5f);
            foreach (var partName in new[] { "Head", "RHand", "LHand" })
            {
                var anchor = FindVisualPart(partName);
                if (anchor == null) continue;
                foreach (var partRenderer in anchor.GetComponentsInChildren<Renderer>(true))
                    if (!arcaneOriginalMaterials.ContainsKey(partRenderer))
                        arcaneOriginalMaterials.Add(partRenderer, partRenderer.sharedMaterials);

                var trailObject = new GameObject($"Arcane Blink Trail - {partName}");
                trailObject.transform.SetParent(anchor, false);
                var trail = trailObject.AddComponent<TrailRenderer>();
                trail.time = 0.42f;
                trail.minVertexDistance = 0.04f;
                trail.startWidth = partName == "Head" ? 0.24f : 0.16f;
                trail.endWidth = 0.01f;
                trail.numCornerVertices = 4;
                trail.numCapVertices = 4;
                trail.alignment = LineAlignment.View;
                trail.textureMode = LineTextureMode.Stretch;
                trail.colorGradient = PurpleTrailGradient();
                trail.sharedMaterial = arcaneTrailMaterial;
                trail.emitting = false;
                arcaneTrails.Add(trail);
            }
        }

        private Transform FindVisualPart(string partName)
        {
            foreach (var child in GetComponentsInChildren<Transform>(true))
                if (string.Equals(child.name, partName, System.StringComparison.OrdinalIgnoreCase)) return child;
            foreach (var child in GetComponentsInChildren<Transform>(true))
                if (string.Equals(child.name, partName + "Visual", System.StringComparison.OrdinalIgnoreCase)) return child;
            return null;
        }

        private static Gradient PurpleTrailGradient()
        {
            var gradient = new Gradient();
            gradient.SetKeys(
                new[]
                {
                    new GradientColorKey(new Color(0.9f, 0.45f, 1f), 0f),
                    new GradientColorKey(new Color(0.35f, 0.03f, 1f), 1f)
                },
                new[] { new GradientAlphaKey(0.95f, 0f), new GradientAlphaKey(0f, 1f) });
            return gradient;
        }

        public override void OnDestroy()
        {
            if (arcaneTrailMaterial != null) Destroy(arcaneTrailMaterial);
            base.OnDestroy();
        }

        public void ServerGrantDamageBonus(float multiplier, float duration)
        {
            if (!IsServer) return;
            externalDamageMultiplier = Mathf.Max(externalDamageMultiplier, multiplier);
            externalDamageBonusEndsAt = System.Math.Max(externalDamageBonusEndsAt,
                NetworkManager.ServerTime.Time + duration);
        }

        public void ServerGrantDamageResistance(float resistance, float duration)
        {
            if (!IsServer) return;
            externalResistance = Mathf.Max(externalResistance, Mathf.Clamp01(resistance));
            externalResistanceEndsAt = System.Math.Max(externalResistanceEndsAt,
                NetworkManager.ServerTime.Time + duration);
        }

        public void ServerRevive(float healthFraction)
        {
            if (!IsServer || !downed.Value) return;
            downed.Value = false;
            currentHealth.Value = maximumHealth.Value * Mathf.Clamp01(healthFraction);
            ClearResurrectionState();
        }

        public void RequestSkillChoice(int stableId, int wave)
        {
            if (IsOwner) ChooseSkillRpc(stableId, wave);
        }

        [Rpc(SendTo.Server)]
        private void ChooseSkillRpc(int stableId, int wave)
        {
            heroSkills?.ServerChoose(HeroSkillCatalog.Find(stableId), wave);
        }

        public void RequestDebugSkillLevel(int wave)
        {
            if (IsOwner) GrantDebugSkillLevelRpc(wave);
        }

        [Rpc(SendTo.Server)]
        private void GrantDebugSkillLevelRpc(int wave)
        {
            if (SceneManager.GetActiveScene().name != CoreSessionManager.DebugSceneName ||
                heroSkills == null || !heroSkills.ServerGrantDebugLevel(wave)) return;
            playerLevel.Value = Mathf.Max(playerLevel.Value, startingLevel + wave);
        }

        public void RequestSkillUse(int stableId, Vector3 point, NetworkObject target)
        {
            if (!IsOwner) return;
            UseSkillRpc(stableId, point, target != null, target != null
                ? new NetworkObjectReference(target) : default);
        }

        [Rpc(SendTo.Server)]
        private void UseSkillRpc(int stableId, Vector3 point, bool hasTarget, NetworkObjectReference targetReference)
        {
            NetworkObject target = null;
            if (hasTarget && !targetReference.TryGet(out target))
            {
                ResolveSkillUseRpc(stableId, false, 0f);
                return;
            }
            var cooldown = 0f;
            var succeeded = heroSkills != null && heroSkills.ServerTryExecute(HeroSkillCatalog.Find(stableId),
                point, target, out cooldown);
            ResolveSkillUseRpc(stableId, succeeded, succeeded ? cooldown : 0f);
        }

        [Rpc(SendTo.Owner)]
        private void ResolveSkillUseRpc(int stableId, bool succeeded, float cooldown)
        {
            heroSkills?.ResolveUseResult(stableId, succeeded, cooldown);
        }

        private void EnterDownedState()
        {
            if (downed.Value) return;
            downed.Value = true;
            currentHealth.Value = 0f;
            reviveProgress.Value = 0;
            action.Value = WarriorAction.None;
            CancelResurrectionChannel();
            DropCarriedLoot();
            ClearInteraction();
        }

        private void DropCarriedLoot()
        {
            var ore = carriedOre.Value;
            var shards = carriedCoreShards.Value;
            carriedOre.Value = 0;
            carriedCoreShards.Value = 0;
            if (ore > 0) CoreLootPickup.Spawn(MinedResourceKind.Ore, ore, transform.position + transform.right * 0.45f);
            if (shards > 0) CoreLootPickup.Spawn(MinedResourceKind.CoreShards, shards, transform.position - transform.right * 0.45f);
        }

        public int TryCollectLoot(MinedResourceKind kind, int requestedAmount)
        {
            if (!IsServer || downed.Value || requestedAmount <= 0) return 0;
            var accepted = Mathf.Min(requestedAmount, CarryingCapacity - CarriedResources);
            if (accepted <= 0) return 0;
            if (kind == MinedResourceKind.Ore) carriedOre.Value += accepted;
            else carriedCoreShards.Value += accepted;
            return accepted;
        }

        [Rpc(SendTo.Server)]
        private void SetDebugDownedRpc(bool value)
        {
            if (value) EnterDownedState();
            else
            {
                downed.Value = false;
                currentHealth.Value = maximumHealth.Value;
                ClearResurrectionState();
            }
        }

        [Rpc(SendTo.Server)]
        private void CycleDebugClassRpc()
        {
            if (SceneManager.GetActiveScene().name != CoreSessionManager.DebugSceneName)
                return;

            var nextClass = (CorePlayerClass)(((int)syncedPlayerClass.Value + 1) % 4);
            var nextCapacity = GetCarryingCapacity(nextClass);
            if (CarriedResources > nextCapacity)
            {
                Debug.LogWarning($"Cannot switch to {nextClass} while carrying {CarriedResources}/{nextCapacity} resources.", this);
                return;
            }
            var prefab = Resources.Load<GameObject>(GetClassPrefabPath(nextClass));
            if (prefab == null)
            {
                Debug.LogError($"Cannot switch to {nextClass}: its player prefab is missing from Resources.", this);
                return;
            }

            var previousNetworkObject = NetworkObject;
            var previousOwner = OwnerClientId;
            var replacementObject = Instantiate(prefab, transform.position, transform.rotation);
            var replacement = replacementObject.GetComponent<NetworkWarrior>();
            var replacementNetworkObject = replacementObject.GetComponent<NetworkObject>();
            if (replacement == null || replacementNetworkObject == null)
            {
                Destroy(replacementObject);
                Debug.LogError($"Cannot switch to {nextClass}: prefab requires NetworkWarrior and NetworkObject.", this);
                return;
            }

            replacement.PrepareClassReplacement(nextClass);
            replacementNetworkObject.SpawnAsPlayerObject(previousOwner, true);
            replacement.RestoreClassReplacementState(nickname.Value, carriedOre.Value, carriedCoreShards.Value,
                playerLevel.Value, currentHealth.Value, maximumHealth.Value, downed.Value, reviveProgress.Value);
            previousNetworkObject.Despawn(true);
        }

        private void PrepareClassReplacement(CorePlayerClass replacementClass)
        {
            playerClass = replacementClass;
            preserveSpawnTransform = true;
        }

        private void RestoreClassReplacementState(FixedString64Bytes previousNickname, int ore, int coreShards,
            int previousLevel, float previousHealth, float previousMaximumHealth, bool wasDowned,
            int previousReviveProgress)
        {
            nickname.Value = previousNickname;
            carriedOre.Value = ore;
            carriedCoreShards.Value = coreShards;
            playerLevel.Value = previousLevel;
            currentHealth.Value = previousHealth;
            maximumHealth.Value = previousMaximumHealth;
            downed.Value = wasDowned;
            reviveProgress.Value = previousReviveProgress;
        }

        private static int GetCarryingCapacity(CorePlayerClass requestedClass) =>
            requestedClass == CorePlayerClass.Builder ? BuilderCarryingCapacity : DefaultCarryingCapacity;

        private static float GetBaseDefense(CorePlayerClass requestedClass) => requestedClass switch
        {
            CorePlayerClass.Warrior => 5f,
            CorePlayerClass.Builder => 3f,
            _ => 0f
        };

        private static string GetClassPrefabPath(CorePlayerClass requestedClass) => requestedClass switch
        {
            CorePlayerClass.Mage => "CoreMage",
            CorePlayerClass.Builder => "CoreBuilder",
            CorePlayerClass.Healer => "CoreHealer",
            _ => "CoreWarrior"
        };

        private void AttachPlayerMarker()
        {
            if (playerMarker != null) return;
            var markerPrefab = Resources.Load<GameObject>($"PlayerMarkers/P{PlayerNumber}");
            if (markerPrefab == null)
            {
                Debug.LogWarning($"Player marker P{PlayerNumber} is missing from Resources/PlayerMarkers.", this);
                return;
            }
            playerMarker = Instantiate(markerPrefab, transform);
            playerMarker.name = $"P{PlayerNumber} Marker";
            playerMarker.transform.localPosition = Vector3.up * 0.025f;
            foreach (var markerCollider in playerMarker.GetComponentsInChildren<Collider>())
                markerCollider.enabled = false;
        }

        public void RequestPlaceBuilding(CoreBuildingType type, Vector3 position) { if (IsOwner) PlaceBuildingRpc(type, position); }
        [Rpc(SendTo.Server)]
        private void PlaceBuildingRpc(CoreBuildingType type, Vector3 requestedPosition)
        {
            if (!CoreBuilding.CanPlace(requestedPosition, out var validPosition)) return;
            var core = FindFirstObjectByType<CoreDebugDeposit>();
            if (core == null || !core.TrySpend(CoreBuildingCatalog.BuildCurrency(type),
                    CoreBuildingCatalog.Cost(type))) return;
            var prefab = Resources.Load<GameObject>(CoreBuildingCatalog.ResourcePath(type));
            if (prefab == null) return;
            var instance = Instantiate(prefab, validPosition, Quaternion.identity);
            var networkObject = instance.GetComponent<NetworkObject>(); networkObject.Spawn(true);
            BeginPlacedBuildingRpc(new NetworkObjectReference(networkObject));
        }
        [Rpc(SendTo.Owner)] private void BeginPlacedBuildingRpc(NetworkObjectReference reference)
        { if (reference.TryGet(out var target)) BeginInteraction(target, ContextInteraction.BuildOrRepair); }

        public void RequestBuildingUpgrade(CoreBuilding building, byte branch)
        { if (IsOwner && building != null) UpgradeBuildingRpc(new NetworkObjectReference(building.NetworkObject), branch); }
        [Rpc(SendTo.Server)]
        private void UpgradeBuildingRpc(NetworkObjectReference reference, byte branch)
        {
            if (!reference.TryGet(out var target) || Vector3.Distance(transform.position, target.transform.position) > 5f) return;
            var building = target.GetComponent<CoreBuilding>(); var core = FindFirstObjectByType<CoreDebugDeposit>();
            if (building == null || core == null || !building.CanUpgrade) return;
            if (core.TrySpend(CoreBuildingCatalog.UpgradeCurrency(building.BuildingType),
                    CoreBuildingCatalog.UpgradeCost(building.BuildingType, building.Level))) building.TryUpgrade(branch);
        }

        public void RequestCoreUpgrade(CoreDebugDeposit core, byte branch)
        { if (IsOwner && core != null) UpgradeCoreRpc(new NetworkObjectReference(core.NetworkObject), branch); }
        [Rpc(SendTo.Server)]
        private void UpgradeCoreRpc(NetworkObjectReference reference, byte branch)
        {
            if (!reference.TryGet(out var target) || Vector3.Distance(transform.position, target.transform.position) > 5f) return;
            target.GetComponent<CoreDebugDeposit>()?.TryUpgrade(branch);
        }
    }
}
