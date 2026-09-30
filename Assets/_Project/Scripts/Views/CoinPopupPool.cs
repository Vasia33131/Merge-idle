using CatMerge.Core;
using UnityEngine;

namespace CatMerge.Views
{
    /// <summary>
    /// Fixed ring of <see cref="CoinPopup"/>s created in Awake — nothing is instantiated at runtime.
    /// When all are busy, the oldest one is restarted at the new spot.
    /// </summary>
    public sealed class CoinPopupPool : MonoBehaviour
    {
        [SerializeField] private CoinPopup prefab;
        [Tooltip("One popup lives ~1s and each cat pays once per second, so max room capacity (30 with upgrades) x2 is plenty.")]
        [SerializeField, Min(1)] private int size = 60;

        private CoinPopup[] _popups;
        private int _next;

        private void Awake()
        {
            _popups = new CoinPopup[size];
            for (int i = 0; i < size; i++)
            {
                var popup = Instantiate(prefab, transform);
                popup.name = $"{prefab.name} {i}";
                popup.Finished += Release;
                popup.gameObject.SetActive(false);
                _popups[i] = popup;
            }
        }

        public void Show(Vector2 position, double amount)
        {
            var popup = _popups[_next];
            _next = (_next + 1) % _popups.Length;
            popup.Show(new Vector3(position.x, position.y, 0f), "+" + NumberFormatter.Format(amount));
        }

        private static void Release(CoinPopup popup) => popup.gameObject.SetActive(false);
    }
}
