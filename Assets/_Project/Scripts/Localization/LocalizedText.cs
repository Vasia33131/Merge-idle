using TMPro;
using UnityEngine;

namespace CatMerge.Localization
{
    /// <summary>
    /// Static TMP label: shows the text for <see cref="Key"/> and follows language switches.
    /// Labels with numbers in them are set from code instead (panels redraw on open / on <see cref="LocalizationService.LanguageChanged"/>).
    /// </summary>
    [RequireComponent(typeof(TMP_Text))]
    [DisallowMultipleComponent]
    public sealed class LocalizedText : MonoBehaviour
    {
        [Tooltip("Key in Resources/Localization/*.json, e.g. shop_button.")]
        [SerializeField] private string key;

        private TMP_Text _text;

        public string Key
        {
            get => key;
            set
            {
                key = value;
                Refresh();
            }
        }

        private void OnEnable()
        {
            LocalizationService.LanguageChanged += Refresh;
            Refresh();
        }

        private void OnDisable()
        {
            LocalizationService.LanguageChanged -= Refresh;
        }

        private void Refresh()
        {
            if (!isActiveAndEnabled || string.IsNullOrEmpty(key)) return;
            if (_text == null) _text = GetComponent<TMP_Text>();
            _text.text = LocalizationService.Get(key);
        }
    }
}
