using System;
using UnityEngine;

namespace CatMerge.Platform
{
    /// <summary>
    /// Default platform for the editor and plain builds: PlayerPrefs (IndexedDB in WebGL) and the system language.
    /// </summary>
    public sealed class EditorPlatform : IPlatformService
    {
        public void Save(string key, string data)
        {
            if (string.IsNullOrEmpty(data)) PlayerPrefs.DeleteKey(key);
            else PlayerPrefs.SetString(key, data);
            PlayerPrefs.Save(); // WebGL only writes to IndexedDB on Save()
        }

        public void Load(string key, Action<string> onLoaded)
        {
            onLoaded?.Invoke(PlayerPrefs.GetString(key, null));
        }

        public string GetLanguage()
        {
            switch (Application.systemLanguage)
            {
                case SystemLanguage.Russian:
                case SystemLanguage.Ukrainian:
                case SystemLanguage.Belarusian:
                    return "ru";
                default:
                    return "en";
            }
        }
    }
}
