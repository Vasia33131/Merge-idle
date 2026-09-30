using CatMerge.Audio;
using CatMerge.Core;
using CatMerge.Input;
using CatMerge.Views;
using UnityEngine;

namespace CatMerge.Game
{
    /// <summary>
    /// When cats meow: the new cat after a merge, a tapped cat, and now and then a random cat on its own.
    /// Each level uses its own <see cref="Data.CatConfig.meowClip"/>; empty slots meow silently (the cat still wiggles).
    /// </summary>
    public sealed class CatMeows : MonoBehaviour
    {
        [SerializeField] private GameController game;
        [SerializeField] private AudioService audioService;
        [SerializeField] private DragController drag;
        [Tooltip("Seconds between spontaneous meows (random in this range).")]
        [SerializeField] private Vector2 ambientInterval = new Vector2(8f, 20f);

        private GameState _state;
        private float _ambientTimer;

        private void Start()
        {
            _state = game.State;
            if (_state == null) return; // GameController failed to init and already logged why

            _state.CatsMerged += OnCatsMerged;
            drag.CatTapped += OnCatTapped;
            ScheduleAmbient();
        }

        private void OnDestroy()
        {
            if (_state != null) _state.CatsMerged -= OnCatsMerged;
            if (drag != null) drag.CatTapped -= OnCatTapped;
        }

        private void Update()
        {
            if (_state == null || !AudioService.IsUnlocked) return;

            _ambientTimer -= Time.deltaTime;
            if (_ambientTimer > 0f) return;
            ScheduleAmbient();

            var cat = PickAmbientCat();
            if (cat != null) Meow(cat, important: false);
        }

        private void OnCatsMerged(CatData dragged, CatData target, CatData result)
        {
            // No wiggle here: the new cat is already playing its spawn pop.
            audioService.PlayMeow(game.CatDatabase.Get(result.Level).meowClip, important: true);
        }

        private void OnCatTapped(CatView cat) => Meow(cat, important: false);

        private void Meow(CatView cat, bool important)
        {
            var clip = game.CatDatabase.Get(cat.Level).meowClip;
            if (audioService.PlayMeow(clip, important)) cat.PlayMeow();
        }

        private CatView PickAmbientCat()
        {
            var views = game.Views;
            if (views.Count == 0) return null;

            int skip = Random.Range(0, views.Count);
            foreach (var view in views)
            {
                if (skip-- > 0) continue;
                return view.IsDragging ? null : view;
            }
            return null;
        }

        private void ScheduleAmbient() => _ambientTimer = Random.Range(ambientInterval.x, ambientInterval.y);
    }
}
