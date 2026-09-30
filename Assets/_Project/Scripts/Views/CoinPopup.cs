using System;
using DG.Tweening;
using TMPro;
using UnityEngine;

namespace CatMerge.Views
{
    /// <summary>
    /// World-space "+X" with a coin icon that floats up and fades. Owned and recycled by <see cref="CoinPopupPool"/>;
    /// its DOTween sequence is built once and replayed, so showing a popup allocates no objects or tweens.
    /// </summary>
    public sealed class CoinPopup : MonoBehaviour
    {
        [Tooltip("Moved and scaled by the animation; the root stays where the popup was spawned.")]
        [SerializeField] private Transform content;
        [SerializeField] private Transform coin;
        [Tooltip("Coin parts that fade together with the text.")]
        [SerializeField] private SpriteRenderer[] coinSprites;
        [SerializeField] private TMP_Text label;
        [SerializeField] private float gap = 0.06f;

        [Header("Animation")]
        [SerializeField] private float rise = 0.8f;
        [SerializeField] private float duration = 1f;
        [SerializeField] private float popDuration = 0.25f;
        [SerializeField, Range(0f, 1f)] private float startScale = 0.5f;
        [Tooltip("Fraction of the duration before fading starts.")]
        [SerializeField, Range(0f, 1f)] private float fadeStart = 0.45f;

        private Sequence _sequence;
        private float _alpha = 1f;

        /// <summary>Called when the animation ends; the pool uses it to deactivate the popup.</summary>
        public event Action<CoinPopup> Finished;

        private void Awake()
        {
            ResetVisual();
            _sequence = DOTween.Sequence()
                .Insert(0f, content.DOLocalMoveY(rise, duration).SetEase(Ease.OutCubic))
                .Insert(0f, content.DOScale(1f, popDuration).SetEase(Ease.OutBack))
                .Insert(duration * fadeStart, DOTween.To(() => _alpha, SetAlpha, 0f, duration * (1f - fadeStart)).SetEase(Ease.InQuad))
                .SetAutoKill(false)
                .SetRecyclable(false)
                .Pause()
                .OnComplete(() => Finished?.Invoke(this));
        }

        private void OnDestroy() => _sequence?.Kill();

        public void Show(Vector3 position, string text)
        {
            transform.position = position;
            label.text = text;
            Layout();
            ResetVisual();
            gameObject.SetActive(true);
            _sequence.Restart();
        }

        /// <summary>Centres "coin + text" as one group; the text width changes with the amount.</summary>
        private void Layout()
        {
            float coinWidth = coin.localScale.x;
            float textWidth = label.GetPreferredValues(label.text).x;
            float left = -(coinWidth + gap + textWidth) * 0.5f;
            coin.localPosition = new Vector3(left + coinWidth * 0.5f, 0f, 0f);
            label.rectTransform.localPosition = new Vector3(left + coinWidth + gap, 0f, 0f);
        }

        private void ResetVisual()
        {
            content.localPosition = Vector3.zero;
            content.localScale = Vector3.one * startScale;
            SetAlpha(1f);
        }

        private void SetAlpha(float alpha)
        {
            _alpha = alpha;
            label.alpha = alpha;
            foreach (var sprite in coinSprites)
            {
                var c = sprite.color;
                c.a = alpha;
                sprite.color = c;
            }
        }
    }
}
