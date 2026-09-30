using System;
using System.Collections.Generic;

namespace CatMerge.Core
{
    /// <summary>
    /// Everything that survives a restart. Plain public fields so Unity's JsonUtility can (de)serialize it.
    /// Bump <see cref="CurrentVersion"/> and migrate in SaveService when the layout changes incompatibly;
    /// new fields with sensible defaults don't need a bump.
    /// </summary>
    [Serializable]
    public sealed class SaveData
    {
        public const int CurrentVersion = 1;

        public int version = CurrentVersion;
        public double coins;
        public int maxLevelReached = GameState.StartLevel;
        /// <summary>Kittens bought per level, index = level (0 unused).</summary>
        public long[] purchases = Array.Empty<long>();
        public List<CatSave> cats = new List<CatSave>();
        /// <summary>Bought upgrade levels by id (see <see cref="Upgrades"/>); upgrades not listed are at level 0.</summary>
        public List<UpgradeSave> upgrades = new List<UpgradeSave>();
        /// <summary>Coins cats earned since the last move (see <see cref="Economy.RunEarned"/>).</summary>
        public double runEarned;

        // Kept across moves (see Prestige, Codex).
        public long fish;
        public int moves;
        /// <summary>Catpedia: discovered cat levels. Older saves have none; levels up to maxLevelReached count as seen.</summary>
        public int[] codex = Array.Empty<int>();
        /// <summary>
        /// Written by a move to a new home: only the fields kept across moves are valid, the run starts fresh
        /// (start coins and kittens) on the next load.
        /// </summary>
        public bool startNewRun;

        /// <summary>
        /// Latest UTC time (unix seconds) the game has ever seen. Never moves backwards, so turning the clock
        /// back doesn't give offline income twice.
        /// </summary>
        public long lastSeenUnix;
    }

    [Serializable]
    public sealed class CatSave
    {
        public int level;
        public float x;
        public float y;
    }

    [Serializable]
    public sealed class UpgradeSave
    {
        public string id;
        public int level;
    }
}
