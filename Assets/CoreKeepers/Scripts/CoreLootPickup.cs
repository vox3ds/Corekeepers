using Unity.Netcode;
using UnityEngine;

namespace CoreKeepers
{
    [RequireComponent(typeof(NetworkObject))]
    public sealed class CoreLootPickup : NetworkBehaviour
    {
        private const string ResourcePath = "CoreLootPickup";

        [SerializeField] private Renderer visualRenderer;
        [SerializeField] private Material oreMaterial;
        [SerializeField] private Material coreShardsMaterial;
        [SerializeField, Min(0.1f)] private float pickupRange = 1f;
        [SerializeField, Min(0f)] private float hoverHeight = 0.65f;
        [SerializeField, Min(0f)] private float hoverAmplitude = 0.12f;
        [SerializeField, Min(0f)] private float hoverSpeed = 2.2f;

        private readonly NetworkVariable<MinedResourceKind> resourceKind = new(MinedResourceKind.Ore);
        private readonly NetworkVariable<int> amount = new(1);
        private Vector3 visualBasePosition;

        public MinedResourceKind ResourceKind => resourceKind.Value;
        public int Amount => amount.Value;

        private void Awake()
        {
            if (visualRenderer != null)
                visualBasePosition = visualRenderer.transform.localPosition;
        }

        public override void OnNetworkSpawn()
        {
            resourceKind.OnValueChanged += OnKindChanged;
            RefreshVisual();
        }

        public override void OnNetworkDespawn()
        {
            resourceKind.OnValueChanged -= OnKindChanged;
        }

        private void Update()
        {
            if (visualRenderer != null)
            {
                visualRenderer.transform.localPosition = visualBasePosition + Vector3.up *
                    (hoverHeight + Mathf.Sin(Time.time * hoverSpeed + NetworkObjectId * 0.17f) * hoverAmplitude);
                var camera = Camera.main;
                if (camera != null)
                    visualRenderer.transform.rotation = Quaternion.LookRotation(camera.transform.forward, Vector3.up);
            }

            if (!IsServer || amount.Value <= 0)
                return;
            foreach (var hero in FindObjectsByType<NetworkWarrior>())
            {
                if (!hero.IsSpawned || hero.IsDowned ||
                    (hero.transform.position - transform.position).sqrMagnitude > pickupRange * pickupRange)
                    continue;
                var collected = hero.TryCollectLoot(resourceKind.Value, amount.Value);
                if (collected <= 0) continue;
                amount.Value -= collected;
                if (amount.Value <= 0 && NetworkObject.IsSpawned)
                    NetworkObject.Despawn(true);
                break;
            }
        }

        public static CoreLootPickup Spawn(MinedResourceKind kind, int value, Vector3 position)
        {
            if (value <= 0 || NetworkManager.Singleton == null || !NetworkManager.Singleton.IsServer)
                return null;
            var prefab = Resources.Load<GameObject>(ResourcePath);
            if (prefab == null)
            {
                Debug.LogError("Resources/CoreLootPickup prefab is missing. Run Core Keepers/Configure Loot Prefab.");
                return null;
            }
            var instance = Instantiate(prefab, position, Quaternion.identity);
            var pickup = instance.GetComponent<CoreLootPickup>();
            instance.GetComponent<NetworkObject>().Spawn(true);
            pickup.resourceKind.Value = kind;
            pickup.amount.Value = value;
            return pickup;
        }

        private void OnKindChanged(MinedResourceKind previous, MinedResourceKind current) => RefreshVisual();

        private void RefreshVisual()
        {
            if (visualRenderer != null)
                visualRenderer.sharedMaterial = resourceKind.Value == MinedResourceKind.Ore
                    ? oreMaterial
                    : coreShardsMaterial;
        }
    }
}
