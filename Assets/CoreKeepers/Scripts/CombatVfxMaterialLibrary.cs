using UnityEngine;

namespace CoreKeepers
{
    [CreateAssetMenu(menuName = "Core Keepers/VFX Material Library", fileName = "CombatVfxMaterialLibrary")]
    public sealed class CombatVfxMaterialLibrary : ScriptableObject
    {
        [SerializeField] private Material explosion9x9;
        [SerializeField] private Material energyExplosion8x8;
        [SerializeField] private Material energyExplosion5x4;
        [SerializeField] private Material darkEnergy8x5;
        [SerializeField] private Material flame8x4;
        [SerializeField] private Material groundFireCrack;
        [SerializeField] private Material coldWind;
        [SerializeField] private Material frostSpikesMaterial;
        [SerializeField] private GameObject frostSpikes;

        public Material Explosion9x9 => explosion9x9;
        public Material EnergyExplosion8x8 => energyExplosion8x8;
        public Material EnergyExplosion5x4 => energyExplosion5x4;
        public Material DarkEnergy8x5 => darkEnergy8x5;
        public Material Flame8x4 => flame8x4;
        public Material GroundFireCrack => groundFireCrack;
        public Material ColdWind => coldWind;
        public Material FrostSpikesMaterial => frostSpikesMaterial;
        public GameObject FrostSpikes => frostSpikes;
    }
}
