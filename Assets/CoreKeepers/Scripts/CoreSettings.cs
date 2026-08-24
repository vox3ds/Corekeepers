using System;
using UnityEngine;

namespace CoreKeepers
{
    public static class CoreSettings
    {
        public const string NicknameKey = "CoreKeepers.Nickname";
        private const string MasterVolumeKey = "CoreKeepers.Audio.Master";
        private const string MusicVolumeKey = "CoreKeepers.Audio.Music";
        private const string SfxVolumeKey = "CoreKeepers.Audio.Sfx";

        public static bool HasNickname => !string.IsNullOrWhiteSpace(Nickname);
        public static string Nickname => PlayerPrefs.GetString(NicknameKey, string.Empty).Trim();
        public static float MasterVolume => PlayerPrefs.GetFloat(MasterVolumeKey, 1f);
        public static float MusicVolume => PlayerPrefs.GetFloat(MusicVolumeKey, 0.8f);
        public static float SfxVolume => PlayerPrefs.GetFloat(SfxVolumeKey, 0.9f);

        public static bool TrySetNickname(string value)
        {
            var nickname = (value ?? string.Empty).Trim();
            if (nickname.Length == 0)
                return false;

            PlayerPrefs.SetString(NicknameKey, nickname.Substring(0, Math.Min(24, nickname.Length)));
            PlayerPrefs.Save();
            return true;
        }

        public static void SetVolumes(float master, float music, float sfx)
        {
            PlayerPrefs.SetFloat(MasterVolumeKey, Mathf.Clamp01(master));
            PlayerPrefs.SetFloat(MusicVolumeKey, Mathf.Clamp01(music));
            PlayerPrefs.SetFloat(SfxVolumeKey, Mathf.Clamp01(sfx));
            PlayerPrefs.Save();
            ApplyAudioSettings();
        }

        public static void ApplyAudioSettings()
        {
            AudioListener.volume = MasterVolume;
        }
    }

    public enum CoreLaunchMode
    {
        None,
        Campaign,
        DebugHost
    }

    public static class CoreLaunchContext
    {
        public static CoreLaunchMode Mode { get; private set; }

        public static void Set(CoreLaunchMode mode) => Mode = mode;
        public static void Clear() => Mode = CoreLaunchMode.None;
    }
}
