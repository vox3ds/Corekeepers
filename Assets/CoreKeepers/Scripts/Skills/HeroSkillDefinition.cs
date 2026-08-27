using UnityEngine;

namespace CoreKeepers
{
    public enum HeroSkillType : byte { Basic, Active, Passive }

    public enum HeroSkillTargeting : byte
    {
        Enemy,
        Self,
        Ground
    }

    public enum HeroSkillEffect : byte
    {
        Passive,
        MeleeArc,
        RadialDamage,
        ShieldBash,
        Charge,
        Taunt,
        SelfBuff,
        SingleProjectile,
        ExplodingProjectile,
        RadialDebuff,
        ChainDamage,
        Blink,
        GroundImpact,
        Vortex,
        RepairPulse,
        ConstructionAura,
        BuildingBuff,
        HealingArea,
        HolyPulse,
        CleanseWard,
        CoreMend,
        Sanctuary,
        DivineIntervention
    }

    // Static, designer-owned data. Runtime cooldowns and acquired passives live on HeroSkillController.
    [CreateAssetMenu(menuName = "Core Keepers/Hero Skill", fileName = "HeroSkill")]
    public sealed class HeroSkillDefinition : ScriptableObject
    {
        [SerializeField] private int stableId;
        [SerializeField] private string displayName;
        [SerializeField] private CorePlayerClass heroClass;
        [SerializeField] private HeroSkillType skillType;
        [SerializeField, Range(0, 6)] private int unlockWave;
        [SerializeField, Min(0f)] private float cooldown;
        [SerializeField] private Sprite icon64;
        [SerializeField] private Sprite icon256;
        [SerializeField, TextArea] private string description;
        [SerializeField] private HeroSkillTargeting targeting;
        [SerializeField] private HeroSkillEffect effect;
        [SerializeField] private EnemyDebuff debuff;
        [SerializeField] private float power = 25f;
        [SerializeField] private float radius = 3f;
        [SerializeField] private float duration = 3f;
        [SerializeField] private float secondaryValue;
        [SerializeField] private int count = 1;

        public int StableId => stableId;
        public string DisplayName => displayName;
        public CorePlayerClass HeroClass => heroClass;
        public HeroSkillType SkillType => skillType;
        public int UnlockWave => unlockWave;
        public float Cooldown => cooldown;
        public Sprite Icon64 => icon64;
        public Sprite Icon256 => icon256;
        public string Description => description;
        public HeroSkillTargeting Targeting => targeting;
        public HeroSkillEffect Effect => effect;
        public EnemyDebuff Debuff => debuff;
        public float Power => power;
        public float Radius => radius;
        public float Duration => duration;
        public float SecondaryValue => secondaryValue;
        public int Count => count;

#if UNITY_EDITOR
        public void Configure(int id, string skillName, CorePlayerClass requestedClass, HeroSkillType type,
            int wave, float requestedCooldown, Sprite smallIcon, Sprite largeIcon, string skillDescription,
            HeroSkillTargeting requestedTargeting, HeroSkillEffect requestedEffect, float requestedPower,
            float requestedRadius, float requestedDuration, float requestedSecondaryValue, int requestedCount,
            EnemyDebuff requestedDebuff = EnemyDebuff.None)
        {
            stableId = id;
            displayName = skillName;
            heroClass = requestedClass;
            skillType = type;
            unlockWave = wave;
            cooldown = requestedCooldown;
            icon64 = smallIcon;
            icon256 = largeIcon;
            description = skillDescription;
            targeting = requestedTargeting;
            effect = requestedEffect;
            power = requestedPower;
            radius = requestedRadius;
            duration = requestedDuration;
            secondaryValue = requestedSecondaryValue;
            count = requestedCount;
            debuff = requestedDebuff;
        }
#endif
    }
}
