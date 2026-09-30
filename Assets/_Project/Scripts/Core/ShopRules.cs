using System;

namespace CatMerge.Core
{
    public static class ShopRules
    {
        /// <summary>
        /// Reached level 4 with offset 3 -> the shop sells up to level 1; reached 7 -> up to 4.
        /// Never below level 1 so the game can always start.
        /// </summary>
        public static int MaxBuyableLevel(int maxLevelReached, int offset) => Math.Max(GameState.StartLevel, maxLevelReached - offset);
    }
}
