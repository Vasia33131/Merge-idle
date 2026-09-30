using System.Collections.Generic;
using CatMerge.Core;
using CatMerge.Game;
using CatMerge.Localization;
using DG.Tweening;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace CatMerge.UI
{
    /// <summary>
    /// Modal upgrade shop: one row per <see cref="UpgradeDef"/> showing "current → next" and the price.
    /// Rows that need a higher cat level are shown locked. Stays open after a purchase.
    /// </summary>
    public sealed class UpgradesPanel : MonoBehaviour
    {
        [SerializeField] private GameController game;
        [Tooltip("Full-screen overlay; animates the window in and out.")]
        [SerializeField] private PanelAnimator panel;
        [SerializeField] private Button closeButton;
        [Tooltip("Dimmed background; tapping it closes the panel.")]
        [SerializeField] private Button backgroundButton;
        [SerializeField] private TMP_Text titleLabel;
        [SerializeField] private ScrollRect scroll;
        [SerializeField] private ShopRow rowTemplate;

        private readonly List<ShopRow> _rows = new List<ShopRow>();
        private GameState _state;
        private Economy _economy;
        private Upgrades _upgrades;

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
            foreach (var row in _rows) row.BuyButton.DOKill();
        }

        private void Update()
        {
            if (IsOpen && Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame) Close();
        }

        public void Open()
        {
            _state = game.State;
            _economy = game.Economy;
            _upgrades = game.Upgrades;
            if (_state == null) return;

            panel.Show();
            Unsubscribe();
            _state.CatAdded += OnCatAdded;
            _state.CatsMerged += OnCatsMerged;
            _state.CapacityChanged += OnCapacityChanged;
            _economy.Wallet.OnChanged += OnCoinsChanged;
            _upgrades.Changed += Refresh;
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
            _upgrades.Changed -= Refresh;
        }

        private void OnCatAdded(CatData _) => Refresh();

        private void OnCatsMerged(CatData a, CatData b, CatData c) => Refresh();

        private void OnCapacityChanged(int _) => Refresh();

        private void OnCoinsChanged(double _) => Refresh();

        private void Refresh()
        {
            titleLabel.text = Loc.Get("upgrades_title");

            var defs = _upgrades.Defs;
            EnsureRows(defs.Count);
            for (int i = 0; i < defs.Count; i++)
            {
                var row = _rows[i];
                row.gameObject.SetActive(true);
                ShowRow(row, defs[i]);
            }
            for (int i = defs.Count; i < _rows.Count; i++) _rows[i].gameObject.SetActive(false);
        }

        private void ShowRow(ShopRow row, UpgradeDef def)
        {
            var config = game.UpgradeDatabase.Get(def.Id);
            string title = config != null ? Loc.Get(config.nameKey) : def.Id;
            var status = _upgrades.GetStatus(def);

            if (status == UpgradeStatus.Locked)
            {
                row.ShowLocked(title, Loc.Format("shop_locked_info", def.UnlockAtCatLevel));
                return;
            }

            Color color = config != null ? config.color : Color.white;
            string swatchText = SwatchText(def);
            string button = status == UpgradeStatus.MaxLevel
                ? Loc.Get("upg_max")
                : Loc.Format("shop_buy", NumberFormatter.Format(_upgrades.GetPrice(def)));
            string info = status == UpgradeStatus.RoomFull ? Loc.Get("buy_full") : Describe(def);

            if (def.Effect == UpgradeEffect.BestKitten)
            {
                // Swatch looks like the kitten it sells.
                int level = _upgrades.BestKittenLevel(def);
                color = game.CatDatabase.Get(level).color;
                swatchText = level.ToString();
                title = Loc.Format("upg_best_kitten_title", title, level);
            }
            else if (def.MaxLevel > 0)
            {
                title = Loc.Format("upg_title_level", title, _upgrades.GetLevel(def), def.MaxLevel);
            }

            row.Show(color, swatchText, title, info, button, status == UpgradeStatus.Available, () => Buy(row, def));
        }

        private void Buy(ShopRow row, UpgradeDef def)
        {
            if (!_upgrades.TryBuy(def)) return;
            row.BuyButton.DOKill(complete: true);
            row.BuyButton.DOPunchScale(Vector3.one * 0.08f, 0.25f, vibrato: 6, elasticity: 0.8f);
        }

        /// <summary>"Spots: 12 → 14", or just the current value once maxed.</summary>
        private string Describe(UpgradeDef def)
        {
            double now = _upgrades.Total(def.Effect);
            double next = now + def.Value;
            bool maxed = _upgrades.IsMaxed(def);
            var balance = game.Balance;

            switch (def.Effect)
            {
                case UpgradeEffect.RoomCapacity:
                    return Pair("upg_capacity_info", _upgrades.Capacity.ToString(),
                        (balance.capacity + (int)System.Math.Round(next)).ToString(), maxed);
                case UpgradeEffect.IncomeMultiplier:
                    return Pair("upg_income_info", "×" + Multiplier(1 + now), "×" + Multiplier(1 + next), maxed);
                case UpgradeEffect.OfflineHours:
                    return Pair("upg_offline_time_info", Loc.FormatDuration(game.OfflineMaxSeconds),
                        Loc.FormatDuration(game.OfflineMaxSeconds + def.Value * 3600), maxed);
                case UpgradeEffect.OfflineRate:
                    return Pair("upg_offline_rate_info", Percent(game.OfflineRate),
                        Percent(System.Math.Min(1, balance.offlineIncomeRate + next)), maxed);
                case UpgradeEffect.BestKitten:
                    return Loc.Format("upg_best_kitten_info", (int)System.Math.Round(def.Value));
                default:
                    return string.Empty;
            }
        }

        private static string Pair(string key, string now, string next, bool maxed) =>
            Loc.Format(key, maxed ? now : $"{now} → {next}");

        private static string SwatchText(UpgradeDef def)
        {
            switch (def.Effect)
            {
                case UpgradeEffect.RoomCapacity: return "+" + def.Value;
                case UpgradeEffect.IncomeMultiplier:
                case UpgradeEffect.OfflineRate: return "%";
                case UpgradeEffect.OfflineHours: return Loc.Format("upg_hours_short", def.Value);
                default: return "?";
            }
        }

        private static string Multiplier(double value) => value.ToString("0.##", System.Globalization.CultureInfo.InvariantCulture);

        private static string Percent(double rate) => Mathf.RoundToInt((float)(rate * 100)) + "%";

        private void EnsureRows(int count)
        {
            while (_rows.Count < count)
            {
                var row = Instantiate(rowTemplate, rowTemplate.transform.parent);
                row.name = $"Row {_rows.Count + 1}";
                _rows.Add(row);
            }
        }
    }
}
