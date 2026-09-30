using CatMerge.Audio;
using CatMerge.Localization;
using DG.Tweening;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace CatMerge.UI
{
    /// <summary>
    /// "Tap to start" overlay. Browsers keep audio muted until the first user gesture, so the tap that closes
    /// this screen is what unlocks sound and starts the music. Shown once per session (not again after a reset).
    /// </summary>
    public sealed class StartScreen : MonoBehaviour
    {
        [SerializeField] private AudioService audioService;
        [Tooltip("Full-screen overlay that blocks the room until tapped.")]
        [SerializeField] private CanvasGroup root;
        [SerializeField] private Button button;
        [SerializeField] private TMP_Text label;
        [SerializeField] private float fadeOut = 0.4f;

        private void Awake()
        {
            if (AudioService.IsUnlocked)
            {
                root.gameObject.SetActive(false);
                return;
            }
            root.gameObject.SetActive(true);
            root.alpha = 1f;
            button.onClick.AddListener(OnTap);
        }

        private void Start()
        {
            if (!root.gameObject.activeSelf) return;

            label.text = Loc.Get("start_tap"); // Loc.Current is set in GameController.Awake
            DOTween.To(() => label.alpha, a => label.alpha = a, 0.35f, 0.9f)
                .SetEase(Ease.InOutSine).SetLoops(-1, LoopType.Yoyo).SetTarget(label);
        }

        private void OnDestroy()
        {
            label.DOKill();
            root.DOKill();
            button.onClick.RemoveListener(OnTap);
        }

        private void OnTap()
        {
            button.onClick.RemoveListener(OnTap);
            root.blocksRaycasts = false;
            audioService.Unlock();

            label.DOKill();
            DOTween.To(() => root.alpha, a => root.alpha = a, 0f, fadeOut).SetEase(Ease.OutQuad).SetTarget(root)
                .OnComplete(() => root.gameObject.SetActive(false));
        }
    }
}
