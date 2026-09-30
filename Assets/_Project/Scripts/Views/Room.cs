using System;
using UnityEngine;
using Random = UnityEngine.Random;

namespace CatMerge.Views
{
    /// <summary>
    /// Rectangular walkable area. Cats wander and land only inside <see cref="Bounds"/>.
    /// Has a landscape and a portrait shape (about the same area); <see cref="RoomCameraFit"/> picks one for the screen.
    /// </summary>
    public sealed class Room : MonoBehaviour
    {
        [SerializeField] private Vector2 size = new Vector2(9f, 6f);
        [Tooltip("Used on portrait screens (phones held upright).")]
        [SerializeField] private Vector2 portraitSize = new Vector2(6.4f, 8.4f);
        [SerializeField] private Vector2 offset;
        [Tooltip("Optional 1x1-unit sprite stretched to fill the room.")]
        [SerializeField] private SpriteRenderer floor;

        /// <summary>Raised when the room switches between landscape and portrait shape.</summary>
        public event Action BoundsChanged;

        public bool IsPortrait { get; private set; }

        private Vector2 CurrentSize => IsPortrait ? portraitSize : size;

        public Rect Bounds => new Rect((Vector2)transform.position + offset - CurrentSize * 0.5f, CurrentSize);

        public void SetPortrait(bool portrait)
        {
            if (portrait == IsPortrait) return;
            IsPortrait = portrait;
            FitFloor();
            BoundsChanged?.Invoke();
        }

        public Vector2 GetRandomPoint(float padding)
        {
            var r = Shrink(padding);
            return new Vector2(Random.Range(r.xMin, r.xMax), Random.Range(r.yMin, r.yMax));
        }

        public Vector2 Clamp(Vector2 point, float padding)
        {
            var r = Shrink(padding);
            return new Vector2(Mathf.Clamp(point.x, r.xMin, r.xMax), Mathf.Clamp(point.y, r.yMin, r.yMax));
        }

        private Rect Shrink(float padding)
        {
            var r = Bounds;
            float px = Mathf.Min(padding, r.width * 0.5f);
            float py = Mathf.Min(padding, r.height * 0.5f);
            return Rect.MinMaxRect(r.xMin + px, r.yMin + py, r.xMax - px, r.yMax - py);
        }

        private void Awake() => FitFloor();

        private void OnValidate()
        {
            size = Vector2.Max(size, Vector2.one);
            portraitSize = Vector2.Max(portraitSize, Vector2.one);
            FitFloor();
        }

        private void FitFloor()
        {
            if (floor == null) return;
            floor.transform.position = Bounds.center;
            floor.transform.localScale = new Vector3(CurrentSize.x, CurrentSize.y, 1f);
        }

        private void OnDrawGizmos()
        {
            var r = Bounds;
            Gizmos.color = new Color(1f, 0.6f, 0.2f, 0.9f);
            Gizmos.DrawWireCube(r.center, r.size);
        }
    }
}
