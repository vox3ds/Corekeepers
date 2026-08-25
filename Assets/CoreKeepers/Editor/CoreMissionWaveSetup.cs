using System.IO;
using CoreKeepers;
using Unity.Netcode;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace CoreKeepersEditor
{
    public static class CoreMissionWaveSetup
    {
        private const string DatabasePath = "Assets/CoreKeepers/Resources/Missions/CoreMissionDatabase.asset";
        private const string SpawnZonePrefabPath = "Assets/CoreKeepers/Prefabs/Waves/EnemySpawnZone.prefab";
        private const string LauncherPrefabPath = "Assets/CoreKeepers/Prefabs/Waves/DebugMissionLauncher.prefab";
        private const string DebugScenePath = "Assets/Scenes/DebugScene.unity";

        [MenuItem("Core Keepers/Configure Mission Waves")]
        public static void ConfigureMissionWaves()
        {
            var database = EnsureDatabase();
            var spawnZonePrefab = EnsureSpawnZonePrefab();
            var launcherPrefab = EnsureLauncherPrefab(database);
            var scene = SceneManager.GetActiveScene();
            if (scene.path != DebugScenePath)
            {
                Debug.Log($"Mission wave assets are ready. Open {DebugScenePath} and run this command again to place them.");
                return;
            }

            var changed = false;
            if (Object.FindAnyObjectByType<CoreMissionWaveController>() == null)
            {
                var launcher = (GameObject)PrefabUtility.InstantiatePrefab(launcherPrefab, scene);
                launcher.name = "Debug Mission Launcher (Select Mission Here)";
                changed = true;
            }

            if (Object.FindAnyObjectByType<CoreEnemySpawnZone>() == null)
            {
                var oldSpawners = Object.FindObjectsByType<CoreDebugEnemySpawner>();
                if (oldSpawners.Length > 0)
                {
                    foreach (var oldSpawner in oldSpawners)
                    {
                        var zone = (GameObject)PrefabUtility.InstantiatePrefab(spawnZonePrefab, scene);
                        zone.name = $"Enemy Spawn Zone ({oldSpawner.name})";
                        zone.transform.SetPositionAndRotation(oldSpawner.transform.position, oldSpawner.transform.rotation);
                    }
                }
                else
                {
                    CreateZone(spawnZonePrefab, scene, "Enemy Spawn Zone A", new Vector3(-10f, 0f, 0f));
                    CreateZone(spawnZonePrefab, scene, "Enemy Spawn Zone B", new Vector3(10f, 0f, 0f));
                }
                changed = true;
            }

            if (changed)
            {
                EditorSceneManager.MarkSceneDirty(scene);
                EditorSceneManager.SaveScene(scene);
                Debug.Log("Mission waves configured in DebugScene. Select the launcher and choose Disabled or Mission01-Mission25.");
            }
        }

        private static CoreMissionWaveDatabase EnsureDatabase()
        {
            EnsureFolder("Assets/CoreKeepers/Resources", "Missions");
            var database = AssetDatabase.LoadAssetAtPath<CoreMissionWaveDatabase>(DatabasePath);
            if (database != null)
                return database;
            database = ScriptableObject.CreateInstance<CoreMissionWaveDatabase>();
            database.EnsureLayout();
            AssetDatabase.CreateAsset(database, DatabasePath);
            EditorUtility.SetDirty(database);
            AssetDatabase.SaveAssets();
            return database;
        }

        private static GameObject EnsureSpawnZonePrefab()
        {
            EnsurePrefabFolder();
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(SpawnZonePrefabPath);
            if (prefab != null)
                return prefab;
            var temporary = new GameObject("Enemy Spawn Zone");
            temporary.AddComponent<CoreEnemySpawnZone>();
            prefab = PrefabUtility.SaveAsPrefabAsset(temporary, SpawnZonePrefabPath);
            Object.DestroyImmediate(temporary);
            return prefab;
        }

        private static GameObject EnsureLauncherPrefab(CoreMissionWaveDatabase database)
        {
            EnsurePrefabFolder();
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(LauncherPrefabPath);
            if (prefab != null)
                return prefab;
            var temporary = new GameObject("Debug Mission Launcher");
            temporary.AddComponent<NetworkObject>();
            var controller = temporary.AddComponent<CoreMissionWaveController>();
            var serializedController = new SerializedObject(controller);
            serializedController.FindProperty("missionDatabase").objectReferenceValue = database;
            serializedController.ApplyModifiedPropertiesWithoutUndo();
            prefab = PrefabUtility.SaveAsPrefabAsset(temporary, LauncherPrefabPath);
            Object.DestroyImmediate(temporary);
            return prefab;
        }

        private static void CreateZone(GameObject prefab, Scene scene, string name, Vector3 position)
        {
            var zone = (GameObject)PrefabUtility.InstantiatePrefab(prefab, scene);
            zone.name = name;
            zone.transform.position = position;
        }

        private static void EnsurePrefabFolder()
        {
            EnsureFolder("Assets/CoreKeepers", "Prefabs");
            EnsureFolder("Assets/CoreKeepers/Prefabs", "Waves");
        }

        private static void EnsureFolder(string parent, string child)
        {
            var path = Path.Combine(parent, child).Replace('\\', '/');
            if (!AssetDatabase.IsValidFolder(path))
                AssetDatabase.CreateFolder(parent, child);
        }
    }

    [CustomEditor(typeof(CoreMissionWaveDatabase))]
    public sealed class CoreMissionWaveDatabaseEditor : Editor
    {
        private SerializedProperty missions;

        private void OnEnable() => missions = serializedObject.FindProperty("missions");

        public override void OnInspectorGUI()
        {
            serializedObject.Update();
            EditorGUILayout.HelpBox(
                "Każda misja ma dokładnie 7 fal. Dodaj prefab przeciwnika oraz Amount w wybranej fali. " +
                "Misje 5, 10, 15, 20 i 25 są domyślnie misjami z bossem.", MessageType.Info);
            for (var missionIndex = 0; missionIndex < missions.arraySize; missionIndex++)
            {
                var mission = missions.GetArrayElementAtIndex(missionIndex);
                mission.isExpanded = EditorGUILayout.Foldout(mission.isExpanded,
                    $"Mission {missionIndex + 1}{((missionIndex + 1) % 5 == 0 ? " (Boss)" : string.Empty)}", true);
                if (!mission.isExpanded)
                    continue;
                using (new EditorGUI.IndentLevelScope())
                {
                    EditorGUILayout.PropertyField(mission.FindPropertyRelative("displayName"));
                    EditorGUILayout.PropertyField(mission.FindPropertyRelative("hasBoss"));
                    var waves = mission.FindPropertyRelative("waves");
                    for (var waveIndex = 0; waveIndex < waves.arraySize; waveIndex++)
                        EditorGUILayout.PropertyField(waves.GetArrayElementAtIndex(waveIndex),
                            new GUIContent($"Wave {waveIndex + 1}"), true);
                }
                EditorGUILayout.Space(4f);
            }
            serializedObject.ApplyModifiedProperties();
        }
    }
}
