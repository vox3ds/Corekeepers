using System.IO;
using System.Collections.Generic;
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

        private readonly struct MissionPlan
        {
            public readonly string Name;
            public readonly string[] Roster;
            public readonly string Boss;

            public MissionPlan(string name, string[] roster, string boss = null)
            {
                Name = name;
                Roster = roster;
                Boss = boss;
            }
        }

        private readonly struct SpawnPlan
        {
            public readonly string Enemy;
            public readonly int Amount;

            public SpawnPlan(string enemy, int amount)
            {
                Enemy = enemy;
                Amount = amount;
            }
        }

        private static readonly float[][] WaveShares =
        {
            new[] { 1f, 0f, 0f, 0f },
            new[] { .75f, .35f, 0f, 0f },
            new[] { .55f, .55f, .2f, 0f },
            new[] { .45f, .45f, .35f, .15f },
            new[] { .3f, .35f, .5f, .25f },
            new[] { .45f, .35f, .35f, .3f },
            new[] { .35f, .35f, .4f, .4f }
        };

        private static readonly MissionPlan[] Campaign =
        {
            // Realm 1: Cursed Wilds
            new("Wildwood Edge", R("Kobold", "Mushroom", "Mossy", "Orc")),
            new("Feral Trail", R("Kobold", "Satyr", "Werewolf", "Mossy")),
            new("Orc Warcamp", R("Kobold", "Orc", "Brute", "Satyr")),
            new("Heart of the Wilds", R("Mushroom", "Mossy", "Werewolf", "Brute")),
            new("Troll's Domain", R("Orc", "Brute", "Satyr", "Werewolf"), "Troll"),
            // Realm 2: Haunted Graveyard
            new("Restless Graves", R("Zombie", "Skeleton", "Bat_Monster", "Pumpkin_Fiend")),
            new("Hollow Crypt", R("Skeleton", "Ghoul", "Ghost", "Cursed_Doll")),
            new("Night of Bats", R("Bat_Monster", "Zombie", "Ghoul", "Pumpkin_Fiend")),
            new("Banshee's Wake", R("Ghost", "Cursed_Doll", "Ghoul", "Pumpkin_Fiend")),
            new("The Wailing Queen", R("Zombie", "Skeleton", "Ghost", "Pumpkin_Fiend"), "Banshee"),
            // Realm 3: Dark Kingdom
            new("Kingdom's Border", R("Dark_Elf", "Book", "Mummy", "Vampire")),
            new("The Fallen Keep", R("Dark_Knight", "Dark_Elf", "Book", "Mummy")),
            new("Blood Court", R("Vampire", "Mummy", "Frankenstein", "Dark_Elf")),
            new("Cyclopean Gate", R("Minotaur", "Dark_Knight", "Frankenstein", "Vampire")),
            new("The One-Eyed Throne", R("Dark_Elf", "Dark_Knight", "Minotaur", "Frankenstein"), "Cyclop"),
            // Realm 4: Sunken Marsh
            new("Marsh Edge", R("Frog", "Water_Slime", "Rat_Mutant", "Spider")),
            new("Toxic Pools", R("Poison_Slime", "Water_Slime", "Frog", "Chompfin")),
            new("Vermin Nest", R("Rat_Mutant", "Spider", "Chompfin", "Poison_Slime")),
            new("Witchwater", R("Frog", "Poison_Slime", "Spider", "Chompfin")),
            new("The Sunken Coven", R("Water_Slime", "Poison_Slime", "Rat_Mutant", "Chompfin"), "Witch"),
            // Realm 5: Elemental Rift
            new("Emberfrost", R("Fire_Elemental", "Frostbite", "Crystal", "Demon")),
            new("Tempest Spires", R("Storm_Elemental", "Stone_Golem", "Crystal", "Frostbite")),
            new("Elemental Clash", R("Fire_Elemental", "Frostbite", "Storm_Elemental", "Demon")),
            new("Warlock's Gate", R("Crystal", "Demon", "Stone_Golem", "Storm_Elemental")),
            new("The Riftmaster", R("Fire_Elemental", "Storm_Elemental", "Stone_Golem", "Demon"), "Warlock")
        };

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

        [MenuItem("Core Keepers/Populate All 25 Mission Waves")]
        public static void PopulateAllMissionWaves()
        {
            var database = EnsureDatabase();
            database.EnsureLayout();
            var serialized = new SerializedObject(database);
            var missions = serialized.FindProperty("missions");
            missions.arraySize = Campaign.Length;

            for (var missionIndex = 0; missionIndex < Campaign.Length; missionIndex++)
            {
                var plan = Campaign[missionIndex];
                var mission = missions.GetArrayElementAtIndex(missionIndex);
                mission.FindPropertyRelative("displayName").stringValue = plan.Name;
                mission.FindPropertyRelative("hasBoss").boolValue = !string.IsNullOrEmpty(plan.Boss);
                var waves = mission.FindPropertyRelative("waves");
                waves.arraySize = CoreMissionWaveDatabase.WavesPerMission;

                for (var waveIndex = 0; waveIndex < CoreMissionWaveDatabase.WavesPerMission; waveIndex++)
                {
                    var entries = BuildWave(missionIndex, waveIndex, plan);
                    var enemies = waves.GetArrayElementAtIndex(waveIndex).FindPropertyRelative("enemies");
                    enemies.arraySize = entries.Count;
                    for (var entryIndex = 0; entryIndex < entries.Count; entryIndex++)
                    {
                        var entry = enemies.GetArrayElementAtIndex(entryIndex);
                        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(
                            $"Assets/CoreKeepers/Resources/Enemies/{entries[entryIndex].Enemy}.prefab");
                        if (prefab == null)
                            throw new System.InvalidOperationException(
                                $"Campaign references missing enemy prefab '{entries[entryIndex].Enemy}'.");
                        entry.FindPropertyRelative("enemyPrefab").objectReferenceValue = prefab;
                        entry.FindPropertyRelative("amount").intValue = entries[entryIndex].Amount;
                    }
                }
            }

            serialized.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(database);
            AssetDatabase.SaveAssets();
            Debug.Log("Populated 25 missions with 7 balanced waves each (175 waves total).", database);
        }

        public static void PopulateAllMissionWavesBatch()
        {
            PopulateAllMissionWaves();
            EditorApplication.Exit(0);
        }

        private static List<SpawnPlan> BuildWave(int missionIndex, int waveIndex, MissionPlan mission)
        {
            var result = new List<SpawnPlan>();
            var pressure = 4f + missionIndex * .45f + waveIndex * 1.75f;
            for (var slot = 0; slot < mission.Roster.Length; slot++)
            {
                var share = WaveShares[waveIndex][slot];
                if (share <= 0f) continue;
                var amount = Mathf.Max(1, Mathf.RoundToInt(pressure * share / EnemyCost(mission.Roster[slot])));
                result.Add(new SpawnPlan(mission.Roster[slot], amount));
            }
            if (waveIndex == CoreMissionWaveDatabase.WavesPerMission - 1 && !string.IsNullOrEmpty(mission.Boss))
                result.Add(new SpawnPlan(mission.Boss, 1));
            return result;
        }

        private static float EnemyCost(string enemy) => enemy switch
        {
            "Bat_Monster" or "Frog" or "Mushroom" or "Kobold" or "Book" or "Cursed_Doll" or "Spider" => 1f,
            "Ghost" or "Poison_Slime" or "Dark_Elf" or "Chompfin" or "Ghoul" or "Water_Slime" or
                "Zombie" or "Witch" or "Vampire" or "Storm_Elemental" or "Rat_Mutant" or "Mummy" => 1.5f,
            "Fire_Elemental" or "Orc" or "Mossy" or "Frostbite" or "Banshee" or "Satyr" or
                "Warlock" or "Werewolf" or "Demon" => 2f,
            "Pumpkin_Fiend" or "Brute" or "Dark_Knight" => 2.75f,
            "Cyclop" or "Frankenstein" or "Troll" or "Minotaur" => 3.5f,
            "Stone_Golem" => 4.5f,
            _ => 2f
        };

        private static string[] R(params string[] enemies) => enemies;

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
