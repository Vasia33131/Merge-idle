using CatMerge.Data;
using CatMerge.Game;
using UnityEngine;

namespace CatMerge.Views
{
    /// <summary>Paints the wall (camera background) and floor of the current home (see <see cref="HomeDatabase"/>).</summary>
    public sealed class RoomDecor : MonoBehaviour
    {
        [SerializeField] private GameController game;
        [SerializeField] private Camera worldCamera;
        [SerializeField] private SpriteRenderer floor;

        private Sprite _defaultFloor;

        private void Awake() => _defaultFloor = floor.sprite;

        private void Start()
        {
            if (game.Prestige == null) return; // GameController failed to init and already logged why
            game.Prestige.Changed += Apply;
            Apply();
        }

        private void OnDestroy()
        {
            if (game != null && game.Prestige != null) game.Prestige.Changed -= Apply;
        }

        private void Apply()
        {
            var home = game.CurrentHome;
            if (home == null) return;
            worldCamera.backgroundColor = home.wallColor;
            floor.color = home.floorColor;
            floor.sprite = home.floorSprite != null ? home.floorSprite : _defaultFloor;
        }
    }
}
