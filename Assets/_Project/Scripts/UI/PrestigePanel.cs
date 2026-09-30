using CatMerge.Core;
using CatMerge.Game;
using CatMerge.Localization;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace CatMerge.UI
{
    /// <summary>
    /// Confirmation for "Moving to a new home": where to, how many fish the move gives (updates live while cats keep
    /// earning), the resulting income bonus and what is reset. Confirming hands over to <see cref="SaveService.MoveToNewHome"/>.
    /// </summary>
    public sealed class PrestigePanel : MonoBehaviour
    {
        [SerializeField] private GameController game;
        [SerializeField] private SaveService saveService;
        [Tooltip("Full-screen overlay; animates the window in and out.")]
        [SerializeField] private PanelAnimator panel;
        [SerializeField] private Button closeButton;
        [Tooltip("Dimmed background; tapping it closes the panel.")]
        [SerializeField] private Button backgroundButton;
        [SerializeField] private TMP_Text titleLabel;
        [SerializeField] private TMP_Text homeLabel;
        [SerializeField] private TMP_Text earnedLabel;
        [SerializeField] private TMP_Text fishCaption;
        [SerializeField] private TMP_Text fishLabel;
        [SerializeField] private TMP_Text totalLabel;
        [SerializeField] private TMP_Text resetsLabel;
        [SerializeField] private Button confirmButton;
        [SerializeField] private TMP_Text confirmLabel;
        [SerializeField] private Button cancelButton;
        [SerializeField] private TMP_Text cancelLabel;

        private Wallet _wallet;

        public bool IsOpen => panel.IsOpen;

        private void Awake()
        {
            panel.HideImmediate();
            closeButton.onClick.AddListener(Close);
            backgroundButton.onClick.AddListener(Close);
            cancelButton.onClick.AddListener(Close);
            confirmButton.onClick.AddListener(Confirm);
        }

        private void OnDestroy()
        {
            Unsubscribe();
        }

        private void Update()
        {
            if (IsOpen && Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame) Close();
        }

        public void Open()
        {
            if (game.Prestige == null || !game.Prestige.IsAvailable) return;

            _wallet = game.Wallet;
            Unsubscribe();
            _wallet.OnChanged += OnCoinsChanged; // income keeps coming in: the fish count grows while reading
            Refresh();

            panel.Show();
        }

        public void Close()
        {
            Unsubscribe();
            panel.Hide();
        }

        private void Unsubscribe()
        {
            if (_wallet != null) _wallet.OnChanged -= OnCoinsChanged;
        }

        private void OnCoinsChanged(double _) => Refresh();

        private void Refresh()
        {
            var prestige = game.Prestige;
            long gain = prestige.PendingFish;
            long total = prestige.Fish + gain;

            titleLabel.text = Loc.Get("move_title");
            var home = game.NextHome;
            homeLabel.text = home != null ? Loc.Format("move_home", Loc.Get(home.nameKey)) : string.Empty;
            earnedLabel.text = Loc.Format("move_earned", NumberFormatter.Format(game.Economy.RunEarned));
            fishCaption.text = Loc.Get("move_fish_caption");
            fishLabel.text = "+" + NumberFormatter.Format(gain);
            totalLabel.text = gain > 0
                ? Loc.Format("move_total", NumberFormatter.Format(total), FormatMultiplier(1 + total * prestige.BonusPerFish))
                : Loc.Get("move_need_more");
            resetsLabel.text = Loc.Get("move_resets");
            confirmLabel.text = Loc.Get("move_confirm");
            cancelLabel.text = Loc.Get("move_cancel");
            confirmButton.interactable = prestige.CanMove;
        }

        private void Confirm()
        {
            if (!saveService.MoveToNewHome()) Refresh(); // on success the scene reloads into the new home
        }

        /// <summary>"×1.5", "×12".</summary>
        public static string FormatMultiplier(double value) =>
            "×" + (value < 1000 ? value.ToString("0.##", System.Globalization.CultureInfo.InvariantCulture) : NumberFormatter.Format(value));
    }
}
