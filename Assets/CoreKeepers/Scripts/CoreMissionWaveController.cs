using System;
using System.Collections.Generic;
using TMPro;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.UI;

namespace CoreKeepers
{
    public enum CoreDebugMissionSelection
    {
        Disabled = 0,
        Mission01 = 1, Mission02 = 2, Mission03 = 3, Mission04 = 4, Mission05 = 5,
        Mission06 = 6, Mission07 = 7, Mission08 = 8, Mission09 = 9, Mission10 = 10,
        Mission11 = 11, Mission12 = 12, Mission13 = 13, Mission14 = 14, Mission15 = 15,
        Mission16 = 16, Mission17 = 17, Mission18 = 18, Mission19 = 19, Mission20 = 20,
        Mission21 = 21, Mission22 = 22, Mission23 = 23, Mission24 = 24, Mission25 = 25
    }

    public enum CoreWavePhase : byte
    {
        Disabled,
        Preparation,
        ReleasingEnemies,
        Fighting,
        Completed
    }

    [RequireComponent(typeof(NetworkObject))]
    public sealed class CoreMissionWaveController : NetworkBehaviour
    {
        private const int WaveCount = CoreMissionWaveDatabase.WavesPerMission;

        [Header("Debug Mission")]
        [SerializeField] private CoreDebugMissionSelection missionToPlay = CoreDebugMissionSelection.Disabled;
        [SerializeField] private CoreMissionWaveDatabase missionDatabase;

        [Header("Timing")]
        [SerializeField, Min(0.1f)] private float preparationDuration = 60f;
        [SerializeField, Min(0.1f)] private float waveReleaseDuration = 30f;

        [Header("Global Loot Goblin")]
        [SerializeField] private GameObject lootGoblinPrefab;
        [SerializeField, Range(0f, 1f)] private float lootGoblinChance = 0.3f;
        [SerializeField, Min(0f)] private float lootGoblinSpawnDelayMin = 90f;
        [SerializeField, Min(0f)] private float lootGoblinSpawnDelayMax = 240f;
        [SerializeField, Min(1f)] private float lootGoblinSpawnRadiusMin = 6f;
        [SerializeField, Min(1f)] private float lootGoblinSpawnRadiusMax = 10f;

        [Header("Spawn Zones")]
        [SerializeField] private List<CoreEnemySpawnZone> spawnZones = new();

        [Header("Mission Progress - Preparation Start")]
        [SerializeField] private float[] preparationFillStart = { 0f, 0.114f, 0.255f, 0.399f, 0.555f, 0.705f, 0.852f };
        [Header("Mission Progress - Preparation End")]
        [SerializeField] private float[] preparationFillEnd = { 0.042f, 0.177f, 0.318f, 0.48f, 0.63f, 0.777f, 0.919f };
        [Header("Mission Progress - Wave Start")]
        [SerializeField] private float[] waveFill = { 0.114f, 0.255f, 0.399f, 0.555f, 0.705f, 0.852f, 1f };

        [Header("HUD (auto-found by name when empty)")]
        [SerializeField] private Image missionFill;
        [SerializeField] private GameObject bossIconBG;
        [SerializeField] private GameObject bossIcon;
        [SerializeField] private Text timeToNextWaveText;
        [SerializeField] private Text numberOfEnemiesText;
        [SerializeField] private Text infoText;
        [SerializeField] private Text[] waveTexts = new Text[WaveCount];
        [SerializeField] private GameObject enemyIcon;
        [SerializeField] private Color completedWaveColor = new(0.2f, 0.9f, 0.25f, 1f);

        private readonly NetworkVariable<CoreWavePhase> phase = new(CoreWavePhase.Disabled);
        private readonly NetworkVariable<int> missionNumber = new(0);
        private readonly NetworkVariable<int> currentWave = new(-1);
        private readonly NetworkVariable<int> completedWaves = new(0);
        private readonly NetworkVariable<int> enemiesRemaining = new(0);
        private readonly NetworkVariable<double> phaseStartedAt = new(0d);
        private readonly NetworkVariable<double> phaseEndsAt = new(0d);
        private readonly NetworkVariable<bool> bossMission = new(false);
        private readonly NetworkVariable<int> missionRevision = new(0);

        private readonly List<GameObject> releaseQueue = new();
        private readonly List<EnemyBrain> activeEnemies = new();
        private CoreMissionDefinition activeMission;
        private int releaseIndex;
        private int spawnZoneIndex;
        private double nextSpawnAt;
        private double releaseStartedAt;
        private bool lootGoblinPending;
        private double lootGoblinSpawnAt;
        private EnemyBrain activeLootGoblin;

        public CoreWavePhase Phase => phase.Value;
        public int CurrentWaveNumber => currentWave.Value + 1;
        public int CompletedWaves => completedWaves.Value;
        public int MissionRevision => missionRevision.Value;
        public int EnemiesRemaining => enemiesRemaining.Value;
        public static CoreMissionWaveController Instance { get; private set; }

        private void Awake()
        {
            Instance = this;
            BindHudByName();
            FindSpawnZonesWhenEmpty();
            SetHudActive(false);
        }

        public override void OnNetworkSpawn()
        {
            BindHudByName();
            if (!IsServer)
                return;
            StartMission((int)missionToPlay);
        }

        private void Update()
        {
            if (IsServer && IsSpawned)
                UpdateServerMission();
            UpdateHud();
        }

        public void StartMission(int oneBasedMissionNumber)
        {
            if (!IsServer)
                return;
            ResetRuntimeState();
            missionRevision.Value++;
            activeMission = missionDatabase != null ? missionDatabase.GetMission(oneBasedMissionNumber) : null;
            if (activeMission == null)
            {
                phase.Value = CoreWavePhase.Disabled;
                missionNumber.Value = 0;
                return;
            }

            missionNumber.Value = oneBasedMissionNumber;
            bossMission.Value = activeMission.HasBoss;
            ScheduleLootGoblin();
            BeginPreparation(0);
        }

        public override void OnDestroy()
        {
            if (Instance == this) Instance = null;
            base.OnDestroy();
        }

        private void UpdateServerMission()
        {
            var now = NetworkManager.ServerTime.Time;
            UpdateLootGoblin(now);
            switch (phase.Value)
            {
                case CoreWavePhase.Preparation:
                    if (now >= phaseEndsAt.Value)
                        BeginWave(now);
                    break;
                case CoreWavePhase.ReleasingEnemies:
                    ReleaseDueEnemies(now);
                    RefreshEnemyCount();
                    if (releaseIndex >= releaseQueue.Count)
                        phase.Value = CoreWavePhase.Fighting;
                    TryCompleteWave();
                    break;
                case CoreWavePhase.Fighting:
                    RefreshEnemyCount();
                    TryCompleteWave();
                    break;
            }
        }

        private void ScheduleLootGoblin()
        {
            lootGoblinPending = UnityEngine.Random.value < lootGoblinChance;
            if (!lootGoblinPending) return;
            var minimum = Mathf.Min(lootGoblinSpawnDelayMin, lootGoblinSpawnDelayMax);
            var maximum = Mathf.Max(lootGoblinSpawnDelayMin, lootGoblinSpawnDelayMax);
            lootGoblinSpawnAt = NetworkManager.ServerTime.Time + UnityEngine.Random.Range(minimum, maximum);
        }

        private void UpdateLootGoblin(double now)
        {
            if (!lootGoblinPending || now < lootGoblinSpawnAt || phase.Value is CoreWavePhase.Disabled or CoreWavePhase.Completed)
                return;
            if (TrySpawnLootGoblin())
                lootGoblinPending = false;
            else
                lootGoblinSpawnAt = now + 5d;
        }

        private bool TrySpawnLootGoblin()
        {
            lootGoblinPrefab ??= Resources.Load<GameObject>("Enemies/Chest");
            if (lootGoblinPrefab == null)
            {
                Debug.LogError("Global loot goblin requires Resources/Enemies/Chest.prefab.", this);
                lootGoblinPending = false;
                return false;
            }
            if (lootGoblinPrefab.GetComponent<NetworkObject>() == null ||
                !NetworkManager.NetworkConfig.Prefabs.Contains(lootGoblinPrefab))
            {
                Debug.LogError("Chest loot goblin is not registered as a NetworkPrefab.", lootGoblinPrefab);
                lootGoblinPending = false;
                return false;
            }

            var heroes = new List<NetworkWarrior>();
            foreach (var hero in FindObjectsByType<NetworkWarrior>())
                if (hero.IsSpawned && !hero.IsDowned) heroes.Add(hero);
            if (heroes.Count == 0) return false;
            var chosen = heroes[UnityEngine.Random.Range(0, heroes.Count)];
            var minimum = Mathf.Min(lootGoblinSpawnRadiusMin, lootGoblinSpawnRadiusMax);
            var maximum = Mathf.Max(lootGoblinSpawnRadiusMin, lootGoblinSpawnRadiusMax);
            for (var attempt = 0; attempt < 12; attempt++)
            {
                var direction = UnityEngine.Random.insideUnitCircle.normalized;
                var candidate = chosen.transform.position +
                    new Vector3(direction.x, 0f, direction.y) * UnityEngine.Random.Range(minimum, maximum);
                if (!NavMesh.SamplePosition(candidate, out var hit, 3f, NavMesh.AllAreas)) continue;
                var away = hit.position - chosen.transform.position;
                away.y = 0f;
                var rotation = away.sqrMagnitude > 0.001f
                    ? Quaternion.LookRotation(away.normalized)
                    : Quaternion.identity;
                var instance = Instantiate(lootGoblinPrefab, hit.position, rotation);
                instance.GetComponent<NetworkObject>().Spawn(true);
                activeLootGoblin = instance.GetComponent<EnemyBrain>();
                return activeLootGoblin != null;
            }
            return false;
        }

        private void BeginPreparation(int waveIndex)
        {
            currentWave.Value = waveIndex;
            phase.Value = CoreWavePhase.Preparation;
            phaseStartedAt.Value = NetworkManager.ServerTime.Time;
            phaseEndsAt.Value = phaseStartedAt.Value + preparationDuration;
            enemiesRemaining.Value = 0;
        }

        private void BeginWave(double now)
        {
            releaseQueue.Clear();
            activeEnemies.Clear();
            releaseIndex = 0;
            var definition = activeMission.Waves[currentWave.Value];
            foreach (var entry in definition.Enemies)
            {
                if (entry == null || entry.EnemyPrefab == null)
                    continue;
                for (var amount = 0; amount < entry.Amount; amount++)
                    releaseQueue.Add(entry.EnemyPrefab);
            }

            Shuffle(releaseQueue);
            phase.Value = CoreWavePhase.ReleasingEnemies;
            phaseStartedAt.Value = now;
            phaseEndsAt.Value = now + waveReleaseDuration;
            releaseStartedAt = now;
            nextSpawnAt = now;
            enemiesRemaining.Value = releaseQueue.Count;
            ReleaseDueEnemies(now);
            TryCompleteWave();
        }

        private void ReleaseDueEnemies(double now)
        {
            while (releaseIndex < releaseQueue.Count && now >= nextSpawnAt)
            {
                SpawnEnemy(releaseQueue[releaseIndex]);
                releaseIndex++;
                nextSpawnAt = releaseQueue.Count <= 1
                    ? releaseStartedAt + waveReleaseDuration
                    : releaseStartedAt + waveReleaseDuration * releaseIndex / (releaseQueue.Count - 1d);
            }
        }

        private void SpawnEnemy(GameObject prefab)
        {
            FindSpawnZonesWhenEmpty();
            if (spawnZones.Count == 0)
            {
                Debug.LogError("Wave cannot spawn an enemy: no CoreEnemySpawnZone exists in the scene.", this);
                return;
            }

            Vector3 position = default;
            Quaternion rotation = default;
            var foundPosition = false;
            for (var attempt = 0; attempt < spawnZones.Count; attempt++)
            {
                var zone = spawnZones[spawnZoneIndex++ % spawnZones.Count];
                if (zone != null && zone.TryGetSpawnPose(out position, out rotation))
                {
                    foundPosition = true;
                    break;
                }
            }
            if (!foundPosition)
            {
                Debug.LogWarning($"Could not find NavMesh in any spawn zone for '{prefab.name}'.", this);
                return;
            }

            var prefabNetworkObject = prefab.GetComponent<NetworkObject>();
            if (prefabNetworkObject == null || !NetworkManager.NetworkConfig.Prefabs.Contains(prefab))
            {
                Debug.LogError($"Enemy prefab '{prefab.name}' is not a registered NetworkPrefab.", prefab);
                return;
            }

            var instance = Instantiate(prefab, position, rotation);
            var networkObject = instance.GetComponent<NetworkObject>();
            networkObject.Spawn(true);
            var enemy = instance.GetComponent<EnemyBrain>();
            if (enemy != null)
                activeEnemies.Add(enemy);
        }

        private void RefreshEnemyCount()
        {
            for (var index = activeEnemies.Count - 1; index >= 0; index--)
                if (activeEnemies[index] == null || !activeEnemies[index].IsAlive)
                    activeEnemies.RemoveAt(index);
            enemiesRemaining.Value = Mathf.Max(0, releaseQueue.Count - releaseIndex) + activeEnemies.Count;
        }

        private void TryCompleteWave()
        {
            RefreshEnemyCount();
            if (releaseIndex < releaseQueue.Count || activeEnemies.Count > 0)
                return;

            completedWaves.Value = currentWave.Value + 1;
            if (completedWaves.Value >= WaveCount)
            {
                phase.Value = CoreWavePhase.Completed;
                phaseStartedAt.Value = NetworkManager.ServerTime.Time;
                phaseEndsAt.Value = phaseStartedAt.Value;
            }
            else
                BeginPreparation(currentWave.Value + 1);
        }

        private void UpdateHud()
        {
            var visible = phase.Value != CoreWavePhase.Disabled;
            SetHudActive(visible);
            if (!visible)
                return;

            var preparing = phase.Value == CoreWavePhase.Preparation;
            var fighting = phase.Value is CoreWavePhase.ReleasingEnemies or CoreWavePhase.Fighting;
            SetActive(timeToNextWaveText, preparing);
            SetActive(numberOfEnemiesText, fighting);
            SetActive(infoText, preparing || fighting);
            SetActive(enemyIcon, fighting);
            if (infoText != null && (preparing || fighting))
                infoText.text = preparing ? "Time to next wave" : "enemies left: ";
            if (timeToNextWaveText != null && preparing)
            {
                var seconds = Mathf.Max(0, Mathf.CeilToInt((float)(phaseEndsAt.Value - ServerTime)));
                timeToNextWaveText.text = $"{seconds / 60:00}:{seconds % 60:00}";
            }
            if (numberOfEnemiesText != null && fighting)
                numberOfEnemiesText.text = enemiesRemaining.Value.ToString();

            SetActive(bossIconBG, bossMission.Value);
            SetActive(bossIcon, bossMission.Value && currentWave.Value == WaveCount - 1 && fighting);
            UpdateWaveLabels();
            UpdateMissionFill();
        }

        private void UpdateWaveLabels()
        {
            for (var index = 0; index < waveTexts.Length; index++)
            {
                var label = waveTexts[index];
                if (label == null)
                    continue;
                label.gameObject.SetActive(!(bossMission.Value && index == WaveCount - 1));
                label.color = index < completedWaves.Value ? completedWaveColor : Color.white;
            }
        }

        private void UpdateMissionFill()
        {
            if (missionFill == null)
                return;
            var waveIndex = Mathf.Clamp(currentWave.Value, 0, WaveCount - 1);
            if (phase.Value == CoreWavePhase.Completed)
                missionFill.fillAmount = 1f;
            else if (phase.Value == CoreWavePhase.Preparation)
            {
                var duration = Math.Max(0.001d, phaseEndsAt.Value - phaseStartedAt.Value);
                var progress = Mathf.Clamp01((float)((ServerTime - phaseStartedAt.Value) / duration));
                missionFill.fillAmount = Mathf.Lerp(preparationFillStart[waveIndex], preparationFillEnd[waveIndex], progress);
            }
            else
                missionFill.fillAmount = waveFill[waveIndex];
        }

        private double ServerTime => NetworkManager != null && NetworkManager.IsListening
            ? NetworkManager.ServerTime.Time
            : Time.unscaledTimeAsDouble;

        private void BindHudByName()
        {
            missionFill ??= FindNamedComponent<Image>("MissionFill");
            bossIconBG ??= FindNamedObject("BossIconBG", "BosslconBG");
            bossIcon ??= FindNamedObject("BossIcon", "Bosslcon");
            timeToNextWaveText ??= FindNamedComponent<Text>("TimeToNextWaveText");
            numberOfEnemiesText ??= FindNamedComponent<Text>("NumberOfEnemiesText");
            infoText ??= FindNamedComponent<Text>("InfoText");
            enemyIcon ??= FindNamedObject("EnemyIcon", "Enemylcon");
            if (waveTexts == null || waveTexts.Length != WaveCount)
                Array.Resize(ref waveTexts, WaveCount);
            for (var index = 0; index < WaveCount; index++)
                waveTexts[index] ??= FindNamedComponent<Text>($"Wave{index + 1}Text");
        }

        private void FindSpawnZonesWhenEmpty()
        {
            spawnZones.RemoveAll(zone => zone == null);
            if (spawnZones.Count == 0)
                spawnZones.AddRange(FindObjectsByType<CoreEnemySpawnZone>());
        }

        private void SetHudActive(bool active)
        {
            SetActive(timeToNextWaveText, active && phase.Value == CoreWavePhase.Preparation);
            SetActive(numberOfEnemiesText, active && phase.Value is CoreWavePhase.ReleasingEnemies or CoreWavePhase.Fighting);
            SetActive(infoText, active);
            SetActive(enemyIcon, active && phase.Value is CoreWavePhase.ReleasingEnemies or CoreWavePhase.Fighting);
            SetActive(bossIconBG, active && bossMission.Value);
            SetActive(bossIcon, active && bossMission.Value && currentWave.Value == WaveCount - 1);
        }

        private void ResetRuntimeState()
        {
            if (activeLootGoblin != null && activeLootGoblin.IsSpawned)
                activeLootGoblin.NetworkObject.Despawn(true);
            activeLootGoblin = null;
            lootGoblinPending = false;
            releaseQueue.Clear();
            activeEnemies.Clear();
            releaseIndex = 0;
            currentWave.Value = -1;
            completedWaves.Value = 0;
            enemiesRemaining.Value = 0;
            bossMission.Value = false;
        }

        private static void Shuffle(List<GameObject> items)
        {
            for (var index = items.Count - 1; index > 0; index--)
            {
                var other = UnityEngine.Random.Range(0, index + 1);
                (items[index], items[other]) = (items[other], items[index]);
            }
        }

        private static GameObject FindNamedObject(params string[] names)
        {
            foreach (var candidate in FindObjectsByType<Transform>(FindObjectsInactive.Include))
                foreach (var name in names)
                    if (candidate.name == name)
                        return candidate.gameObject;
            return null;
        }

        private static T FindNamedComponent<T>(string name) where T : Component
        {
            var gameObject = FindNamedObject(name);
            return gameObject != null ? gameObject.GetComponent<T>() : null;
        }

        private static void SetActive(Component component, bool active)
        {
            if (component != null)
                component.gameObject.SetActive(active);
        }

        private static void SetActive(GameObject gameObject, bool active)
        {
            if (gameObject != null)
                gameObject.SetActive(active);
        }

        private void OnValidate()
        {
            EnsureSeven(ref preparationFillStart, new[] { 0f, 0.114f, 0.255f, 0.399f, 0.555f, 0.705f, 0.852f });
            EnsureSeven(ref preparationFillEnd, new[] { 0.042f, 0.177f, 0.318f, 0.48f, 0.63f, 0.777f, 0.919f });
            EnsureSeven(ref waveFill, new[] { 0.114f, 0.255f, 0.399f, 0.555f, 0.705f, 0.852f, 1f });
            if (waveTexts == null || waveTexts.Length != WaveCount)
                Array.Resize(ref waveTexts, WaveCount);
        }

        private static void EnsureSeven(ref float[] values, float[] defaults)
        {
            if (values == null || values.Length != WaveCount)
                values = defaults;
        }
    }
}
