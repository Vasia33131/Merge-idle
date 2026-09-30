using System;
using CatMerge.Data;
using CatMerge.Localization;
using CatMerge.Views;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace CatMerge.UI
{
    /// <summary>
    /// One entry in a shop list: swatch, name, description and a buy button with the price.
    /// Used for kittens (<see cref="ShopPanel"/>) and upgrades (<see cref="UpgradesPanel"/>).
    /// </summary>
    public sealed class ShopRow : MonoBehaviour
    {
        private static readonly Color LockedSwatch = new Color(0.72f, 0.66f, 0.60f);
        private static readonly Color LockedText = new Color(0.55f, 0.48f, 0.43f);

        [SerializeField] private Image swatch;
        [SerializeField] private TMP_Text swatchLabel;
        [SerializeField] private TMP_Text nameLabel;
        [SerializeField] private TMP_Text infoLabel;
        [SerializeField] private Button buyButton;
        [SerializeField] private TMP_Text buyLabel;

        private Action _onBuy;
        private Color _nameColor;

        public Transform BuyButton => buyButton.transform;

        private void Awake()
        {
            _nameColor = nameLabel.color;
            buyButton.onClick.AddListener(() => _onBuy?.Invoke());
        }

        public void ShowBuyable(CatConfig cat, string price, bool canBuy, Action onBuy)
        {
            if (cat.sprite != null) swatch.sprite = cat.sprite;
            Show(cat.color, cat.level.ToString(), Loc.Get(cat.nameKey), Loc.Get(cat.descKey), price, canBuy, onBuy);
        }

        /// <summary>Generic entry; the swatch keeps its current sprite.</summary>
        public void Show(Color swatchColor, string swatchText, string title, string info, string button, bool canBuy, Action onBuy)
        {
            _onBuy = onBuy;
            swatch.color = swatchColor;
            swatchLabel.text = swatchText;
            swatchLabel.color = CatView.GetLabelColor(swatchColor);
            nameLabel.text = title;
            nameLabel.color = _nameColor;
            infoLabel.text = info;
            buyButton.gameObject.SetActive(true);
            buyButton.interactable = canBuy;
            buyLabel.text = button;
        }

        /// <summary>Greyed-out teaser with a "?" swatch and no button.</summary>
        public void ShowLocked(string title, string info)
        {
            _onBuy = null;
            swatch.color = LockedSwatch;
            swatchLabel.text = "?";
            swatchLabel.color = Color.white;
            nameLabel.text = title;
            nameLabel.color = LockedText;
            infoLabel.text = info;
            buyButton.gameObject.SetActive(false);
        }
    }
}
