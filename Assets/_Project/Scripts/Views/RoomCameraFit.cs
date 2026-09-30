using UnityEngine;
using UnityEngine.UI;

namespace CatMerge.Views
{
    /// <summary>
    /// Keeps the whole room visible between the top and bottom HUD for any screen: 16:9 and 4:3 monitors,
    /// portrait phones, notches (safe area). Picks the room's portrait shape on upright screens.
    /// HUD space is given in canvas units, so it matches the UI however the <see cref="CanvasScaler"/> scales it.
    /// </summary>
    [RequireComponent(typeof(Camera))]
    public sealed class RoomCameraFit : MonoBehaviour
    {
        [SerializeField] private Room room;
        [Tooltip("Scaler of the HUD canvas, used to turn the reserved HUD space into pixels.")]
        [SerializeField] private CanvasScaler hudScaler;
        [Tooltip("World units of floor margin around the room.")]
        [SerializeField] private float padding = 0.4f;
        [Tooltip("Canvas units at the top of the safe area kept free for coins, income and the counter.")]
        [SerializeField] private float topHud = 240f;
        [Tooltip("Top space on portrait screens, where the settings / Catpedia / move buttons sit above the room.")]
        [SerializeField] private float portraitTopHud = 430f;
        [Tooltip("Canvas units at the bottom of the safe area kept free for the buy / shop / upgrades buttons.")]
        [SerializeField] private float bottomHud = 220f;

        private Camera _camera;
        private Vector2Int _screen;
        private Rect _safeArea;

        private void Awake()
        {
            _camera = GetComponent<Camera>();
            // Before any Start: cats restored from the save are clamped into the right room shape.
            Fit();
        }

        private void LateUpdate()
        {
            if (Screen.width == _screen.x && Screen.height == _screen.y && Screen.safeArea == _safeArea) return;
            Fit();
        }

        private void Fit()
        {
            _screen = new Vector2Int(Screen.width, Screen.height);
            _safeArea = Screen.safeArea;
            if (_screen.x <= 0 || _screen.y <= 0) return;

            var safe = _safeArea.width > 0f && _safeArea.height > 0f ? _safeArea : new Rect(0f, 0f, _screen.x, _screen.y);
            bool portrait = safe.height > safe.width;
            room.SetPortrait(portrait);

            // Free band between the HUD rows, in pixels.
            float pixelsPerUnit = CanvasScale();
            float top = (portrait ? portraitTopHud : topHud) * pixelsPerUnit;
            float bottom = bottomHud * pixelsPerUnit;
            var band = Rect.MinMaxRect(safe.xMin, safe.yMin + bottom, safe.xMax, safe.yMax - top);
            if (band.height < safe.height * 0.3f) band = safe; // tiny window: show the room rather than the gaps

            var view = room.Bounds;
            view.min -= Vector2.one * padding;
            view.max += Vector2.one * padding;

            float worldPerPixel = Mathf.Max(view.width / band.width, view.height / band.height);
            _camera.orthographicSize = _screen.y * worldPerPixel * 0.5f;

            // Put the room's centre on the band's centre.
            Vector2 bandOffset = band.center - new Vector2(_screen.x, _screen.y) * 0.5f;
            Vector2 position = view.center - bandOffset * worldPerPixel;
            transform.position = new Vector3(position.x, position.y, transform.position.z);
        }

        /// <summary>Screen pixels per canvas unit, same formula as CanvasScaler's "Match Width Or Height".</summary>
        private float CanvasScale()
        {
            if (hudScaler == null || hudScaler.uiScaleMode != CanvasScaler.ScaleMode.ScaleWithScreenSize) return 1f;
            var reference = hudScaler.referenceResolution;
            float logWidth = Mathf.Log(_screen.x / reference.x, 2f);
            float logHeight = Mathf.Log(_screen.y / reference.y, 2f);
            return Mathf.Pow(2f, Mathf.Lerp(logWidth, logHeight, hudScaler.matchWidthOrHeight));
        }
    }
}
