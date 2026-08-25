using System;
using System.Collections.Generic;
using UnityEngine;

namespace CoreKeepers
{
    [Serializable]
    public sealed class CoreWaveEnemyEntry
    {
        [SerializeField] private GameObject enemyPrefab;
        [SerializeField, Min(0)] private int amount = 1;

        public GameObject EnemyPrefab => enemyPrefab;
        public int Amount => Mathf.Max(0, amount);
    }

    [Serializable]
    public sealed class CoreWaveDefinition
    {
        [SerializeField] private List<CoreWaveEnemyEntry> enemies = new();

        public IReadOnlyList<CoreWaveEnemyEntry> Enemies => enemies;
        public int TotalEnemies
        {
            get
            {
                var total = 0;
                foreach (var entry in enemies)
                    if (entry != null && entry.EnemyPrefab != null)
                        total += entry.Amount;
                return total;
            }
        }
    }

    [Serializable]
    public sealed class CoreMissionDefinition
    {
        [SerializeField] private string displayName;
        [SerializeField] private bool hasBoss;
        [SerializeField] private CoreWaveDefinition[] waves = new CoreWaveDefinition[CoreMissionWaveDatabase.WavesPerMission];

        public string DisplayName => displayName;
        public bool HasBoss => hasBoss;
        public IReadOnlyList<CoreWaveDefinition> Waves => waves;

        internal void EnsureLayout(int missionIndex)
        {
            if (string.IsNullOrWhiteSpace(displayName))
                displayName = $"Mission {missionIndex + 1}";
            if ((missionIndex + 1) % 5 == 0)
                hasBoss = true;
            if (waves == null || waves.Length != CoreMissionWaveDatabase.WavesPerMission)
                Array.Resize(ref waves, CoreMissionWaveDatabase.WavesPerMission);
            for (var index = 0; index < waves.Length; index++)
                waves[index] ??= new CoreWaveDefinition();
        }
    }

    [CreateAssetMenu(fileName = "CoreMissionDatabase", menuName = "Core Keepers/Mission Wave Database")]
    public sealed class CoreMissionWaveDatabase : ScriptableObject
    {
        public const int MissionCount = 25;
        public const int WavesPerMission = 7;

        [SerializeField] private CoreMissionDefinition[] missions = new CoreMissionDefinition[MissionCount];

        public IReadOnlyList<CoreMissionDefinition> Missions => missions;

        public CoreMissionDefinition GetMission(int oneBasedMissionNumber)
        {
            if (missions == null || oneBasedMissionNumber < 1 || oneBasedMissionNumber > missions.Length)
                return null;
            return missions[oneBasedMissionNumber - 1];
        }

        public void EnsureLayout()
        {
            if (missions == null || missions.Length != MissionCount)
                Array.Resize(ref missions, MissionCount);
            for (var index = 0; index < missions.Length; index++)
            {
                missions[index] ??= new CoreMissionDefinition();
                missions[index].EnsureLayout(index);
            }
        }

        private void OnValidate() => EnsureLayout();
    }
}
