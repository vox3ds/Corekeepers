using Unity.Netcode;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.UI;

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
        private static readonly float[] MaximumHealth = { 120f, 180f, 220f, 80f, 110f };
        private static readonly string[] Icons = { "▲", "⬢", "▰", "◇", "✦" };

        public static string Name(CoreBuildingType type) => Names[(int)type];
        public static string Icon(CoreBuildingType type) => Icons[(int)type];
        public static int Cost(CoreBuildingType type) => Costs[(int)type];
        public static MinedResourceKind BuildCurrency(CoreBuildingType type) => MinedResourceKind.Ore;
        public static int RequiredBuildPoints(CoreBuildingType type) => BuildPoints[(int)type];
        public static float MaxHealth(CoreBuildingType type) => MaximumHealth[(int)type];
        public static int UpgradeCost(CoreBuildingType type, int level) => Mathf.RoundToInt(Cost(type) * (0.75f + level * 0.5f));
        public static MinedResourceKind UpgradeCurrency(CoreBuildingType type) => MinedResourceKind.CoreShards;
        public static string ResourcePath(CoreBuildingType type) => $"Buildings/{type}";
    }

    [RequireComponent(typeof(NetworkObject))]
    public sealed class CoreBuilding : NetworkBehaviour
    {
        [SerializeField] private CoreBuildingType buildingType;
        [SerializeField] private GameObject foundationVisual;
        [SerializeField] private GameObject completedVisual;
        [SerializeField, Min(1)] private int maximumLevel = 3;

        private readonly NetworkVariable<CoreBuildingState> state = new(CoreBuildingState.UnderConstruction);
        private readonly NetworkVariable<int> constructionPoints = new(0);
        private readonly NetworkVariable<float> health = new(1f);
        private readonly NetworkVariable<byte> level = new(1);
        private readonly NetworkVariable<byte> upgradeBranch = new(0);
        private readonly GameObject[] buildProgressStages = new GameObject[5];
        private Transform buildProgressRoot;
        private Transform constructionProgressBarRoot;
        private Image constructionProgressBarFill;
        private static readonly string[] BuildProgressNames =
            { "Build0%", "Build20%", "Build40%", "Build60%", "Build80%" };

        public CoreBuildingType BuildingType => buildingType;
        public CoreBuildingState State => state.Value;
        public int ConstructionPoints => constructionPoints.Value;
        public int RequiredConstructionPoints => CoreBuildingCatalog.RequiredBuildPoints(buildingType);
        public float Health => health.Value;
        public float MaximumHealth => CoreBuildingCatalog.MaxHealth(buildingType) * (1f + (level.Value - 1) * 0.25f);
        public int Level => level.Value;
        public int MaximumLevel => maximumLevel;
        public bool CanUpgrade => state.Value != CoreBuildingState.UnderConstruction && level.Value < maximumLevel;
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
            }
            CacheBuildProgressStages();
            CacheConstructionProgressBar();
            RefreshVisuals();
        }

        private void Update() => RefreshVisuals();

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
