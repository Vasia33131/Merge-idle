using System;
using CatMerge.Game;
using CatMerge.Views;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;

namespace CatMerge.Input
{
    /// <summary>
    /// Pick up a cat with mouse or touch, drag it, drop it. <see cref="Pointer.current"/> covers both
    /// devices (it switches to the touchscreen as soon as it is touched), which is what WebGL needs.
    /// </summary>
    public sealed class DragController : MonoBehaviour
    {
        [SerializeField] private Camera worldCamera;
        [SerializeField] private GameController game;
        [SerializeField] private Room room;
        [Tooltip("A press released within this time and distance (fraction of screen height) counts as a tap.")]
        [SerializeField] private float tapMaxDuration = 0.35f;
        [SerializeField, Range(0f, 0.1f)] private float tapMaxDistance = 0.02f;

        private CatView _dragged;
        private Vector2 _grabOffset;
        private Vector2 _pressScreen;
        private float _pressTime;
        private bool _movedTooFar;

        /// <summary>A cat was pressed and released without being dragged away.</summary>
        public event Action<CatView> CatTapped;

        private void Update()
        {
            var pointer = Pointer.current;
            if (pointer == null) return;

            Vector2 screen = pointer.position.ReadValue();
            Vector2 world = ScreenToWorld(screen);

            if (_dragged == null)
            {
                if (pointer.press.wasPressedThisFrame && !IsPointerOverUI() && TryGrab(world))
                {
                    _pressScreen = screen;
                    _pressTime = Time.unscaledTime;
                    _movedTooFar = false;
                }
                return;
            }

            if (pointer.press.isPressed)
            {
                if ((screen - _pressScreen).magnitude > tapMaxDistance * Screen.height) _movedTooFar = true;
                // Keep the cat's centre inside the room while dragging.
                _dragged.MoveTo(room.Clamp(world + _grabOffset, 0f));
            }
            else
            {
                Release(allowTap: true);
            }
        }

        private void OnDisable()
        {
            // Skip during scene teardown: the cat may already be inactive and can't run its bounce.
            if (_dragged != null && _dragged.isActiveAndEnabled) Release(allowTap: false);
            _dragged = null;
        }

        private bool TryGrab(Vector2 world)
        {
            var cat = game.FindCatAt(world);
            if (cat == null) return false;

            _dragged = cat;
            _grabOffset = cat.Position - world;
            cat.BeginDrag();
            return true;
        }

        private void Release(bool allowTap)
        {
            var cat = _dragged;
            _dragged = null;
            bool tap = allowTap && !_movedTooFar && Time.unscaledTime - _pressTime <= tapMaxDuration;
            game.Drop(cat);
            // A tap onto an overlapping twin merges it away; that cat must not meow.
            if (tap && game.TryGetView(cat.CatId, out _)) CatTapped?.Invoke(cat);
        }

        private Vector2 ScreenToWorld(Vector2 screen)
        {
            return worldCamera.ScreenToWorldPoint(new Vector3(screen.x, screen.y, -worldCamera.transform.position.z));
        }

        private static bool IsPointerOverUI()
        {
            var es = EventSystem.current;
            return es != null && es.IsPointerOverGameObject();
        }
    }
}
