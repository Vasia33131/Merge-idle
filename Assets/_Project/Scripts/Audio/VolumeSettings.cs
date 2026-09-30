using UnityEngine;

namespace CatMerge.Audio
{
    /// <summary>Music/SFX volume (0..1) kept in PlayerPrefs, separate from game progress so a reset doesn't touch it.</summary>
    public static class VolumeSettings
    {
        private const string MusicKey = "catmerge_volume_music";
        private const string SfxKey = "catmerge_volume_sfx";

        public const float DefaultMusic = 0.6f;
        public const float DefaultSfx = 0.9f;

        public static float Music
        {
            get => Mathf.Clamp01(PlayerPrefs.GetFloat(MusicKey, DefaultMusic));
            set => Store(MusicKey, value);
        }

        public static float Sfx
        {
            get => Mathf.Clamp01(PlayerPrefs.GetFloat(SfxKey, DefaultSfx));
            set => Store(SfxKey, value);
        }

        /// <summary>Writes to disk. Called when the settings panel closes, not per slider step (slow on WebGL).</summary>
        public static void Flush() => PlayerPrefs.Save();

        private static void Store(string key, float value) => PlayerPrefs.SetFloat(key, Mathf.Clamp01(value));

        /// <summary>Linear 0..1 slider value to mixer decibels (-80 dB is silence).</summary>
        public static float ToDecibels(float linear) => linear <= 0.0001f ? -80f : Mathf.Log10(linear) * 20f;
    }
}
