using System;

namespace CatMerge.Platform
{
    /// <summary>
    /// Everything that differs between hosts (editor/plain WebGL, Yandex Games, CrazyGames...).
    /// Calls are callback-based because web SDKs answer asynchronously. Ads will be added here later.
    /// </summary>
    public interface IPlatformService
    {
        /// <summary>Stores <paramref name="data"/> under <paramref name="key"/>. An empty string clears the slot.</summary>
        void Save(string key, string data);

        /// <summary>Calls <paramref name="onLoaded"/> once with the stored string, or null/empty if there is none.</summary>
        void Load(string key, Action<string> onLoaded);

        /// <summary>Two-letter ISO 639-1 code of the player's language, e.g. "ru" or "en".</summary>
        string GetLanguage();
    }
}
