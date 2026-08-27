using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace CoreKeepers.Editor
{
    [InitializeOnLoad]
    public static class HeroSkillsProjectSetup
    {
        private const string DefinitionsRoot = "Assets/CoreKeepers/Resources/HeroSkills";
        private const string IconsRoot = "Assets/UI/Skills/icons";
        private static bool autoConfigurationQueued;

        static HeroSkillsProjectSetup()
        {
            EditorApplication.delayCall += AutoConfigureWhenNeeded;
        }

        private readonly struct Spec
        {
            public readonly int Id;
            public readonly string Name;
            public readonly CorePlayerClass Class;
            public readonly HeroSkillType Type;
            public readonly int Wave;
            public readonly float Cooldown;
            public readonly HeroSkillTargeting Targeting;
            public readonly HeroSkillEffect Effect;
            public readonly float Power;
            public readonly float Radius;
            public readonly float Duration;
            public readonly float Secondary;
            public readonly int Count;
            public readonly EnemyDebuff Debuff;

            public Spec(int id, string name, CorePlayerClass heroClass, HeroSkillType type, int wave,
                float cooldown, HeroSkillTargeting targeting, HeroSkillEffect effect, float power = 0f,
                float radius = 0f, float duration = 0f, float secondary = 0f, int count = 1,
                EnemyDebuff debuff = EnemyDebuff.None)
            {
                Id = id; Name = name; Class = heroClass; Type = type; Wave = wave; Cooldown = cooldown;
                Targeting = targeting; Effect = effect; Power = power; Radius = radius; Duration = duration;
                Secondary = secondary; Count = count; Debuff = debuff;
            }
        }

        [MenuItem("Core Keepers/Skills/Configure Hero Skills")]
        public static void Configure()
        {
            ImportIconsAsSprites();
            CreateDefinitions();
            ConfigurePlayerPrefabs();
            ConfigureUndeadPrefabs();
            ConfigureDebugScene();
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("Hero skills configured: definitions, icons, player prefabs and DebugScene UI are ready.");
        }

        // Entry point used by the command-line verification workflow.
        public static void ConfigureBatchMode()
        {
            Configure();
            EditorApplication.Exit(0);
        }

        private static void AutoConfigureWhenNeeded()
        {
            if (autoConfigurationQueued || EditorApplication.isPlayingOrWillChangePlaymode ||
                AssetDatabase.LoadAssetAtPath<HeroSkillDefinition>($"{DefinitionsRoot}/Warrior/001_sword_slash.asset") != null)
                return;
            autoConfigurationQueued = true;
            Configure();
        }

        private static void ImportIconsAsSprites()
        {
            foreach (var guid in AssetDatabase.FindAssets("t:Texture2D", new[] { IconsRoot }))
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                if (AssetImporter.GetAtPath(path) is not TextureImporter importer) continue;
                if (importer.textureType == TextureImporterType.Sprite && !importer.mipmapEnabled) continue;
                importer.textureType = TextureImporterType.Sprite;
                importer.spriteImportMode = SpriteImportMode.Single;
                importer.alphaIsTransparency = true;
                importer.mipmapEnabled = false;
                importer.SaveAndReimport();
            }
        }

        private static void CreateDefinitions()
        {
            Directory.CreateDirectory(DefinitionsRoot);
            foreach (var spec in Specs())
            {
                var className = spec.Class.ToString();
                var folder = $"{DefinitionsRoot}/{className}";
                Directory.CreateDirectory(folder);
                var slug = Slug(spec.Name);
                var assetPath = $"{folder}/{spec.Id:000}_{slug}.asset";
                var definition = AssetDatabase.LoadAssetAtPath<HeroSkillDefinition>(assetPath);
                if (definition == null)
                {
                    definition = ScriptableObject.CreateInstance<HeroSkillDefinition>();
                    AssetDatabase.CreateAsset(definition, assetPath);
                }
                var iconPrefix = spec.Class.ToString().ToLowerInvariant();
                var withinClass = spec.Id % 100;
                var iconName = $"{iconPrefix}_{withinClass:00}_{slug}.png";
                var icon64 = AssetDatabase.LoadAssetAtPath<Sprite>($"{IconsRoot}/{iconPrefix}/64/{iconName}");
                var icon256 = AssetDatabase.LoadAssetAtPath<Sprite>($"{IconsRoot}/{iconPrefix}/256/{iconName}");
                if (icon64 == null || icon256 == null)
                    Debug.LogWarning($"Skill '{spec.Name}' is missing {(icon64 == null ? "64" : "256")}px icon assignment.");
                definition.Configure(spec.Id, spec.Name, spec.Class, spec.Type, spec.Wave, spec.Cooldown,
                    icon64, icon256, Description(spec), spec.Targeting, spec.Effect, spec.Power, spec.Radius,
                    spec.Duration, spec.Secondary, spec.Count, spec.Debuff);
                EditorUtility.SetDirty(definition);
            }
            HeroSkillCatalog.ClearCache();
        }

        private static void ConfigurePlayerPrefabs()
        {
            ConfigurePlayer("Assets/CoreKeepers/Resources/CoreWarrior.prefab", 130f);
            ConfigurePlayer("Assets/CoreKeepers/Resources/CoreMage.prefab", 75f);
            ConfigurePlayer("Assets/CoreKeepers/Resources/CoreBuilder.prefab", 110f);
            ConfigurePlayer("Assets/CoreKeepers/Resources/CoreHealer.prefab", 95f);
        }

        private static void ConfigurePlayer(string path, float maximumHealth)
        {
            var root = PrefabUtility.LoadPrefabContents(path);
            try
            {
                if (root.GetComponent<HeroSkillController>() == null) root.AddComponent<HeroSkillController>();
                var warrior = root.GetComponent<NetworkWarrior>();
                if (warrior != null)
                {
                    var serialized = new SerializedObject(warrior);
                    serialized.FindProperty("configuredMaximumHealth").floatValue = maximumHealth;
                    serialized.ApplyModifiedPropertiesWithoutUndo();
                }
                PrefabUtility.SaveAsPrefabAsset(root, path);
            }
            finally { PrefabUtility.UnloadPrefabContents(root); }
        }

        private static void ConfigureUndeadPrefabs()
        {
            var undeadNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            { "Banshee", "Cursed_Doll", "Frankenstein", "Ghost", "Ghoul", "Mummy", "Skeleton", "Vampire", "Zombie" };
            foreach (var guid in AssetDatabase.FindAssets("t:Prefab", new[] { "Assets/CoreKeepers/Resources/Enemies" }))
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                if (!undeadNames.Contains(Path.GetFileNameWithoutExtension(path))) continue;
                var root = PrefabUtility.LoadPrefabContents(path);
                try
                {
                    var enemy = root.GetComponent<EnemyBrain>();
                    if (enemy == null) continue;
                    var serialized = new SerializedObject(enemy);
                    serialized.FindProperty("enemyType").enumValueIndex = (int)CoreEnemyType.Undead;
                    serialized.ApplyModifiedPropertiesWithoutUndo();
                    PrefabUtility.SaveAsPrefabAsset(root, path);
                }
                finally { PrefabUtility.UnloadPrefabContents(root); }
            }
        }

        private static void ConfigureDebugScene()
        {
            const string path = "Assets/Scenes/DebugScene.unity";
            var scene = SceneManager.GetSceneByPath(path);
            var openedForSetup = !scene.IsValid() || !scene.isLoaded;
            if (openedForSetup) scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Additive);
            var skillsPanel = FindSceneObject(scene, "SkillsPanel");
            var popup = FindSceneObject(scene, "Skill Upgrade Popup");
            if (skillsPanel == null) Debug.LogError("DebugScene/Core Gameplay Canvas/SkillsPanel was not found.");
            else
            {
                var ui = skillsPanel.GetComponent<HeroSkillsUI>() ?? skillsPanel.AddComponent<HeroSkillsUI>();
                ui.BindPreparedHierarchy();
                EditorUtility.SetDirty(ui);
            }
            if (popup == null) Debug.LogError("DebugScene/Skill Upgrade Popup was not found.");
            else
            {
                var popupUi = popup.GetComponent<SkillUpgradePopupUI>() ?? popup.AddComponent<SkillUpgradePopupUI>();
                popupUi.BindPreparedHierarchy();
                popup.SetActive(false);
                EditorUtility.SetDirty(popupUi);
            }
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene, path);
            if (openedForSetup) EditorSceneManager.CloseScene(scene, true);
        }

        private static GameObject FindSceneObject(Scene scene, string objectName)
        {
            foreach (var root in scene.GetRootGameObjects())
                foreach (var child in root.GetComponentsInChildren<Transform>(true))
                    if (child.name == objectName) return child.gameObject;
            return null;
        }

        private static string Description(Spec spec) =>
            $"{spec.Name} — {spec.Type} skill unlocked at wave {spec.Wave}. Values are editable in this asset.";

        private static string Slug(string value) => value.ToLowerInvariant().Replace(" ", "_").Replace("-", "_");

        private static IEnumerable<Spec> Specs()
        {
            const HeroSkillTargeting enemy = HeroSkillTargeting.Enemy;
            const HeroSkillTargeting self = HeroSkillTargeting.Self;
            const HeroSkillTargeting ground = HeroSkillTargeting.Ground;
            const HeroSkillType basic = HeroSkillType.Basic;
            const HeroSkillType active = HeroSkillType.Active;
            const HeroSkillType passive = HeroSkillType.Passive;
            const HeroSkillEffect pass = HeroSkillEffect.Passive;

            yield return new Spec(1, "Sword Slash", CorePlayerClass.Warrior, basic, 0, 1.2f, enemy, HeroSkillEffect.MeleeArc, 30, 2.4f);
            yield return new Spec(2, "Whirlwind", CorePlayerClass.Warrior, active, 1, 9, self, HeroSkillEffect.RadialDamage, 40, 3.2f, 5f);
            yield return new Spec(3, "Shield Bash", CorePlayerClass.Warrior, active, 1, 10, enemy, HeroSkillEffect.ShieldBash, 28, 2.6f, 5f, 0f);
            yield return new Spec(4, "Iron Skin", CorePlayerClass.Warrior, passive, 2, 0, self, pass, .25f);
            yield return new Spec(5, "Sharpened Blade", CorePlayerClass.Warrior, passive, 2, 0, self, pass, .2f);
            yield return new Spec(6, "Battle Charge", CorePlayerClass.Warrior, active, 3, 14, enemy, HeroSkillEffect.Charge, 85, 5f, .9f, 0f);
            yield return new Spec(7, "Taunting Roar", CorePlayerClass.Warrior, active, 3, 16, self, HeroSkillEffect.Taunt, 0, 6f, 6f, .3f);
            yield return new Spec(8, "Berserker", CorePlayerClass.Warrior, passive, 4, 0, self, pass, .4f, 0, 0, .25f);
            yield return new Spec(9, "Unbreakable", CorePlayerClass.Warrior, passive, 4, 0, self, pass, .4f);
            yield return new Spec(10, "Earthshatter", CorePlayerClass.Warrior, active, 5, 26, self, HeroSkillEffect.RadialDebuff, 12, 5f, 3f, 0, 1, EnemyDebuff.Stun);
            yield return new Spec(11, "Last Stand", CorePlayerClass.Warrior, active, 5, 30, self, HeroSkillEffect.SelfBuff, 1.6f, 0, 7f, .8f);
            yield return new Spec(12, "Against the Horde", CorePlayerClass.Warrior, passive, 6, 0, self, pass, .35f, 7f, 3f, 10);
            yield return new Spec(13, "Executioner", CorePlayerClass.Warrior, passive, 6, 0, self, pass, .5f, 0, 0, .25f);

            yield return new Spec(101, "Arcane Bolt", CorePlayerClass.Mage, basic, 0, 1.5f, enemy, HeroSkillEffect.SingleProjectile, 45, 16f);
            yield return new Spec(102, "Fireball", CorePlayerClass.Mage, active, 1, 8, enemy, HeroSkillEffect.ExplodingProjectile, 58, 3.5f, 5f, 16f, 1, EnemyDebuff.OnFire);
            yield return new Spec(103, "Frost Nova", CorePlayerClass.Mage, active, 1, 10, self, HeroSkillEffect.RadialDebuff, 30, 4.5f, 5f, 0, 1, EnemyDebuff.Chill);
            yield return new Spec(104, "Arcane Power", CorePlayerClass.Mage, passive, 2, 0, self, pass, .2f);
            yield return new Spec(105, "Quick Casting", CorePlayerClass.Mage, passive, 2, 0, self, pass, .2f);
            yield return new Spec(106, "Chain Lightning", CorePlayerClass.Mage, active, 3, 14, enemy, HeroSkillEffect.ChainDamage, 42, 16f, 4f, .15f, 4);
            yield return new Spec(107, "Arcane Blink", CorePlayerClass.Mage, active, 3, 15, ground, HeroSkillEffect.Blink, 0, 7f, 3f, 3f);
            yield return new Spec(108, "Arcane Exposure", CorePlayerClass.Mage, passive, 4, 0, self, pass, .15f, 0, 5f);
            yield return new Spec(109, "Glass Cannon", CorePlayerClass.Mage, passive, 4, 0, self, pass, .25f, 0, 0, -.15f);
            yield return new Spec(110, "Meteor Strike", CorePlayerClass.Mage, active, 5, 28, ground, HeroSkillEffect.GroundImpact, 110, 4.5f, 5f, 0, 1, EnemyDebuff.OnFire);
            yield return new Spec(111, "Gravity Vortex", CorePlayerClass.Mage, active, 5, 26, ground, HeroSkillEffect.Vortex, 12, 4.5f, 6f, 1.2f);
            yield return new Spec(112, "Arcane Mastery", CorePlayerClass.Mage, passive, 6, 0, self, pass, .1f, 0, 0, 0, 3);
            yield return new Spec(113, "Elemental Detonation", CorePlayerClass.Mage, passive, 6, 0, self, pass, 18, 2.5f);

            yield return new Spec(201, "Hammer Strike", CorePlayerClass.Builder, basic, 0, 1f, enemy, HeroSkillEffect.MeleeArc, 35, 2.2f);
            yield return new Spec(202, "Repair Burst", CorePlayerClass.Builder, active, 1, 10, self, HeroSkillEffect.RepairPulse, 60, 5f);
            yield return new Spec(203, "Construction Rush", CorePlayerClass.Builder, active, 1, 30, self, HeroSkillEffect.ConstructionAura, 2, 7f, 10f);
            yield return new Spec(204, "Master Craftsman", CorePlayerClass.Builder, passive, 2, 0, self, pass, .3f);
            yield return new Spec(205, "Expanded Backpack", CorePlayerClass.Builder, passive, 2, 0, self, pass, .5f);
            yield return new Spec(206, "Warforge Blessing", CorePlayerClass.Builder, active, 3, 16, self, HeroSkillEffect.BuildingBuff, .25f, 7f, 10f, .3f);
            yield return new Spec(207, "Stone Ward", CorePlayerClass.Builder, active, 3, 18, self, HeroSkillEffect.BuildingBuff, 0, 7f, 9f, 0);
            yield return new Spec(208, "Reinforced Masonry", CorePlayerClass.Builder, passive, 4, 0, self, pass, .25f);
            yield return new Spec(209, "Prospector", CorePlayerClass.Builder, passive, 4, 0, self, pass, .25f, 0, 0, 1);
            yield return new Spec(210, "Runic Empowerment", CorePlayerClass.Builder, active, 5, 30, self, HeroSkillEffect.BuildingBuff, .25f, 8f, 12f, .35f);
            yield return new Spec(211, "Emergency Repairs", CorePlayerClass.Builder, active, 5, 28, self, HeroSkillEffect.RepairPulse, 120, 8f, 5f, 5f);
            yield return new Spec(212, "Mending Runes", CorePlayerClass.Builder, passive, 6, 0, self, pass, 2, 5f);
            yield return new Spec(213, "Master Builder", CorePlayerClass.Builder, passive, 6, 0, self, pass, .1f, 6f, 0, .1f);

            yield return new Spec(301, "Light Bolt", CorePlayerClass.Healer, basic, 0, 1.4f, enemy, HeroSkillEffect.SingleProjectile, 25, 16f);
            yield return new Spec(302, "Healing Circle", CorePlayerClass.Healer, active, 1, 10, ground, HeroSkillEffect.HealingArea, 6, 4.5f, 8f);
            yield return new Spec(303, "Holy Pulse", CorePlayerClass.Healer, active, 1, 10, self, HeroSkillEffect.HolyPulse, 40, 6f);
            yield return new Spec(304, "Healing Aura", CorePlayerClass.Healer, passive, 2, 0, self, pass, 2, 5f);
            yield return new Spec(305, "Empowering Aura", CorePlayerClass.Healer, passive, 2, 0, self, pass, .1f, 6f);
            yield return new Spec(306, "Sanctified Ward", CorePlayerClass.Healer, active, 3, 20, self, HeroSkillEffect.CleanseWard, 0, 6f, 8f);
            yield return new Spec(307, "Core Mend", CorePlayerClass.Healer, active, 3, 18, self, HeroSkillEffect.CoreMend, 150, 8f, 8f, .3f);
            yield return new Spec(308, "Guardian Angel", CorePlayerClass.Healer, passive, 4, 0, self, pass, 15, 6f, 20f, .25f);
            yield return new Spec(309, "Undead Bane", CorePlayerClass.Healer, passive, 4, 0, self, pass, .35f);
            yield return new Spec(310, "Divine Sanctuary", CorePlayerClass.Healer, active, 5, 28, ground, HeroSkillEffect.Sanctuary, 8, 6f, 10f, .25f);
            yield return new Spec(311, "Divine Intervention", CorePlayerClass.Healer, active, 5, 45, self, HeroSkillEffect.DivineIntervention, 65, 8f, 0, .4f);
            yield return new Spec(312, "Second Chance", CorePlayerClass.Healer, passive, 6, 0, self, pass, .5f, 0, 90f);
            yield return new Spec(313, "Beacon of Hope", CorePlayerClass.Healer, passive, 6, 0, self, pass, .5f);
        }
    }
}
