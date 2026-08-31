using Unity.Netcode;
using UnityEngine;

namespace CoreKeepers
{
    [RequireComponent(typeof(CoreBuilding), typeof(Collider))]
    public sealed class CoreTrapPlate : NetworkBehaviour
    {
        [SerializeField, Min(0f)] private float damage = 50f;
        private bool consumed;
        private CoreBuilding building;

        private void Awake() => building = GetComponent<CoreBuilding>();

        private void OnTriggerEnter(Collider other)
        {
            if (!IsServer || consumed || building.State == CoreBuildingState.UnderConstruction) return;
            var enemy = other.GetComponentInParent<EnemyBrain>();
            if (enemy == null || !enemy.IsAlive) return;
            consumed = true;
            enemy.TakeDamage(damage);
            if (NetworkObject.IsSpawned) NetworkObject.Despawn(true);
        }
    }
}
