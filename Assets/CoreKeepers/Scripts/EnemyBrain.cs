using Unity.Netcode;
using UnityEngine;
using UnityEngine.AI;

namespace CoreKeepers
{
    public enum EnemyAnimationState : byte
    {
        Idle,
        Walk,
        Attack_LHand,
        Attack_RHand,
        Smash,
        ThrowRock,
        CastProjectile,
        CastBuff,
        TakeHit,
        Freeze,
        Burn,
        Die
    }

    [System.Flags]
    public enum EnemyDebuff : byte
    {
        None = 0,
        Freeze = 1 << 0,
        Stun = 1 << 1,
        Chill = 1 << 2,
        Swamp = 1 << 3,
        OnFire = 1 << 4,
        Poisoned = 1 << 5
    }

    [RequireComponent(typeof(NetworkObject), typeof(NavMeshAgent))]
    public sealed class EnemyBrain : NetworkBehaviour
    {
        private const ulong NoTarget = ulong.MaxValue;

        [Header("Targeting")]
        [SerializeField, Min(0.1f)] private float barricadePriorityRange = 1f;
        [SerializeField, Min(0.1f)] private float defenseDetectionRange = 7f;
        [SerializeField, Min(0.1f)] private float heroDetectionRange = 7f;
        [SerializeField, Min(0.05f)] private float targetRefreshInterval = 0.25f;
        [SerializeField] private bool canPassThroughBarricades;
        [SerializeField] private bool assassin;
        [SerializeField] private bool coreOnly;

        [Header("Combat")]
        [SerializeField, Min(1f)] private float maximumHealth = 100f;
        [SerializeField, Min(0f)] private float damage = 10f;
        [SerializeField, Min(0.1f)] private float attackRange = 1.4f;
        [SerializeField, Min(0.05f)] private float attackDuration = 0.55f;
        [SerializeField, Min(0.05f)] private float attackCooldown = 1.1f;
        [SerializeField, Min(0.1f)] private float movementSpeed = 3.5f;
        [SerializeField, Min(0f)] private float heroAggroDuration = 10f;
        [SerializeField, Min(0f)] private float tauntDuration = 15f;
        [SerializeField, Min(0.05f)] private float deathAnimationDuration = 1.1f;
        [Header("Loot")]
        [SerializeField, Min(0)] private int coreShardsDropMin;
        [SerializeField, Min(0)] private int coreShardsDropMax;

        [Header("Debuff Resistances")]
        [SerializeField] private bool resistant_Freeze;
        [SerializeField] private bool resistant_Stun;
        [SerializeField] private bool resistant_Chill;
        [SerializeField] private bool resistant_Swamp;
        [SerializeField] private bool resistant_OnFire;
        [SerializeField] private bool resistant_Poisoned;

        [Header("Debuff Defaults")]
        [SerializeField, Range(0f, 0.95f)] private float chillSlow = 0.3f;
        [SerializeField, Range(0f, 0.95f)] private float swampSlow = 0.3f;
        [SerializeField, Min(0.1f)] private float fireDuration = 5f;
        [SerializeField, Min(0f)] private float fireDamagePerSecond = 2f;
        [SerializeField, Min(0.1f)] private float poisonDuration = 20f;
        [SerializeField, Min(0f)] private float poisonDamagePerSecond = 1f;
        [SerializeField, Min(0f)] private float poisonHealthFloor = 5f;

        private readonly NetworkVariable<float> health = new(100f);
        private readonly NetworkVariable<ulong> currentTargetId = new(NoTarget);
        private readonly NetworkVariable<EnemyAnimationState> animationState = new(EnemyAnimationState.Idle);
        private readonly NetworkVariable<double> animationStartedAt = new(0d);
        private readonly NetworkVariable<double> animationEndsAt = new(0d);
        private readonly NetworkVariable<EnemyDebuff> debuffs = new(EnemyDebuff.None);

        private readonly double[] debuffEndsAt = new double[6];
        private NavMeshAgent agent;
        private NetworkObject currentTarget;
        private Collider[] currentTargetColliders;
        private NetworkWarrior forcedHero;
        private double forcedHeroEndsAt;
        private bool forcedByTaunt;
        private double nextTargetRefreshAt;
        private double nextAttackAt;
        private double nextFireTickAt;
        private double nextPoisonTickAt;
        private double despawnAt;
        private Vector3 previousPosition;
        private float normalizedSpeed;
        private bool nextAttackUsesRightHand;
        private bool bypassingBarricade;
        private double barricadeBypassEndsAt;

        public float Health => health.Value;
        public float MaximumHealth => maximumHealth;
        public bool IsAlive => health.Value > 0f;
        public bool CanPassThroughBarricades => canPassThroughBarricades;
        public bool IsAssassin => assassin;
        public bool IsCoreOnly => coreOnly;
        public EnemyDebuff ActiveDebuffs => debuffs.Value;
        public EnemyAnimationState CurrentAnimation => animationState.Value;
        public float NormalizedSpeed => normalizedSpeed;
        public Transform CurrentTarget => ResolveReplicatedTarget()?.transform;
        public float AnimationProgress
        {
            get
            {
                var duration = animationEndsAt.Value - animationStartedAt.Value;
                return duration <= 0d || NetworkManager == null ? 1f :
                    Mathf.Clamp01((float)((NetworkManager.ServerTime.Time - animationStartedAt.Value) / duration));
            }
        }

        private void Awake()
        {
            agent = GetComponent<NavMeshAgent>();
            previousPosition = transform.position;
        }

        public override void OnNetworkSpawn()
        {
            agent.enabled = IsServer;
            if (!IsServer)
                return;
            health.Value = maximumHealth;
            agent.speed = movementSpeed;
            agent.stoppingDistance = attackRange * 0.85f;
            FindAndSetCore();
        }

        private void Update()
        {
            UpdateMeasuredSpeed();
            if (!IsServer)
                return;

            var now = NetworkManager.ServerTime.Time;
            UpdateDebuffs(now);
            if (!IsAlive)
            {
                if (agent.enabled) agent.isStopped = true;
                if (now >= despawnAt && NetworkObject.IsSpawned)
                    NetworkObject.Despawn(true);
                return;
            }

            if (animationState.Value != EnemyAnimationState.Idle && now >= animationEndsAt.Value)
                animationState.Value = EnemyAnimationState.Idle;
            if (IsImmobilized())
            {
                if (agent.enabled) agent.isStopped = true;
                return;
            }

            if (agent.enabled) agent.isStopped = false;
            ApplyMovementSpeed();
            if (now >= nextTargetRefreshAt)
            {
                nextTargetRefreshAt = now + targetRefreshInterval;
                SelectTarget(now);
            }
            if (UpdateBarricadeBypass(now))
                return;
            FollowAndAttack(now);
        }

        private void UpdateMeasuredSpeed()
        {
            var distance = transform.position - previousPosition;
            distance.y = 0f;
            normalizedSpeed = Mathf.MoveTowards(normalizedSpeed,
                Mathf.Clamp01(distance.magnitude / Mathf.Max(Time.deltaTime * movementSpeed, 0.0001f)),
                Time.deltaTime * 10f);
            previousPosition = transform.position;
        }

        private void SelectTarget(double now)
        {
            var barricade = canPassThroughBarricades ? null : FindClosestBuilding(CoreBuildingType.Barricade, barricadePriorityRange);
            if (barricade != null)
            {
                SetTarget(barricade.NetworkObject);
                return;
            }

            if (forcedHero != null && forcedHero.IsSpawned && !forcedHero.IsDowned && now < forcedHeroEndsAt)
            {
                SetTarget(forcedHero.NetworkObject);
                return;
            }
            forcedHero = null;
            forcedByTaunt = false;

            if (!coreOnly)
            {
                if (assassin)
                {
                    var hero = FindClosestHero(heroDetectionRange);
                    if (hero != null)
                    {
                        SetTarget(hero.NetworkObject);
                        return;
                    }
                }

                var defense = FindClosestDefense(defenseDetectionRange);
                if (defense != null)
                {
                    SetTarget(defense.NetworkObject);
                    return;
                }
            }

            FindAndSetCore();
        }

        private void FollowAndAttack(double now)
        {
            if (!IsValidTarget(currentTarget))
            {
                FindAndSetCore();
                return;
            }
            var targetPoint = ClosestTargetPoint(currentTarget);
            var offset = targetPoint - transform.position;
            offset.y = 0f;
            if (offset.magnitude > attackRange)
            {
                if (agent.isOnNavMesh)
                    agent.SetDestination(targetPoint);
                return;
            }

            if (agent.isOnNavMesh)
                agent.ResetPath();
            if (offset.sqrMagnitude > 0.001f)
                transform.rotation = Quaternion.RotateTowards(transform.rotation,
                    Quaternion.LookRotation(offset.normalized), agent.angularSpeed * Time.deltaTime);
            if (now < nextAttackAt)
                return;

            nextAttackAt = now + attackCooldown;
            PlayAnimation(nextAttackUsesRightHand ? EnemyAnimationState.Attack_RHand : EnemyAnimationState.Attack_LHand,
                attackDuration);
            nextAttackUsesRightHand = !nextAttackUsesRightHand;
            DamageCurrentTarget();
        }

        private bool UpdateBarricadeBypass(double now)
        {
            if (!canPassThroughBarricades)
                return false;

            if (currentTarget == null)
            {
                if (bypassingBarricade)
                {
                    bypassingBarricade = false;
                    agent.enabled = true;
                }
                return false;
            }

            if (!bypassingBarricade)
            {
                var nearby = FindClosestBuilding(CoreBuildingType.Barricade, 1.6f);
                if (nearby == null) return false;
                var targetDirection = currentTarget.transform.position - transform.position;
                var barricadeDirection = nearby.transform.position - transform.position;
                targetDirection.y = 0f;
                barricadeDirection.y = 0f;
                if (targetDirection.sqrMagnitude < 0.01f || Vector3.Dot(targetDirection.normalized,
                    barricadeDirection.normalized) < 0.25f) return false;
                IgnoreBarricadeCollisions(nearby);
                bypassingBarricade = true;
                barricadeBypassEndsAt = now + 1.15d;
                agent.enabled = false;
            }

            var direction = currentTarget.transform.position - transform.position;
            direction.y = 0f;
            if (direction.sqrMagnitude > 0.001f)
            {
                direction.Normalize();
                transform.position += direction * movementSpeed * Time.deltaTime;
                transform.rotation = Quaternion.RotateTowards(transform.rotation, Quaternion.LookRotation(direction),
                    540f * Time.deltaTime);
            }
            if (now < barricadeBypassEndsAt) return true;

            bypassingBarricade = false;
            if (NavMesh.SamplePosition(transform.position, out var hit, 2.5f, NavMesh.AllAreas))
            {
                agent.enabled = true;
                agent.Warp(hit.position);
            }
            else
                agent.enabled = true;
            return false;
        }

        private void IgnoreBarricadeCollisions(CoreBuilding barricade)
        {
            var ownColliders = GetComponentsInChildren<Collider>();
            var barricadeColliders = barricade.GetComponentsInChildren<Collider>();
            foreach (var own in ownColliders)
                foreach (var obstacle in barricadeColliders)
                    Physics.IgnoreCollision(own, obstacle, true);
        }

        private void DamageCurrentTarget()
        {
            var hero = currentTarget.GetComponent<NetworkWarrior>();
            if (hero != null) { hero.TakeDamage(damage); return; }
            var building = currentTarget.GetComponent<CoreBuilding>();
            if (building != null) { building.Damage(damage); return; }
            currentTarget.GetComponent<CoreDebugDeposit>()?.Damage(damage);
        }

        public void TakeDamage(float amount, NetworkWarrior attacker = null)
        {
            if (!IsServer || !IsAlive || amount <= 0f)
                return;
            health.Value = Mathf.Max(0f, health.Value - amount);
            if (health.Value <= 0f)
            {
                debuffs.Value = EnemyDebuff.None;
                var shards = Random.Range(Mathf.Min(coreShardsDropMin, coreShardsDropMax),
                    Mathf.Max(coreShardsDropMin, coreShardsDropMax) + 1);
                if (shards > 0)
                    CoreLootPickup.Spawn(MinedResourceKind.CoreShards, shards, transform.position);
                PlayAnimation(EnemyAnimationState.Die, deathAnimationDuration);
                despawnAt = NetworkManager.ServerTime.Time + deathAnimationDuration;
                return;
            }
            PlayAnimation(EnemyAnimationState.TakeHit, 0.28f);
            if (attacker != null && !forcedByTaunt)
                ForceHero(attacker, heroAggroDuration, false);
        }

        public void ApplyTaunt(NetworkWarrior hero, float duration = -1f)
        {
            if (!IsServer || hero == null || coreOnly)
                return;
            ForceHero(hero, duration > 0f ? duration : tauntDuration, true);
        }

        private void ForceHero(NetworkWarrior hero, float duration, bool taunt)
        {
            forcedHero = hero;
            forcedHeroEndsAt = NetworkManager.ServerTime.Time + duration;
            forcedByTaunt = taunt;
            SetTarget(hero.NetworkObject);
        }

        public bool ApplyDebuff(EnemyDebuff debuff, float duration = -1f)
        {
            if (!IsServer || !IsAlive || debuff == EnemyDebuff.None || IsResistant(debuff))
                return false;
            var index = DebuffIndex(debuff);
            if (index < 0)
                return false;
            var effectiveDuration = duration > 0f ? duration : DefaultDuration(debuff);
            debuffs.Value |= debuff;
            debuffEndsAt[index] = NetworkManager.ServerTime.Time + effectiveDuration;
            if (debuff == EnemyDebuff.OnFire)
            {
                nextFireTickAt = NetworkManager.ServerTime.Time + 1d;
                PlayAnimation(EnemyAnimationState.Burn, 0.35f);
            }
            else if (debuff == EnemyDebuff.Poisoned)
                nextPoisonTickAt = NetworkManager.ServerTime.Time + 1d;
            else if (debuff == EnemyDebuff.Freeze)
                PlayAnimation(EnemyAnimationState.Freeze, effectiveDuration);
            return true;
        }

        public void SetInSwamp(bool value)
        {
            if (!IsServer || resistant_Swamp)
                return;
            if (value) debuffs.Value |= EnemyDebuff.Swamp;
            else debuffs.Value &= ~EnemyDebuff.Swamp;
        }

        public void PlaySkillAnimation(EnemyAnimationState state, float duration)
        {
            if (!IsServer || !IsAlive || state == EnemyAnimationState.Die)
                return;
            PlayAnimation(state, Mathf.Max(0.05f, duration));
        }

        private void UpdateDebuffs(double now)
        {
            for (var index = 0; index < debuffEndsAt.Length; index++)
            {
                var flag = (EnemyDebuff)(1 << index);
                if (flag == EnemyDebuff.Swamp || (debuffs.Value & flag) == 0 || now < debuffEndsAt[index])
                    continue;
                debuffs.Value &= ~flag;
            }
            if ((debuffs.Value & EnemyDebuff.OnFire) != 0 && now >= nextFireTickAt)
            {
                nextFireTickAt = now + 1d;
                TakeDamage(fireDamagePerSecond);
            }
            if ((debuffs.Value & EnemyDebuff.Poisoned) != 0 && now >= nextPoisonTickAt)
            {
                nextPoisonTickAt = now + 1d;
                if (health.Value > poisonHealthFloor)
                    health.Value = Mathf.Max(poisonHealthFloor, health.Value - poisonDamagePerSecond);
                if (health.Value <= poisonHealthFloor)
                    debuffs.Value &= ~EnemyDebuff.Poisoned;
            }
        }

        private bool IsImmobilized() => (debuffs.Value & (EnemyDebuff.Freeze | EnemyDebuff.Stun)) != 0;

        private void ApplyMovementSpeed()
        {
            var multiplier = 1f;
            if ((debuffs.Value & EnemyDebuff.Chill) != 0) multiplier *= 1f - chillSlow;
            if ((debuffs.Value & EnemyDebuff.Swamp) != 0) multiplier *= 1f - swampSlow;
            agent.speed = movementSpeed * multiplier;
        }

        private bool IsResistant(EnemyDebuff debuff) => debuff switch
        {
            EnemyDebuff.Freeze => resistant_Freeze,
            EnemyDebuff.Stun => resistant_Stun,
            EnemyDebuff.Chill => resistant_Chill,
            EnemyDebuff.Swamp => resistant_Swamp,
            EnemyDebuff.OnFire => resistant_OnFire,
            EnemyDebuff.Poisoned => resistant_Poisoned,
            _ => true
        };

        private float DefaultDuration(EnemyDebuff debuff) => debuff switch
        {
            EnemyDebuff.OnFire => fireDuration,
            EnemyDebuff.Poisoned => poisonDuration,
            _ => 5f
        };

        private static int DebuffIndex(EnemyDebuff debuff)
        {
            for (var index = 0; index < 6; index++)
                if (debuff == (EnemyDebuff)(1 << index)) return index;
            return -1;
        }

        private void PlayAnimation(EnemyAnimationState state, float duration)
        {
            animationState.Value = state;
            animationStartedAt.Value = NetworkManager.ServerTime.Time;
            animationEndsAt.Value = animationStartedAt.Value + duration;
        }

        private CoreBuilding FindClosestDefense(float range)
        {
            CoreBuilding closest = null;
            var closestSqr = range * range;
            foreach (var building in FindObjectsByType<CoreBuilding>(FindObjectsSortMode.None))
            {
                if (building.BuildingType == CoreBuildingType.TrapPlate || building.BuildingType == CoreBuildingType.Barricade)
                    continue;
                var sqr = SqrDistanceToBuilding(building);
                if (sqr <= closestSqr) { closestSqr = sqr; closest = building; }
            }
            return closest;
        }

        private CoreBuilding FindClosestBuilding(CoreBuildingType type, float range)
        {
            CoreBuilding closest = null;
            var closestSqr = range * range;
            foreach (var building in FindObjectsByType<CoreBuilding>(FindObjectsSortMode.None))
            {
                if (building.BuildingType != type) continue;
                var sqr = SqrDistanceToBuilding(building);
                if (sqr <= closestSqr) { closestSqr = sqr; closest = building; }
            }
            return closest;
        }

        private NetworkWarrior FindClosestHero(float range)
        {
            NetworkWarrior closest = null;
            var closestSqr = range * range;
            foreach (var hero in FindObjectsByType<NetworkWarrior>(FindObjectsSortMode.None))
            {
                if (!hero.IsSpawned || hero.IsDowned) continue;
                var sqr = HorizontalSqrDistance(hero.transform.position);
                if (sqr <= closestSqr) { closestSqr = sqr; closest = hero; }
            }
            return closest;
        }

        private float HorizontalSqrDistance(Vector3 position)
        {
            var offset = position - transform.position;
            offset.y = 0f;
            return offset.sqrMagnitude;
        }

        private float SqrDistanceToBuilding(CoreBuilding building)
        {
            return HorizontalSqrDistance(ClosestTargetPoint(building.NetworkObject));
        }

        private Vector3 ClosestTargetPoint(NetworkObject target)
        {
            var closestPoint = target.transform.position;
            var closestSqrDistance = HorizontalSqrDistance(closestPoint);
            var targetColliders = target == currentTarget
                ? currentTargetColliders
                : target.GetComponentsInChildren<Collider>();
            if (targetColliders == null)
                return closestPoint;
            foreach (var targetCollider in targetColliders)
            {
                if (!targetCollider.enabled || targetCollider.isTrigger)
                    continue;
                var candidate = targetCollider.ClosestPoint(transform.position);
                var candidateSqrDistance = HorizontalSqrDistance(candidate);
                if (candidateSqrDistance >= closestSqrDistance)
                    continue;
                closestPoint = candidate;
                closestSqrDistance = candidateSqrDistance;
            }
            return closestPoint;
        }

        private void FindAndSetCore()
        {
            var core = FindFirstObjectByType<CoreDebugDeposit>();
            SetTarget(core != null ? core.NetworkObject : null);
        }

        private void SetTarget(NetworkObject target)
        {
            if (currentTarget != target)
                currentTargetColliders = target != null ? target.GetComponentsInChildren<Collider>() : null;
            currentTarget = target;
            currentTargetId.Value = target != null && target.IsSpawned ? target.NetworkObjectId : NoTarget;
        }

        private NetworkObject ResolveReplicatedTarget()
        {
            if (IsServer)
                return currentTarget;
            if (currentTargetId.Value == NoTarget || NetworkManager == null)
                return null;
            NetworkManager.SpawnManager.SpawnedObjects.TryGetValue(currentTargetId.Value, out var target);
            return target;
        }

        private static bool IsValidTarget(NetworkObject target)
        {
            if (target == null || !target.IsSpawned) return false;
            var hero = target.GetComponent<NetworkWarrior>();
            if (hero != null) return !hero.IsDowned;
            var building = target.GetComponent<CoreBuilding>();
            if (building != null) return building.Health > 0f;
            var core = target.GetComponent<CoreDebugDeposit>();
            return core != null && core.CurrentHealth > 0f;
        }
    }
}
