using System;
using System.Linq;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

namespace CoreKeepers
{
    public sealed class CoreDebugEnemySpawner : MonoBehaviour
    {
        [SerializeField, HideInInspector] private int enemyIndex;
        [SerializeField, Min(0.1f)] private float navMeshSampleRadius = 3f;
        [SerializeField] private bool faceCoreOnSpawn = true;

        private GameObject[] enemyPrefabs = Array.Empty<GameObject>();

        public int EnemyIndex => enemyIndex;
        public string SelectedEnemyName => enemyPrefabs.Length == 0
            ? "No enemy prefabs"
            : enemyPrefabs[Mathf.Clamp(enemyIndex, 0, enemyPrefabs.Length - 1)].name;

        private void Awake()
        {
            ReloadEnemyPrefabs();
        }

        private void Update()
        {
            if (SceneManager.GetActiveScene().name != CoreSessionManager.DebugSceneName)
                return;
            var keyboard = Keyboard.current;
            if (keyboard == null)
                return;

            if (keyboard.equalsKey.wasPressedThisFrame)
                SelectRelative(1);
            if (keyboard.minusKey.wasPressedThisFrame)
                SelectRelative(-1);
            if (keyboard.vKey.wasPressedThisFrame)
                SpawnSelected();
        }

        public void SetEnemyIndex(int value)
        {
            ReloadEnemyPrefabs();
            if (enemyPrefabs.Length == 0)
            {
                enemyIndex = 0;
                return;
            }
            enemyIndex = Mathf.Clamp(value, 0, enemyPrefabs.Length - 1);
        }

        public void SelectRelative(int direction)
        {
            ReloadEnemyPrefabs();
            if (enemyPrefabs.Length == 0)
                return;
            enemyIndex = (enemyIndex + direction) % enemyPrefabs.Length;
            if (enemyIndex < 0) enemyIndex += enemyPrefabs.Length;
            Debug.Log($"Debug enemy selected: {SelectedEnemyName}", this);
        }

        public bool SpawnSelected()
        {
            ReloadEnemyPrefabs();
            if (enemyPrefabs.Length == 0)
            {
                Debug.LogWarning("Debug spawner found no prefabs in Resources/Enemies.", this);
                return false;
            }

            var manager = NetworkManager.Singleton;
            if (manager == null || !manager.IsListening || !manager.IsServer)
            {
                Debug.LogWarning("Only the active host/server can use the debug enemy spawner.", this);
                return false;
            }

            var prefab = enemyPrefabs[enemyIndex];
            var prefabNetworkObject = prefab.GetComponent<NetworkObject>();
            if (prefabNetworkObject == null)
            {
                Debug.LogError($"Enemy prefab '{prefab.name}' has no NetworkObject. Run Core Keepers/Configure Enemy Prefabs.", prefab);
                return false;
            }
            if (!manager.NetworkConfig.Prefabs.Contains(prefab))
            {
                Debug.LogError($"Enemy prefab '{prefab.name}' is not registered in NetworkPrefabs.", prefab);
                return false;
            }

            var spawnPosition = transform.position;
            if (NavMesh.SamplePosition(spawnPosition, out var hit, navMeshSampleRadius, NavMesh.AllAreas))
                spawnPosition = hit.position;
            else
            {
                Debug.LogWarning($"No NavMesh found within {navMeshSampleRadius:0.0}m of the debug spawner.", this);
                return false;
            }

            var spawnRotation = transform.rotation;
            if (faceCoreOnSpawn)
            {
                var core = FindFirstObjectByType<CoreDebugDeposit>();
                if (core != null)
                {
                    var direction = core.transform.position - spawnPosition;
                    direction.y = 0f;
                    if (direction.sqrMagnitude > 0.001f)
                        spawnRotation = Quaternion.LookRotation(direction.normalized);
                }
            }

            var instance = Instantiate(prefab, spawnPosition, spawnRotation);
            var networkObject = instance.GetComponent<NetworkObject>();
            networkObject.Spawn(true);
            Debug.Log($"Spawned debug enemy: {prefab.name}", instance);
            return true;
        }

        private void ReloadEnemyPrefabs()
        {
            enemyPrefabs = Resources.LoadAll<GameObject>("Enemies")
                .OrderBy(prefab => prefab.name, StringComparer.Ordinal)
                .ToArray();
            enemyIndex = enemyPrefabs.Length == 0 ? 0 : Mathf.Clamp(enemyIndex, 0, enemyPrefabs.Length - 1);
        }

        private void OnDrawGizmos()
        {
            Gizmos.color = new Color(0.9f, 0.15f, 0.12f, 0.9f);
            Gizmos.DrawWireSphere(transform.position + Vector3.up * 0.55f, 0.65f);
            Gizmos.DrawLine(transform.position, transform.position + transform.forward * 1.5f);
        }
    }
}
