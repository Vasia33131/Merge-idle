using CatMerge.Audio;
using CatMerge.Game;
using CatMerge.Localization;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace CatMerge.UI
{
    /// <summary>
    /// Settings modal: music and sound volume, language, plus a debug "reset progress" button that needs two taps.
    /// Static captions are <see cref="LocalizedText"/>, so switching the language redraws the open panel at once.
    /// </summary>
    public sealed class SettingsPanel : MonoBehaviour
    {
        [SerializeField] private SaveService saveService;
        [SerializeField] private AudioService audioService;
        [Tooltip("Full-screen overlay; animates the window in and out.")]
        [SerializeField] private PanelAnimator panel;
        [SerializeField] private Button closeButton;
        [Tooltip("Dimmed background; tapping it closes the panel.")]
        [SerializeField] private Button backgroundButton;

        [Header("Volume")]
        [SerializeField] private Slider musicSlider;
        [SerializeField] private Slider sfxSlider;

        [Header("Language")]
        [Tooltip("One button per language, in the order of LocalizationService.Languages (RU, EN).")]
        [SerializeField] private Button[] languageButtons;
        [SerializeField] private Color selectedColor = new Color(0.96f, 0.56f, 0.33f);
        [SerializeField] private Color idleColor = new Color(0.95f, 0.88f, 0.79f);
        [SerializeField] private Color selectedTextColor = Color.white;
        [SerializeField] private Color idleTextColor = new Color(0.30f, 0.22f, 0.18f);

        [Header("Reset")]
        [SerializeField] private Button resetButton;
        [SerializeField] private LocalizedText resetLabel;

        private bool _confirming;

        public bool IsOpen => panel.IsOpen;

        private void Awake()
        {
            panel.HideImmediate();
            closeButton.onClick.AddListener(Close);
            backgroundButton.onClick.AddListener(Close);
            resetButton.onClick.AddListener(OnResetClicked);
            musicSlider.onValueChanged.AddListener(audioService.SetMusicVolume);
            sfxSlider.onValueChanged.AddListener(audioService.SetSfxVolume);

            var languages = LocalizationService.Languages;
            for (int i = 0; i < languageButtons.Length && i < languages.Count; i++)
            {
                var language = languages[i];
                languageButtons[i].onClick.AddListener(() => LocalizationService.SetLanguage(language));
            }
            LocalizationService.LanguageChanged += RefreshLanguageButtons;
        }

        private void OnDestroy()
        {
            LocalizationService.LanguageChanged -= RefreshLanguageButtons;
            closeButton.onClick.RemoveListener(Close);
            backgroundButton.onClick.RemoveListener(Close);
            resetButton.onClick.RemoveListener(OnResetClicked);
            musicSlider.onValueChanged.RemoveListener(audioService.SetMusicVolume);
            sfxSlider.onValueChanged.RemoveListener(audioService.SetSfxVolume);
            foreach (var button in languageButtons) button.onClick.RemoveAllListeners();
        }

        private void Update()
        {
            if (IsOpen && Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame) Close();
        }

        public void Open()
        {
            musicSlider.SetValueWithoutNotify(audioService.MusicVolume);
            sfxSlider.SetValueWithoutNotify(audioService.SfxVolume);
            SetConfirming(false);
            RefreshLanguageButtons();
            panel.Show();
        }

        public void Close()
        {
            if (!IsOpen) return;
            SetConfirming(false);
            panel.Hide();
            VolumeSettings.Flush();
        }

        private void RefreshLanguageButtons()
        {
            var languages = LocalizationService.Languages;
            var current = LocalizationService.Current;
            for (int i = 0; i < languageButtons.Length && i < languages.Count; i++)
            {
                bool selected = languages[i] == current;
                var button = languageButtons[i];
                button.targetGraphic.color = selected ? selectedColor : idleColor;
                var label = button.GetComponentInChildren<TMPro.TMP_Text>();
                if (label != null) label.color = selected ? selectedTextColor : idleTextColor;
            }
        }

        private void OnResetClicked()
        {
            if (!_confirming)
            {
                SetConfirming(true);
                return;
            }
            saveService.ResetProgress();
        }

        private void SetConfirming(bool value)
        {
            _confirming = value;
            resetLabel.Key = value ? "settings_reset_confirm" : "settings_reset";
        }
    }
}
