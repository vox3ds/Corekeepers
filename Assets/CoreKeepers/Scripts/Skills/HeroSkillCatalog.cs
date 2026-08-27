using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace CoreKeepers
{
    public static class HeroSkillCatalog
    {
        private static readonly Dictionary<CorePlayerClass, HeroSkillDefinition[]> ByClass = new();
        private static readonly Dictionary<int, HeroSkillDefinition> ById = new();

        public static IReadOnlyList<HeroSkillDefinition> ForClass(CorePlayerClass heroClass)
        {
            EnsureLoaded();
            return ByClass.TryGetValue(heroClass, out var definitions) ? definitions : System.Array.Empty<HeroSkillDefinition>();
        }

        public static HeroSkillDefinition Find(int stableId)
        {
            EnsureLoaded();
            ById.TryGetValue(stableId, out var definition);
            return definition;
        }

        public static HeroSkillDefinition Basic(CorePlayerClass heroClass) =>
            ForClass(heroClass).FirstOrDefault(skill => skill.SkillType == HeroSkillType.Basic);

        public static HeroSkillDefinition[] Choices(CorePlayerClass heroClass, int wave) =>
            ForClass(heroClass).Where(skill => skill.UnlockWave == wave).OrderBy(skill => skill.StableId).ToArray();

        public static void ClearCache()
        {
            ByClass.Clear();
            ById.Clear();
        }

        private static void EnsureLoaded()
        {
            if (ById.Count > 0)
                return;
            foreach (var definition in Resources.LoadAll<HeroSkillDefinition>("HeroSkills"))
            {
                if (definition == null || definition.StableId <= 0)
                    continue;
                ById[definition.StableId] = definition;
            }
            foreach (CorePlayerClass heroClass in System.Enum.GetValues(typeof(CorePlayerClass)))
                ByClass[heroClass] = ById.Values.Where(skill => skill.HeroClass == heroClass)
                    .OrderBy(skill => skill.UnlockWave).ThenBy(skill => skill.StableId).ToArray();
        }
    }
}
