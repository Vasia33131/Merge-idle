using CatMerge.Core;
using CatMerge.Game;
using CatMerge.Localization;
using DG.Tweening;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace CatMerge.UI
{
    /// <summary>
    /// Top: coins, income per second, fish, cat counter, settings / Catpedia buttons and the "move" button once the top
    /// cat is reached. Bottom: quick-buy button for the best kitten the shop sells
    /// (disabled when coins are short or the room is full) and the buttons that open the <see cref="ShopPanel"/> and <see cref="UpgradesPanel"/>.
    /// </summary>
    public sealed class HudPanel : MonoBehaviour
    {
        [SerializeField] private GameController game;
        [SerializeField] private ShopPanel shop;
        [SerializeField] private UpgradesPanel upgrades;
        [SerializeField] private SettingsPanel settings;
        [SerializeField] private CodexPanel codex;
        [SerializeField] private PrestigePanel prestige;

        [Header("Top")]
        [SerializeField] private TMP_Text coinsLabel;
        [SerializeField] private Transform coinsIcon;
        [SerializeField] private TMP_Text incomeLabel;
        [SerializeField] private TMP_Text counterLabel;
        [SerializeField] private Button settingsButton;
        [Tooltip("Fish and their income bonus; hidden until the first move.")]
        [SerializeField] private TMP_Text fishLabel;
        [SerializeField] private Button codexButton;
        [Tooltip("Shown once the top cat level is reached in this run.")]
        [SerializeField] private Button moveButton;

        [Header("Bottom")]
        [SerializeField] private Button buyButton;
        [SerializeField] private TMP_Text buyCaption;
        [SerializeField] private TMP_Text buyPrice;
        [SerializeField] private Button shopButton;
        [SerializeField] private Button upgradesButton;

        [Header("Animation")]
        [Tooltip("Seconds the coin counter takes to count up to a new balance.")]
        [SerializeField] private float countUpDuration = 0.35f;

        private GameState _state;
        private Economy _economy;
        private Tween _coinsTween;
        private double _shownCoins;

        private void Start()
        {
            _state = game.State;
            _economy = game.Economy;
            if (_state == null) return; // GameController failed to init and already logged why

            _state.CatAdded += OnCatAdded;
            _state.CatsMerged += OnCatsMerged;
            _state.CapacityChanged += OnCapacityChanged;
            _economy.Wallet.OnChanged += OnCoinsChanged;
            _economy.IncomeChanged += OnIncomeChanged;
            buyButton.onClick.AddListener(OnBuyClicked);
            shopButton.onClick.AddListener(shop.Open);
            upgradesButton.onClick.AddListener(upgrades.Open);
            settingsButton.onClick.AddListener(settings.Open);
            codexButton.onClick.AddListener(codex.Open);
            moveButton.onClick.AddListener(prestige.Open);
            game.Prestige.Changed += OnPrestigeChanged;
            LocalizationService.LanguageChanged += OnLanguageChanged;
            SetShownCoins(_economy.Wallet.Coins);
            OnIncomeChanged(_economy.IncomePerSec);
            OnPrestigeChanged();
            Refresh();
        }

        private void OnDestroy()
        {
            _coinsTween?.Kill();
            buyButton.transform.DOKill();
            coinsIcon.DOKill();
            buyButton.onClick.RemoveListener(OnBuyClicked);
            shopButton.onClick.RemoveListener(shop.Open);
            upgradesButton.onClick.RemoveListener(upgrades.Open);
            settingsButton.onClick.RemoveListener(settings.Open);
            codexButton.onClick.RemoveListener(codex.Open);
            moveButton.onClick.RemoveListener(prestige.Open);
            moveButton.transform.DOKill();
            LocalizationService.LanguageChanged -= OnLanguageChanged;
            if (_state == null) return;
            game.Prestige.Changed -= OnPrestigeChanged;
            _state.CatAdded -= OnCatAdded;
            _state.CatsMerged -= OnCatsMerged;
            _state.CapacityChanged -= OnCapacityChanged;
            _economy.Wallet.OnChanged -= OnCoinsChanged;
            _economy.IncomeChanged -= OnIncomeChanged;
        }

        // Button captions are LocalizedText; only the labels with numbers in them are redrawn here.
        private void OnLanguageChanged()
        {
            OnIncomeChanged(_economy.IncomePerSec);
            OnPrestigeChanged();
            Refresh();
        }

        private void OnCatAdded(CatData _) => Refresh();

        private void OnCatsMerged(CatData a, CatData b, CatData c) => Refresh();

        private void OnCapacityChanged(int _) => Refresh();

        private void OnCoinsChanged(double coins)
        {
            _coinsTween?.Kill();
            if (coins < _shownCoins)
            {
                SetShownCoins(coins); // spending shows at once so the price visibly leaves the counter
            }
            else
            {
                _coinsTween = DOTween.To(() => _shownCoins, SetShownCoins, coins, countUpDuration).SetEase(Ease.OutQuad);
            }
            Refresh();
        }

        private void OnIncomeChanged(double perSec)
        {
            incomeLabel.text = Loc.Format("hud_income", NumberFormatter.Format(perSec));
        }

        private void OnBuyClicked()
        {
            if (!game.TryBuyCat(_state.MaxBuyableLevel)) return;

            Punch(buyButton.transform, 0.08f);
            Punch(coinsIcon, 0.25f);
        }

        private void Refresh()
        {
            int level = _state.MaxBuyableLevel;
            counterLabel.text = Loc.Format("cats_counter", _state.CatCount, _state.MaxCats);
            buyCaption.text = _state.IsFull
                ? Loc.Get("buy_full")
                : Loc.Format("buy_caption", Loc.Get(game.CatDatabase.Get(level).nameKey), level);
            buyPrice.text = NumberFormatter.Format(_economy.GetPrice(level));
            buyButton.interactable = _economy.CanBuy(level);
            ShowMoveButton(game.Prestige.IsAvailable);
        }

        private void OnPrestigeChanged()
        {
            var prestige = game.Prestige;
            fishLabel.gameObject.SetActive(prestige.Fish > 0);
            fishLabel.text = Loc.Format("hud_fish", NumberFormatter.Format(prestige.Fish),
                NumberFormatter.Format(System.Math.Round(prestige.Fish * prestige.BonusPerFish * 100)));
        }

        /// <summary>Shows the move button with a gentle breathing pulse so it gets noticed.</summary>
        private void ShowMoveButton(bool show)
        {
            var go = moveButton.gameObject;
            if (go.activeSelf == show) return;
            go.SetActive(show);
            var target = moveButton.transform;
            target.DOKill();
            target.localScale = Vector3.one;
            if (show) target.DOScale(1.06f, 0.7f).SetEase(Ease.InOutSine).SetLoops(-1, LoopType.Yoyo);
        }

        private void SetShownCoins(double value)
        {
            _shownCoins = value;
            coinsLabel.text = NumberFormatter.Format(value);
        }

        private static void Punch(Transform target, float strength)
        {
            target.DOKill(complete: true);
            target.DOPunchScale(Vector3.one * strength, 0.25f, vibrato: 6, elasticity: 0.8f);
        }
    }
}
