using System;
using DG.Tweening;
using UnityEngine;

namespace CatMerge.UI
{
    /// <summary>
    /// Modal overlay: fades the dimmed background in/out and pops the window with a soft overshoot.
    /// Also shrinks the window to fit its parent (the safe area) on small or portrait screens, so a window
    /// designed for 1920×1080 never gets cut off.
    /// </summary>
    [RequireComponent(typeof(CanvasGroup))]
    public sealed class PanelAnimator : MonoBehaviour
    {
        [Tooltip("The window inside the overlay; its parent is the area it must fit into.")]
        [SerializeField] private RectTransform window;
        [SerializeField] private float showDuration = 0.3f;
        [SerializeField] private float hideDuration = 0.16f;
        [SerializeField, Range(0.5f, 1f)] private float popFrom = 0.86f;
        [SerializeField, Range(0.5f, 1f)] private float shrinkTo = 0.94f;
        [Tooltip("Minimal gap between the window and the edge of the safe area, in canvas units.")]
        [SerializeField] private float margin = 24f;

        private CanvasGroup _group;
        private Sequence _sequence;
        private float _fit = 1f;
        private Vector2 _fittedArea;
        private bool _hiding;

        public RectTransform Window => window;

        /// <summary>Visible and not on its way out.</summary>
        public bool IsOpen => gameObject.activeSelf && !_hiding;

        private CanvasGroup Group => _group != null ? _group : _group = GetComponent<CanvasGroup>();

        public void Show()
        {
            bool wasVisible = gameObject.activeSelf && !_hiding;
            gameObject.SetActive(true);
            _hiding = false;
            Group.blocksRaycasts = true;
            Group.interactable = true;
            UpdateFit();
            if (wasVisible) return; // already open: e.g. a popup showing its next entry

            _sequence?.Kill();
            Group.alpha = 0f;
            window.localScale = Vector3.one * (_fit * popFrom);
            _sequence = DOTween.Sequence().SetTarget(this)
                .Join(Fade(1f, showDuration * 0.7f).SetEase(Ease.OutQuad))
                .Join(window.DOScale(_fit, showDuration).SetEase(Ease.OutBack, 1.6f));
        }

        public void Hide(Action onHidden = null)
        {
            if (!gameObject.activeSelf || _hiding)
            {
                onHidden?.Invoke();
                return;
            }
            _hiding = true;
            Group.blocksRaycasts = false; // taps go through to the room while it fades
            Group.interactable = false;

            _sequence?.Kill();
            _sequence = DOTween.Sequence().SetTarget(this)
                .Join(Fade(0f, hideDuration).SetEase(Ease.InQuad))
                .Join(window.DOScale(_fit * shrinkTo, hideDuration).SetEase(Ease.InQuad))
                .OnComplete(() =>
                {
                    HideImmediate();
                    onHidden?.Invoke();
                });
        }

        public void HideImmediate()
        {
            _sequence?.Kill();
            _sequence = null;
            _hiding = false;
            gameObject.SetActive(false);
        }

        private void OnDestroy() => _sequence?.Kill();

        // Screen resized or rotated while open (the safe area frame updates in its own Update).
        private void LateUpdate()
        {
            if (_hiding || !(window.parent is RectTransform area) || area.rect.size == _fittedArea) return;
            _sequence?.Complete(); // a pop-in still running would end on the old scale
            UpdateFit();
            window.localScale = Vector3.one * _fit;
        }

        private Tween Fade(float to, float duration) =>
            DOTween.To(() => Group.alpha, a => Group.alpha = a, to, duration);

        private void UpdateFit()
        {
            var area = window.parent as RectTransform;
            if (area == null) return;
            _fittedArea = area.rect.size;
            Vector2 available = area.rect.size - Vector2.one * (2f * margin);
            Vector2 size = window.rect.size;
            if (size.x <= 0f || size.y <= 0f) return;
            _fit = Mathf.Clamp01(Mathf.Min(available.x / size.x, available.y / size.y));
        }
    }
}
