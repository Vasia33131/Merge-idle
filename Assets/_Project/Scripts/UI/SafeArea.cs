using UnityEngine;

namespace CatMerge.UI
{
    /// <summary>
    /// Fits this RectTransform to <see cref="Screen.safeArea"/> (notches, rounded corners, home indicator).
    /// Its parent must cover the whole screen. Re-applies when the screen is resized or rotated.
    /// </summary>
    [RequireComponent(typeof(RectTransform))]
    public sealed class SafeArea : MonoBehaviour
    {
        private RectTransform _rect;
        private Rect _applied;
        private Vector2Int _screen;

        private void Awake()
        {
            _rect = (RectTransform)transform;
            Apply();
        }

        private void Update() => Apply();

        private void Apply()
        {
            var safe = Screen.safeArea;
            var screen = new Vector2Int(Screen.width, Screen.height);
            if (safe == _applied && screen == _screen) return;
            _applied = safe;
            _screen = screen;
            if (screen.x <= 0 || screen.y <= 0) return;

            _rect.anchorMin = new Vector2(safe.xMin / screen.x, safe.yMin / screen.y);
            _rect.anchorMax = new Vector2(safe.xMax / screen.x, safe.yMax / screen.y);
            _rect.offsetMin = _rect.offsetMax = Vector2.zero;
        }
    }
}
