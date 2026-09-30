using System.Collections.Generic;
using CatMerge.Core;
using CatMerge.Views;
using UnityEngine;

namespace CatMerge.Game
{
    /// <summary>
    /// Every cat pays its income once per <see cref="tickInterval"/> on its own timer and shows a "+X" coin popup.
    /// Timers start at random phases so the popups don't all fire on the same frame.
    /// </summary>
    public sealed class IncomeSystem : MonoBehaviour
    {
        [SerializeField] private GameController game;
        [SerializeField] private CoinPopupPool popups;
        [Tooltip("Seconds between payouts of one cat. Income per second stays the same, only the chunk size changes.")]
        [SerializeField, Min(0.1f)] private float tickInterval = 1f;
        [Tooltip("Popup position relative to the cat's centre.")]
        [SerializeField] private Vector2 popupOffset = new Vector2(0f, 0.7f);

        private readonly Dictionary<int, float> _timers = new Dictionary<int, float>();
        private GameState _state;
        private Economy _economy;

        private void Start()
        {
            _state = game.State;
            _economy = game.Economy;
            if (_state == null)
            {
                enabled = false; // GameController failed to init and already logged why
                return;
            }

            _state.CatAdded += OnCatAdded;
            _state.CatsMerged += OnCatsMerged;
            // Start cats may have been granted before this Start ran.
            foreach (var cat in _state.Cats) OnCatAdded(cat);
        }

        private void OnDestroy()
        {
            if (_state == null) return;
            _state.CatAdded -= OnCatAdded;
            _state.CatsMerged -= OnCatsMerged;
        }

        private void OnCatAdded(CatData cat)
        {
            if (!_timers.ContainsKey(cat.Id)) _timers.Add(cat.Id, Random.Range(0.2f, 1f) * tickInterval);
        }

        private void OnCatsMerged(CatData dragged, CatData target, CatData result)
        {
            _timers.Remove(dragged.Id);
            _timers.Remove(target.Id);
            // The merged cat pops in with an animation; its first payout comes after a full interval.
            _timers.Add(result.Id, tickInterval);
        }

        private void Update()
        {
            float dt = Time.deltaTime;
            var cats = _state.Cats;
            for (int i = 0; i < cats.Count; i++)
            {
                var cat = cats[i];
                float timer = _timers[cat.Id] - dt;
                int ticks = 0;
                while (timer <= 0f)
                {
                    timer += tickInterval;
                    ticks++;
                }
                _timers[cat.Id] = timer;
                if (ticks > 0) Pay(cat, ticks);
            }
        }

        private void Pay(CatData cat, int ticks)
        {
            double amount = _economy.IncomeOf(cat) * tickInterval * ticks;
            if (amount <= 0) return;

            _economy.Earn(amount);
            if (game.TryGetView(cat.Id, out var view)) popups.Show(view.Position + popupOffset, amount);
        }
    }
}
