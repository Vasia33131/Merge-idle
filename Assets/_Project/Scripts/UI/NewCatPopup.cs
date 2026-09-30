using System.Collections.Generic;
using CatMerge.Audio;
using CatMerge.Game;
using CatMerge.Localization;
using CatMerge.Views;
using DG.Tweening;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace CatMerge.UI
{
    /// <summary>
    /// "New cat!" celebration the first time a cat level appears (see <see cref="Core.Codex.Discovered"/>).
    /// Several discoveries in a row are shown one after another.
    /// </summary>
    public sealed class NewCatPopup : MonoBehaviour
    {
        [SerializeField] private GameController game;
        [SerializeField] private AudioService audioService;
        [Tooltip("Full-screen overlay; animates the window in and out.")]
        [SerializeField] private PanelAnimator panel;
        [SerializeField] private TMP_Text titleLabel;
        [SerializeField] private Image swatch;
        [SerializeField] private TMP_Text swatchLabel;
        [Tooltip("Soft glow behind the cat that keeps pulsing while the popup is open.")]
        [SerializeField] private Transform glow;
        [SerializeField] private TMP_Text nameLabel;
        [SerializeField] private TMP_Text descLabel;
        [SerializeField] private Button meowButton;
        [SerializeField] private TMP_Text meowLabel;
        [SerializeField] private Button okButton;
        [SerializeField] private TMP_Text okLabel;
        [Tooltip("Pause after the merge so the new cat's pop-in and meow are seen first.")]
        [SerializeField] private float showDelay = 0.45f;

        private readonly Queue<int> _queue = new Queue<int>();
        private int _level;
        private Tween _delay;

        private void Awake()
        {
            panel.HideImmediate();
            meowButton.onClick.AddListener(Meow);
            okButton.onClick.AddListener(Next);
        }

        private void Start()
        {
            if (game.Codex != null) game.Codex.Discovered += OnDiscovered;
        }

        private void OnDestroy()
        {
            _delay?.Kill();
            swatch.transform.DOKill();
            glow.DOKill();
            if (game != null && game.Codex != null) game.Codex.Discovered -= OnDiscovered;
        }

        private void OnDiscovered(int level)
        {
            _queue.Enqueue(level);
            if (panel.IsOpen || _delay != null) return;
            _delay = DOVirtual.DelayedCall(showDelay, () =>
            {
                _delay = null;
                Next();
            });
        }

        /// <summary>Shows the next queued cat, or closes when there is none.</summary>
        private void Next()
        {
            if (_queue.Count == 0)
            {
                glow.DOKill();
                panel.Hide();
                return;
            }
            Show(_queue.Dequeue());
        }

        private void Show(int level)
        {
            _level = level;
            var cat = game.CatDatabase.Get(level);

            titleLabel.text = Loc.Get("newcat_title");
            if (cat.sprite != null) swatch.sprite = cat.sprite;
            swatch.color = cat.color;
            swatchLabel.text = level.ToString();
            swatchLabel.color = CatView.GetLabelColor(cat.color);
            nameLabel.text = Loc.Get(cat.nameKey);
            descLabel.text = Loc.Get(cat.descKey);
            meowLabel.text = Loc.Get("codex_meow");
            okLabel.text = Loc.Get("newcat_ok");

            panel.Show();

            var picture = swatch.transform;
            picture.DOKill();
            picture.localScale = Vector3.zero;
            picture.DOScale(1f, 0.5f).SetDelay(0.1f).SetEase(Ease.OutBack, 2.5f);

            glow.DOKill();
            glow.localScale = Vector3.one;
            glow.DOScale(1.15f, 0.8f).SetEase(Ease.InOutSine).SetLoops(-1, LoopType.Yoyo);
        }

        private void Meow()
        {
            if (!audioService.PlayMeow(game.CatDatabase.Get(_level).meowClip, important: true)) return;
            var target = swatch.transform;
            target.DOKill(complete: true);
            target.DOPunchScale(Vector3.one * 0.15f, 0.35f, vibrato: 6, elasticity: 0.7f);
        }
    }
}
