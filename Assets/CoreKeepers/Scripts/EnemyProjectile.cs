using Unity.Netcode;
using UnityEngine;

namespace CoreKeepers
{
    public enum EnemyProjectileFlightMode
    {
        Homing,
        Straight
    }

    [RequireComponent(typeof(NetworkObject))]
    public sealed class EnemyProjectile : NetworkBehaviour
    {
        [Header("Flight")]
        [SerializeField] private EnemyProjectileFlightMode flightMode = EnemyProjectileFlightMode.Homing;
        [SerializeField, Min(0.1f)] private float speed = 9f;
        [SerializeField, Min(0.01f)] private float hitRadius = 0.28f;
        [SerializeField, Min(0.1f)] private float lifetime = 5f;
        [Tooltip("How quickly a Homing projectile turns toward its moving target.")]
        [SerializeField, Range(0f, 20f)] private float homing = 8f;

        private NetworkObject currentTarget;
        private Vector3 flightDirection = Vector3.forward;
        private float projectileDamage;
        private double expiresAt;
        private bool initialized;

        public override void OnNetworkSpawn()
        {
            if (IsServer)
                expiresAt = NetworkManager.ServerTime.Time + lifetime;
        }

        public void Initialize(NetworkObject target, float damage)
        {
            if (!IsServer || target == null || !target.IsSpawned)
                return;
            currentTarget = target;
            var direction = target.transform.position - transform.position;
            direction.y += 0.65f;
            flightDirection = direction.sqrMagnitude > 0.001f ? direction.normalized : transform.forward;
            projectileDamage = Mathf.Max(0f, damage);
            initialized = true;
        }

        private void Update()
        {
            if (!IsServer)
                return;
            if (NetworkManager.ServerTime.Time >= expiresAt)
            {
                Despawn();
                return;
            }
            if (!initialized || !IsDamageable(currentTarget))
            {
                Move(flightDirection);
                return;
            }

            var targetPoint = currentTarget.transform.position + Vector3.up * 0.65f;
            var offset = targetPoint - transform.position;
            var distance = offset.magnitude;
            var desiredDirection = distance > 0.001f ? offset / distance : flightDirection;
            var direction = flightDirection;
            if (flightMode == EnemyProjectileFlightMode.Homing)
            {
                var turn = 1f - Mathf.Exp(-homing * Time.deltaTime);
                direction = Vector3.Slerp(flightDirection, desiredDirection, turn).normalized;
                flightDirection = direction;
            }
            var nextPosition = transform.position + direction * speed * Time.deltaTime;
            if (SegmentPassesTarget(transform.position, nextPosition, targetPoint, hitRadius))
            {
                ApplyDamage(currentTarget, projectileDamage);
                Despawn();
                return;
            }
            Move(direction);
        }

        private static bool SegmentPassesTarget(Vector3 start, Vector3 end, Vector3 target, float radius)
        {
            var segment = end - start;
            var lengthSquared = segment.sqrMagnitude;
            var progress = lengthSquared > 0.0001f
                ? Mathf.Clamp01(Vector3.Dot(target - start, segment) / lengthSquared)
                : 0f;
            var closestPoint = start + segment * progress;
            return (target - closestPoint).sqrMagnitude <= radius * radius;
        }

        private void Move(Vector3 direction)
        {
            if (direction.sqrMagnitude < 0.001f) return;
            transform.position += direction.normalized * speed * Time.deltaTime;
            transform.rotation = Quaternion.LookRotation(direction.normalized);
        }

        private void Despawn()
        {
            if (NetworkObject.IsSpawned) NetworkObject.Despawn(true);
        }

        private static bool IsDamageable(NetworkObject target)
        {
            if (target == null || !target.IsSpawned) return false;
            var hero = target.GetComponent<NetworkWarrior>();
            if (hero != null) return !hero.IsDowned;
            var building = target.GetComponent<CoreBuilding>();
            if (building != null) return building.Health > 0f;
            var core = target.GetComponent<CoreDebugDeposit>();
            return core != null && core.CurrentHealth > 0f;
        }

        private static void ApplyDamage(NetworkObject target, float damage)
        {
            var hero = target.GetComponent<NetworkWarrior>();
            if (hero != null) { hero.TakeDamage(damage); return; }
            var building = target.GetComponent<CoreBuilding>();
            if (building != null) { building.Damage(damage); return; }
            target.GetComponent<CoreDebugDeposit>()?.Damage(damage);
        }
    }
}
