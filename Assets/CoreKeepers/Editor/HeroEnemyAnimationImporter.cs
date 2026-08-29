using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEngine;

namespace CoreKeepers.Editor
{
    /// <summary>
    /// Splits HeroEnemyAnimations.fbx into clips described by the neighbouring txt file.
    /// Keeping the ranges outside code lets an animator adjust the timeline without
    /// modifying an editor script.
    /// </summary>
    public sealed class HeroEnemyAnimationImporter : AssetPostprocessor
    {
        private const string ModelPath = "Assets/Animation/HeroEnemyAnimations.fbx";
        private static readonly Regex ClipLinePattern = new(
            @"^\s*(?<first>\d+(?:[\.,]\d+)?)\s*-\s*(?<last>\d+(?:[\.,]\d+)?)\s+(?<name>.+?)\s*$",
            RegexOptions.Compiled);

        public override uint GetVersion() => 1;

        private void OnPreprocessModel()
        {
            if (!string.Equals(assetPath, ModelPath, StringComparison.OrdinalIgnoreCase))
                return;

            var importer = (ModelImporter)assetImporter;
            importer.importAnimation = true;
            importer.animationType = ModelImporterAnimationType.Generic;

            var descriptionPath = Path.ChangeExtension(assetPath, ".txt");
            if (!File.Exists(descriptionPath))
            {
                Debug.LogError($"Animation description is missing: '{descriptionPath}'.", importer);
                return;
            }

            var sourceClips = importer.defaultClipAnimations;
            var takeName = sourceClips.Length > 0 ? sourceClips[0].takeName : "Take 001";
            var clips = ParseClips(descriptionPath, takeName);
            if (clips.Count == 0)
            {
                Debug.LogError($"No valid animation ranges were found in '{descriptionPath}'.", importer);
                return;
            }

            importer.clipAnimations = clips.ToArray();
        }

        private static List<ModelImporterClipAnimation> ParseClips(string path, string takeName)
        {
            var clips = new List<ModelImporterClipAnimation>();
            var names = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var lines = File.ReadAllLines(path);

            for (var lineIndex = 0; lineIndex < lines.Length; lineIndex++)
            {
                var line = lines[lineIndex].Trim();
                if (line.Length == 0 || line.StartsWith("#", StringComparison.Ordinal))
                    continue;

                var match = ClipLinePattern.Match(line);
                if (!match.Success ||
                    !TryParseFrame(match.Groups["first"].Value, out var firstFrame) ||
                    !TryParseFrame(match.Groups["last"].Value, out var lastFrame))
                {
                    Debug.LogError($"Invalid animation range at {path}:{lineIndex + 1}: '{lines[lineIndex]}'.");
                    continue;
                }

                var clipName = match.Groups["name"].Value.Trim();
                if (clipName.Length == 0 || lastFrame <= firstFrame)
                {
                    Debug.LogError($"Invalid animation clip at {path}:{lineIndex + 1}: '{lines[lineIndex]}'.");
                    continue;
                }

                if (!names.Add(clipName))
                {
                    Debug.LogError($"Duplicate animation name '{clipName}' at {path}:{lineIndex + 1}.");
                    continue;
                }

                var loops = IsLoopingClip(clipName);
                clips.Add(new ModelImporterClipAnimation
                {
                    name = clipName,
                    takeName = takeName,
                    firstFrame = firstFrame,
                    lastFrame = lastFrame,
                    loopTime = loops,
                    loopPose = loops,
                    lockRootRotation = true,
                    lockRootHeightY = true,
                    lockRootPositionXZ = true
                });
            }

            return clips;
        }

        private static bool TryParseFrame(string value, out float frame)
        {
            return float.TryParse(value.Replace(',', '.'), NumberStyles.Float,
                CultureInfo.InvariantCulture, out frame);
        }

        private static bool IsLoopingClip(string clipName)
        {
            return clipName.EndsWith("Loop", StringComparison.OrdinalIgnoreCase) ||
                   string.Equals(clipName, "Idle", StringComparison.OrdinalIgnoreCase) ||
                   string.Equals(clipName, "Float", StringComparison.OrdinalIgnoreCase);
        }
    }

    public static class HeroEnemyAnimationImporterTools
    {
        private const string ModelPath = "Assets/Animation/HeroEnemyAnimations.fbx";
        private const string DescriptionPath = "Assets/Animation/HeroEnemyAnimations.txt";

        [MenuItem("Core Keepers/Animations/Reimport Hero Enemy Animation Library")]
        public static void ReimportAndValidate()
        {
            AssetDatabase.ImportAsset(ModelPath, ImportAssetOptions.ForceUpdate);

            var importer = AssetImporter.GetAtPath(ModelPath) as ModelImporter;
            if (importer == null)
                throw new InvalidOperationException($"Model importer was not found at '{ModelPath}'.");

            var expectedClipCount = CountDescriptionEntries();
            var clips = importer.clipAnimations;
            if (clips.Length != expectedClipCount)
                throw new InvalidOperationException(
                    $"Imported {clips.Length} animation clips, but {expectedClipCount} were described.");

            Debug.Log($"Hero/enemy animation library imported successfully: {clips.Length} clips from '{ModelPath}'.");
        }

        private static int CountDescriptionEntries()
        {
            var count = 0;
            foreach (var rawLine in File.ReadAllLines(DescriptionPath))
            {
                var line = rawLine.Trim();
                if (line.Length > 0 && !line.StartsWith("#", StringComparison.Ordinal))
                    count++;
            }

            return count;
        }
    }
}
