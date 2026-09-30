using System.Collections.Generic;
using CatMerge.Audio;
using CatMerge.Game;
using CatMerge.Localization;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace CatMerge.UI
{
    /// <summary>
    /// The Catpedia: a grid with one <see cref="CodexCard"/> per cat level, discovered ones shown in full,
    /// the rest as silhouettes. Updates live if a new cat is discovered while it is open.
    /// </summary>
    public sealed class CodexPanel : MonoBehaviour
    {
        [SerializeField] private GameController game;
        [SerializeField] private AudioService audioService;
        [Tooltip("Full-screen overlay; animates the window in and out.")]
        [SerializeField] private PanelAnimator panel;
        [SerializeField] private Button closeButton;
        [Tooltip("Dimmed background; tapping it closes the panel.")]
        [SerializeField] private Button backgroundButton;
        [SerializeField] private TMP_Text titleLabel;
        [SerializeField] private ScrollRect scroll;
        [SerializeField] private CodexCard cardTemplate;

        private readonly List<CodexCard> _cards = new List<CodexCard>();

        public bool IsOpen => panel.IsOpen;

        private void Awake()
        {
            cardTemplate.gameObject.SetActive(false);
            panel.HideImmediate();
            closeButton.onClick.AddListener(Close);
            backgroundButton.onClick.AddListener(Close);
        }

        private void OnDestroy()
        {
            if (game != null && game.Codex != null) game.Codex.Discovered -= OnDiscovered;
        }

        private void Update()
        {
            if (IsOpen && Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame) Close();
        }

        public void Open()
        {
            if (game.Codex == null) return;

            panel.Show();
            game.Codex.Discovered -= OnDiscovered;
            game.Codex.Discovered += OnDiscovered;
            Refresh();
            scroll.verticalNormalizedPosition = 1f;
        }

        public void Close()
        {
            if (game.Codex != null) game.Codex.Discovered -= OnDiscovered;
            panel.Hide();
        }

        private void OnDiscovered(int _) => Refresh();

        private void Refresh()
        {
            var codex = game.Codex;
            var database = game.CatDatabase;
            titleLabel.text = Loc.Format("codex_title", codex.Count, codex.MaxLevel);

            EnsureCards(codex.MaxLevel);
            for (int level = 1; level <= codex.MaxLevel; level++)
            {
                var card = _cards[level - 1];
                var cat = database.Get(level);
                card.gameObject.SetActive(true);
                if (codex.IsDiscovered(level)) card.ShowDiscovered(cat, () => Meow(card, cat.meowClip));
                else card.ShowUnknown(cat);
            }
        }

        private void Meow(CodexCard card, AudioClip clip)
        {
            // Important: the player asked for it, so the ambient cooldown doesn't apply.
            if (audioService.PlayMeow(clip, important: true)) card.PlayMeow();
        }

        private void EnsureCards(int count)
        {
            while (_cards.Count < count)
            {
                var card = Instantiate(cardTemplate, cardTemplate.transform.parent);
                card.name = $"Card L{_cards.Count + 1}";
                _cards.Add(card);
            }
        }
    }
}
