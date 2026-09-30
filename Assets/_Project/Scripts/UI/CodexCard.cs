using System;
using CatMerge.Data;
using CatMerge.Localization;
using CatMerge.Views;
using DG.Tweening;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace CatMerge.UI
{
    /// <summary>
    /// One Catpedia entry. Discovered: the cat, its name, description and a "listen to the meow" button.
    /// Not yet: a dark silhouette and "???".
    /// </summary>
    public sealed class CodexCard : MonoBehaviour
    {
        private static readonly Color SilhouetteColor = new Color(0.36f, 0.30f, 0.27f);
        private static readonly Color UnknownText = new Color(0.55f, 0.48f, 0.43f);

        [SerializeField] private Image swatch;
        [SerializeField] private TMP_Text swatchLabel;
        [SerializeField] private TMP_Text nameLabel;
        [SerializeField] private TMP_Text descLabel;
        [SerializeField] private Button meowButton;
        [SerializeField] private TMP_Text meowLabel;

        private Action _onMeow;
        private Color _nameColor;

        private void Awake()
        {
            _nameColor = nameLabel.color;
            meowButton.onClick.AddListener(() => _onMeow?.Invoke());
        }

        private void OnDestroy() => swatch.transform.DOKill();

        public void ShowDiscovered(CatConfig cat, Action onMeow)
        {
            _onMeow = onMeow;
            if (cat.sprite != null) swatch.sprite = cat.sprite;
            swatch.color = cat.color;
            swatchLabel.text = cat.level.ToString();
            swatchLabel.color = CatView.GetLabelColor(cat.color);
            nameLabel.text = Loc.Get(cat.nameKey);
            nameLabel.color = _nameColor;
            descLabel.text = Loc.Get(cat.descKey);
            meowLabel.text = Loc.Get("codex_meow");
            meowButton.gameObject.SetActive(true);
        }

        public void ShowUnknown(CatConfig cat)
        {
            _onMeow = null;
            // Same shape as the real cat, so the silhouette already hints at it once there is art.
            if (cat.sprite != null) swatch.sprite = cat.sprite;
            swatch.color = SilhouetteColor;
            swatchLabel.text = "?";
            swatchLabel.color = Color.white;
            nameLabel.text = Loc.Get("codex_unknown");
            nameLabel.color = UnknownText;
            descLabel.text = string.Empty;
            meowButton.gameObject.SetActive(false);
        }

        /// <summary>Small wiggle of the picture while its meow plays.</summary>
        public void PlayMeow()
        {
            var target = swatch.transform;
            target.DOKill(complete: true);
            target.DOPunchScale(Vector3.one * 0.15f, 0.35f, vibrato: 6, elasticity: 0.7f);
        }
    }
}
