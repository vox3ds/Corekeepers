using Unity.Netcode;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.UI;
using System.Collections.Generic;

namespace CoreKeepers
{
    public enum CoreBuildingType : byte
    {
        SmallTower,
        HeavyTower,
        Barricade,
        TrapPlate,
        SupportPylon
    }

    public enum CoreBuildingState : byte
    {
        UnderConstruction,
        Active,
        Damaged
    }

    public static class CoreBuildingCatalog
    {
        private static readonly string[] Names = { "Small Tower", "Heavy Tower", "Barricade", "Trap Plate", "Support Pylon" };
        private static readonly int[] Costs = { 20, 30, 15, 18, 25 };
        private static readonly int[] BuildPoints = { 60, 90, 45, 50, 75 };
        // Baseline balance targets: towers survive roughly 12-20 normal enemy hits,
        // while the barricade buys the team a longer repair window.
        private static readonly float[] MaximumHealth = { 180f, 260f, 360f, 90f, 160f };
        private static readonly float[] BaseDamage = { 22f, 55f, 0f, 50f, 0f };
        private static readonly float[] AttackCooldown = { 0.8f, 1.8f, 0f, 0f, 0f };
        private static readonly float[] AttackRange = { 7f, 8.5f, 0f, 0f, 0f };
        private static readonly string[] Icons = { "▲", "⬢", "▰", "◇", "✦" };

        public static string Name(CoreBuildingType type) => Names[(int)type];
        public static string Icon(CoreBuildingType type) => Icons[(int)type];
        public static int Cost(CoreBuildingType type) => Costs[(int)type];
        public static MinedResourceKind BuildCurrency(CoreBuildingType type) => MinedResourceKind.Ore;
        public static int RequiredBuildPoints(CoreBuildingType type) => BuildPoints[(int)type];
        public static float MaxHealth(CoreBuildingType type) => MaximumHealth[(int)type];
        public static float Damage(CoreBuildingType type) => BaseDamage[(int)type];
        public static float Cooldown(CoreBuildingType type) => AttackCooldown[(int)type];
        public static float Range(CoreBuildingType type) => AttackRange[(int)type];
        public static int UpgradeCost(CoreBuildingType type, int level) => Mathf.RoundToInt(Cost(type) * (0.75f + level * 0.5f));
        public static MinedResourceKind UpgradeCurrency(CoreBuildingType type) => MinedResourceKind.CoreShards;
        public static string ResourcePath(CoreBuildingType type) => $"Buildings/{type}";
    }

    [RequireComponent(typeof(NetworkObject))]
    public sealed class CoreBuilding : NetworkBehaviour
    {
        private struct TimedModifier
        {
            public float Damage;
            public float AttackSpeed;
            public float Range;
            public float Resistance;
            public double EndsAt;
        }
        [SerializeField] private CoreBuildingType buildingType;
        [SerializeField] private GameObject foundationVisual;
        [SerializeField] private GameObject completedVisual;
        [SerializeField, Min(1)] private int maximumLevel = 3;

        private readonly NetworkVariable<CoreBuildingState> state = new(CoreBuildingState.UnderConstruction);
        private readonly NetworkVariable<int> constructionPoints = new(0);
        private readonly NetworkVariable<float> health = new(1f);
        private readonly NetworkVariable<byte> level = new(1);
        private readonly NetworkVariable<byte> upgradeBranch = new(0);
        private readonly NetworkVariable<float> maximumHealthMultiplier = new(1f);
        private readonly GameObject[] buildProgressStages = new GameObject[5];
        private Transform buildProgressRoot;
        private Transform constructionProgressBarRoot;
        private Image constructionProgressBarFill;
        private double nextAttackAt;
        private readonly Dictionary<ulong, TimedModifier> timedModifiers = new();
        private static readonly string[] BuildProgressNames =
            { "Build0%", "Build20%", "Build40%", "Build60%", "Build80%" };

        public CoreBuildingType BuildingType => buildingType;
        public CoreBuildingState State => state.Value;
        public int ConstructionPoints => constructionPoints.Value;
        public int RequiredConstructionPoints => CoreBuildingCatalog.RequiredBuildPoints(buildingType);
        public float Health => health.Value;
        public float MaximumHealth => CoreBuildingCatalog.MaxHealth(buildingType) *
            (1f + (level.Value - 1) * 0.25f) * maximumHealthMultiplier.Value;
        public int Level => level.Value;
        public int MaximumLevel => maximumLevel;
        public bool CanUpgrade => state.Value != CoreBuildingState.UnderConstruction && level.Value < maximumLevel;
        public float DamageMultiplier => Aggregate(modifier => modifier.Damage, 1f);
        public float AttackSpeedMultiplier => Aggregate(modifier => modifier.AttackSpeed, 1f);
        public float RangeMultiplier => Aggregate(modifier => modifier.Range, 1f);
        public string StatusLabel => state.Value switch
        {
            CoreBuildingState.UnderConstruction => $"BUILD {constructionPoints.Value}/{RequiredConstructionPoints}",
            CoreBuildingState.Damaged => $"REPAIR {Mathf.CeilToInt(health.Value)}/{Mathf.CeilToInt(MaximumHealth)}",
            _ => $"LEVEL {level.Value}"
        };

        private void Awake()
        {
            CacheBuildProgressStages();
            CacheConstructionProgressBar();
        }

        public void Configure(CoreBuildingType type, GameObject foundation, GameObject completed)
        {
            buildingType = type;
            foundationVisual = foundation;
            completedVisual = completed;
            CacheBuildProgressStages();
            CacheConstructionProgressBar();
        }

        public override void OnNetworkSpawn()
        {
            if (IsServer)
            {
                state.Value = CoreBuildingState.UnderConstruction;
                constructionPoints.Value = 0;
                health.Value = 1f;
                level.Value = 1;
                maximumHealthMultiplier.Value = 1f;
            }
            CacheBuildProgressStages();
            CacheConstructionProgressBar();
            RefreshVisuals();
        }

        private void Update()
        {
            RefreshVisuals();
            if (!IsServer) return;
            var now = NetworkManager.ServerTime.Time;
            if (timedModifiers.Count > 0)
            {
                var expired = new List<ulong>();
                foreach (var entry in timedModifiers)
                    if (now >= entry.Value.EndsAt) expired.Add(entry.Key);
                foreach (var source in expired) timedModifiers.Remove(source);
            }
            TryAttack(now);
        }

        private void TryAttack(double now)
        {
            if (state.Value == CoreBuildingState.UnderConstruction || health.Value <= 0f || now < nextAttackAt)
                return;

            var baseDamage = CoreBuildingCatalog.Damage(buildingType);
            var baseCooldown = CoreBuildingCatalog.Cooldown(buildingType);
            var range = CoreBuildingCatalog.Range(buildingType) * RangeMultiplier;
            if (baseDamage <= 0f || baseCooldown <= 0f || range <= 0f)
                return;

            EnemyBrain closest = null;
            var closestSqr = range * range;
            foreach (var enemy in FindObjectsByType<EnemyBrain>())
            {
                if (!enemy.IsAlive) continue;
                var offset = enemy.transform.position - transform.position;
                offset.y = 0f;
                var sqr = offset.sqrMagnitude;
                if (sqr >= closestSqr) continue;
                closestSqr = sqr;
                closest = enemy;
            }
            if (closest == null)
            {
                nextAttackAt = now + 0.2d;
                return;
            }

            var levelMultiplier = 1f + (level.Value - 1) * 0.2f;
            closest.TakeDamage(baseDamage * levelMultiplier * DamageMultiplier);
            nextAttackAt = now + baseCooldown / Mathf.Max(0.01f, AttackSpeedMultiplier);
        }

        public void BuildOrRepair(int points)
        {
            if (!IsServer || points <= 0)
                return;

            if (state.Value == CoreBuildingState.UnderConstruction)
            {
                constructionPoints.Value = Mathf.Min(RequiredConstructionPoints, constructionPoints.Value + points);
                if (constructionPoints.Value >= RequiredConstructionPoints)
                {
                    state.Value = CoreBuildingState.Active;
                    health.Value = MaximumHealth;
                }
            }
            else if (health.Value < MaximumHealth)
            {
                health.Value = Mathf.Min(MaximumHealth, health.Value + points);
                state.Value = health.Value < MaximumHealth ? CoreBuildingState.Damaged : CoreBuildingState.Active;
            }
        }

        public void Damage(float amount)
        {
            if (!IsServer || amount <= 0f)
                return;
            amount *= 1f - Mathf.Clamp01(Aggregate(modifier => modifier.Resistance, 0f));
            if (state.Value == CoreBuildingState.UnderConstruction)
            {
                NetworkObject.Despawn(true);
                return;
            }
            health.Value = Mathf.Max(0f, health.Value - amount);
            if (health.Value <= 0f)
                NetworkObject.Despawn(true);
            else
                state.Value = CoreBuildingState.Damaged;
        }

        public void ApplyTimedModifiers(ulong sourceId, float damageMultiplier, float attackSpeedMultiplier,
            float rangeMultiplier, float resistance, float duration)
        {
            if (!IsServer || duration <= 0f) return;
            timedModifiers[sourceId] = new TimedModifier
            {
                Damage = Mathf.Max(1f, damageMultiplier),
                AttackSpeed = Mathf.Max(1f, attackSpeedMultiplier),
                Range = Mathf.Max(1f, rangeMultiplier),
                Resistance = Mathf.Clamp01(resistance),
                EndsAt = NetworkManager.ServerTime.Time + duration
            };
        }

        public void ApplyMaximumHealthBonus(float multiplier)
        {
            if (!IsServer || multiplier <= maximumHealthMultiplier.Value) return;
            var previousMaximum = MaximumHealth;
            maximumHealthMultiplier.Value = multiplier;
            health.Value = Mathf.Min(MaximumHealth, health.Value + MaximumHealth - previousMaximum);
        }

        private float Aggregate(System.Func<TimedModifier, float> selector, float initial)
        {
            var value = initial;
            foreach (var modifier in timedModifiers.Values)
                value = Mathf.Max(value, selector(modifier));
            return value;
        }

        public bool TryUpgrade(byte branch)
        {
            if (!IsServer || !CanUpgrade)
                return false;
            upgradeBranch.Value = branch;
            level.Value++;
            health.Value = MaximumHealth;
            state.Value = CoreBuildingState.Active;
            return true;
        }

        private void RefreshVisuals()
        {
            var completed = state.Value != CoreBuildingState.UnderConstruction;
            if (foundationVisual != null)
                foundationVisual.SetActive(!completed);
            if (completedVisual != null)
            {
                completedVisual.SetActive(completed);
                var pulse = state.Value == CoreBuildingState.Damaged ? 0.92f + Mathf.Sin(Time.time * 8f) * 0.04f : 1f;
                completedVisual.transform.localScale = Vector3.one * pulse;
            }
            RefreshBuildProgress(completed);
            RefreshConstructionProgressBar(completed);
        }

        private void CacheBuildProgressStages()
        {
            buildProgressRoot = FindDeepChild(transform, "BuildProgress");
            if (buildProgressRoot == null)
                return;
            for (var index = 0; index < BuildProgressNames.Length; index++)
            {
                var stage = FindDeepChild(buildProgressRoot, BuildProgressNames[index]);
                buildProgressStages[index] = stage != null ? stage.gameObject : null;
            }
        }

        private void RefreshBuildProgress(bool completed)
        {
            if (buildProgressRoot == null)
                CacheBuildProgressStages();

            var ratio = RequiredConstructionPoints > 0
                ? Mathf.Clamp01((float)constructionPoints.Value / RequiredConstructionPoints)
                : 0f;
            var activeStage = completed ? -1 : Mathf.Clamp(Mathf.FloorToInt(ratio * 5f), 0, 4);

            if (buildProgressRoot != null && buildProgressRoot.gameObject.activeSelf != !completed)
                buildProgressRoot.gameObject.SetActive(!completed);

            for (var index = 0; index < buildProgressStages.Length; index++)
            {
                var stage = buildProgressStages[index];
                if (stage != null && stage.activeSelf != (index == activeStage))
                    stage.SetActive(index == activeStage);
            }
        }

        private void CacheConstructionProgressBar()
        {
            constructionProgressBarRoot = FindDeepChild(transform, "BuildProgressBar");
            constructionProgressBarFill = null;
            if (constructionProgressBarRoot == null)
                return;

            var canvas = FindDirectChild(constructionProgressBarRoot, "Canvas");
            var fill = canvas != null ? FindDirectChild(canvas, "Fill") : null;
            if (fill != null)
                constructionProgressBarFill = fill.GetComponent<Image>();
        }

        private void RefreshConstructionProgressBar(bool completed)
        {
            if (constructionProgressBarRoot == null || constructionProgressBarFill == null)
                CacheConstructionProgressBar();
            if (constructionProgressBarRoot == null)
                return;

            var ratio = RequiredConstructionPoints > 0
                ? Mathf.Clamp01((float)constructionPoints.Value / RequiredConstructionPoints)
                : 0f;
            if (constructionProgressBarFill != null)
                constructionProgressBarFill.fillAmount = ratio;
            if (constructionProgressBarRoot.gameObject.activeSelf != !completed)
                constructionProgressBarRoot.gameObject.SetActive(!completed);
        }

        private static Transform FindDirectChild(Transform root, string childName)
        {
            for (var index = 0; index < root.childCount; index++)
            {
                var child = root.GetChild(index);
                if (child.name == childName)
                    return child;
            }
            return null;
        }

        private static Transform FindDeepChild(Transform root, string childName)
        {
            foreach (var child in root.GetComponentsInChildren<Transform>(true))
                if (child.name == childName)
                    return child;
            return null;
        }

        public static bool CanPlace(Vector3 requestedPosition, out Vector3 validPosition)
        {
            validPosition = requestedPosition;
            if (!NavMesh.SamplePosition(requestedPosition, out var hit, 2f, NavMesh.AllAreas))
                return false;
            validPosition = hit.position;
            var center = validPosition + Vector3.up * 0.55f;
            var overlaps = Physics.OverlapBox(center, new Vector3(0.75f, 0.48f, 0.75f), Quaternion.identity,
                ~0, QueryTriggerInteraction.Collide);
            foreach (var collider in overlaps)
            {
                if (collider.GetComponentInParent<NetworkWarrior>() != null ||
                    collider.GetComponentInParent<CoreBuilding>() != null ||
                    collider.GetComponentInParent<CoreDebugDummy>() != null ||
                    collider.GetComponentInParent<EnemyBrain>() != null ||
                    collider.GetComponentInParent<CoreDebugDeposit>() != null)
                    return false;
            }
            return true;
        }
    }
}
