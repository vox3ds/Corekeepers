using Unity.Netcode;
using UnityEngine;

namespace CoreKeepers
{
    public sealed class CoreDebugDeposit : NetworkBehaviour
    {
        [SerializeField, Min(1f)] private float configuredMaximumHealth = 1500f;
        [Header("Debug Starting Resources")]
        [SerializeField, Min(0)] private int startingOre = 1000;
        [SerializeField, Min(0)] private int startingCoreShards = 1000;
        private readonly NetworkVariable<int> depositedOre = new(0);
        private readonly NetworkVariable<int> depositedCoreShards = new(0);
        private readonly NetworkVariable<byte> level = new(1);
        private readonly NetworkVariable<byte> branch = new(0);
        private readonly NetworkVariable<float> currentHealth = new(1000f);
        private readonly NetworkVariable<float> maximumHealth = new(1000f);
        private float damageResistance;
        private double damageResistanceEndsAt;
        public static CoreDebugDeposit Instance { get; private set; }
        public int DepositedResources => depositedOre.Value + depositedCoreShards.Value;
        public int DepositedOre => depositedOre.Value;
        public int DepositedCoreShards => depositedCoreShards.Value;
        public int Level => level.Value;
        public float CurrentHealth => currentHealth.Value;
        public float MaximumHealth => maximumHealth.Value;
        public bool IsInDanger => maximumHealth.Value > 0f && currentHealth.Value / maximumHealth.Value <= 0.25f;
        public bool CanUpgrade => level.Value < 3;
        public int UpgradeCost => 40 + (level.Value - 1) * 35;
        public MinedResourceKind UpgradeCurrency => MinedResourceKind.CoreShards;

        private void Awake() => Instance = this;

        public override void OnNetworkSpawn()
        {
            if (!IsServer)
                return;
            depositedOre.Value = startingOre;
            depositedCoreShards.Value = startingCoreShards;
            maximumHealth.Value = Mathf.Max(1500f, configuredMaximumHealth);
            currentHealth.Value = maximumHealth.Value;
        }

        public override void OnDestroy()
        {
            if (Instance == this)
                Instance = null;
            base.OnDestroy();
        }

        public void Deposit(int ore, int coreShards)
        {
            if (IsServer)
            {
                depositedOre.Value += Mathf.Max(0, ore);
                depositedCoreShards.Value += Mathf.Max(0, coreShards);
            }
        }

        public int GetAmount(MinedResourceKind resourceKind) => resourceKind == MinedResourceKind.Ore
            ? depositedOre.Value
            : depositedCoreShards.Value;

        public bool CanAfford(MinedResourceKind resourceKind, int amount) =>
            amount >= 0 && GetAmount(resourceKind) >= amount;

        public bool TrySpend(MinedResourceKind resourceKind, int amount)
        {
            if (!IsServer || !CanAfford(resourceKind, amount))
                return false;
            if (resourceKind == MinedResourceKind.Ore)
                depositedOre.Value -= amount;
            else
                depositedCoreShards.Value -= amount;
            return true;
        }

        public bool TryUpgrade(byte selectedBranch)
        {
            if (!IsServer || !CanUpgrade || !TrySpend(UpgradeCurrency, UpgradeCost))
                return false;
            branch.Value = selectedBranch;
            level.Value++;
            return true;
        }

        public void Damage(float amount)
        {
            if (!IsServer || amount <= 0f || currentHealth.Value <= 0f)
                return;
            if (NetworkManager.ServerTime.Time >= damageResistanceEndsAt) damageResistance = 0f;
            amount *= 1f - Mathf.Clamp01(damageResistance);
            currentHealth.Value = Mathf.Max(0f, currentHealth.Value - amount);
        }

        public void Heal(float amount)
        {
            if (!IsServer || amount <= 0f || currentHealth.Value <= 0f) return;
            currentHealth.Value = Mathf.Min(maximumHealth.Value, currentHealth.Value + amount);
        }

        public void ApplyDamageResistance(float resistance, float duration)
        {
            if (!IsServer || duration <= 0f) return;
            damageResistance = Mathf.Max(damageResistance, Mathf.Clamp01(resistance));
            damageResistanceEndsAt = System.Math.Max(damageResistanceEndsAt,
                NetworkManager.ServerTime.Time + duration);
        }

        private void Update()
        {
            var pulse = 1f + Mathf.Sin(Time.time * 2.2f) * 0.04f;
            transform.localScale = Vector3.one * pulse;
        }
    }
}
