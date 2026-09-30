using System.Collections.Generic;
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
    /// Modal kitten shop: every level the shop sells (not only the best one) plus a teaser of the next locked level.
    /// Stays open after a purchase so several kittens can be bought in a row.
    /// </summary>
    public sealed class ShopPanel : MonoBehaviour
    {
        [SerializeField] private GameController game;
        [Tooltip("Full-screen overlay; animates the window in and out.")]
        [SerializeField] private PanelAnimator panel;
        [SerializeField] private Button closeButton;
        [Tooltip("Dimmed background; tapping it closes the shop.")]
        [SerializeField] private Button backgroundButton;
        [SerializeField] private TMP_Text titleLabel;
        [SerializeField] private ScrollRect scroll;
        [SerializeField] private ShopRow rowTemplate;

        private readonly List<ShopRow> _rows = new List<ShopRow>();
        private GameState _state;
        private Economy _economy;

        public bool IsOpen => panel.IsOpen;

        private void Awake()
        {
            rowTemplate.gameObject.SetActive(false);
            panel.HideImmediate();
            closeButton.onClick.AddListener(Close);
            backgroundButton.onClick.AddListener(Close);
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
            _state = game.State;
            _economy = game.Economy;
            if (_state == null) return;

            panel.Show();
            Unsubscribe();
            _state.CatAdded += OnCatAdded;
            _state.CatsMerged += OnCatsMerged;
            _state.CapacityChanged += OnCapacityChanged;
            _economy.Wallet.OnChanged += OnCoinsChanged;
            Refresh();
            scroll.verticalNormalizedPosition = 1f;
        }

        public void Close()
        {
            Unsubscribe();
            panel.Hide();
        }

        private void Unsubscribe()
        {
            if (_state == null) return;
            _state.CatAdded -= OnCatAdded;
            _state.CatsMerged -= OnCatsMerged;
            _state.CapacityChanged -= OnCapacityChanged;
            _economy.Wallet.OnChanged -= OnCoinsChanged;
        }

        private void OnCatAdded(CatData _) => Refresh();

        private void OnCapacityChanged(int _) => Refresh();

        private void OnCatsMerged(CatData a, CatData b, CatData c) => Refresh();

        private void OnCoinsChanged(double _) => Refresh();

        private void Refresh()
        {
            var database = game.CatDatabase;
            int buyable = _state.MaxBuyableLevel;
            int locked = buyable < _state.MaxLevel ? buyable + 1 : 0;
            int shown = locked > 0 ? locked : buyable;

            titleLabel.text = _state.IsFull
                ? Loc.Format("shop_title_full", _state.CatCount, _state.MaxCats)
                : Loc.Format("shop_title", _state.CatCount, _state.MaxCats);

            EnsureRows(shown);
            for (int level = 1; level <= shown; level++)
            {
                var row = _rows[level - 1];
                row.gameObject.SetActive(true);
                if (level == locked)
                {
                    row.ShowLocked(Loc.Format("shop_locked_name", level),
                        Loc.Format("shop_locked_info", level + _state.ShopLevelOffset));
                    continue;
                }

                int buyLevel = level;
                double price = _economy.GetPrice(level);
                row.ShowBuyable(database.Get(level), Loc.Format("shop_buy", NumberFormatter.Format(price)),
                    _economy.CanBuy(level), () => game.TryBuyCat(buyLevel));
            }
            for (int i = shown; i < _rows.Count; i++) _rows[i].gameObject.SetActive(false);
        }

        private void EnsureRows(int count)
        {
            while (_rows.Count < count)
            {
                var row = Instantiate(rowTemplate, rowTemplate.transform.parent);
                row.name = $"Row L{_rows.Count + 1}";
                _rows.Add(row);
            }
        }
    }
}
