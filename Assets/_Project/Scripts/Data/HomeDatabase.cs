using System;
using System.Collections.Generic;
using UnityEngine;

namespace CatMerge.Data
{
    /// <summary>Look of one home: wall (camera background) and floor. Created by Tools/CatMerge/Generate Home Configs.</summary>
    [Serializable]
    public sealed class HomeConfig
    {
        [Tooltip("Loc key of the home's name, e.g. home_01_name.")]
        public string nameKey;
        [Tooltip("Everything around the floor (camera background). Keep it light: the HUD text is dark.")]
        public Color wallColor = Color.white;
        public Color floorColor = Color.white;
        [Tooltip("Optional floor picture; empty keeps the placeholder square. Tinted by Floor Color and stretched " +
                 "over the room, so import it as 1 unit across (Pixels Per Unit = width).")]
        public Sprite floorSprite;
        [Tooltip("Optional floor picture for portrait screens (the room is taller there, so one picture would " +
                 "stretch). Empty falls back to Floor Sprite. Same import rule: 1 unit across.")]
        public Sprite floorSpritePortrait;
    }

    /// <summary>
    /// Homes in moving order: the first game is in home 0, each move goes to the next one and after the last
    /// it starts over from the first.
    /// </summary>
    [CreateAssetMenu(menuName = "CatMerge/Home Database", fileName = "HomeDatabase")]
    public sealed class HomeDatabase : ScriptableObject
    {
        [SerializeField] private List<HomeConfig> homes = new List<HomeConfig>();

        public IReadOnlyList<HomeConfig> Homes => homes;

        /// <summary>The home after <paramref name="moves"/> moves, or null if the list is empty.</summary>
        public HomeConfig ForMoves(int moves) => homes.Count == 0 ? null : homes[Math.Max(0, moves) % homes.Count];

        public void SetHomes(IEnumerable<HomeConfig> configs)
        {
            homes.Clear();
            homes.AddRange(configs);
        }
    }
}
