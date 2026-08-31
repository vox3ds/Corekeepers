using Unity.Netcode;
using Unity.Netcode.Transports.UTP;
using UnityEngine;
using Unity.AI.Navigation;
using UnityEngine.AI;

namespace CoreKeepers
{
    public sealed class CoreDebugSceneBootstrap : MonoBehaviour
    {
        [SerializeField] private GameObject playerPrefab;
        [SerializeField, Min(0)] private int debugPort;

        private void Start()
        {
            CoreHeroPanel.AttachToScenePanel();
            CoreStatusPanel.AttachToScenePanel();
            HeroSkillsUI.AttachToPreparedPanel();
            SkillUpgradePopupUI.AttachToPreparedPopup();
            CoreVfxDebugLab.EnsureExists();
            EnsureNavigation();

            if (NetworkManager.Singleton != null && NetworkManager.Singleton.IsListening)
                return;

            var manager = NetworkManager.Singleton;
            if (manager == null)
            {
                var systems = new GameObject("Direct Debug Network");
                var transport = systems.AddComponent<UnityTransport>();
                manager = systems.AddComponent<NetworkManager>();
                manager.NetworkConfig ??= new NetworkConfig();
                manager.NetworkConfig.NetworkTransport = transport;
            }

            if (playerPrefab == null)
            {
                Debug.LogError("Direct Debug Scene host cannot start: Warrior player prefab is missing.", this);
                return;
            }

            manager.NetworkConfig.PlayerPrefab = playerPrefab;
            manager.NetworkConfig.EnableSceneManagement = true;

            // A direct DebugScene host does not need a predictable public port. Port 0 asks
            // the operating system for a free ephemeral port and avoids collisions with a
            // running game/editor instance. Set debugPort in the inspector only when an
            // external client needs to connect to this particular debug host.
            if (manager.NetworkConfig.NetworkTransport is UnityTransport debugTransport)
            {
                var port = (ushort)Mathf.Clamp(debugPort, 0, ushort.MaxValue);
                debugTransport.SetConnectionData(true, "127.0.0.1", port, "127.0.0.1");
            }

            if (!manager.NetworkConfig.Prefabs.Contains(playerPrefab))
                manager.NetworkConfig.Prefabs.Add(new NetworkPrefab { Prefab = playerPrefab });
            RegisterClassPrefab(manager, "CoreMage");
            RegisterClassPrefab(manager, "CoreBuilder");
            RegisterClassPrefab(manager, "CoreHealer");
            foreach (var buildingPrefab in Resources.LoadAll<GameObject>("Buildings"))
                if (!manager.NetworkConfig.Prefabs.Contains(buildingPrefab))
                    manager.NetworkConfig.Prefabs.Add(new NetworkPrefab { Prefab = buildingPrefab });
            foreach (var enemyPrefab in Resources.LoadAll<GameObject>("Enemies"))
                if (!manager.NetworkConfig.Prefabs.Contains(enemyPrefab))
                    manager.NetworkConfig.Prefabs.Add(new NetworkPrefab { Prefab = enemyPrefab });
            foreach (var projectilePrefab in Resources.LoadAll<GameObject>("EnemyProjectiles"))
                if (!manager.NetworkConfig.Prefabs.Contains(projectilePrefab))
                    manager.NetworkConfig.Prefabs.Add(new NetworkPrefab { Prefab = projectilePrefab });
            var lootPrefab = Resources.Load<GameObject>("CoreLootPickup");
            if (lootPrefab != null && !manager.NetworkConfig.Prefabs.Contains(lootPrefab))
                manager.NetworkConfig.Prefabs.Add(new NetworkPrefab { Prefab = lootPrefab });

            CoreLaunchContext.Set(CoreLaunchMode.DebugHost);
            if (!manager.StartHost())
                Debug.LogError("Direct Debug Scene host failed to start.", this);
        }

        private static void EnsureNavigation()
        {
            var surface = FindFirstObjectByType<NavMeshSurface>();
            if (surface == null)
            {
                var navigation = new GameObject("Core Navigation");
                surface = navigation.AddComponent<NavMeshSurface>();
                surface.collectObjects = CollectObjects.All;
                surface.useGeometry = NavMeshCollectGeometry.PhysicsColliders;
                surface.layerMask = ~0;
            }

            if (surface.navMeshData == null)
                surface.BuildNavMesh();
        }

        private static void RegisterClassPrefab(NetworkManager manager, string resourcePath)
        {
            var prefab = Resources.Load<GameObject>(resourcePath);
            if (prefab != null)
            {
                if (!manager.NetworkConfig.Prefabs.Contains(prefab))
                    manager.NetworkConfig.Prefabs.Add(new NetworkPrefab { Prefab = prefab });
            }
            else
                Debug.LogError($"Debug player prefab Resources/{resourcePath} is missing.");
        }
    }
}
