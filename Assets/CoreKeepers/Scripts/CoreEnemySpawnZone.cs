using UnityEngine;
using UnityEngine.AI;

namespace CoreKeepers
{
    public sealed class CoreEnemySpawnZone : MonoBehaviour
    {
        [SerializeField, Min(0f)] private float radius = 2.5f;
        [SerializeField, Min(0.1f)] private float navMeshSampleRadius = 3f;
        [SerializeField] private bool faceCoreOnSpawn = true;

        public bool TryGetSpawnPose(out Vector3 position, out Quaternion rotation)
        {
            var point = Random.insideUnitCircle * radius;
            var candidate = transform.position + new Vector3(point.x, 0f, point.y);
            if (!NavMesh.SamplePosition(candidate, out var hit, navMeshSampleRadius, NavMesh.AllAreas))
            {
                position = default;
                rotation = default;
                return false;
            }

            position = hit.position;
            rotation = transform.rotation;
            if (!faceCoreOnSpawn)
                return true;

            var core = FindAnyObjectByType<CoreDebugDeposit>();
            if (core == null)
                return true;
            var direction = core.transform.position - position;
            direction.y = 0f;
            if (direction.sqrMagnitude > 0.001f)
                rotation = Quaternion.LookRotation(direction.normalized);
            return true;
        }

        private void OnDrawGizmos()
        {
            Gizmos.color = new Color(0.95f, 0.18f, 0.08f, 0.85f);
            Gizmos.DrawWireSphere(transform.position, radius);
            Gizmos.DrawLine(transform.position, transform.position + transform.forward * Mathf.Max(1f, radius));
        }
    }
}
