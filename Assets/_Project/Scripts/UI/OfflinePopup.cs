using CatMerge.Core;
using CatMerge.Game;
using CatMerge.Localization;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace CatMerge.UI
{
    /// <summary>
    /// "While you were away, the cats earned X". The coins are already in the wallet when it shows
    /// (so closing the tab doesn't lose them); the button just closes the popup.
    /// </summary>
    public sealed class OfflinePopup : MonoBehaviour
    {
        [SerializeField] private SaveService saveService;
        [Tooltip("Full-screen overlay; animates the window in and out.")]
        [SerializeField] private PanelAnimator panel;
        [SerializeField] private TMP_Text titleLabel;
        [SerializeField] private TMP_Text bodyLabel;
        [SerializeField] private TMP_Text amountLabel;
        [SerializeField] private Button collectButton;
        [SerializeField] private TMP_Text collectLabel;

        private void Awake()
        {
            panel.HideImmediate();
            collectButton.onClick.AddListener(Close);
        }

        private void Start()
        {
            saveService.OfflineIncomeGranted += Show;
            // The first payout may have happened before this Start ran.
            var pending = saveService.PendingOfflineIncome;
            if (pending.HasValue) Show(pending.Value.coins, pending.Value.seconds);
        }

        private void OnDestroy()
        {
            collectButton.onClick.RemoveListener(Close);
            if (saveService != null) saveService.OfflineIncomeGranted -= Show;
        }

        private void Show(double coins, double seconds)
        {
            saveService.ClearPendingOfflineIncome();

            titleLabel.text = Loc.Get("offline_title");
            bodyLabel.text = Loc.Format("offline_body", Loc.FormatDuration(seconds));
            amountLabel.text = "+" + NumberFormatter.Format(coins);
            collectLabel.text = Loc.Get("offline_collect");

            panel.Show();
        }

        private void Close() => panel.Hide();
    }
}
