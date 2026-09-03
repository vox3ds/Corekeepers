using System;
using UnityEngine;

namespace CoreKeepers
{
    [Serializable]
    public sealed class FireballParticleSettings
    {
        [Min(0.01f)] public float lifetimeMin = 0.1f;
        [Min(0.01f)] public float lifetimeMax = 0.3f;
        [Min(0f)] public float speedMin;
        [Min(0f)] public float speedMax = 0.2f;
        [Min(0.01f)] public float sizeMin = 0.3f;
        [Min(0.01f)] public float sizeMax = 0.8f;
        [Min(0f)] public float emissionRate = 100f;
        [Min(0)] public int burstCount = 16;
        [Min(0.001f)] public float shapeRadius = 0.01f;
        [Min(1)] public int maxParticles = 1000;
        [Range(0f, 1f)] public float startAlphaMin = 1f;
        [Range(0f, 1f)] public float startAlphaMax = 1f;
        [Min(0f)] public float noiseStrength = 0.2f;
        [Min(0f)] public float noiseFrequency = 1f;
        public float noiseScrollSpeed = 1f;
        public float verticalVelocityMin;
        public float verticalVelocityMax;
        public int sortingOrder;
    }

    [CreateAssetMenu(menuName = "Core Keepers/VFX Material Library", fileName = "CombatVfxMaterialLibrary")]
    public sealed class CombatVfxMaterialLibrary : ScriptableObject
    {
        [SerializeField] private Material explosion9x9;
        [SerializeField] private Material energyExplosion8x8;
        [SerializeField] private Material energyExplosion5x4;
        [SerializeField] private Material electric3x4;
        [SerializeField] private Material darkEnergy8x5;
        [SerializeField] private Material flame8x4;
        [SerializeField] private Material fireball8x5;
        [SerializeField] private Material smoke8x5;
        [SerializeField] private Material groundFireCrack;
        [SerializeField] private Material iceGround;
        [SerializeField] private Material meteorGround;
        [SerializeField] private Material coldWind;
        [SerializeField] private Material frostSpikesMaterial;
        [SerializeField] private GameObject frostSpikes;

        public Material Explosion9x9 => explosion9x9;
        public Material EnergyExplosion8x8 => energyExplosion8x8;
        public Material EnergyExplosion5x4 => energyExplosion5x4;
        public Material Electric3x4 => electric3x4;
        public Material DarkEnergy8x5 => darkEnergy8x5;
        public Material Flame8x4 => flame8x4;
        public Material Fireball8x5 => fireball8x5;
        public Material Smoke8x5 => smoke8x5;
        public Material GroundFireCrack => groundFireCrack;
        public Material IceGround => iceGround;
        public Material MeteorGround => meteorGround;
        public Material ColdWind => coldWind;
        public Material FrostSpikesMaterial => frostSpikesMaterial;
        public GameObject FrostSpikes => frostSpikes;
    }
}
