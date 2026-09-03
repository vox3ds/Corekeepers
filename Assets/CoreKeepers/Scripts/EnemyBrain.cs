using Unity.Netcode;
using UnityEngine;
using UnityEngine.AI;

namespace CoreKeepers
{
    public enum CoreEnemyType : byte { Normal, Undead }

    public enum EnemyAnimationState : byte
    {
        Idle,
        Walk,
        Attack_LHand,
        Attack_RHand,
        Smash,
        ThrowRock,
        BowShot,
        CastProjectile_LHand,
        CastProjectile_RHand,
        CastBuff,
        TakeHit,
        Freeze,
        Burn,
        Die,
        HeadAttack
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
        [SerializeField, Min(0.1f)] private float heroPriorityRange = 2f;
        [SerializeField, Min(0.05f)] private float targetRefreshInterval = 0.25f;
        [SerializeField] private bool canPassThroughBarricades;
        [SerializeField] private bool assassin;
        [SerializeField] private bool coreOnly;
        [SerializeField] private bool lootGoblin;

        [Header("Combat")]
        [SerializeField] private CoreEnemyType enemyType;
        [SerializeField, Min(1f)] private float maximumHealth = 100f;
        [SerializeField, Min(0f)] private float damage = 10f;
        [SerializeField, Min(0.1f)] private float attackRange = 1.4f;
        [SerializeField, Min(0.05f)] private float attackDuration = 0.55f;
        [SerializeField, Min(0.05f)] private float attackCooldown = 1.1f;
        [SerializeField, Min(0.1f)] private float movementSpeed = 3.5f;
        [SerializeField, Min(0f)] private float heroAggroDuration = 10f;
        [SerializeField, Min(0f)] private float tauntDuration = 15f;
        [SerializeField, Min(0.05f)] private float deathAnimationDuration = 1.1f;

        [Header("Loot Goblin")]
        [SerializeField, Min(1f)] private float lootGoblinEscapeDuration = 35f;
        [SerializeField, Min(1f)] private float lootGoblinFleeDistance = 8f;
        [SerializeField, Min(0.05f)] private float lootGoblinPathRefreshInterval = 0.35f;

        [Header("Physics Roll")]
        [SerializeField] private GameObject explosionEffectPrefab;
        [SerializeField, Min(0.05f)] private float explosionEffectLifetime = 2f;

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
        [SerializeField, Range(0f, 0.95f)] private float chillSlow = 0.5f;
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
        private Rigidbody body;
        private EnemyProceduralAnimator proceduralAnimator;
        private NetworkObject currentTarget;
        private Collider[] currentTargetColliders;
        private NetworkWarrior forcedHero;
        private double forcedHeroEndsAt;
        private bool forcedByTaunt;
        private NetworkWarrior proximityAggroHero;
        private double proximityAggroEndsAt;
        private double nextTargetRefreshAt;
        private double nextAttackAt;
        private double nextFireTickAt;
        private double nextPoisonTickAt;
        private double despawnAt;
        private double lootGoblinEscapeAt;
        private double nextLootGoblinPathAt;
        private Vector3 previousPosition;
        private float normalizedSpeed;
        private bool nextAttackUsesRightHand;
        private NetworkObject pendingProjectileTarget;
        private double pendingProjectileAt;
        private bool pendingProjectileRightSide;
        private Vector3 rollingDirection;
        private bool exploded;
        private bool bypassingBarricade;
        private double barricadeBypassEndsAt;
        private ulong arcaneExposureOwner = ulong.MaxValue;
        private double arcaneExposureEndsAt;
        private float arcaneExposureBonus;
        private NetworkWarrior elementalStatusSource;
        private Vector3 gravityVortexCenter;
        private float gravityVortexStrength;
        private double gravityVortexEndsAt;
        private bool gravityVortexActive;
        private bool gravityVortexWasPhysicsRoll;
        private Quaternion gravityVortexUprightRotation;
        private bool gravityVortexRecoveryActive;
        private double gravityVortexRecoveryStartedAt;
        private Vector3 gravityVortexRecoveryStart;
        private Vector3 gravityVortexRecoveryTarget;
        private Quaternion gravityVortexRecoveryStartRotation;

        public float Health => health.Value;
        public float MaximumHealth => maximumHealth;
        public CoreEnemyType EnemyType => enemyType;
        public bool IsAlive => health.Value > 0f;
        public bool CanPassThroughBarricades => canPassThroughBarricades;
        public bool IsAssassin => assassin;
        public bool IsCoreOnly => coreOnly;
        public bool IsLootGoblin => lootGoblin;
        public EnemyDebuff ActiveDebuffs => debuffs.Value;
        public EnemyAnimationState CurrentAnimation => animationState.Value;
        public float NormalizedSpeed => gravityVortexActive || gravityVortexRecoveryActive ? 0f : normalizedSpeed;
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
            body = GetComponent<Rigidbody>();
            proceduralAnimator = GetComponent<EnemyProceduralAnimator>();
            previousPosition = transform.position;
        }

        public override void OnNetworkSpawn()
        {
            var physicsRoll = proceduralAnimator != null && proceduralAnimator.UsesPhysicsRolling;
            agent.enabled = IsServer && !physicsRoll;
            if (body != null)
            {
                body.isKinematic = !IsServer || !physicsRoll;
                body.useGravity = physicsRoll;
                body.interpolation = RigidbodyInterpolation.Interpolate;
            }
            if (!IsServer)
                return;
            health.Value = maximumHealth;
            if (agent.enabled)
            {
                agent.speed = movementSpeed;
                agent.stoppingDistance = attackRange * 0.85f;
            }
            if (lootGoblin)
                lootGoblinEscapeAt = NetworkManager.ServerTime.Time + lootGoblinEscapeDuration;
            else
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

            if (UpdateGravityVortex(now))
                return;

            if (lootGoblin && now >= lootGoblinEscapeAt)
            {
                if (NetworkObject.IsSpawned) NetworkObject.Despawn(true);
                return;
            }

            UpdatePendingProjectile(now);

            if (animationState.Value != EnemyAnimationState.Idle && now >= animationEndsAt.Value)
                animationState.Value = EnemyAnimationState.Idle;
            if (IsImmobilized())
            {
                if (agent.enabled) agent.isStopped = true;
                rollingDirection = Vector3.zero;
                return;
            }

            if (lootGoblin)
            {
                FleeFromHeroes(now);
                return;
            }

            if (agent.enabled) agent.isStopped = false;
            if (agent.enabled) ApplyMovementSpeed();
            if (now >= nextTargetRefreshAt)
            {
                nextTargetRefreshAt = now + targetRefreshInterval;
                SelectTarget(now);
            }
            if (proceduralAnimator != null && proceduralAnimator.UsesPhysicsRolling)
            {
                FollowWithPhysics();
                return;
            }
            if (UpdateBarricadeBypass(now))
                return;
            FollowAndAttack(now);
        }

        private void FixedUpdate()
        {
            if (IsServer && gravityVortexActive && body != null && !body.isKinematic)
            {
                var offset = gravityVortexCenter - body.position;
                offset.y = 0f;
                var vortexPlanarVelocity = new Vector3(body.linearVelocity.x, 0f, body.linearVelocity.z);
                if (offset.sqrMagnitude > 0.01f)
                {
                    var direction = offset.normalized;
                    var acceleration = Mathf.Max(10f, gravityVortexStrength * 12f);
                    body.AddForce(direction * acceleration - vortexPlanarVelocity * 2.2f, ForceMode.Acceleration);
                    body.AddTorque(Vector3.Cross(Vector3.up, direction) * acceleration * 0.7f,
                        ForceMode.Acceleration);
                    var maximumSpeed = Mathf.Max(5f, gravityVortexStrength * 4.5f);
                    if (vortexPlanarVelocity.magnitude > maximumSpeed)
                    {
                        var clamped = vortexPlanarVelocity.normalized * maximumSpeed;
                        body.linearVelocity = new Vector3(clamped.x, body.linearVelocity.y, clamped.z);
                    }
                }
                else
                    body.AddForce(-vortexPlanarVelocity * 4f, ForceMode.Acceleration);
                return;
            }
            if (!IsServer || body == null || body.isKinematic || rollingDirection.sqrMagnitude < 0.001f)
                return;
            var desiredVelocity = rollingDirection * movementSpeed;
            var planarVelocity = new Vector3(body.linearVelocity.x, 0f, body.linearVelocity.z);
            body.AddForce((desiredVelocity - planarVelocity) * 8f, ForceMode.Acceleration);
            var rollAxis = Vector3.Cross(Vector3.up, rollingDirection);
            body.AddTorque(rollAxis * movementSpeed * 2.2f, ForceMode.Acceleration);
        }

        private void FollowWithPhysics()
        {
            rollingDirection = Vector3.zero;
            if (!IsValidTarget(currentTarget))
            {
                FindAndSetCore();
                return;
            }
            var offset = ClosestTargetPoint(currentTarget) - transform.position;
            offset.y = 0f;
            if (offset.magnitude <= attackRange)
            {
                Explode();
                return;
            }
            rollingDirection = offset.normalized;
        }

        private void FleeFromHeroes(double now)
        {
            if (!agent.enabled || !agent.isOnNavMesh)
                return;
            agent.isStopped = false;
            ApplyMovementSpeed();
            if (now < nextLootGoblinPathAt)
                return;
            nextLootGoblinPathAt = now + lootGoblinPathRefreshInterval;

            NetworkWarrior nearest = null;
            var nearestSqr = float.MaxValue;
            foreach (var hero in FindObjectsByType<NetworkWarrior>())
            {
                if (!hero.IsSpawned || hero.IsDowned) continue;
                var offset = transform.position - hero.transform.position;
                offset.y = 0f;
                if (offset.sqrMagnitude >= nearestSqr) continue;
                nearestSqr = offset.sqrMagnitude;
                nearest = hero;
            }
            if (nearest == null)
            {
                agent.ResetPath();
                return;
            }

            var direction = transform.position - nearest.transform.position;
            direction.y = 0f;
            if (direction.sqrMagnitude < 0.01f)
            {
                var random = Random.insideUnitCircle.normalized;
                direction = new Vector3(random.x, 0f, random.y);
            }
            direction.Normalize();
            var sideways = Vector3.Cross(Vector3.up, direction) * Random.Range(-0.35f, 0.35f);
            var candidate = transform.position + (direction + sideways).normalized * lootGoblinFleeDistance;
            if (NavMesh.SamplePosition(candidate, out var hit, lootGoblinFleeDistance * 0.6f, NavMesh.AllAreas))
                agent.SetDestination(hit.position);
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
            if (HasActiveTaunt(now))
            {
                var tauntBarricade = canPassThroughBarricades
                    ? null
                    : FindClosestBuilding(CoreBuildingType.Barricade, barricadePriorityRange);
                SetTarget(tauntBarricade != null ? tauntBarricade.NetworkObject : forcedHero.NetworkObject);
                return;
            }

            var nearbyPriorityHero = coreOnly ? null : FindClosestHero(heroPriorityRange);
            var priorityHero = nearbyPriorityHero != null
                ? nearbyPriorityHero
                : IsActiveProximityAggro(now) ? proximityAggroHero : null;
            if (priorityHero != null)
            {
                var heroSqrDistance = HorizontalSqrDistance(priorityHero.transform.position);
                var comparisonRange = Mathf.Max(barricadePriorityRange, Mathf.Sqrt(heroSqrDistance));
                var closerBarricade = canPassThroughBarricades
                    ? null
                    : FindClosestBuilding(CoreBuildingType.Barricade, comparisonRange);
                if (closerBarricade != null && SqrDistanceToBuilding(closerBarricade) < heroSqrDistance)
                {
                    SetTarget(closerBarricade.NetworkObject);
                    return;
                }

                if (nearbyPriorityHero != null)
                {
                    proximityAggroHero = nearbyPriorityHero;
                    proximityAggroEndsAt = now + heroAggroDuration * 0.5d;
                }
                SetTarget(priorityHero.NetworkObject);
                return;
            }
            proximityAggroHero = null;

            var barricade = canPassThroughBarricades
                ? null
                : FindClosestBuilding(CoreBuildingType.Barricade, barricadePriorityRange);
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

        private bool HasActiveTaunt(double now) => forcedByTaunt && forcedHero != null && forcedHero.IsSpawned &&
            !forcedHero.IsDowned && now < forcedHeroEndsAt;

        private bool IsActiveProximityAggro(double now) => proximityAggroHero != null &&
            proximityAggroHero.IsSpawned && !proximityAggroHero.IsDowned && now < proximityAggroEndsAt;

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
            // ClosestTargetPoint is measured from the agent's centre to the target surface.
            // Include the agent body radius so avoidance cannot leave it stranded just outside
            // the nominal attack range, especially around the large Core collider.
            var effectiveAttackRange = attackRange + agent.radius;
            if (offset.magnitude > effectiveAttackRange)
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

            var attackState = proceduralAnimator != null
                ? proceduralAnimator.GetNextAttackState(ref nextAttackUsesRightHand)
                : (nextAttackUsesRightHand ? EnemyAnimationState.Attack_RHand : EnemyAnimationState.Attack_LHand);
            if (attackState == EnemyAnimationState.Idle)
                return;
            nextAttackAt = now + attackCooldown;
            PlayAnimation(attackState, attackDuration);
            if (proceduralAnimator == null) nextAttackUsesRightHand = !nextAttackUsesRightHand;
            if (attackState == EnemyAnimationState.CastProjectile_LHand ||
                attackState == EnemyAnimationState.CastProjectile_RHand)
                QueueMagicProjectile(attackState == EnemyAnimationState.CastProjectile_RHand, now);
            else
                DamageCurrentTarget();
        }

        private void QueueMagicProjectile(bool rightSide, double now)
        {
            pendingProjectileTarget = currentTarget;
            pendingProjectileRightSide = rightSide;
            var releaseTime = proceduralAnimator != null ? proceduralAnimator.ProjectileReleaseTime : 0.55f;
            pendingProjectileAt = now + attackDuration * releaseTime;
        }

        private void UpdatePendingProjectile(double now)
        {
            if (pendingProjectileTarget == null || now < pendingProjectileAt)
                return;
            var expectedState = pendingProjectileRightSide
                ? EnemyAnimationState.CastProjectile_RHand
                : EnemyAnimationState.CastProjectile_LHand;
            var target = pendingProjectileTarget;
            pendingProjectileTarget = null;
            if (animationState.Value != expectedState || !IsValidTarget(target))
                return;
            SpawnMagicProjectile(pendingProjectileRightSide, target);
        }

        private void SpawnMagicProjectile(bool rightSide, NetworkObject target)
        {
            var prefab = proceduralAnimator != null ? proceduralAnimator.ProjectilePrefab : null;
            if (prefab == null)
            {
                Debug.LogError($"Enemy '{name}' uses Alternating Magic Projectile but has no projectile prefab.", this);
                return;
            }
            if (target == null || !target.IsSpawned)
                return;
            var origin = proceduralAnimator.GetProjectileOrigin(rightSide);
            var direction = target.transform.position - origin;
            if (direction.sqrMagnitude < 0.001f) direction = transform.forward;
            var instance = Instantiate(prefab, origin, Quaternion.LookRotation(direction.normalized));
            var projectile = instance.GetComponent<EnemyProjectile>();
            var projectileNetworkObject = instance.GetComponent<NetworkObject>();
            if (projectile == null || projectileNetworkObject == null)
            {
                Destroy(instance);
                Debug.LogError($"Projectile prefab '{prefab.name}' requires EnemyProjectile and NetworkObject.", prefab);
                return;
            }
            projectileNetworkObject.Spawn(true);
            projectile.Initialize(target, damage);
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

        public void TakeDamage(float amount, NetworkWarrior attacker = null, bool activeSkill = false,
            bool suppressDeathTrigger = false)
        {
            if (!IsServer || !IsAlive || amount <= 0f)
                return;
            if (attacker != null && attacker.OwnerClientId != arcaneExposureOwner &&
                NetworkManager.ServerTime.Time < arcaneExposureEndsAt)
                amount *= 1f + arcaneExposureBonus;
            health.Value = Mathf.Max(0f, health.Value - amount);
            if (health.Value <= 0f)
            {
                if (!suppressDeathTrigger && elementalStatusSource != null &&
                    (debuffs.Value & (EnemyDebuff.OnFire | EnemyDebuff.Freeze | EnemyDebuff.Chill)) != 0)
                    elementalStatusSource.GetComponent<HeroSkillController>()?.ServerElementalDetonation(transform.position);
                debuffs.Value = EnemyDebuff.None;
                var shards = Random.Range(Mathf.Min(coreShardsDropMin, coreShardsDropMax),
                    Mathf.Max(coreShardsDropMin, coreShardsDropMax) + 1);
                if (shards > 0)
                    CoreLootPickup.Spawn(MinedResourceKind.CoreShards, shards, transform.position);
                if (proceduralAnimator != null && proceduralAnimator.UsesPhysicsRolling)
                    Explode();
                else
                {
                    PlayAnimation(EnemyAnimationState.Die, deathAnimationDuration);
                    despawnAt = NetworkManager.ServerTime.Time + deathAnimationDuration;
                }
                return;
            }
            PlayAnimation(EnemyAnimationState.TakeHit, 0.28f);
            if (!lootGoblin && attacker != null && !forcedByTaunt)
                ForceHero(attacker, heroAggroDuration, false);
        }

        public void ApplyTaunt(NetworkWarrior hero, float duration = -1f)
        {
            if (lootGoblin) return;
            if (!IsServer || hero == null || coreOnly)
                return;
            ForceHero(hero, duration > 0f ? duration : tauntDuration, true);
        }

        public void ApplyArcaneExposure(ulong mageOwner, float duration, float bonus)
        {
            if (!IsServer || !IsAlive) return;
            arcaneExposureOwner = mageOwner;
            arcaneExposureBonus = Mathf.Max(arcaneExposureBonus, bonus);
            arcaneExposureEndsAt = NetworkManager.ServerTime.Time + duration;
        }

        public void ApplyImpulse(Vector3 impulse)
        {
            if (!IsServer || !IsAlive) return;
            var destination = transform.position + new Vector3(impulse.x, 0f, impulse.z);
            if (NavMesh.SamplePosition(destination, out var hit, 2f, NavMesh.AllAreas))
            {
                if (agent != null && agent.enabled && agent.isOnNavMesh) agent.Warp(hit.position);
                else transform.position = hit.position;
            }
        }

        public void PullToward(Vector3 center, float distance)
        {
            var offset = center - transform.position; offset.y = 0f;
            if (offset.sqrMagnitude < 0.01f) return;
            ApplyImpulse(offset.normalized * Mathf.Min(distance, offset.magnitude));
        }

        public void ApplyGravityVortex(Vector3 center, float strength, float duration)
        {
            if (!IsServer || !IsAlive || duration <= 0f) return;
            gravityVortexCenter = new Vector3(center.x, transform.position.y, center.z);
            gravityVortexStrength = Mathf.Max(gravityVortexStrength, strength);
            gravityVortexEndsAt = System.Math.Max(gravityVortexEndsAt,
                NetworkManager.ServerTime.Time + duration);
            if (gravityVortexActive) return;

            gravityVortexActive = true;
            gravityVortexRecoveryActive = false;
            gravityVortexWasPhysicsRoll = proceduralAnimator != null && proceduralAnimator.UsesPhysicsRolling;
            var forward = transform.forward;
            forward.y = 0f;
            gravityVortexUprightRotation = forward.sqrMagnitude > 0.001f
                ? Quaternion.LookRotation(forward.normalized, Vector3.up)
                : Quaternion.identity;
            rollingDirection = Vector3.zero;
            bypassingBarricade = false;
            pendingProjectileTarget = null;
            if (agent != null && agent.enabled)
            {
                if (agent.isOnNavMesh) agent.ResetPath();
                agent.enabled = false;
            }
            if (body != null)
            {
                body.isKinematic = false;
                body.useGravity = true;
                body.interpolation = RigidbodyInterpolation.Interpolate;
            }
            animationState.Value = EnemyAnimationState.Idle;
            animationStartedAt.Value = NetworkManager.ServerTime.Time;
            animationEndsAt.Value = NetworkManager.ServerTime.Time;
        }

        private bool UpdateGravityVortex(double now)
        {
            if (gravityVortexActive && now < gravityVortexEndsAt)
            {
                rollingDirection = Vector3.zero;
                animationState.Value = EnemyAnimationState.Idle;
                return true;
            }

            if (gravityVortexActive)
            {
                gravityVortexActive = false;
                gravityVortexStrength = 0f;
                gravityVortexRecoveryActive = true;
                gravityVortexRecoveryStartedAt = now;
                gravityVortexRecoveryStart = transform.position;
                gravityVortexRecoveryStartRotation = transform.rotation;
                gravityVortexRecoveryTarget = transform.position;
                if (NavMesh.SamplePosition(transform.position, out var recoveryHit, 2.5f, NavMesh.AllAreas))
                    gravityVortexRecoveryTarget = recoveryHit.position;
                if (body != null)
                {
                    body.linearVelocity = Vector3.zero;
                    body.angularVelocity = Vector3.zero;
                    body.isKinematic = true;
                    body.useGravity = false;
                }
            }

            if (!gravityVortexRecoveryActive) return false;
            const float recoveryDuration = 0.4f;
            var progress = Mathf.Clamp01((float)((now - gravityVortexRecoveryStartedAt) / recoveryDuration));
            var eased = Mathf.SmoothStep(0f, 1f, progress);
            transform.position = Vector3.Lerp(gravityVortexRecoveryStart, gravityVortexRecoveryTarget, eased);
            transform.rotation = Quaternion.Slerp(gravityVortexRecoveryStartRotation,
                gravityVortexUprightRotation, eased);
            if (progress < 1f) return true;

            gravityVortexRecoveryActive = false;
            if (body != null)
            {
                body.position = gravityVortexRecoveryTarget;
                body.rotation = gravityVortexUprightRotation;
                body.isKinematic = !gravityVortexWasPhysicsRoll;
                body.useGravity = gravityVortexWasPhysicsRoll;
            }
            if (!gravityVortexWasPhysicsRoll && agent != null)
            {
                agent.enabled = true;
                if (agent.isOnNavMesh) agent.ResetPath();
            }
            return false;
        }

        private void ForceHero(NetworkWarrior hero, float duration, bool taunt)
        {
            forcedHero = hero;
            forcedHeroEndsAt = NetworkManager.ServerTime.Time + duration;
            forcedByTaunt = taunt;
            SetTarget(hero.NetworkObject);
        }

        public bool ApplyDebuff(EnemyDebuff debuff, float duration = -1f, NetworkWarrior source = null)
        {
            if (!IsServer || !IsAlive || debuff == EnemyDebuff.None || IsResistant(debuff))
                return false;
            var index = DebuffIndex(debuff);
            if (index < 0)
                return false;
            var effectiveDuration = duration > 0f ? duration : DefaultDuration(debuff);
            debuffs.Value |= debuff;
            if (source != null && (debuff & (EnemyDebuff.OnFire | EnemyDebuff.Freeze | EnemyDebuff.Chill)) != 0)
                elementalStatusSource = source;
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

        private void Explode()
        {
            if (exploded || !IsServer) return;
            exploded = true;
            rollingDirection = Vector3.zero;
            if (body != null) body.linearVelocity = Vector3.zero;
            if (IsValidTarget(currentTarget)) DamageCurrentTarget();
            SpawnExplosionRpc(transform.position);
            despawnAt = NetworkManager.ServerTime.Time + 0.15d;
            health.Value = 0f;
        }

        [Rpc(SendTo.ClientsAndHost)]
        private void SpawnExplosionRpc(Vector3 position)
        {
            if (explosionEffectPrefab != null)
            {
                var configuredEffect = Instantiate(explosionEffectPrefab, position, Quaternion.identity);
                Destroy(configuredEffect, explosionEffectLifetime);
                return;
            }
            var effect = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            effect.name = "Pumpkin Explosion";
            effect.transform.position = position + Vector3.up * 0.65f;
            effect.transform.localScale = Vector3.one * 0.25f;
            Destroy(effect.GetComponent<Collider>());
            var renderer = effect.GetComponent<Renderer>();
            if (renderer != null) renderer.material.color = new Color(1f, 0.24f, 0.03f, 1f);
            effect.AddComponent<EnemyExplosionEffect>();
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
            var building = target.GetComponent<CoreBuilding>();
            if (building != null)
            {
                var footprintCollider = target.GetComponent<Collider>();
                if (footprintCollider != null && footprintCollider.enabled && !footprintCollider.isTrigger)
                    return footprintCollider.ClosestPoint(transform.position);
            }

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

    public sealed class EnemyExplosionEffect : MonoBehaviour
    {
        private const float Lifetime = 0.45f;
        private const float MaximumScale = 4f;
        private float age;

        private void Update()
        {
            age += Time.deltaTime;
            var progress = Mathf.Clamp01(age / Lifetime);
            transform.localScale = Vector3.one * Mathf.Lerp(0.25f, MaximumScale,
                1f - (1f - progress) * (1f - progress));
            var effectRenderer = GetComponent<Renderer>();
            if (effectRenderer != null)
            {
                var color = effectRenderer.material.color;
                color.a = 1f - progress;
                effectRenderer.material.color = color;
            }
            if (age >= Lifetime) Destroy(gameObject);
        }
    }
}
