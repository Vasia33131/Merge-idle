using System;
using System.Collections.Generic;

namespace CatMerge.Core
{
    /// <summary>
    /// The Catpedia: which cat levels the player has ever seen. Survives moving to a new home.
    /// Watches <see cref="GameState"/> itself: every cat that appears (bought, granted or merged) is discovered.
    /// Level 1 is known from the start, so the first kittens don't trigger a "new cat" celebration.
    /// </summary>
    public sealed class Codex
    {
        private readonly bool[] _found;

        public int MaxLevel { get; }
        public int Count { get; private set; }

        /// <summary>A level was seen for the very first time (not raised by <see cref="Restore"/>).</summary>
        public event Action<int> Discovered;

        public Codex(GameState state)
        {
            if (state == null) throw new ArgumentNullException(nameof(state));
            MaxLevel = state.MaxLevel;
            _found = new bool[MaxLevel + 1];
            Mark(GameState.StartLevel);

            state.CatAdded += cat => Discover(cat.Level);
            state.CatsMerged += (a, b, result) => Discover(result.Level);
        }

        public bool IsDiscovered(int level) => level >= GameState.StartLevel && level <= MaxLevel && _found[level];

        /// <summary>Marks the level as seen; true (and <see cref="Discovered"/>) only the first time.</summary>
        public bool Discover(int level)
        {
            if (level < GameState.StartLevel || level > MaxLevel || _found[level]) return false;
            Mark(level);
            Discovered?.Invoke(level);
            return true;
        }

        /// <summary>
        /// Loads saved levels without events; invalid ones are ignored. Every level up to
        /// <paramref name="maxLevelReached"/> counts as seen too: saves from before the Catpedia have no list, and a level
        /// can't be reached without having had the one below it. Call before restoring cats so they don't celebrate.
        /// </summary>
        public void Restore(IEnumerable<int> levels, int maxLevelReached)
        {
            if (levels != null)
            {
                foreach (int level in levels)
                {
                    if (level >= GameState.StartLevel && level <= MaxLevel) Mark(level);
                }
            }
            for (int level = GameState.StartLevel; level <= Math.Min(maxLevelReached, MaxLevel); level++) Mark(level);
        }

        /// <summary>Discovered levels in ascending order, for saving.</summary>
        public int[] ToArray()
        {
            var result = new int[Count];
            int n = 0;
            for (int level = GameState.StartLevel; level <= MaxLevel; level++)
            {
                if (_found[level]) result[n++] = level;
            }
            return result;
        }

        private void Mark(int level)
        {
            if (_found[level]) return;
            _found[level] = true;
            Count++;
        }
    }
}
