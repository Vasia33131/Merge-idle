using System;
using System.Collections.Generic;
using CatMerge.Core;
using CatMerge.Platform;
using UnityEngine;

namespace CatMerge.Localization
{
    public enum Language { En, Ru }

    /// <summary>
    /// String tables from Resources/Localization/{en,ru}.json (flat "key": "text" objects).
    /// The language comes from the platform (<see cref="IPlatformService.GetLanguage"/>) unless the player picked one in
    /// the settings; that choice is remembered in PlayerPrefs. Missing keys fall back to English, then to the key itself.
    /// </summary>
    public static class LocalizationService
    {
        private const string ResourcesFolder = "Localization/";
        private const string PrefsKey = "CatMerge.Language";

        private static readonly Dictionary<Language, Dictionary<string, string>> Tables =
            new Dictionary<Language, Dictionary<string, string>>();

        private static bool _initialized;
        private static Language _current;

        /// <summary>Raised after <see cref="Current"/> changes; <see cref="LocalizedText"/> and the HUD redraw on it.</summary>
        public static event Action LanguageChanged;

        public static IReadOnlyList<Language> Languages { get; } = new[] { Language.Ru, Language.En };

        public static Language Current
        {
            get
            {
                EnsureInitialized();
                return _current;
            }
        }

        /// <summary>Switches the language, remembers the player's choice and notifies listeners.</summary>
        public static void SetLanguage(Language language)
        {
            EnsureInitialized();
            PlayerPrefs.SetString(PrefsKey, ToCode(language));
            PlayerPrefs.Save();
            if (_current == language) return;
            _current = language;
            LanguageChanged?.Invoke();
        }

        public static string Get(string key)
        {
            EnsureInitialized();
            if (string.IsNullOrEmpty(key)) return string.Empty;
            if (GetTable(_current).TryGetValue(key, out var text)) return text;
            if (_current != Language.En && GetTable(Language.En).TryGetValue(key, out text)) return text;
            Debug.LogWarning($"[CatMerge] Missing localization key '{key}'.");
            return key;
        }

        public static string Format(string key, params object[] args)
        {
            string pattern = Get(key);
            try
            {
                return string.Format(pattern, args);
            }
            catch (FormatException)
            {
                Debug.LogError($"[CatMerge] Bad placeholders in '{key}' ({ToCode(Current)}): \"{pattern}\"");
                return pattern;
            }
        }

        /// <summary>Language for an ISO 639-1 code from the platform; unknown codes fall back to English.</summary>
        public static Language FromCode(string code)
        {
            switch (code?.Trim().ToLowerInvariant())
            {
                case "ru":
                case "uk":
                case "be":
                case "kk":
                    return Language.Ru;
                default:
                    return Language.En;
            }
        }

        public static string ToCode(Language language) => language == Language.Ru ? "ru" : "en";

        private static void EnsureInitialized()
        {
            if (_initialized) return;
            _initialized = true;

            string saved = PlayerPrefs.GetString(PrefsKey, string.Empty);
            _current = FromCode(saved.Length > 0 ? saved : PlatformProvider.Current.GetLanguage());
        }

        private static Dictionary<string, string> GetTable(Language language)
        {
            if (Tables.TryGetValue(language, out var table)) return table;

            string path = ResourcesFolder + ToCode(language);
            var asset = Resources.Load<TextAsset>(path);
            if (asset == null)
            {
                Debug.LogError($"[CatMerge] Localization file Resources/{path}.json not found.");
                table = new Dictionary<string, string>();
            }
            else
            {
                try
                {
                    table = FlatJson.Parse(asset.text);
                }
                catch (FormatException e)
                {
                    Debug.LogError($"[CatMerge] Resources/{path}.json: {e.Message}", asset);
                    table = new Dictionary<string, string>();
                }
                Resources.UnloadAsset(asset);
            }
            Tables[language] = table;
            return table;
        }

#if UNITY_EDITOR
        // Domain reload may be disabled in the editor: start every play session from a clean state.
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            Tables.Clear();
            _initialized = false;
            LanguageChanged = null;
        }
#endif
    }
}
