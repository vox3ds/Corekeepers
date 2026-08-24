using Unity.Netcode;
using UnityEngine;

namespace CoreKeepers
{
    public sealed class CoreDebugDummy : NetworkBehaviour
    {
        [SerializeField, Min(1f)] private float maximumHealth = 100f;
        [SerializeField, Min(0.1f)] private float resetDelay = 2f;
        private readonly NetworkVariable<float> health = new(100f);
        private double resetAt;
        private Renderer cachedRenderer;

        public float Health => health.Value;

        private void Awake() => cachedRenderer = GetComponentInChildren<Renderer>();

        public override void OnNetworkSpawn()
        {
            if (IsServer)
                health.Value = maximumHealth;
        }

        private void Update()
        {
            if (cachedRenderer != null)
            {
                var ratio = Mathf.Clamp01(health.Value / maximumHealth);
                cachedRenderer.material.color = Color.Lerp(new Color(0.25f, 0.05f, 0.05f), new Color(0.9f, 0.25f, 0.12f), ratio);
                transform.localScale = Vector3.one * Mathf.Lerp(0.65f, 1f, ratio);
            }
            if (IsServer && health.Value <= 0f && NetworkManager.ServerTime.Time >= resetAt)
                health.Value = maximumHealth;
        }

        public void TakeDamage(float amount)
        {
            if (!IsServer || health.Value <= 0f)
                return;
            health.Value = Mathf.Max(0f, health.Value - Mathf.Max(0f, amount));
            if (health.Value <= 0f)
                resetAt = NetworkManager.ServerTime.Time + resetDelay;
        }
    }
}
