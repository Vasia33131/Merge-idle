using CatMerge.Data;
using CatMerge.Game;
using UnityEngine;

namespace CatMerge.Views
{
    /// <summary>
    /// Paints the wall (camera background) and floor of the current home (see <see cref="HomeDatabase"/>) and
    /// brings the upgrade items into the room (<see cref="RoomProps"/>).
    /// </summary>
    public sealed class RoomDecor : MonoBehaviour
    {
        [SerializeField] private GameController game;
        [SerializeField] private Camera worldCamera;
        [SerializeField] private SpriteRenderer floor;

        private Sprite _defaultFloor;
        private Room _room;

        private void Awake()
        {
            _defaultFloor = floor.sprite;
            _room = GetComponent<Room>();
            // Added here so scenes built before the props existed pick them up without a rebuild.
            var props = GetComponent<RoomProps>();
            if (props == null) props = gameObject.AddComponent<RoomProps>();
            props.Init(game, _room, _defaultFloor);
        }

        private void Start()
        {
            if (game.Prestige == null) return; // GameController failed to init and already logged why
            game.Prestige.Changed += Apply;
            if (_room != null) _room.BoundsChanged += Apply;
            Apply();
        }

        private void OnDestroy()
        {
            if (game != null && game.Prestige != null) game.Prestige.Changed -= Apply;
            if (_room != null) _room.BoundsChanged -= Apply;
        }

        private void Apply()
        {
            var home = game.CurrentHome;
            if (home == null) return;
            worldCamera.backgroundColor = home.wallColor;
            floor.color = home.floorColor;
            var picture = _room != null && _room.IsPortrait && home.floorSpritePortrait != null
                ? home.floorSpritePortrait
                : home.floorSprite;
            floor.sprite = picture != null ? picture : _defaultFloor;
        }
    }
}
